"""Install a verified local text-only overlay for Steam build 24949409 (Python 3.9+)."""
import argparse
import contextlib
import hashlib
import json
import os
import re
import struct
import sys
import tempfile
from pathlib import Path
from layout import wrap_chinese

GAME = 'Collapse A Political Simulator'
ASSET = Path('PoliticalSimulator_Data/resources.assets')
PRISTINE = 'd2f6021a4dd06752b15fdefa1de07bd39026b85fb04de1cc8142fae9c98ca52c'
ITEM = '3771481708'
FONT_HASHES = {
    'Regular': '4ee547aaae1f3b04404fffe295ceaf0bec2271b0abde2d8e47e36f24111f650b',
    'Semibold': '5a8ee249f37abd104dac21c4c6fda1edbcda13650eff64466a436cd300d162c6',
}
# English TextAsset object ID, directory entry offset, record count, upstream UTF-8 file hash.
TABLES = {
    'event_text': (671, 16248, 8813, '670748f262211fac24d4fe8f95cfedfaf87a677e0a0f9b86fa706706823d76f2'),
    'gui_text': (680, 16464, 2777, 'a0d456c481c16162207817e4d1b1c54cb8986bf3b103a00f3ffeaf18f65f24c3'),
    'system_text': (678, 16416, 1701, 'e9f2c4d714b2990e2318f235f6bb6ac4ef05c68cb6f01ecdfb617e091359b21d'),
    'other_text': (666, 16128, 204, '8453f3617b928ed57686b155a86b6167c4c375ffef8061fd646bbd3b273e4c09'),
    'personages': (674, 16320, 267, '47554138083eaf66482fffb7a173c0e14afa85a12eaf6aa2627c7c9ef79df7a2'),
    'credits_text': (669, 16200, 87, 'bb6bbebe3746ca8e471661ea24d9f155c61c9861810b3863e618361e7f212228'),
    'modifiesnames_text': (668, 16176, 1, '0893080ba166ae968ccc918ff5e01da8b1b75fbad50cd7796aa548b48e8d8288'),
}

def digest(data):
    return hashlib.sha256(data).hexdigest()

def require(condition, message):
    if not condition:
        raise ValueError(message)

def atomic_write(path, data):
    with tempfile.NamedTemporaryFile(dir=path.parent, delete=False) as f:
        tmp = Path(f.name)
    try:
        tmp.write_bytes(data)
        os.replace(tmp, path)
    finally:
        tmp.unlink(missing_ok=True)

def locate():
    import winreg
    with winreg.OpenKey(winreg.HKEY_CURRENT_USER, 'Software\\Valve\\Steam') as k:
        steam = Path(winreg.QueryValueEx(k, 'SteamPath')[0])
    config = (steam / 'steamapps/libraryfolders.vdf').read_text('utf-8-sig')
    for base in [steam] + [Path(s.replace('\\\\', '\\')) for s in re.findall(r'"path"\s+"([^"]+)"', config)]:
        game = base / 'steamapps/common' / GAME
        if (game / ASSET).is_file():
            return game
    raise ValueError('未找到游戏，请用 --game 指定游戏目录。')

def load_edits():
    out = {key: {} for key in TABLES}
    for row in Path(__file__).with_name('revisions.tsv').read_text('utf-8-sig').splitlines():
        key, num, value = row.split('\t', 2)
        num = int(num)
        require(num not in out[key] and 0 < num <= TABLES[key][2], '修订行号无效')
        out[key][num] = value
    return out

