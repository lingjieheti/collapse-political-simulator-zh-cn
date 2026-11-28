"""Validate the distributable patch files without Steam, network access, or third-party modules."""
import bz2
import hashlib
import json
import os
from pathlib import Path, PureWindowsPath
import re
import runpy
import struct
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[1]
GAMES = ROOT / "games"
sys.dont_write_bytecode = True


def require(value, message):
    if not value:
        raise ValueError(message)

def read_json(path):
    def unique(pairs):
        result = {}
        for key, value in pairs:
            require(key not in result, f"{path.name}: duplicate key {key}")
            result[key] = value
        return result
    return json.loads(path.read_text("utf-8-sig"), object_pairs_hook=unique)

def text_line(value):
    require(isinstance(value, str) and value.strip(), f"Empty or invalid text: {value!r}")
    require("\ufffd" not in value and "\0" not in value, "Text contains an invalid character")
    return value

def font_coverage_and_spaces(data):
    require(data[:4] == b"\x00\x01\x00\x00", "Expected a TrueType font")
    count = struct.unpack_from(">H", data, 4)[0]
    tables = {}
    for i in range(count):
        name, _, pos, size = struct.unpack_from(">4sIII", data, 12 + 16 * i)
        require(name not in tables and pos + size <= len(data), "Invalid sfnt table directory")
        tables[name] = data[pos:pos+size]
    require({b"head", b"glyf", b"loca", b"cmap", b"hmtx", b"name"} <= tables.keys(), "Missing TrueType tables")
    cmap = tables[b"cmap"]
    records = struct.unpack_from(">H", cmap, 2)[0]
    available = set()
    space_glyphs = {}
    for platform, encoding, pos in struct.iter_unpack(">HHI", cmap[4:4+8*records]):
        if not (platform == 0 or (platform, encoding) == (3, 10)):
            continue
        if struct.unpack_from(">H", cmap, pos)[0] != 12:
            continue
        _, _, length, _, groups = struct.unpack_from(">HHIII", cmap, pos)
        require(length == 16 + 12 * groups and pos + length <= len(cmap), "Malformed Unicode cmap")
        for start, end, glyph in struct.iter_unpack(">III", cmap[pos+16:pos+length]):
            require(start <= end <= 0x10ffff, "Invalid Unicode group")
            available.update(range(start + int(glyph == 0), end + 1))
            for cp in (0x20, 0x200a):
                if start <= cp <= end:
                    space_glyphs[cp] = glyph + cp - start
    require(available and 0x20 in space_glyphs, "Incomplete character map or missing ASCII space")
    long_metrics = struct.unpack_from(">H", tables[b"hhea"], 34)[0]
    require(long_metrics > 0, "Missing horizontal font metrics")
    advances = {cp: struct.unpack_from(">H", tables[b"hmtx"], 4 * min(gid, long_metrics - 1))[0]
                for cp, gid in space_glyphs.items()}
    return available, advances

def check_collapse_and_fonts(chars):
    folder = GAMES / "collapse"
    # Invoke the same parser and font reconstruction routines as the player installer.
    sys.path.insert(0, str(folder))
    try:
        installer = runpy.run_path(str(folder / "collapse.py"))
        icons = runpy.run_path(str(folder / "scene_icons.py"))
    finally:
        sys.path.pop(0)
    edits = installer["load_edits"]()
    total = sum(len(entries) for entries in edits.values())
    require(total >= 181, "Collapse revision list is incomplete")
    require(set(range(753, 760)) <= edits["gui_text"].keys(), "One of the seven party descriptions is absent")
    require(installer["wrap_chinese"]("Karl Marx") == "Karl \u200aMarx",
            "Latin word pairs lost their normal visual spacing")
    require(installer["wrap_chinese"]("共 产 党") == "共产党", "Short CJK labels contain artificial gaps")
    for entries in edits.values():
        for value in entries.values():
            chars.update(text_line(value))
    for line in (folder / "display_literals.tsv").read_text("utf-8").splitlines():
        chars.update(text_line(line.split("\t")[4]))
    for sections in installer["load_guide_sections"]().values():
        for section in sections.values():
            chars.update(text_line(section))
    needed = {ord(c) for c in chars if 0x3400 <= ord(c) <= 0x9fff}
    for weight in ("Regular", "Semibold"):
        font = (ROOT / "fonts" / f"UI-{weight}.ttf").read_bytes()
        require(hashlib.sha256(font).hexdigest() == installer["FONT_HASHES"][weight], f"{weight} font hash mismatch")
        before, before_spaces = font_coverage_and_spaces(font)
        missing = needed - before
        require(not missing, f"{weight} lacks release-text glyphs: {''.join(chr(c) for c in sorted(missing))}")
        with_icons = icons["add_icons"](font, weight)
        after, after_spaces = font_coverage_and_spaces(with_icons)
        require(needed <= after, f"Scene-icon patch lost {weight} text coverage")
        require(0 < after_spaces[0x20] < before_spaces[0x20], f"Chinese line breaks have full {weight} space width")
        require(0x200a in after_spaces and after_spaces[0x20] + after_spaces[0x200a] == before_spaces[0x20],
                f"Latin word spacing changed in {weight}")
    print(f"Collapse: {total} edits, both font weights and both icon deltas verified")
    print(f"Fonts: all {len(needed)} distinct Han characters in the public revision files are mapped")

def check_revised_text():
    rows = {}
    for line in (GAMES / "collapse/revisions.tsv").read_text("utf-8-sig").splitlines():
        table, number, value = line.split("\t", 2)
        rows[table, int(number)] = value
    require("经济部" in rows["gui_text", 985], "Economy Ministry budget label is missing")
    require(all("10%" in rows["gui_text", i] and "{0}" in rows["gui_text", i] for i in (980, 981)),
            "Monthly loan repayments or amount placeholders are missing")
    require("50" in rows["gui_text", 1125], "Budget cost diverges from the supported game's calculation")
    require(len({rows["event_text", i] for i in range(3880,3890)}) == 10 and
            len({rows["event_text", i] for i in range(3921,3931)}) == 10,
            "A diplomatic relationship outcome has been duplicated")


def main():
    check_revised_text()
    for path in ROOT.rglob("*.py"):
        if ".git" not in path.parts:
            compile(path.read_bytes(), str(path.relative_to(ROOT)), "exec")
    chars = set()
    check_collapse_and_fonts(chars)

    for path in ("manage.py", "games/collapse/collapse.py"):
        proc = subprocess.run([sys.executable, "-B", "-X", "utf8", str(ROOT / path), "--help"],
                              capture_output=True, encoding="utf-8", timeout=30)
        require(proc.returncode == 0 and "install" in proc.stdout and "restore" in proc.stdout,
                f"Player entry point failed: {path}")
    print("Release files and both player entry points passed.")

if __name__ == "__main__":
    main()
