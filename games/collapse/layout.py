"""Introduce Chinese word-wrap opportunities for the Unity text renderer."""
import ctypes
import re
from functools import lru_cache

HAN = r'\u3400-\u9fff\uf900-\ufaff'
CLOSE = '，。、；：？！）》」』】”’…'
OPEN = '（《「『【“‘'
SPECIAL = re.compile(r'(<[^<>]*>|\{\d+(?::[^{}]*)?\}|\|)')
TERMS = re.compile('(社会民主党|国家人民党|保守共和党|民族主义|法团主义|爱国主义|社会主义|资本主义|马列主义|市场经济|瑞典模式|共和国|蓝衣社|自由党|共产党|第三条道路)')

@lru_cache(maxsize=1)
def icu():
    try:
        library = ctypes.CDLL('icu.dll', winmode=0x800)  # System32
    except OSError:
        return None
    library.ubrk_open.argtypes = [ctypes.c_int,ctypes.c_char_p,ctypes.c_void_p,ctypes.c_int,ctypes.POINTER(ctypes.c_int)]
    library.ubrk_open.restype = ctypes.c_void_p
    library.ubrk_next.argtypes = [ctypes.c_void_p]
    library.ubrk_next.restype = ctypes.c_int
    library.ubrk_close.argtypes = [ctypes.c_void_p]
    return library

@lru_cache(maxsize=16384)
def break_run(run):
    # Treat recurrent party names and ideologies as one typographic word.
    if TERMS.search(run):
        return ' '.join(part if idx%2 else break_words(part)
                        for idx,part in enumerate(TERMS.split(run)) if part)
    return break_words(run)

def break_words(run):
    # This function receives only BMP Han characters, so an ICU UTF-16 offset
    # is also a Python character index. Strings, markup and substitutions are
    # tokenized separately by the caller.
    if len(run) < 3:
        return run
    words = []
    lib = icu()
    if lib:
        buf = ctypes.create_string_buffer(run.encode('utf-16-le'))
        status = ctypes.c_int()
        iterator = lib.ubrk_open(1, b'zh_CN', buf, len(run), ctypes.byref(status))
        if iterator:
            try:
                start = 0
                while status.value <= 0:
                    end = lib.ubrk_next(iterator)
                    if end < 0:
                        break
                    words.append(run[start:end])
                    start = end
            finally:
                lib.ubrk_close(iterator)
    if ''.join(words) != run:
        words = [run]
    tokens = [w[i:i+4] for w in words for i in range(0,len(w),4)]
    groups = []
    for word in tokens:
        if groups and len(groups[-1])+len(word)<=4 and (
                len(groups[-1])==1 or (len(word)==1 and word in '的了着过们人性化者党派部日月年次')):
            groups[-1] += word
        else:
            groups.append(word)
    return ' '.join(groups)

def prose(part):
    # Remove legacy per-glyph padding inside Chinese while preserving spaces
    # between Latin words, numbers and parties' names.
    part = re.sub(fr'(?<=[{HAN}{OPEN}{CLOSE}]) +(?=[{HAN}{OPEN}{CLOSE}])','',part)
    part = re.sub(fr'(?<=[{OPEN}]) +| +(?=[{CLOSE}])','',part)
    part = re.sub(fr'[{HAN}]+',lambda m:break_run(m[0]),part)
    part = re.sub(fr'(?<=[{CLOSE}])(?=[{HAN}{OPEN}A-Za-z0-9])',' ',part)
    part = re.sub(fr'(?<=[{HAN}])(?=[{OPEN}])',' ',part)
    return part

def wrap_chinese(text):
    # U+0020 remains the game's actual wrap delimiter. In Western word pairs
    # U+200A restores the original .224/.226-em space advance for names and
    # credits; the other CJK wrap opportunities use the narrow .080-em space.
    parts = SPECIAL.split(text)
    for i in range(0,len(parts),2):
        parts[i] = re.sub(r'(?<=[A-Za-zÀ-ɏА-Яа-яЁё]) (?=[A-Za-zÀ-ɏА-Яа-яЁё])',' \u200a',parts[i])
    text = ''.join(parts)
    bare = SPECIAL.sub('',text)
    if not re.search(fr'[{HAN}]',bare):
        return text
    if len(re.sub(r'\s','',bare)) <= 6:
        return re.sub(fr'(?<=[{HAN}]) +(?=[{HAN}])','',text)
    return ''.join(part if i % 2 else prose(part) for i,part in enumerate(SPECIAL.split(text)))
