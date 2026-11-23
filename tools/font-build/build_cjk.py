from pathlib import Path
import concurrent.futures
import hashlib
import io
import urllib.parse
import urllib.request
from fontTools import subset
from fontTools.ttLib import TTFont
from fontTools.varLib.instancer import instantiateVariableFont

DEST = Path(__file__).resolve().parents[2] / 'fonts'
PIN = 'a85815a42757630ce188fdad368c2dfc444d4773'
BASE = f'https://raw.githubusercontent.com/google/fonts/{PIN}/ofl/notosanssc/'

def acquire(name, sha):
    data = urllib.request.urlopen(BASE + urllib.parse.quote(name), timeout=100).read()
    assert hashlib.sha1(b'blob ' + str(len(data)).encode() + b'\0' + data).hexdigest() == sha
    return data

def create(name, weight, source, codepoints):
    font = TTFont(io.BytesIO(source), recalcTimestamp=False)
    opts = subset.Options()
    opts.name_IDs = ['*']
    opts.name_languages = ['*']
    selected = subset.Subsetter(options=opts)
    selected.populate(unicodes=codepoints)
    selected.subset(font)
    font = instantiateVariableFont(font, {'wght': weight}, inplace=True, optimize=True)
    family = 'Nostal UI SC'
    for record in font['name'].names:
        value = {1: family, 2: name, 3: f'NostalUISubset-{name}-1', 4: f'{family} {name}', 6: f'NostalUISubset-{name}', 16: family, 17: name}.get(record.nameID)
        if value is not None:
            record.string = value.encode(record.getEncoding())
    target = DEST / f'UI-{name}.ttf'
    tmp = target.with_suffix('.tmp')
    font.save(tmp)
    check = TTFont(tmp)
    assert 'fvar' not in check and check['OS/2'].usWeightClass == weight
    assert set(check.getBestCmap()) == codepoints
    tmp.replace(target)
    print(target.name, target.stat().st_size, len(codepoints), flush=True)

if __name__ == '__main__':
    DEST.mkdir(parents=True, exist_ok=True)
    source = acquire('NotoSansSC[wght].ttf','fb0637bafbcd804fe32152370a1225990745b4bc')
    (DEST/'OFL.txt').write_bytes(acquire('OFL.txt','1c9f43281b8f216c5461fe9ac729afbade7724e4'))
    font = TTFont(io.BytesIO(source))
    # Retain all Unicode characters mapped by the upstream, including rare names.
    codepoints = set(font.getBestCmap())
    with concurrent.futures.ProcessPoolExecutor(max_workers=2) as pool:
        jobs = [pool.submit(create,n,w,source,codepoints) for n,w in [('Regular',400),('Semibold',600)]]
        for task in jobs:
            task.result()
