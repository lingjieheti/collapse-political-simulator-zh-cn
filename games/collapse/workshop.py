"""Retrieve the pinned base through the game's own Windows Steamworks API."""
import ctypes as c
import hashlib
import os
from pathlib import Path
import time

APP = 990260
ITEM = 3771481708

class Callback(c.Structure):
    _fields_ = [('user',c.c_int32),('id',c.c_int32),('data',c.c_void_p),('size',c.c_int32)]

class DownloadResult(c.Structure):
    _fields_ = [('app',c.c_uint32),('item',c.c_uint64),('result',c.c_int32)]

def download(game):
    path = game / 'PoliticalSimulator_Data/Plugins/x86_64/steam_api64.dll'
    expected = 'e6d9bafb9a41e42fba7b21553db49f8719027af3f0deb23ff86cfc44d60e776d'
    if hashlib.sha256(path.read_bytes()).hexdigest() != expected:
        raise ValueError('Steamworks 库的版本不匹配，请通过 Steam 创意工坊获取底包。')
    if c.sizeof(c.c_void_p)!=8:
        raise ValueError('自动下载需要 64 位 Python，或先从工坊获取底包。')
    dll = c.CDLL(str(path))
    def function(name,res,*args):
        f = getattr(dll,name)
        f.restype,f.argtypes = res,args
        return f
    ptr,u32,u64 = c.c_void_p,c.c_uint32,c.c_uint64
    os.environ['SteamAppId'] = os.environ['SteamGameId'] = str(APP)
    err = c.create_string_buffer(1024)
    if function('SteamAPI_InitFlat',c.c_int,ptr)(err):
        raise ValueError('请启动 Steam 并登录拥有游戏的账户，再重试安装。')
    try:
        ugc = function('SteamAPI_SteamUGC_v021',ptr)()
        function('SteamAPI_ManualDispatch_Init',None)()
        pipe = function('SteamAPI_GetHSteamPipe',c.c_int32)()
        request = function('SteamAPI_ISteamUGC_DownloadItem',c.c_bool,ptr,u64,c.c_bool)
        install = function('SteamAPI_ISteamUGC_GetItemInstallInfo',c.c_bool,ptr,u64,c.POINTER(u64),ptr,u32,c.POINTER(u32))
        run = function('SteamAPI_ManualDispatch_RunFrame',None,c.c_int32)
        poll = function('SteamAPI_ManualDispatch_GetNextCallback',c.c_bool,c.c_int32,c.POINTER(Callback))
        free = function('SteamAPI_ManualDispatch_FreeLastCallback',None,c.c_int32)
        if not ugc or not request(ugc,ITEM,False):
            raise ValueError('Steam 未接受下载请求，请从创意工坊获取底包。')
        print('正在通过 Steam 下载汉化底包……',flush=True)
        deadline = time.monotonic() + 150
        while time.monotonic() < deadline:
            run(pipe)
            event = Callback()
            while poll(pipe,c.byref(event)):
                try:
                    if event.id != 3406 or event.size != c.sizeof(DownloadResult):
                        continue
                    result = DownloadResult.from_buffer_copy(c.string_at(event.data,event.size))
                    if (result.app,result.item) != (APP,ITEM):
                        continue
                    if result.result != 1:
                        raise ValueError(f'Steam 工坊下载失败（结果代码 {result.result}）。')
                    folder = c.create_string_buffer(4096)
                    if install(ugc,ITEM,c.byref(u64()),folder,len(folder),c.byref(u32())):
                        return Path(folder.value.decode('utf-8'))
                    raise ValueError('Steam 未提供底包目录。')
                finally:
                    free(pipe)
            time.sleep(.1)
        raise ValueError('Steam 工坊下载超时。请稍后重试，或通过 --workshop 指定底包。')
    finally:
        function('SteamAPI_Shutdown',None)()
