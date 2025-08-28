using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Newtonsoft.Json;

namespace LyricSync.Windows
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        private Process adbProcess;
        private bool isListening = false;
        private DispatcherTimer progressTimer;
        private MusicInfo currentMusic;
        private string adbPath;
        
        public MainWindow()
        {
            InitializeComponent();
            InitializeTimer();
            UpdateConnectionStatus(false);
            InitializeAdbPath();
        }
        
        private void InitializeTimer()
        {
            progressTimer = new DispatcherTimer();
            progressTimer.Interval = TimeSpan.FromSeconds(1);
            progressTimer.Tick += ProgressTimer_Tick;
        }
        
        private void InitializeAdbPath()
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
                    LogMessage("🔧 正在从嵌入式资源中提取ADB工具...");
                    
                    // 提取adb.exe
                    ExtractEmbeddedResource("adb.exe", adbExePath);
                    
                    // 提取AdbWinApi.dll
                    ExtractEmbeddedResource("AdbWinApi.dll", adbApiPath);
                    
                    // 提取AdbWinUsbApi.dll
                    ExtractEmbeddedResource("AdbWinUsbApi.dll", adbUsbApiPath);
                    
                    LogMessage("✅ ADB工具提取完成");
                }
                
                adbPath = adbExePath;
                LogMessage("✅ 内置ADB工具已就绪，路径: " + adbPath);
                LogMessage("📱 可以开始连接Android设备");
            }
            catch (Exception ex)
            {
                LogMessage("❌ 初始化ADB工具失败: " + ex.Message);
                adbPath = null;
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
                
                LogMessage($"✅ 已提取: {resourceName}");
            }
            catch (Exception ex)
            {
                throw new Exception($"提取资源 {resourceName} 失败: {ex.Message}");
            }
        }
        
        private void ProgressTimer_Tick(object sender, EventArgs e)
        {
            if (currentMusic != null && currentMusic.IsPlaying)
            {
                currentMusic.Position += 1000; // 增加1秒
                UpdateProgressBar();
            }
        }
        
        private async void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            if (!isListening)
            {
                await StartListening();
            }
            else
            {
                StopListening();
            }
        }
        
        private async Task StartListening()
        {
            try
            {
                LogMessage("正在启动ADB日志监听...");
                UpdateConnectionStatus(false, "启动中...");
                
                // 检查ADB是否可用
                if (!await CheckAdbAvailable())
                {
                    MessageBox.Show("未找到内置ADB工具！\n\n请按以下步骤操作：\n1. 运行 download_adb_tools.bat 脚本下载ADB工具\n2. 或者手动将ADB工具复制到 Tools 目录\n3. 重新启动应用程序", "ADB工具缺失", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                
                // 启动ADB logcat监听
                await StartAdbLogcat();
                
                isListening = true;
                UpdateConnectionStatus(true, "正在监听安卓端日志");
                ConnectButton.Content = "停止监听";
                LogMessage("ADB日志监听已启动，等待音乐信息...");
                
                BottomStatusText.Text = "正在监听安卓端日志，请确保安卓端已启动并播放音乐";
            }
            catch (Exception ex)
            {
                LogMessage($"启动监听失败: {ex.Message}");
                UpdateConnectionStatus(false, "启动失败");
                MessageBox.Show($"启动监听失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void StopListening()
        {
            try
            {
                StopAdbLogcat();
                isListening = false;
                UpdateConnectionStatus(false);
                ConnectButton.Content = "开始监听";
                LogMessage("已停止监听");
                BottomStatusText.Text = "准备就绪";
                
                // 停止进度条更新
                progressTimer.Stop();
                currentMusic = null;
                ResetMusicDisplay();
            }
            catch (Exception ex)
            {
                LogMessage($"停止监听时出错: {ex.Message}");
            }
        }
        
        private async Task<bool> CheckAdbAvailable()
        {
            // 检查ADB路径是否已设置
            if (string.IsNullOrEmpty(adbPath))
            {
                LogMessage("❌ ADB工具路径未设置，请先下载ADB工具");
                return false;
            }
            
            try
            {
                LogMessage("🔍 正在检测内置ADB工具...");
                
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
                process.WaitForExit();
                
                if (process.ExitCode == 0)
                {
                    LogMessage($"✅ 内置ADB工具检测成功: {adbPath}");
                    LogMessage("🚀 ADB工具已就绪，可以开始连接设备");
                    return true;
                }
                else
                {
                    LogMessage($"❌ 内置ADB工具检测失败，退出码: {process.ExitCode}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                LogMessage($"❌ 检测内置ADB工具时发生错误: {ex.Message}");
                LogMessage("💡 请确保ADB工具文件完整且可执行");
                return false;
            }
        }
        
        private async Task StartAdbLogcat()
        {
            // 检查ADB路径是否已设置
            if (string.IsNullOrEmpty(adbPath))
            {
                LogMessage("❌ 无法启动ADB logcat：ADB工具路径未设置");
                throw new InvalidOperationException("ADB工具路径未设置");
            }
            
            try
            {
                LogMessage("🧹 清理之前的ADB日志...");
                // 先清理之前的日志
                await ExecuteAdbCommand("logcat -c");
                
                LogMessage("📡 启动ADB logcat监听进程...");
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
                
                adbProcess.OutputDataReceived += OnLogcatOutput;
                adbProcess.ErrorDataReceived += OnLogcatError;
                
                adbProcess.Start();
                adbProcess.BeginOutputReadLine();
                adbProcess.BeginErrorReadLine();
                
                LogMessage("✅ ADB logcat进程已启动，正在监听USB_MUSIC标签");
                LogMessage("🎵 请在Android设备上播放音乐，音乐信息将自动同步");
            }
            catch (Exception ex)
            {
                LogMessage($"❌ 启动ADB logcat失败: {ex.Message}");
                throw;
            }
        }
        
        private void StopAdbLogcat()
        {
            try
            {
                if (adbProcess != null && !adbProcess.HasExited)
                {
                    adbProcess.Kill();
                    adbProcess.Dispose();
                    adbProcess = null;
                }
                LogMessage("ADB logcat进程已停止");
            }
            catch (Exception ex)
            {
                LogMessage($"停止ADB logcat失败: {ex.Message}");
            }
        }
        
        private void OnLogcatOutput(object sender, DataReceivedEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                ProcessLogcatLine(e.Data);
            }
        }
        
        private void OnLogcatError(object sender, DataReceivedEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                LogMessage($"ADB错误: {e.Data}");
            }
        }
        
        private void ProcessLogcatLine(string line)
        {
            try
            {
                // 查找JSON数据
                int jsonStart = line.IndexOf('{');
                if (jsonStart >= 0)
                {
                    string jsonData = line.Substring(jsonStart);
                    ProcessMusicData(jsonData);
                }
            }
            catch (Exception ex)
            {
                LogMessage($"处理日志行失败: {ex.Message}");
            }
        }
        
        private void ProcessMusicData(string data)
        {
            try
            {
                // 尝试解析JSON数据
                var musicInfo = JsonConvert.DeserializeObject<MusicInfo>(data);
                if (musicInfo != null)
                {
                    Dispatcher.Invoke(() =>
                    {
                        currentMusic = musicInfo;
                        UpdateMusicDisplay(musicInfo);
                        LogMessage($"收到音乐信息: {musicInfo.Title} - {musicInfo.Artist}");
                        
                        if (musicInfo.IsPlaying)
                        {
                            progressTimer.Start();
                        }
                        else
                        {
                            progressTimer.Stop();
                        }
                    });
                }
            }
            catch (JsonException ex)
            {
                LogMessage($"解析音乐数据失败: {ex.Message}");
            }
        }
        
        private async Task ExecuteAdbCommand(string arguments)
        {
            // 检查ADB路径是否已设置
            if (string.IsNullOrEmpty(adbPath))
            {
                LogMessage("❌ 无法执行ADB命令：ADB工具路径未设置");
                return;
            }
            
            try
            {
                LogMessage($"🔧 执行ADB命令: {arguments}");
                
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
                process.WaitForExit();
                
                if (process.ExitCode != 0)
                {
                    LogMessage($"❌ ADB命令执行失败: {arguments}，退出码: {process.ExitCode}");
                }
                else
                {
                    LogMessage($"✅ ADB命令执行成功: {arguments}");
                }
            }
            catch (Exception ex)
            {
                LogMessage($"❌ 执行ADB命令时发生错误: {ex.Message}");
            }
        }
        
        private void UpdateMusicDisplay(MusicInfo music)
        {
            SongTitle.Text = music.Title ?? "未知歌曲";
            ArtistName.Text = music.Artist ?? "未知艺术家";
            AlbumName.Text = music.Album ?? "未知专辑";
            
            UpdateProgressBar();
        }
        
        private void UpdateProgressBar()
        {
            if (currentMusic != null)
            {
                ProgressBar.Value = currentMusic.Position;
                CurrentTime.Text = FormatTime(currentMusic.Position);
                TotalTime.Text = FormatTime(currentMusic.Duration);
            }
        }
        
        private void ResetMusicDisplay()
        {
            SongTitle.Text = "未播放";
            ArtistName.Text = "";
            AlbumName.Text = "";
            ProgressBar.Value = 0;
            CurrentTime.Text = "0:00";
            TotalTime.Text = "0:00";
        }
        
        private string FormatTime(long milliseconds)
        {
            if (milliseconds <= 0) return "0:00";
            
            TimeSpan time = TimeSpan.FromMilliseconds(milliseconds);
            return $"{(int)time.TotalMinutes}:{time.Seconds:D2}";
        }
        
        private void UpdateConnectionStatus(bool connected, string description = null)
        {
            if (connected)
            {
                StatusText.Text = "正在监听";
                StatusText.Foreground = System.Windows.Media.Brushes.Green;
                StatusDescription.Text = description ?? "正在监听安卓端日志";
            }
            else
            {
                StatusText.Text = "未监听";
                StatusText.Foreground = System.Windows.Media.Brushes.Red;
                StatusDescription.Text = description ?? "请点击开始监听按钮";
            }
        }
        
        private void LogMessage(string message)
        {
            string timestamp = DateTime.Now.ToString("HH:mm:ss");
            string logEntry = $"[{timestamp}] {message}";
            
            Dispatcher.Invoke(() =>
            {
                LogTextBox.AppendText(logEntry + Environment.NewLine);
                LogTextBox.ScrollToEnd();
            });
        }
        
        private async void PlayPauseButton_Click(object sender, RoutedEventArgs e)
        {
            if (isListening)
            {
                await SendControlCommand(85); // 播放/暂停
            }
        }
        
        private async void PreviousButton_Click(object sender, RoutedEventArgs e)
        {
            if (isListening)
            {
                await SendControlCommand(88); // 上一首
            }
        }
        
        private async void NextButton_Click(object sender, RoutedEventArgs e)
        {
            if (isListening)
            {
                await SendControlCommand(87); // 下一首
            }
        }
        
        private async Task SendControlCommand(int keyCode)
        {
            // 检查ADB路径是否已设置
            if (string.IsNullOrEmpty(adbPath))
            {
                LogMessage("❌ 无法发送控制命令：ADB工具路径未设置");
                return;
            }
            
            try
            {
                // 通过ADB发送按键事件到安卓端
                string command = $"shell input keyevent {keyCode}";
                await ExecuteAdbCommand(command);
                
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
                
                LogMessage($"🎮 发送音乐控制命令: {action}");
            }
            catch (Exception ex)
            {
                LogMessage($"❌ 发送控制命令失败: {ex.Message}");
            }
        }
        
        protected override void OnClosed(EventArgs e)
        {
            StopListening();
            CleanupTempFiles();
            base.OnClosed(e);
        }
        
        private void CleanupTempFiles()
        {
            try
            {
                // 清理临时ADB工具文件
                string tempDir = Path.Combine(Path.GetTempPath(), "LyricSync_ADB");
                if (Directory.Exists(tempDir))
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
                    
                    // 删除临时文件
                    try
                    {
                        Directory.Delete(tempDir, true);
                        LogMessage("🧹 临时ADB工具文件已清理");
                    }
                    catch (Exception ex)
                    {
                        LogMessage($"⚠️ 清理临时文件时出错: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                LogMessage($"⚠️ 清理临时文件时出错: {ex.Message}");
            }
        }
    }
    
    public class MusicInfo
    {
        [JsonProperty("title")]
        public string Title { get; set; }
        
        [JsonProperty("artist")]
        public string Artist { get; set; }
        
        [JsonProperty("album")]
        public string Album { get; set; }
        
        [JsonProperty("position")]
        public long Position { get; set; }
        
        [JsonProperty("state")]
        public bool IsPlaying { get; set; }
        
        [JsonProperty("duration")]
        public long Duration { get; set; } = 0;
    }
}