# Game text uses {0} for soft line breaks on many panels, and numbered fields for
# actual substitutions. Keep both intact. Short labels fit without inter-character
# spaces; retain the word-wrap opportunities in long community narrative paragraphs.
CJK = r'\u3400-\u9fff，。！？、；：（）《》“”‘’…·'
def clean_line(key, num, old, original):
    if key == 'credits_text':
        return old
    line = old.replace('<color=<#', '<color=#')
    line = re.sub(r'<\s*/\s*color\s*>', '</color>', line, flags=re.I)
    line = re.sub(r'<\s*color\s*=\s*#\s*([a-f\d]{6,8})\s*>', r'<color=#\1>', line, flags=re.I)
    line = re.sub(r'\{\s+(\d+)\s*\}', r'{\1}', line)
    def joined(match):
        candidate = match[0].replace(' ', '')
        if re.search(r'(?<![A-Za-z0-9])'+re.escape(candidate)+r'(?![A-Za-z0-9])',original,re.I):
            return candidate
        return match[0]
    line = re.sub(r'(?<![A-Za-z0-9])(?:\d +)+\d(?![A-Za-z0-9])',joined,line)
    line = re.sub(r'(?<![A-Za-z])(?:[A-Za-z] +){2,}[A-Za-z](?![A-Za-z])',joined,line)
    line = re.sub(r'(?<=\d) +\. +(?=\d)', '.', line)
    if key == 'event_text':
        if num == 2503:
            line = line.replace('{0}}','{0}')
        if num == 5211:
            line = line.replace('0}', '{0}')
        if num == 5104:
            line = line.replace('{0 {0}', '{0}{0}')
        if num == 8036:
            line = re.sub(r'\{\s*(?=我)', '', line)
        if num == 8030:
            line = re.sub(r'（\s*M\s*e\s*č\s*i\s*a\s*r\s*）','（Mečiar）',line)
    if key == 'gui_text' and num == 1657:
        line = line.replace('{1}}','{1}')
    # Term corrections are constrained by the exact English record, so they do
    # not rename an unrelated foreign treasury or a historical cabinet.
    if re.search(r'Ministr(?:y|ies) of(?:\{0\}|\s+)(?:Economy|Economics)', original, re.I):
        line = re.sub(r'财\s*政\s*部', '经济部', line)
    if 'Public Dialogue Committee' in original:
        line = re.sub(r'公\s*关\s*委\s*员\s*会', '公共对话委员会', line)
    if key == 'system_text' and 1575 <= num <= 1580:
        line = re.sub(r'制\s*造\s*业', '建筑业', line)
    if key == 'event_text' and num == 1220:
        line = re.sub(r'^\s*社\s*会\s*党', '{0}', line)
    if key == 'event_text' and num in (1611, 1612, 2506):
        line = '{0}——' + line.lstrip()
    if key == 'event_text' and num in (2786,2789,2790,2791,2792,2793,2794,6620):
        line = re.sub(r'当\s*局', '{0}当局', line, count=1)
    if key == 'event_text' and num == 3069:
        line = re.sub(r'参\s*与\s*到', '{0}参与到', line, count=1)
    visible = re.sub(r'<[^<>]*>|\{[^{}]*\}|\s', '', line)
    if len(visible) <= 22:
        line = re.sub(r'(?<=[' + CJK + r']) +(?=[' + CJK + r'])', '', line)
    return line

def object_data(asset, offset):
    data_start = struct.unpack_from('>Q', asset, 32)[0]
    obj_id, rel, size = struct.unpack_from('<qqI', asset, offset)
    p = data_start + rel
    name_len = struct.unpack_from('<I', asset, p)[0]
    name_end = p + 4 + name_len
    script_pos = (name_end + 3) & ~3
    length = struct.unpack_from('<I', asset, script_pos)[0]
    require(script_pos+4+length <= p+size <= len(asset), 'TextAsset 越界')
    return obj_id, bytes(asset[p+4:name_end]).decode(), bytes(asset[script_pos+4:script_pos+4+length])

def build(pristine, upstream):
    require(digest(pristine) == PRISTINE, '游戏资源与支持的 Steam Build 24949409 不符。')
    out = bytearray(pristine)
    edits = load_edits()
    summary = {}
    start = struct.unpack_from('>Q', out, 32)[0]
    for key, (obj, offset, count, sha) in TABLES.items():
        source = (upstream / (key + '.txt')).read_bytes()
        require(digest(source) == sha, f'工坊 3.1.0.1 源文件不匹配：{key}')
        actual, name, eng = object_data(pristine, offset)
        require((actual, name) == (obj, key), 'TextAsset 目录与预期不符。')
        orig = eng.decode('utf-8-sig').splitlines()
        base = source.decode('utf-8-sig').splitlines()
        require(len(orig) == len(base) == count, f'行数不匹配：{key}')
        rows = [edits[key][n] if n in edits[key] else clean_line(key,n,t,orig[n-1]) for n,t in enumerate(base,1)]
        rows = [wrap_chinese(row) for row in rows]
        require(all(bool(a.strip()) == bool(b.strip()) for a,b in zip(base,rows)), f'空行结构改变：{key}')
        text = ('\n'.join(rows) + ('\n' if eng.endswith(b'\n') else '')).encode()
        summary[key] = (count, sum(a != b for a,b in zip(base,rows)))
        raw_name = key.encode()
        payload = bytearray(struct.pack('<I',len(raw_name)) + raw_name)
        payload.extend(bytes(-len(payload) % 4))
        payload.extend(struct.pack('<I',len(text)) + text)
        payload.extend(bytes(-len(payload) % 4))
        out.extend(bytes(-len(out) % 8))
        struct.pack_into('<qI',out,offset+8,len(out)-start,len(payload))
        out.extend(payload)
        require(object_data(out,offset) == (obj,key,text), '资源回读失败。')
    struct.pack_into('>Q',out,24,len(out))
    return bytes(out), summary

