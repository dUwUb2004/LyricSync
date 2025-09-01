# LyricSync Windows 项目构建脚本
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "LyricSync Windows 项目构建脚本" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

Write-Host "正在清理之前的构建文件..." -ForegroundColor Yellow
if (Test-Path "obj") { Remove-Item -Recurse -Force "obj" }
if (Test-Path "bin") { Remove-Item -Recurse -Force "bin" }
Write-Host "清理完成！" -ForegroundColor Green
Write-Host ""

Write-Host "正在构建项目..." -ForegroundColor Yellow
Write-Host "请确保已安装 Visual Studio 或 .NET Framework SDK" -ForegroundColor Yellow
Write-Host ""

# 尝试使用 MSBuild
try {
    $msbuildPath = Get-Command msbuild -ErrorAction Stop
    Write-Host "使用 MSBuild 构建..." -ForegroundColor Green
    Write-Host "MSBuild 路径: $($msbuildPath.Source)" -ForegroundColor Gray
    
    & msbuild LyricSync.Windows.csproj /p:Configuration=Debug /p:Platform="Any CPU" /verbosity:minimal
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host ""
        Write-Host "✅ 构建成功！" -ForegroundColor Green
        Write-Host "可执行文件位置: bin\Debug\LyricSync.Windows.exe" -ForegroundColor Green
    } else {
        Write-Host ""
        Write-Host "❌ MSBuild 构建失败！" -ForegroundColor Red
        Write-Host "请检查错误信息并修复问题。" -ForegroundColor Red
    }
} catch {
    Write-Host "MSBuild 未找到，尝试使用 dotnet..." -ForegroundColor Yellow
    
    try {
        & dotnet build --verbosity minimal
        
        if ($LASTEXITCODE -eq 0) {
            Write-Host ""
            Write-Host "✅ 构建成功！" -ForegroundColor Green
            Write-Host "可执行文件位置: bin\Debug\LyricSync.Windows.exe" -ForegroundColor Green
        } else {
            throw "dotnet 构建失败"
        }
    } catch {
        Write-Host ""
        Write-Host "❌ 构建失败！" -ForegroundColor Red
        Write-Host ""
        Write-Host "💡 解决方案：" -ForegroundColor Cyan
        Write-Host "1. 使用 Visual Studio 打开 LyricSync.Windows.sln" -ForegroundColor White
        Write-Host "2. 右键点击解决方案 → '重新生成解决方案'" -ForegroundColor White
        Write-Host "3. 或者安装 .NET Framework SDK 并使用 MSBuild" -ForegroundColor White
        Write-Host ""
        Write-Host "注意：这是一个 .NET Framework 4.8 的 WPF 项目，" -ForegroundColor Yellow
        Write-Host "dotnet 命令可能不完全支持。" -ForegroundColor Yellow
    }
}

Write-Host ""
Write-Host "按任意键退出..." -ForegroundColor Gray
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
