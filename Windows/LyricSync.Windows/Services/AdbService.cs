using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using LyricSync.Windows.Utils;

namespace LyricSync.Windows.Services
{
    public class AdbService
    {
        private Process adbProcess;
        private string adbPath;
        private readonly ILogger logger;

        public AdbService(ILogger logger)
        {
            this.logger = logger;
        }

        public async Task<bool> InitializeAsync()
        {
            try
            {
                // 从嵌入式资源中提取ADB工具
                string tempDir = Path.Combine(Path.GetTempPath(), "LyricSync_ADB");
                if (!Directory.Exists(tempDir))
                {
                    Directory.CreateDirectory(tempDir);
                }
                
                string adbExePath = Path.Combine(tempDir, "adb.exe");
                string adbApiPath = Path.Combine(tempDir, "AdbWinApi.dll");
                string adbUsbApiPath = Path.Combine(tempDir, "AdbWinUsbApi.dll");
                
                // 检查是否需要提取文件
                bool needExtract = !File.Exists(adbExePath) || !File.Exists(adbApiPath) || !File.Exists(adbUsbApiPath);
                
                if (needExtract)
                {
                    logger.LogMessage("🔧 正在从嵌入式资源中提取ADB工具...");
                    
                    // 提取adb.exe
                    ExtractEmbeddedResource("adb.exe", adbExePath);
                    
                    // 提取AdbWinApi.dll
                    ExtractEmbeddedResource("AdbWinApi.dll", adbApiPath);
                    
                    // 提取AdbWinUsbApi.dll
                    ExtractEmbeddedResource("AdbWinUsbApi.dll", adbUsbApiPath);
                    
                    logger.LogMessage("✅ ADB工具提取完成");
                }
                
                adbPath = adbExePath;
                logger.LogMessage("✅ 内置ADB工具已就绪，路径: " + adbPath);
                logger.LogMessage("📱 可以开始连接Android设备");
                
                return await CheckAdbAvailableAsync();
            }
            catch (Exception ex)
            {
                logger.LogMessage("❌ 初始化ADB工具失败: " + ex.Message);
                adbPath = null;
                return false;
            }
        }

        public async Task<bool> CheckAdbAvailableAsync()
        {
            if (string.IsNullOrEmpty(adbPath))
            {
                logger.LogMessage("❌ ADB工具路径未设置，请先下载ADB工具");
                return false;
            }
            
            try
            {
                logger.LogMessage("🔍 正在检测内置ADB工具...");
                
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = adbPath,
                        Arguments = "version",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    }
                };
                
                process.Start();
                await Task.Run(() => process.WaitForExit());
                
                if (process.ExitCode == 0)
                {
                    logger.LogMessage($"✅ 内置ADB工具检测成功: {adbPath}");
                    logger.LogMessage("🚀 ADB工具已就绪，可以开始连接设备");
                    return true;
                }
                else
                {
                    logger.LogMessage($"❌ 内置ADB工具检测失败，退出码: {process.ExitCode}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                logger.LogMessage($"❌ 检测内置ADB工具时发生错误: {ex.Message}");
                logger.LogMessage("💡 请确保ADB工具文件完整且可执行");
                return false;
            }
        }

        public async Task StartLogcatAsync(Action<string> onOutputReceived, Action<string> onErrorReceived)
        {
            if (string.IsNullOrEmpty(adbPath))
            {
                logger.LogMessage("❌ 无法启动ADB logcat：ADB工具路径未设置");
                throw new InvalidOperationException("ADB工具路径未设置");
            }
            
            try
            {
                logger.LogMessage("🧹 清理之前的ADB日志...");
                // 先清理之前的日志
                await ExecuteCommandAsync("logcat -c");
                
                logger.LogMessage("📡 启动ADB logcat监听进程...");
                // 启动logcat监听，过滤USB_MUSIC标签
                adbProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = adbPath,
                        Arguments = "logcat -s USB_MUSIC:D",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                        StandardOutputEncoding = Encoding.UTF8
                    }
                };
                
