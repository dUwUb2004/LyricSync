# ADB 工具安装和配置指南

## 什么是 ADB？

ADB（Android Debug Bridge）是 Android SDK 提供的一个命令行工具，用于与 Android 设备进行通信。在 LyricSync Windows 端中，我们使用 ADB 来：

1. 读取安卓端的 logcat 日志
2. 发送按键事件控制音乐播放

## 安装方法

### 方法一：安装完整的 Android SDK（推荐）

1. 下载并安装 [Android Studio](https://developer.android.com/studio)
2. 在 Android Studio 中安装 Android SDK
3. ADB 工具位于：`%ANDROID_HOME%\platform-tools\adb.exe`

### 方法二：仅安装 ADB 工具

1. 下载 [Platform Tools](https://developer.android.com/studio/releases/platform-tools)
2. 解压到任意目录，如：`C:\adb\`
3. 将 `C:\adb\` 添加到系统 PATH 环境变量

### 方法三：使用 Chocolatey 包管理器

```cmd
choco install adb
```

## 配置环境变量

### Windows 10/11

1. 右键"此电脑" → "属性" → "高级系统设置"
2. 点击"环境变量"
3. 在"系统变量"中找到"Path"，点击"编辑"
4. 点击"新建"，添加 ADB 工具所在目录
5. 点击"确定"保存所有更改

### 验证安装

打开命令提示符或 PowerShell，运行：

```cmd
adb version
```

如果显示版本信息，说明安装成功。

## 设备连接

### 1. 启用开发者选项

1. 在安卓设备上进入"设置"
2. 找到"关于手机"，连续点击"版本号"7次
3. 返回设置，找到"开发者选项"
4. 启用"开发者选项"和"USB 调试"

### 2. 连接设备

1. 使用 USB 线连接安卓设备和电脑
2. 在安卓设备上选择"文件传输"模式
3. 允许 USB 调试的提示

### 3. 验证连接

运行以下命令查看已连接的设备：

```cmd
adb devices
```

应该显示类似输出：
```
List of devices attached
ABCD1234    device
```

## 常见问题

### 问题1：adb 不是内部或外部命令

**解决方案**：检查环境变量配置，确保 ADB 工具目录已添加到 PATH。

### 问题2：设备未授权

**解决方案**：在安卓设备上查看并允许 USB 调试授权。

### 问题3：设备离线

**解决方案**：
1. 断开并重新连接 USB 线
2. 重启 ADB 服务：`adb kill-server && adb start-server`
3. 重新授权 USB 调试

### 问题4：多个设备连接

**解决方案**：如果连接了多个设备，使用设备序列号指定目标：

```cmd
adb -s <设备序列号> logcat -s USB_MUSIC:D
```

## 测试 ADB 功能

### 测试日志读取

```cmd
adb logcat -s USB_MUSIC:D
```

### 测试按键发送

```cmd
adb shell input keyevent 4  # 返回键
adb shell input keyevent 85 # 播放/暂停
```

## 安全注意事项

- 仅在受信任的电脑上启用 USB 调试
- 不要在不安全的网络环境中使用 ADB
- 定期检查已授权的设备列表

## 更多资源

- [Android 开发者文档](https://developer.android.com/studio/command-line/adb)
- [ADB 命令参考](https://developer.android.com/studio/command-line/adb#commandsummary)
- [Platform Tools 下载](https://developer.android.com/studio/releases/platform-tools)