@contextlib.contextmanager
def installation_lock(state):
    import msvcrt
    state.mkdir(exist_ok=True)
    with (state/'install.lock').open('a+b') as lock:
        lock.seek(0)
        msvcrt.locking(lock.fileno(), msvcrt.LK_NBLCK, 1)
        try:
            yield
        finally:
            lock.seek(0)
            msvcrt.locking(lock.fileno(), msvcrt.LK_UNLCK, 1)

def operation(args, game):
    from visual_assets import VISUAL_HASHES, build_visual
    directory = (game / ASSET).parent
    state = game / '.collapse-zh-24949409'
    receipt = state / 'installed.json'
    prior = json.loads(receipt.read_text()) if receipt.is_file() else {}
    previous = prior.get('files',{})
    if 'installed' in prior:
        previous['resources.assets'] = prior
    originals, currents, targets = {}, {}, {}
    hashes = {'resources.assets': PRISTINE, **VISUAL_HASHES}
    for name, expected in hashes.items():
        data = (directory/name).read_bytes()
        current_hash = digest(data)
        old = previous.get(name,{})
        require(current_hash in {expected,old.get('previous'),old.get('installed')},
                f'当前版本或资源与补丁不匹配：{name}')
        backup = state / (name + '.original')
        originals[name] = data if current_hash == expected else backup.read_bytes()
        require(digest(originals[name]) == expected, f'原始备份校验失败：{name}')
        currents[name] = data
    if args.action == 'restore':
        targets = originals
    else:
        upstream = args.workshop or game.parent.parent / 'workshop/content/990260' / ITEM
        if args.action=='install' and not args.workshop and not (upstream/'gui_text.txt').is_file():
            from workshop import download
            upstream = download(game)
        data, counts = build(originals['resources.assets'], upstream)
        targets['resources.assets'] = data
        for key,(total,changed) in counts.items():
            print(f'{key}: {total} 行，本地修订或排版 {changed} 行')
        font_dir = args.fonts or Path(__file__).resolve().parents[2] / 'fonts'
        fonts = {w:(font_dir/f'UI-{w}.ttf').read_bytes() for w in ('Regular','Semibold')}
        require(all(digest(fonts[w])==expected for w,expected in FONT_HASHES.items()),'字体文件与本版本不匹配。')
        from scene_icons import add_icons
        fonts = {weight:add_icons(font,weight) for weight,font in fonts.items()}
        for name in VISUAL_HASHES:
            targets[name] = build_visual(name, originals[name], fonts)
    modified = [n for n in hashes if currents[n] != targets[n]]
    for name, data in targets.items():
        print(f'{name}  {digest(data)}')
    if args.action == 'check':
        require(not modified,'本机文件与预期安装结果不同：' + '、'.join(modified))
        print('生成及校验通过，本机已安装同一结果。')
        return
    if not modified:
        print('已经处于要求的状态。')
        return
    for name in modified:
        backup = state / (name + '.original')
        if backup.exists():
            require(digest(backup.read_bytes()) == hashes[name], f'备份不匹配：{name}')
        else:
            atomic_write(backup,originals[name])
        require((directory/name).read_bytes() == currents[name], f'操作期间游戏资源发生变化：{name}')
    # Journal both versions first. An interrupted process can be safely retried.
    record = {n:{'installed':digest(targets[n]),'previous':digest(currents[n])} for n in hashes}
    atomic_write(receipt,json.dumps({'files':record}).encode())
    committed = []
    try:
        for name in modified:
            atomic_write(directory/name,targets[name])
            committed.append(name)
            require((directory/name).read_bytes() == targets[name],f'写入回读失败：{name}')
    except BaseException:
        for name in reversed(committed):
            atomic_write(directory/name,currents[name])
        raise
    print('已还原原版文件。' if args.action=='restore' else '已安装。请使用 English 语言槽，并停用游戏内其他工坊语言包。')

def run(args):
    game = (args.game or locate()).resolve()
    require((game / ASSET).is_file(), '游戏目录缺少 resources.assets。')
    if args.action == 'check':
        operation(args, game)
    else:
        with installation_lock(game / '.collapse-zh-24949409'):
            operation(args, game)

if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('action',choices=['install','check','restore'])
    parser.add_argument('--game',type=Path)
    parser.add_argument('--workshop',type=Path)
    parser.add_argument('--fonts',type=Path)
    try:
        run(parser.parse_args())
    except (OSError, ValueError, KeyError, struct.error) as exc:
        sys.exit(str(exc))
