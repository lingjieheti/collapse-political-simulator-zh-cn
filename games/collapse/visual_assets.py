"""Byte-preserving edits to known Unity 2022.3 objects, validated by stock file hashes."""
import struct

QUALITY = 'globalgamemanagers'
VISUAL_HASHES = {
    'level2': '356e33540026610cf2ac1e7421cef857cd0e6d78c96749b758d507ff41e17223',
    'level4': 'e1504c4d3aa469a28efd01b31a994aaadf3db009ebb1746c78388317628efe10',
    'level5': 'd61ce16a13c11710ebc0fbd3aff62a928aa10595bc9c3913f78f945233d4e1f9',
    'level6': '23835b8dd2a5f2db7f536f38bd192609ec376a78b00ca49de73beab197b90be5',
    'level12': '4d17e38fa940fa9ebe73e0c23fb8bc8809c61d5ad2e1119cf9f703fea1fc95f5',
    'level13': 'b083cc5c58e41bf627af0cc702733673e63c81dd9091abd960d87027001bb7bf',
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

# Pinned Unity v22 object table offset, TextMesh type index, Transform type index.
SCENE_TABLES = {
    'level2': (1068, 6, 1), 'level4': (716, 6, 1), 'level5': (1668, 10, 1),
    'level6': (700, 7, 1), 'level12': (992, 6, 1), 'level13': (1200, 10, 2),
}

# Each group shares a physical container and the same stock TextMesh size.
MESH_SIZES = {
    'level2': ((.09, .058, (283,)),),  # auto-resume in the settings grid
    'level4': (
        (.14, .112, (162, 165, 167, 168, 170)),  # five difficulty buttons
        (.06, .05, (171, 172)),  # additional info and Last Hero
    ),
    'level5': ((.12, .10, (988,)),),  # pause menu: save game
    'level6': (
        (.10, .08, (569,)),   # full name of a selected, shortened row
        (.12, .08, (621,)),   # filter text on the narrow search plate
        (.05, .030, (628,)),  # leave space between the hint and the name counter
        (.08, .065, (562, 571, 575, 588, 594, 597, 598, 604, 609, 616, 625,
                     626, 631, 637, 639, 648, 652)),  # footer, filters and confirmation buttons
        (.12, .085, (  # save-list headings and 11 data rows per column
            601, 564, 566, 578, 586, 595, 611, 627, 633, 634, 644, 646,  # name
            582, 563, 567, 568, 574, 589, 607, 622, 623, 649, 650, 651,  # local timestamp
            590, 559, 583, 587, 600, 605, 608, 612, 638, 642, 643, 647,  # game date
            641, 561, 570, 573, 585, 591, 599, 603, 606, 614, 617, 619,  # scenario
            565, 558, 576, 579, 593, 615, 620, 624, 630, 632, 636, 645,  # party
            629, 560, 572, 577, 580, 584, 592, 596, 602, 610, 613, 618,  # Last Hero
        )),
    ),
    'level12': ((.075, .059, (408,)),),  # party action: call for an early election
    'level13': (
        (.30, .225, (1107,)),  # return to map
        (.13, .085, (1070,)),  # economy summary below the graphs
        (.07, .055, (1073, 1088, 1096, 1108, 1124, 1135, 1144, 1153, 1156,
                     1164, 1187, 1192, 1193)),  # population and economy
        (.065, .055, (1075, 1089, 1126, 1131, 1134, 1142, 1151)),  # parties
        (.04, .030, (1072, 1074, 1092, 1093, 1111, 1116, 1140, 1145, 1158,
                     1159, 1182, 1196, 1201, 1206, 1220, 1229)),  # half-height rows
    ),
}

# Anchor the one-line setting at the switch centre and centre small statistics
# captions in their 0.171-world-unit rows. Values are local to each parent.
TRANSFORM_Y = {
    'level2': ((1.03, .76, (145,)),),
    'level6': ((.482, 1.08, (206,)),),
    'level13': ((-.10, -.035, (354, 358, 400, 403, 436, 452, 505, 511, 536,
                               537, 585, 620, 629, 639, 663, 683)),),
}

# One digit on each rolling drum, in three animation states. The game writes
# the digits and their Y positions every day, but preserves their local scale.
DATE_DIGITS = (367, 368, 371, 413, 443, 477, 479, 490, 499, 502, 511, 515,
               518, 521, 523, 556, 564, 567, 571, 609, 610, 617, 623, 629)


def object_offset(scene, data, object_id, type_index):
    table = SCENE_TABLES[scene][0]
    entry = table + (object_id - 1) * 24
    actual_id, relative, size, actual_type = struct.unpack_from('<qqIi', data, entry)
    start = struct.unpack_from('>Q', data, 32)[0] + relative
    if actual_id != object_id or actual_type != type_index or not 0 <= start <= len(data) - size:
        raise ValueError(f'{scene} 场景对象 {object_id} 与预期不符。')
    return start


def replace_field(data, offset, fmt, old, new):
    expected = struct.pack(fmt, old)
    if data[offset:offset + len(expected)] != expected:
        raise ValueError('场景控件的原始字段与预期不符。')
    struct.pack_into(fmt, data, offset, new)


def fit_map_calendar(data):
    _, mesh_type, transform_type = SCENE_TABLES['level5']
    root = object_offset('level5', data, 587, transform_type)
    replace_field(data, root + 28, '<f', -17.663, -17.763)
    replace_field(data, root + 40, '<f', 1, 1.10)
    for object_id in (*DATE_DIGITS, 595):
        node = object_offset('level5', data, object_id, transform_type)
        # BaseText (595) contains only the two separators.
        replace_field(data, node + 40, '<f', 1, 1.05 if object_id == 595 else .355)
        replace_field(data, node + 44, '<f', 1, .72)
    mesh = object_offset('level5', data, 1060, mesh_type)
    old = utf8_string(b'.    .')
    new = utf8_string(b'.      .')
    if data[mesh + 12:mesh + 12 + len(old)] != old:
        raise ValueError('地图日期的两个分隔点与预期不符。')
    data[mesh + 12:mesh + 12 + len(old)] = new


def fit_scene_controls(scene, data):
    _, mesh_type, transform_type = SCENE_TABLES[scene]
    for old_size, new_size, ids in MESH_SIZES[scene]:
        for object_id in ids:
            start = object_offset(scene, data, object_id, mesh_type)
            text_length = struct.unpack_from('<I', data, start + 12)[0]
            size_field = start + 20 + ((text_length + 3) & ~3)
            replace_field(data, size_field, '<f', old_size, new_size)
            if scene == 'level2' and object_id == 283:
                replace_field(data, size_field + 8, '<H', 1, 4)  # UpperCenter -> MiddleCenter
            if scene == 'level6' and object_id == 569:
                replace_field(data, size_field + 8, '<H', 0, 3)  # UpperLeft → MiddleLeft
    for old_y, new_y, ids in TRANSFORM_Y.get(scene, ()):
        for object_id in ids:
            start = object_offset(scene, data, object_id, transform_type)
            replace_field(data, start + 32, '<f', old_y, new_y)
    if scene == 'level6':
        # Repurpose the original empty selection caption below the last row.
        # Its object remains in the original hierarchy and zooms with the list.
        start = object_offset(scene, data, 206, transform_type)
        replace_field(data, start + 28, '<f', -1.24454534, -3.19)
        replace_field(data, start + 36, '<f', -1, -2)
    if scene == 'level5':
        fit_map_calendar(data)

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
    if name in SCENE_TABLES:
        fit_scene_controls(name, out)
        if name != 'level4':
            return bytes(out)
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
