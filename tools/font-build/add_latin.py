from pathlib import Path
from io import BytesIO
from hashlib import sha1
from urllib.request import urlopen
from urllib.parse import quote
from fontTools.ttLib import TTFont
from fontTools.varLib.instancer import instantiateVariableFont
from fontTools.pens.recordingPen import DecomposingRecordingPen
from fontTools.pens.ttGlyphPen import TTGlyphPen

out = Path(__file__).resolve().parents[2] / 'fonts'
base = 'https://raw.githubusercontent.com/google/fonts/8b0a1d0f5983c89bc2b93f1b5fb55f9e252744b5/ofl/notosans/'

def acquire(name, sha):
    data = urlopen(base + quote(name),timeout=60).read()
    assert sha1(b'blob '+str(len(data)).encode()+b'\0'+data).hexdigest()==sha
    return data

src = acquire('NotoSans[wdth,wght].ttf','75575046c015ff623a848096a15779867ba71453')
(out/'OFL-Latin.txt').write_bytes(acquire('OFL.txt','6843f31878c9945e1e71e1baa275546b4eefead8'))
for name,weight in [('Regular',400),('Semibold',600)]:
    path=out/f'UI-{name}.ttf'
    target=TTFont(path,recalcTimestamp=False)
    donor=instantiateVariableFont(TTFont(BytesIO(src),recalcTimestamp=False),{'wdth':100,'wght':weight})
    assert donor['head'].unitsPerEm == target['head'].unitsPerEm
    cmap=donor.getBestCmap()
    have=target.getBestCmap()
    glyphs=donor.getGlyphSet()
    for u in range(0x0100,0x0250):
        if u in have or u not in cmap:continue
        gid=cmap[u]
        key=f'latinExt{u:04X}'
        record=DecomposingRecordingPen(glyphs)
        glyphs[gid].draw(record)
        pen=TTGlyphPen(None)
        record.replay(pen)
        target['glyf'][key]=pen.glyph()
        target['hmtx'][key]=donor['hmtx'][gid]
        if 'vmtx' in target:target['vmtx'][key]=target['vmtx'][have[ord('C')]]
        for mapping in target['cmap'].tables:
            if mapping.isUnicode() and mapping.format in (4,12):
                mapping.cmap[u]=key
    temp=path.with_suffix('.new')
    target.save(temp)
    check=TTFont(temp)
    chars=check.getBestCmap()
    assert all(ord(c) in chars for c in 'ćžőŠšĆČŽčş')
    assert all(check['glyf'][chars[ord(c)]].numberOfContours > 0 for c in 'ćžőŠšĆČŽčş')
    temp.replace(path)
    print(name,len(chars),flush=True)
