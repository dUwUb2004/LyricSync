# LyricSync Windows 客户端

这是一个WPF应用程序，用于与Android设备同步音乐播放信息。

## 项目结构

```
LyricSync.Windows/
├── LyricSync.Windows.sln          # Visual Studio解决方案文件
├── LyricSync.Windows/             # 主项目目录
│   ├── LyricSync.Windows.csproj   # 项目文件
│   ├── App.xaml                   # 应用程序定义
│   ├── App.xaml.cs                # 应用程序代码
│   ├── MainWindow.xaml            # 主窗口界面
│   ├── MainWindow.xaml.cs         # 主窗口代码
│   ├── Properties/
│   │   └── AssemblyInfo.cs        # 程序集信息
│   ├── Lib/
│   │   └── Newtonsoft.Json.dll    # JSON序列化库
│   └── app.config                 # 应用程序配置
└── README.md                      # 本文件
```

## 系统要求

- Windows 10 或更高版本
- .NET Framework 4.8
- Visual Studio 2019 或更高版本
- **ADB工具已集成，无需单独安装**

## 构建说明

### 方法1：使用Visual Studio

1. 打开 `LyricSync.Windows.sln` 解决方案文件
2. 确保目标框架为 .NET Framework 4.8
3. 选择 **生成 → 生成解决方案**
4. 构建成功后，右键点击项目选择 **"设为启动项目"**
5. 按 **F5** 启动调试

### 方法2：使用命令行

```cmd
# 使用MSBuild构建
msbuild LyricSync.Windows.sln /p:Configuration=Debug /p:Platform="Any CPU"

# 或使用dotnet CLI（如果安装了.NET SDK）
dotnet build LyricSync.Windows.sln
```

### 方法3：首次构建（嵌入ADB工具）

如果是首次构建，可以运行以下脚本将ADB工具嵌入到exe中：

```cmd
embed_adb_tools.bat
```

然后正常构建项目。ADB工具将直接包含在exe文件中，无需额外文件。

## 功能特性

- **ADB连接管理**：自动检测和连接Android设备
- **音乐信息同步**：实时获取Android端音乐播放状态
- **网易云音乐匹配**：自动搜索并匹配歌曲信息
- **播放控制**：支持播放/暂停、上一首、下一首操作
- **进度显示**：显示当前播放进度和总时长
- **日志记录**：详细的连接和操作日志

## 使用方法

1. **启动应用**：运行编译后的exe文件
2. **连接设备**：确保Android设备已连接并启用USB调试
3. **开始监听**：点击"开始监听"按钮
4. **音乐控制**：使用播放控制按钮操作音乐播放

## 技术细节

- **框架**：WPF (.NET Framework 4.8)
- **语言**：C# 7.3
- **依赖**：Newtonsoft.Json (JSON序列化)
- **通信**：ADB命令行工具
- **UI**：XAML + 代码后台

## 注意事项

- **ADB工具已嵌入在exe文件中，完全自包含**
- **应用程序完全独立，不依赖任何外部文件**
- Android设备需要启用开发者选项和USB调试
- 首次构建前请运行 `embed_adb_tools.bat` 脚本嵌入ADB工具
- **需要启动网易云音乐API服务器**（默认地址：http://localhost:3000）

## 许可证

本项目采用MIT许可证。
