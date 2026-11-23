"""Add four licensed scene symbols to a verified shared UI font."""
import bz2
import hashlib
from pathlib import Path
import re
import struct

HASHES = {
    'Regular': ('132d4b5844b05aa58da690dc4271cea4a3fd4ec45b79630f74b0d57e104e7f57',
                'b588376e5534ebd0a8218ebad481f1f6dc440badc2044c2f9048b338f09e5ba0'),
    'Semibold': ('b3ad43d5cb72603709f5efffd1a821db939687bed9c64ab4b31fad98a10e42df',
                 '32d9be826849b9aed67ca3ab269aec3c59e769d7d2cdf1b047903ebf03e41865'),
}

def add_icons(base, weight):
    patch = Path(__file__).with_name(f'icons-{weight}.bsdiff').read_bytes()
    known_patch,known_output = HASHES[weight]
    if hashlib.sha256(patch).hexdigest()!=known_patch:
        raise ValueError('场景符号差分未通过校验。')
    magic,cs,ds,size = struct.unpack_from('<8sQQQ',patch)
    if magic!=b'BSDIFF40' or not 0 < size <= 12_000_000:
        raise ValueError('场景字体差分格式无效。')
    control = bz2.decompress(patch[32:32+cs])
    delta = bz2.decompress(patch[32+cs:32+cs+ds])
    extra = bz2.decompress(patch[32+cs+ds:])
    out = bytearray()
    src = d = x = 0
    for common,add,seek in struct.iter_unpack('<QQQ',control):
        if seek & (1<<63):
            seek = -(seek & ((1<<63)-1))
        if src < 0 or src+common > len(base) or d+common > len(delta) or x+add > len(extra):
            raise ValueError('场景字体差分越界。')
        pos = len(out)
        out.extend(base[src:src+common])
        for span in re.finditer(b'[^\x00]+', delta[d:d+common]):
            for i,value in enumerate(span[0],pos+span.start()):
                out[i] = (out[i]+value)%256
        out.extend(extra[x:x+add])
        src += common+seek
        d += common
        x += add
    if len(out)!=size or hashlib.sha256(out).hexdigest()!=known_output:
        raise ValueError('场景字体生成结果校验失败。')
    return bytes(out)
