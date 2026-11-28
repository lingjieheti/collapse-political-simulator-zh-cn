"""Build the save and settings display hooks from the verified Steam assembly."""
import hashlib
import os
from pathlib import Path
import subprocess
import tempfile
import zipfile

HERE = Path(__file__).resolve().parent
FILE = 'Managed/Assembly-CSharp.dll'
ORIGINAL = 'b421226acdfd29f27f64758378b0ab94af76f89566d9ad1e9280df913c8646ef'
PATCHED = 'f32eb3e6687d656788e4e333f9a66130763511acff83bbfdc3b80fbe16bcd812'
CECIL = 'd2a23832aaa948ba9a01acc42b5726e34c5f995958f1b30d45c0e7c70b3a72d5'


def build(original, managed):
    if hashlib.sha256(original).hexdigest() != ORIGINAL:
        raise ValueError('游戏界面程序与已适配的 Steam 构建不符。')
    framework = Path(os.environ['SystemRoot']) / 'Microsoft.NET'
    compiler = next((p for p in (framework / 'Framework64/v4.0.30319/csc.exe',
                                 framework / 'Framework/v4.0.30319/csc.exe') if p.is_file()), None)
    if compiler is None:
        raise ValueError('此 Windows 系统缺少 .NET Framework 4 编译组件，无法构建游戏界面组件。')
    package = HERE/'vendor/mono.cecil.0.11.6.nupkg'
    if hashlib.sha256(package.read_bytes()).hexdigest() != CECIL:
        raise ValueError('Mono.Cecil 组件的校验值不符。')
    with tempfile.TemporaryDirectory(prefix='collapse-save-names-') as directory:
        work = Path(directory)
        with zipfile.ZipFile(package) as bundled:
            (work/'Mono.Cecil.dll').write_bytes(bundled.read('lib/net40/Mono.Cecil.dll'))
        source = work/'Assembly-CSharp.dll'
        source.write_bytes(original)
        helper = work/'OurSaveNameLayout.dll'
        builder = work/'BuildSaveNameLayout.exe'
        target = work/'localized.dll'
        references = [managed/name for name in ('mscorlib.dll', 'netstandard.dll', 'System.dll',
                                                 'UnityEngine.CoreModule.dll', 'UnityEngine.TextRenderingModule.dll',
                                                 'UnityEngine.Physics2DModule.dll')]
        def run(command):
            try:
                result = subprocess.run(command, cwd=work, capture_output=True,
                                        creationflags=subprocess.CREATE_NO_WINDOW, timeout=90)
            except subprocess.TimeoutExpired as exc:
                raise ValueError('本地生成游戏界面组件超时，请重试安装。') from exc
            if result.returncode:
                detail = (result.stderr or result.stdout).decode(errors='replace').strip()
                raise ValueError('本地生成游戏界面组件失败。\n' + detail[-1200:])
        run([compiler, '/nologo', '/codepage:65001', '/noconfig', '/nostdlib+', '/target:library', '/optimize+',
             '/out:'+str(helper), *('/r:'+str(p) for p in [*references,source]), HERE/'SaveNameLayout.cs',
             HERE/'DiplomacyLayout.cs'])
        run([compiler, '/nologo', '/codepage:65001', '/optimize+', '/r:'+str(work/'Mono.Cecil.dll'),
             '/out:'+str(builder), HERE/'BuildSaveNameLayout.cs'])
        run([builder,source,helper,target,managed,HERE/'display_literals.tsv'])
        out = target.read_bytes()
        if hashlib.sha256(out).hexdigest() != PATCHED:
            raise ValueError('本地生成的游戏界面组件与发行版本不一致。')
        return out
