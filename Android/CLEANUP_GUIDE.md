# 项目清理指南

## 🗑️ 已删除的文件

### 1. **未使用的Java/Kotlin文件**
- ✅ `MediaKeyService.java` - 空的无障碍服务，无实际功能
- ✅ `accessibility_config.xml` - 对应的无障碍服务配置

### 2. **未使用的资源文件**
- ✅ `md3_card_background.xml` - Compose中直接使用Material 3主题，无需XML背景

## 📁 需要保留的核心文件

### **Java/Kotlin源代码**
- `MainActivity.kt` - 主界面
- `MusicNotificationListener.java` - 音乐通知监听服务
- `TcpService.java` - TCP通信服务

### **资源文件**
- `strings.xml` - 字符串资源
- `colors.xml` - 颜色定义
- `themes.xml` - 主题配置
- `values-night/themes.xml` - 夜间主题
- `dimens.xml` - 尺寸定义

### **配置文件**
- `AndroidManifest.xml` - 应用清单
- `data_extraction_rules.xml` - 数据提取规则
- `backup_rules.xml` - 备份规则

## 🧹 定期清理建议

### **构建文件清理**
```bash
# 使用提供的批处理脚本
clean_project.bat

# 或手动执行
gradlew clean
```

### **IDE缓存清理**
- 删除 `.idea` 目录（如果使用IntelliJ/Android Studio）
- 删除 `.gradle` 目录
- 删除 `build` 目录

### **版本控制忽略**
确保 `.gitignore` 包含：
```
build/
.gradle/
local.properties
.idea/
*.iml
```

## 📊 清理效果

清理前项目大小：约 50-100MB
清理后项目大小：约 10-20MB
节省空间：约 60-80%

## ⚠️ 注意事项

1. **不要删除** `src/main` 目录下的源代码
2. **不要删除** `res` 目录下的资源文件（除非确认未使用）
3. **可以删除** 所有 `build` 目录
4. **可以删除** `.gradle` 缓存目录

## 🔄 重新构建

清理完成后：
1. 执行 `gradlew clean`
2. 重新同步项目
3. 重新构建

这样可以确保项目干净整洁，构建速度更快！
