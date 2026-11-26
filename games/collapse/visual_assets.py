"""Byte-preserving edits to known Unity 2022.3 objects, validated by stock file hashes."""
import struct

QUALITY = 'globalgamemanagers'
VISUAL_HASHES = {
    'level4': 'e1504c4d3aa469a28efd01b31a994aaadf3db009ebb1746c78388317628efe10',
    'level15': '1843918192931e74a2276d911b48d1535630f5c13eb99d0362ef24118473863c',
    QUALITY: 'd9c3ea7f7c2d2259b2b8afd7f8de98af27483ea364a5ba1a03b4ef6409bc6fab',
    'sharedassets0.assets': '024c8f9fd00c0797e3076413130dcb50162aa7dab5268fc6b055d35ff423ca1d',
    'sharedassets5.assets': 'f3cb4b1135fc235e10f4937c1c35e31209adecfbc7787e27d1ffae71b0f66369',
}
# Directory entry, object ID, font-data offset within object, original byte count,
# original family, new weight. TextMesh object IDs and references stay intact.
FONTS = {
    'sharedassets0.assets': [
        (1456,50,6256,134956,'Pobeda','Regular'),
        (1480,51,84,189044,'Bellota','Semibold'),
    ],
    'sharedassets5.assets': [
        (980,31,88,398968,'Noto Sans','Regular'),
        (1004,32,84,70612,'Bebas Neue Cyrillic','Semibold'),
    ],
}

def utf8_string(raw):
    length = struct.pack('<I',len(raw))
    return length + raw + bytes(-len(raw) % 4)

def font_family(font):
    table_count = struct.unpack_from('>H',font,4)[0]
    for j in range(table_count):
        tag,_,offset,size = struct.unpack_from('>4sIII',font,12+16*j)
        if tag != b'name':
            continue
        table = font[offset:offset+size]
        _,count,storage = struct.unpack_from('>HHH',table)
        candidates = []
        for i in range(count):
            platform,_,language,name_id,length,start = struct.unpack_from('>6H',table,6+i*12)
            if platform!=3 or name_id not in (1,16):
                continue
            value = table[storage+start:storage+start+length].decode('utf-16-be')
            candidates.append(((language!=0x409,name_id!=16),value))
        if candidates:
            return min(candidates)[1]
    raise ValueError('字体缺少可用的 Windows 字体家族名称。')

def build_visual(name, pristine, fonts):
    out = bytearray(pristine)
    if name == 'level15':
        # Back/UI/MainText (TextMesh 111) displays the title of each backstory.
        # Scale within the fixed red strip and centre the ink vertically.
        # Transform 61 retains the title's left edge and depth.
        offset = 14232
        position = 8620
        if (out[offset:offset+8] != struct.pack('<ff',0.32,1.0) or
                out[position:position+12] != struct.pack('<fff',-13.05,6.05,-1.0)):
            raise ValueError('人物背景故事标题的字体字段与预期不符。')
        struct.pack_into('<f',out,offset,0.25)
        struct.pack_into('<f',out,position+4,6.11)
        return bytes(out)
    if name == 'level4':
        # GameSelect/Back/TextField/TextZ and UI (0)/TextField (1)/Text.
        # Both panels take their width from a fixed world-space constant in
        # GameSelectController. Scaling evenly also leaves a right-hand inset.
        for pos,base_size in [(23320,0.10),(23984,0.09)]:
            if out[pos:pos+8]!=struct.pack('<ff',base_size,1.0) or struct.unpack_from('<I',out,pos+20)[0]!=1:
                raise ValueError('情景选择面板的字体字段与预期不符。')
            struct.pack_into('<ff',out,pos,base_size*0.90,1.15)
            struct.pack_into('<I',out,pos+20,0)  # reader text uses regular
        for trans,x,y in [(13744,-3.11,1.62),(15928,-2.947,1.5)]:
            if out[trans+28:trans+52]!=struct.pack('<ffffff',x,y,-1.0,1.0,1.0,1.0):
                raise ValueError('情景选择面板的坐标与预期不符。')
            struct.pack_into('<ff',out,trans+28,x+0.12,y-0.045)
            struct.pack_into('<ff',out,trans+40,0.955,0.955)
        return bytes(out)
    if name == QUALITY:
        # The game displays the Unity quality level name, and persists / applies
        # the level by integer index. The original name occupies eight bytes.
        pos = 79944
        if out[pos:pos+12] != utf8_string(b'Standard'):
            raise ValueError('画质等级字段与预期不符。')
        out[pos:pos+12] = utf8_string('标准'.encode())
        return bytes(out)
    data_start = struct.unpack_from('>Q',out,32)[0]
    for entry,obj,font_pos,old_length,old_family,weight in FONTS[name]:
        actual,rel,size = struct.unpack_from('<qqI',out,entry)
        raw = bytes(out[data_start+rel:data_start+rel+size])
        if actual!=obj or struct.unpack_from('<I',raw,font_pos-4)[0]!=old_length:
            raise ValueError('字体对象与预期不符。')
        font = fonts[weight]
        old = utf8_string(old_family.encode())
        tail = raw[font_pos+old_length+(-old_length%4):]
        if tail.count(old)!=1:
            raise ValueError('字体家族字段与预期不符。')
        tail = tail.replace(old,utf8_string(font_family(font).encode()))
        payload = raw[:font_pos-4] + utf8_string(font) + tail
        out.extend(bytes(-len(out)%8))
        struct.pack_into('<qI',out,entry+8,len(out)-data_start,len(payload))
        out.extend(payload)
    struct.pack_into('>Q',out,24,len(out))
    return bytes(out)
