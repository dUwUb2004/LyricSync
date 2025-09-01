using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Newtonsoft.Json;
using LyricSync.Windows.Services;
using LyricSync.Windows.Utils;
using LyricSync.Windows.ViewModels;

namespace LyricSync.Windows
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        private MainViewModel viewModel;
        private Logger logger;
        private UIService uiService;
        private DispatcherTimer progressTimer;

        public MainWindow()
        {
            InitializeComponent();
            InitializeServices();
            InitializeTimer();
        }

        private void InitializeServices()
        {
            // 初始化日志服务
            logger = new Logger(LogTextBox, LogStatusText, Dispatcher);
            
            // 初始化UI服务
            uiService = new UIService(
                logger,
                SongTitle, ArtistName, AlbumName,
                ProgressBar, CurrentTime, TotalTime,
                StatusText, StatusDescription, BottomStatusText,
                MatchedSongTitle, MatchedSongArtist, MatchedSongAlbum,
                MatchedSongDuration, MatchedSongId, JsonDisplayTextBox,
                MatchedSongExpander, AlbumCoverImage, DefaultMusicIcon
            );
            
            // 初始化视图模型
            viewModel = new MainViewModel(logger, uiService);
            
            // 订阅音乐信息更新事件
            viewModel.OnMusicInfoUpdated += OnMusicInfoUpdated;
            
            // 设置初始连接状态
            uiService.UpdateConnectionStatus(false);
            
            // 异步初始化
            _ = InitializeAsync();
        }

        private async System.Threading.Tasks.Task InitializeAsync()
        {
            try
            {
                bool initialized = await viewModel.InitializeAsync();
                if (initialized)
                {
                    logger.LogMessage("✅ 系统初始化完成");
                }
                else
                {
                    logger.LogMessage("❌ 系统初始化失败");
                }
            }
            catch (Exception ex)
            {
                logger.LogMessage($"❌ 初始化过程中发生错误: {ex.Message}");
            }
        }

        private void InitializeTimer()
        {
            progressTimer = new DispatcherTimer();
            progressTimer.Interval = TimeSpan.FromSeconds(1);
            progressTimer.Tick += ProgressTimer_Tick;
        }

        private void ProgressTimer_Tick(object sender, EventArgs e)
        {
            var currentMusic = viewModel.CurrentMusic;
            if (currentMusic != null && currentMusic.IsPlaying)
            {
                // 只更新进度条的值和当前时间，不覆盖总时长
                uiService.UpdateProgressBarValueOnly(currentMusic);
            }
        }

        private async void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            if (!viewModel.IsListening)
            {
                await StartListening();
            }
            else
            {
                StopListening();
            }
        }

        private async System.Threading.Tasks.Task StartListening()
        {
            try
            {
                uiService.UpdateConnectionStatus(false, "启动中...");
                uiService.UpdateBottomStatus("正在启动ADB日志监听...");
                
                await viewModel.StartListeningAsync();
                
                uiService.UpdateConnectionStatus(true, "正在监听安卓端日志");
                
                // 使用Dispatcher确保UI更新在主线程执行
                Dispatcher.Invoke(() =>
                {
                    ConnectButton.Content = "停止监听";
                });
                
                uiService.UpdateBottomStatus("正在监听安卓端日志，请确保安卓端已启动并播放音乐");
            }
            catch (Exception ex)
            {
                logger.LogMessage($"启动监听失败: {ex.Message}");
                uiService.UpdateConnectionStatus(false, "启动失败");
                uiService.UpdateBottomStatus("启动失败");
                MessageBox.Show($"启动监听失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void StopListening()
        {
            try
            {
                viewModel.StopListening();
                uiService.UpdateConnectionStatus(false);
                
                // 使用Dispatcher确保UI更新在主线程执行
                Dispatcher.Invoke(() =>
                {
                    ConnectButton.Content = "开始监听";
                });
                
                uiService.UpdateBottomStatus("准备就绪");
                
                // 停止进度条更新
                progressTimer.Stop();
                uiService.ResetMusicDisplay();
            }
            catch (Exception ex)
            {
                logger.LogMessage($"停止监听时出错: {ex.Message}");
            }
        }

        private async void PlayPauseButton_Click(object sender, RoutedEventArgs e)
        {
            if (viewModel.IsListening)
            {
                await viewModel.SendControlCommandAsync(85); // 播放/暂停
            }
        }

        private async void PreviousButton_Click(object sender, RoutedEventArgs e)
        {
            if (viewModel.IsListening)
            {
                await viewModel.SendControlCommandAsync(88); // 上一首
            }
        }

        private async void NextButton_Click(object sender, RoutedEventArgs e)
        {
            if (viewModel.IsListening)
            {
                await viewModel.SendControlCommandAsync(87); // 下一首
            }
        }

        private async void TestSearchButton_Click(object sender, RoutedEventArgs e)
        {
            await viewModel.TestManualSearchAsync();
        }

        private void ClearLogButton_Click(object sender, RoutedEventArgs e)
        {
            logger.ClearLogs();
        }

        private void LogInfoButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string info = logger.GetLogInfo();
                MessageBox.Show(info, "日志信息", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                logger.LogMessage($"❌ 获取日志信息失败: {ex.Message}");
            }
        }

        private async void ExportLrcButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 禁用按钮防止重复点击
                ExportLrcButton.IsEnabled = false;
                ExportLrcButton.Content = "⏳ 导出中...";
                
                logger.LogMessage("🎵 用户点击导出LRC歌词按钮");
                
                // 调用ViewModel的导出方法
                bool success = await viewModel.ExportLrcLyricAsync();
                
                if (success)
                {
                    logger.LogMessage("✅ LRC歌词导出完成");
                }
                else
                {
                    logger.LogMessage("❌ LRC歌词导出失败");
                }
            }
            catch (Exception ex)
            {
                logger.LogMessage($"❌ 导出LRC歌词时发生异常: {ex.Message}");
            }
            finally
            {
                // 恢复按钮状态
                ExportLrcButton.IsEnabled = true;
                ExportLrcButton.Content = "📄 导出LRC歌词";
            }
        }

        private void OnMusicInfoUpdated(Models.MusicInfo musicInfo)
        {
            if (musicInfo != null)
            {
                uiService.UpdateMusicDisplay(musicInfo);
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            try
            {
                // 取消订阅事件
                if (viewModel != null)
                {
                    viewModel.OnMusicInfoUpdated -= OnMusicInfoUpdated;
                }
                
                progressTimer?.Stop();
                viewModel?.Dispose();
            }
            catch (Exception ex)
            {
                logger.LogMessage($"⚠️ 关闭窗口时出错: {ex.Message}");
            }
            base.OnClosed(e);
        }
    }
}
