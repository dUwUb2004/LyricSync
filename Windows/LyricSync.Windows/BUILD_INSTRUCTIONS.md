# LyricSync Windows 项目构建说明

## 项目概述
这是一个基于 .NET Framework 4.8 的 WPF 桌面应用程序，用于同步 Android 设备的音乐播放信息并搜索网易云音乐。

## 构建要求
- Windows 10 或更高版本
- .NET Framework 4.8 或更高版本
- Visual Studio 2019/2022 或 .NET Framework SDK

## 构建方法

### 方法 1：使用 Visual Studio（推荐）
1. 双击 `LyricSync.Windows.sln` 文件打开解决方案
2. 确保已安装 .NET Framework 4.8 开发工具
3. 右键点击解决方案 → "重新生成解决方案"
4. 构建成功后，按 F5 运行项目

### 方法 2：使用 MSBuild
1. 打开 "Developer Command Prompt for VS" 或 "x64 Native Tools Command Prompt"
2. 导航到项目目录
3. 运行以下命令：
   ```cmd
   msbuild LyricSync.Windows.csproj /p:Configuration=Debug /p:Platform="Any CPU"
   ```

### 方法 3：使用构建脚本
1. 双击 `build.bat` 文件（Windows 批处理）
2. 或在 PowerShell 中运行 `.\build.ps1`

### 方法 4：使用 dotnet 命令（可能不完全支持）
```cmd
dotnet build --verbosity quiet
```

## 常见问题

### 问题 1：找不到 MSBuild
**解决方案**：安装 Visual Studio 或 .NET Framework SDK

### 问题 2：XAML 编译错误
**原因**：dotnet 命令不完全支持 .NET Framework WPF 项目
**解决方案**：使用 Visual Studio 或 MSBuild

### 问题 3：缺少依赖项
**解决方案**：确保已安装 Newtonsoft.Json 库（项目已包含在 Lib 目录中）

## 项目结构
```
LyricSync.Windows/
├── Models/                 # 数据模型层
├── Services/               # 业务逻辑服务层
├── Utils/                  # 工具类层
├── ViewModels/             # 视图模型层
├── Views/                  # 视图文件（XAML）
├── Tools/                  # ADB 工具文件
├── Lib/                    # 第三方库
├── Properties/             # 项目属性
├── LyricSync.Windows.sln  # 解决方案文件
├── LyricSync.Windows.csproj # 项目文件
├── build.bat              # Windows 构建脚本
├── build.ps1              # PowerShell 构建脚本
└── BUILD_INSTRUCTIONS.md  # 本文件
```

## 运行项目
构建成功后，可执行文件位于：
- Debug 版本：`bin\Debug\LyricSync.Windows.exe`
- Release 版本：`bin\Release\LyricSync.Windows.exe`

## 注意事项
- 这是一个传统的 .NET Framework 项目，不是 .NET Core/.NET 5+ 项目
- 使用 dotnet 命令可能无法正确编译 XAML 文件
- 推荐使用 Visual Studio 进行开发和构建
- 确保 Android 设备已启用 USB 调试模式
- 确保网易云音乐 API 服务器正在运行（http://localhost:3000）

## 技术支持
如果遇到构建问题，请：
1. 检查是否安装了正确的 .NET Framework 版本
2. 尝试使用 Visual Studio 打开解决方案
3. 检查错误日志和构建输出
4. 确保所有依赖项都已正确安装
