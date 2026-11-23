# 《崩溃：政治模拟器》简体中文补丁

适用于 Windows 版 Steam 构建 **24949409**。本项目独立提供这款游戏的中文文本校订、字体补丁和安装器。[下载 Windows 版](https://github.com/lingjieheti/collapse-political-simulator-zh-cn/releases/latest) · [本次文本校对报告](games/collapse/AUDIT.md)

本次修正了三档生育政策的说明顺序和火灾事件的犯罪率符号。外交报告现在分别描述国家间的实际关系与外部势力的影响，相关政策的部门预算变化也已对照游戏逻辑复核。

## 安装与更新

1. 在 Steam 安装游戏，登录拥有该游戏的账号，然后退出游戏。
2. 安装 Windows **64 位 Python 3.12**，下载 Release 中以 `-Windows.zip` 结尾的文件并完整解压。[Python 官方下载](https://www.python.org/downloads/windows/)
3. 双击 **汉化管理.cmd**，选择 **安装或更新**。完成后从 Steam 启动游戏。再次打开管理器，可以核验已安装的文件或还原。

使用游戏的 English 语言槽。曾在游戏内启用其他语言模组的玩家，请先停用相关模组再重启。首次安装会通过 Steam 获取对应工坊底包。

高级用法、备份位置、上游译者署名与字体授权见[安装与来源说明](games/collapse/README.md)。也可在解压目录打开终端运行：

```powershell
py -3.12 manage.py install
py -3.12 manage.py verify
py -3.12 manage.py restore
```

## 实机画面

下图记录 Windows 实机检查的剧本选择页。文本校对的范围、此次修订及仍待验证的内容见[校对报告](games/collapse/AUDIT.md)。

![崩溃：政治模拟器实机截图](docs/collapse-start.webp)
