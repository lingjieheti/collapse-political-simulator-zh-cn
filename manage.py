"""Player entry point for the standalone Simplified Chinese patch."""
import argparse
import csv
import ctypes
from ctypes import wintypes
import os
from pathlib import Path
import subprocess
import sys

ROOT = Path(__file__).resolve().parent
sys.path.insert(0, str(ROOT / "tools"))
from steam_game import ASSET, EXECUTABLE, installed_build, locate, validate


def process_image(pid):
    kernel = ctypes.WinDLL("kernel32", use_last_error=True)
    kernel.OpenProcess.argtypes = [wintypes.DWORD, wintypes.BOOL, wintypes.DWORD]
    kernel.OpenProcess.restype = wintypes.HANDLE
    kernel.QueryFullProcessImageNameW.argtypes = [
        wintypes.HANDLE, wintypes.DWORD, wintypes.LPWSTR, ctypes.POINTER(wintypes.DWORD)]
    kernel.CloseHandle.argtypes = [wintypes.HANDLE]
    handle = kernel.OpenProcess(0x1000, False, pid)  # PROCESS_QUERY_LIMITED_INFORMATION
    if not handle:
        raise ctypes.WinError(ctypes.get_last_error())
    try:
        buffer = ctypes.create_unicode_buffer(32768)
        length = wintypes.DWORD(len(buffer))
        if not kernel.QueryFullProcessImageNameW(handle, 0, buffer, ctypes.byref(length)):
            raise ctypes.WinError(ctypes.get_last_error())
        return Path(buffer.value)
    finally:
        kernel.CloseHandle(handle)


def game_running(game):
    tasklist = Path(os.environ["SystemRoot"]) / "System32/tasklist.exe"
    output = subprocess.check_output([str(tasklist), "/FO", "CSV", "/NH", "/FI",
                                      "IMAGENAME eq " + EXECUTABLE], encoding="mbcs", errors="replace")
    for row in csv.reader(output.splitlines()):
        if not row or row[0].casefold() != EXECUTABLE.casefold():
            continue
        try:
            image = process_image(int(row[1]))
            if image.samefile(game / EXECUTABLE) or (image.parent / ASSET).samefile(game / ASSET):
                return True
        except (IndexError, ValueError, OSError):
            # If Windows cannot identify the process, keep the game protected.
            return True
    return False


def main():
    parser = argparse.ArgumentParser(description="《崩溃：政治模拟器》简体中文汉化补丁")
    parser.add_argument("action", nargs="?", default="install", choices=["locate", "build", "install", "verify", "restore"])
    parser.add_argument("--game-dir", type=Path, help="手动指定游戏主目录；默认自动识别 Steam 库")
    args = parser.parse_args()
    if os.name != "nt" or sys.maxsize <= 2**32:
        parser.exit(1, "请使用 64 位 Windows。\n")
    game = validate(args.game_dir) if args.game_dir is not None else locate()
    if args.action == "locate":
        print(game, flush=True)
        return 0
    if args.action == "build":
        print(installed_build(game) or "unknown", flush=True)
        return 0
    if args.action != "verify" and game_running(game):
        parser.exit(1, "请先保存进度并退出游戏，再重试。\n")
    backend = ROOT / "games/collapse/collapse.py"
    action = "check" if args.action == "verify" else args.action
    command = [sys.executable, "-B", "-u", "-X", "utf8"]

    return subprocess.run([*command, str(backend), action, "--game-dir", str(game)]).returncode


if __name__ == "__main__":
    try:
        sys.exit(main())
    except (OSError, ValueError, subprocess.SubprocessError) as error:
        sys.exit(str(error))
