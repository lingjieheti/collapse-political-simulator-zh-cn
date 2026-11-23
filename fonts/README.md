# Nostal UI SC

本项目提供常规（400）和半粗（600）两个静态字重，供旧版 Unity 界面使用。正文字形取自 Noto Sans SC，保留原字体的字符映射，再从 Noto Sans 补入拉丁扩展 A、B 区缺少的字形。两个字重各覆盖 31,173 个码位。

字体内部家族名已改为 Nostal UI SC。版权与授权分别见 [中文字体许可](OFL.txt) 和 [补充字形许可](OFL-Latin.txt)。上游文件均按固定版本取用。[Noto Sans SC 源文件](https://github.com/google/fonts/tree/a85815a42757630ce188fdad368c2dfc444d4773/ofl/notosanssc) · [Noto Sans 源文件](https://github.com/google/fonts/tree/8b0a1d0f5983c89bc2b93f1b5fb55f9e252744b5/ofl/notosans)

维护者可用 Python 3.12 与 fonttools 4.62.1 重建共享字体。在仓库副本的根目录依次执行以下命令，产物会写入该副本的 `fonts` 目录。

```powershell
py -3.12 tools/font-build/build_cjk.py
py -3.12 tools/font-build/add_latin.py
py -3.12 tools/check_release.py
```
