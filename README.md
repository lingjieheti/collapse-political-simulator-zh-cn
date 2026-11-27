# 《崩溃：政治模拟器》简体中文汉化补丁

**[下载 Windows 安装程序（.exe）](https://github.com/lingjieheti/collapse-political-simulator-zh-cn/releases/latest/download/collapse-political-simulator-zh-cn-Setup.exe)**

退出游戏，双击安装程序。它会自动找到 Steam 游戏目录，点击 **安装汉化** 即可。从 Steam 启动后，在设置中选择 **简体中文**。若已启用其他翻译模组，请在游戏内停用后重启。

首次安装如缺少社区翻译底包，请保持 Steam 已登录，安装器会请求 Steam 下载。也可手动选择目录。游戏更新后，可点 **核验汉化** 检查当前安装。需要恢复原版时，点击 **还原原版**。

适配 Steam Windows x64 构建 **24949409**。v1.2.5 修复了新游戏选择页的两处裁字。选择党派时，“更多信息”下面的介绍，以及画面下方的剧本说明，现在都能完整显示首行笔画。较长文本滚动至末尾时，最后一行也有独立的显示空间。

[党派与剧本首行实机对照](docs/party-description.webp) · [本版复测记录](games/collapse/COMMUNITY-1.2.5.md) · [存档界面](docs/save-names.webp) · [设置页](docs/settings-zh.webp) · [序章标题](docs/prologue-title.webp) · [校对记录](games/collapse/AUDIT.md) · [补丁来源](games/collapse/README.md)

<details>
<summary>从源码构建安装程序</summary>

在 Windows x64 和 Python 3.12 环境中运行：

```powershell
py -3.12 tools/build_setup.py
```

生成的独立 EXE 与 SHA-256 校验文件保存在 `dist` 目录。构建器从 Python.org 获取固定哈希的 Windows 嵌入式运行时。玩家直接使用上方的安装程序。

</details>
