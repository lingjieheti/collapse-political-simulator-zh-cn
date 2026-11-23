@echo off
chcp 65001 >nul
setlocal
cd /d "%~dp0"
if exist "dist\collapse-political-simulator-zh-cn-Setup.exe" (
  start "" "dist\collapse-political-simulator-zh-cn-Setup.exe"
  exit /b
)
start "" "https://github.com/lingjieheti/collapse-political-simulator-zh-cn/releases/latest/download/collapse-political-simulator-zh-cn-Setup.exe"
