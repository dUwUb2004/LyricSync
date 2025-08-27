@echo off
echo 正在清理Android项目构建文件...

echo 清理build目录...
if exist "app\build" (
    rmdir /s /q "app\build"
    echo ✓ 已清理app\build目录
)

if exist "build" (
    rmdir /s /q "build"
    echo ✓ 已清理build目录
)

echo 清理.gradle缓存...
if exist ".gradle" (
    rmdir /s /q ".gradle"
    echo ✓ 已清理.gradle缓存
)

echo 清理local.properties (如果存在)...
if exist "local.properties" (
    del "local.properties"
    echo ✓ 已清理local.properties
)

echo.
echo 清理完成！现在可以重新构建项目。
echo 建议执行: gradlew clean build
pause
