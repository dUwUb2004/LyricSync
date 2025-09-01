# LyricSync Windows 项目结构

## 项目概述
LyricSync Windows 是一个基于 WPF 的桌面应用程序，用于同步 Android 设备的音乐播放信息并搜索网易云音乐。

## 项目架构

### 目录结构
```
LyricSync.Windows/
├── Models/                 # 数据模型
│   ├── MusicInfo.cs       # 音乐信息模型
│   └── NeteaseModels.cs   # 网易云音乐API数据模型
├── Services/               # 业务逻辑服务
│   ├── AdbService.cs      # ADB工具服务
│   ├── NeteaseMusicService.cs # 网易云音乐API服务
│   └── UIService.cs       # UI更新服务
├── Utils/                  # 工具类
│   ├── ILogger.cs         # 日志接口
│   ├── Logger.cs          # 日志实现
│   ├── TimeFormatter.cs   # 时间格式化工具
│   └── JsonFormatter.cs   # JSON格式化工具
├── ViewModels/             # 视图模型
│   └── MainViewModel.cs   # 主窗口视图模型
├── Views/                  # 视图文件
│   ├── App.xaml           # 应用程序定义
│   └── MainWindow.xaml    # 主窗口视图
├── Properties/             # 项目属性
├── Tools/                  # ADB工具文件
├── Lib/                    # 第三方库
└── MainWindow.xaml.cs      # 主窗口代码后台
```

## 架构设计

### 分层架构
1. **表示层 (Views)**: XAML 文件和代码后台
2. **视图模型层 (ViewModels)**: 业务逻辑和状态管理
3. **服务层 (Services)**: 业务逻辑服务
4. **数据层 (Models)**: 数据模型和实体类
5. **工具层 (Utils)**: 通用工具和辅助类

### 设计模式
- **MVVM (Model-View-ViewModel)**: 分离视图和业务逻辑
- **依赖注入**: 通过构造函数注入依赖
- **事件驱动**: 使用事件进行组件间通信
- **异步编程**: 使用 async/await 处理异步操作

## 核心组件

### Models
- `MusicInfo`: 音乐播放信息模型
- `NeteaseSong`: 网易云音乐歌曲模型
- `NeteaseArtist`: 艺术家信息模型
- `NeteaseAlbum`: 专辑信息模型

### Services
- `AdbService`: 管理 ADB 工具和 Android 设备连接
- `NeteaseMusicService`: 处理网易云音乐 API 调用
- `UIService`: 管理 UI 更新和显示逻辑

### ViewModels
- `MainViewModel`: 主窗口的业务逻辑和状态管理

### Utils
- `ILogger`/`Logger`: 日志记录和管理
- `TimeFormatter`: 时间格式化工具
- `JsonFormatter`: JSON 格式化工具

## 主要功能

1. **Android 设备连接**: 通过 ADB 连接 Android 设备
2. **音乐信息监听**: 监听设备音乐播放状态
3. **网易云音乐搜索**: 根据音乐信息搜索匹配的歌曲
4. **专辑封面显示**: 显示搜索到的歌曲封面
5. **音乐控制**: 远程控制 Android 设备音乐播放

## 技术栈

- **框架**: .NET Framework 4.8
- **UI**: WPF (Windows Presentation Foundation)
- **序列化**: Newtonsoft.Json
- **异步编程**: Task-based Asynchronous Pattern (TAP)
- **进程管理**: System.Diagnostics.Process

## 开发说明

### 添加新功能
1. 在相应的层中添加新的类
2. 遵循现有的命名约定和架构模式
3. 使用依赖注入和事件驱动进行组件通信

### 修改现有功能
1. 保持接口不变，确保向后兼容
2. 更新相关的单元测试
3. 更新文档说明

### 代码规范
- 使用中文注释
- 遵循 C# 编码规范
- 使用有意义的变量和方法命名
- 添加适当的异常处理

## 构建和部署

### 构建要求
- Visual Studio 2019 或更高版本
- .NET Framework 4.8 SDK
- Windows 10 或更高版本

### 构建步骤
1. 打开解决方案文件 `LyricSync.Windows.sln`
2. 还原 NuGet 包
3. 构建解决方案
4. 运行应用程序

### 部署说明
- 确保目标机器安装了 .NET Framework 4.8
- 复制所有必要的 DLL 文件
- 确保 ADB 工具文件完整

## 故障排除

### 常见问题
1. **ADB 连接失败**: 检查设备 USB 调试是否启用
2. **网易云 API 连接失败**: 检查 API 服务器是否运行
3. **UI 更新异常**: 检查是否在正确的线程上更新 UI

### 日志查看
- 应用程序内置日志系统
- 日志文件位置: 应用程序目录下的日志文件
- 日志级别: INFO, WARNING, ERROR

## 贡献指南

1. Fork 项目
2. 创建功能分支
3. 提交更改
4. 推送到分支
5. 创建 Pull Request

## 许可证

本项目采用 MIT 许可证。