                adbProcess.OutputDataReceived += (sender, e) => 
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        onOutputReceived?.Invoke(e.Data);
                    }
                };
                
                adbProcess.ErrorDataReceived += (sender, e) => 
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        onErrorReceived?.Invoke(e.Data);
                    }
                };
                
                adbProcess.Start();
                adbProcess.BeginOutputReadLine();
                adbProcess.BeginErrorReadLine();
                
                logger.LogMessage("✅ ADB logcat进程已启动，正在监听USB_MUSIC标签");
                logger.LogMessage("🎵 请在Android设备上播放音乐，音乐信息将自动同步");
            }
            catch (Exception ex)
            {
                logger.LogMessage($"❌ 启动ADB logcat失败: {ex.Message}");
                throw;
            }
        }

        public void StopLogcat()
        {
            try
            {
                if (adbProcess != null && !adbProcess.HasExited)
                {
                    adbProcess.Kill();
                    adbProcess.Dispose();
                    adbProcess = null;
                }
                logger.LogMessage("ADB logcat进程已停止");
            }
            catch (Exception ex)
            {
                logger.LogMessage($"停止ADB logcat失败: {ex.Message}");
            }
        }

        public async Task ExecuteCommandAsync(string arguments)
        {
            if (string.IsNullOrEmpty(adbPath))
            {
                logger.LogMessage("❌ 无法执行ADB命令：ADB工具路径未设置");
                return;
            }
            
            try
            {
                logger.LogMessage($"🔧 执行ADB命令: {arguments}");
                
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = adbPath,
                        Arguments = arguments,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    }
                };
                
                process.Start();
                await Task.Run(() => process.WaitForExit());
                
                if (process.ExitCode != 0)
                {
                    logger.LogMessage($"❌ ADB命令执行失败: {arguments}，退出码: {process.ExitCode}");
                }
                else
                {
                    logger.LogMessage($"✅ ADB命令执行成功: {arguments}");
                }
            }
            catch (Exception ex)
            {
                logger.LogMessage($"❌ 执行ADB命令时发生错误: {ex.Message}");
            }
        }

        public async Task SendControlCommandAsync(int keyCode)
        {
            if (string.IsNullOrEmpty(adbPath))
            {
                logger.LogMessage("❌ 无法发送控制命令：ADB工具路径未设置");
                return;
            }
            
            try
            {
                // 通过ADB发送按键事件到安卓端
                string command = $"shell input keyevent {keyCode}";
                await ExecuteCommandAsync(command);
                
                string action;
                switch (keyCode)
                {
                    case 85:
                        action = "播放/暂停";
                        break;
                    case 87:
                        action = "下一首";
                        break;
                    case 88:
                        action = "上一首";
                        break;
                    default:
                        action = "未知命令";
                        break;
                }
                
                logger.LogMessage($"🎮 发送音乐控制命令: {action}");
            }
            catch (Exception ex)
            {
                logger.LogMessage($"❌ 发送控制命令失败: {ex.Message}");
            }
        }

        private void ExtractEmbeddedResource(string resourceName, string outputPath)
        {
            try
            {
                // 获取当前程序集
                Assembly assembly = Assembly.GetExecutingAssembly();
                
                // 构建完整的资源名称（包含命名空间）
                string fullResourceName = $"LyricSync.Windows.Tools.{resourceName}";
                
                // 从嵌入式资源中读取数据
                using (Stream resourceStream = assembly.GetManifestResourceStream(fullResourceName))
                {
                    if (resourceStream == null)
                    {
                        throw new Exception($"找不到嵌入式资源: {fullResourceName}");
                    }
                    
                    // 写入到临时文件
                    using (FileStream fileStream = new FileStream(outputPath, FileMode.Create))
                    {
                        resourceStream.CopyTo(fileStream);
                    }
                }
                
                logger.LogMessage($"✅ 已提取: {resourceName}");
            }
            catch (Exception ex)
            {
                throw new Exception($"提取资源 {resourceName} 失败: {ex.Message}");
            }
        }

        public void Cleanup()
        {
            try
            {
                // 停止所有ADB进程
                if (adbProcess != null && !adbProcess.HasExited)
                {
                    try
                    {
                        adbProcess.Kill();
                        adbProcess.Dispose();
                    }
                    catch { }
                }
                
                // 等待一下让进程完全退出
                System.Threading.Thread.Sleep(1000);
                
                // 清理临时ADB工具文件
                string tempDir = Path.Combine(Path.GetTempPath(), "LyricSync_ADB");
                if (Directory.Exists(tempDir))
                {
                    try
                    {
                        Directory.Delete(tempDir, true);
                        logger.LogMessage("🧹 临时ADB工具文件已清理");
                    }
                    catch (Exception ex)
                    {
                        logger.LogMessage($"⚠️ 清理临时文件时出错: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogMessage($"⚠️ 清理ADB服务时出错: {ex.Message}");
            }
        }
    }
}
