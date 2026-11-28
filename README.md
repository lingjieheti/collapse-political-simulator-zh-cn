# 《崩溃：政治模拟器》简体中文汉化补丁

**[下载 Windows 安装程序（.exe）](https://github.com/lingjieheti/collapse-political-simulator-zh-cn/releases/latest/download/collapse-political-simulator-zh-cn-Setup.exe)**

退出游戏，双击安装程序。它会自动找到 Steam 游戏目录，点击 **安装汉化** 即可。从 Steam 启动后，在设置中选择 **简体中文**。若已启用其他翻译模组，请在游戏内停用后重启。

首次安装如缺少社区翻译底包，请保持 Steam 已登录，安装器会请求 Steam 下载。也可手动选择目录。游戏更新后，可点 **核验汉化** 检查当前安装。需要恢复原版时，点击 **还原原版**。

适配 Steam Windows x64 构建 **24949409**。v1.2.8 补全了人物属性、配偶、子女和好友的中文效果提示。现在可以看清“尚未任职”和“卷入丑闻”分别意味着什么，俄国领导人也会按实际结局显示。另有 173 条文本修订，涉及党派结盟、地区改选、教会绝罚和历史事件。已有旧版可退出游戏后直接升级。

[人物效果修订前后对照](docs/personality-effects.webp) · [本版覆盖与验证](games/collapse/COMMUNITY-1.2.8.md) · [西方基金会说明](docs/foundation-policy.webp) · [顶部悬停提示](docs/hover-tooltips.webp) · [党派与剧本首行对照](docs/party-description.webp) · [存档界面](docs/save-names.webp) · [设置页](docs/settings-zh.webp) · [补丁来源](games/collapse/README.md)

<details>
<summary>从源码构建安装程序</summary>

在 Windows x64 和 Python 3.12 环境中运行：

```powershell
py -3.12 tools/build_setup.py
```

生成的独立 EXE 与 SHA-256 校验文件保存在 `dist` 目录。构建器从 Python.org 获取固定哈希的 Windows 嵌入式运行时。玩家直接使用上方的安装程序。

</details>
