using System;
using System.Diagnostics;
using System.IO;
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
        
        public MainWindow()
        {
            InitializeComponent();
            InitializeTimer();
            UpdateConnectionStatus(false);
        }
        
        private void InitializeTimer()
        {
            progressTimer = new DispatcherTimer();
            progressTimer.Interval = TimeSpan.FromSeconds(1);
            progressTimer.Tick += ProgressTimer_Tick;
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
                    MessageBox.Show("未找到ADB工具，请确保已安装Android SDK或ADB工具", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
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
            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "adb",
                        Arguments = "version",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    }
                };
                
                process.Start();
                process.WaitForExit();
                
                return process.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }
        
        private async Task StartAdbLogcat()
        {
            try
            {
                // 先清理之前的日志
                await ExecuteAdbCommand("logcat -c");
                
                // 启动logcat监听，过滤USB_MUSIC标签
                adbProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "adb",
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
                
                LogMessage("ADB logcat进程已启动");
            }
            catch (Exception ex)
            {
                LogMessage($"启动ADB logcat失败: {ex.Message}");
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
            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "adb",
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
                    LogMessage($"ADB命令执行失败: {arguments}");
                }
            }
            catch (Exception ex)
            {
                LogMessage($"执行ADB命令失败: {ex.Message}");
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
                
                LogMessage($"发送控制命令: {action}");
            }
            catch (Exception ex)
            {
                LogMessage($"发送控制命令失败: {ex.Message}");
            }
        }
        
        protected override void OnClosed(EventArgs e)
        {
            StopListening();
            base.OnClosed(e);
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
