# Tools 目录

此目录包含应用程序运行所需的工具文件。

## ADB 工具

- `adb.exe` - Android Debug Bridge 主程序
- `AdbWinApi.dll` - ADB Windows API 库
- `AdbWinUsbApi.dll` - ADB Windows USB API 库

这些文件是从 Android SDK Platform Tools 中提取的，用于与 Android 设备通信。

## 注意事项

- 这些文件是应用程序的一部分，不应删除
- 如果更新 Android SDK，可以替换这些文件
- 确保这些文件与应用程序在同一目录下
