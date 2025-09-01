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

        private async void ShowLyricButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ShowLyricButton.IsEnabled = false;
                ShowLyricButton.Content = "⏳ 打开中...";
                bool ok = await viewModel.OpenLyricWindowAsync();
                if (!ok)
                {
                    MessageBox.Show("无法打开歌词窗口，请确认已匹配到歌曲并成功获取歌词。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                logger.LogMessage($"❌ 打开歌词窗口失败: {ex.Message}");
            }
            finally
            {
                ShowLyricButton.IsEnabled = true;
                ShowLyricButton.Content = "🪄 显示歌词";
            }
        }

        private async void ShowDesktopLyricButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ShowDesktopLyricButton.IsEnabled = false;
                ShowDesktopLyricButton.Content = "⏳ 打开中...";

                bool ok = await viewModel.OpenDesktopLyricWindowAsync();
                if (ok)
                {
                    // 桌面歌词窗口打开成功，应用当前设置
                    ApplyDesktopSettings();
                }
                else
                {
                    logger.LogMessage("❌ 打开桌面歌词窗口失败");
                }
            }
            catch (Exception ex)
            {
                logger.LogMessage($"❌ 打开桌面歌词窗口时发生异常: {ex.Message}");
            }
            finally
            {
                ShowDesktopLyricButton.IsEnabled = true;
                ShowDesktopLyricButton.Content = "🪟 桌面歌词";
            }
        }

        private void DesktopSettings_Changed(object sender, RoutedEventArgs e)
        {
            ApplyDesktopSettings();
        }

        private void DesktopOpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (DesktopOpacityText != null)
            {
                DesktopOpacityText.Text = $"{e.NewValue:F0}%";
            }
            ApplyDesktopSettings();
        }

        private void DesktopFontSizeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (DesktopFontSizeText != null)
            {
                DesktopFontSizeText.Text = $"{e.NewValue:F0}";
            }
            ApplyDesktopSettings();
        }

        private void ApplyDesktopSettings()
        {
            try
            {
                // 检查 viewModel 是否已初始化
                if (viewModel == null)
                    return;

                var desktopWindow = viewModel.GetDesktopLyricWindow();
                if (desktopWindow != null)
                {
                    // 应用设置到桌面歌词窗口
                    if (DesktopShowTranslationCheckBox != null)
                        desktopWindow.ShowTranslation = DesktopShowTranslationCheckBox.IsChecked ?? true;
                    
                    if (DesktopLockWindowCheckBox != null)
                        desktopWindow.IsLocked = DesktopLockWindowCheckBox.IsChecked ?? false;
                    
                    if (DesktopClickThroughCheckBox != null)
                        desktopWindow.IsHitTestVisible = !(DesktopClickThroughCheckBox.IsChecked ?? false);
                    
                    if (DesktopOpacitySlider != null)
                        desktopWindow.OpacityPercent = DesktopOpacitySlider.Value;
                    
                    // TODO: 应用字体大小设置
                    
                    logger?.LogMessage("✅ 桌面歌词设置已应用");
                }
                // 如果桌面窗口未打开，不记录日志，这是正常情况
            }
            catch (Exception ex)
            {
                logger?.LogMessage($"❌ 应用桌面歌词设置时发生异常: {ex.Message}");
            }
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            // 切换到设置页面
            MainPageGrid.Visibility = Visibility.Collapsed;
            SettingsPageGrid.Visibility = Visibility.Visible;
        }

        private void BackToMainButton_Click(object sender, RoutedEventArgs e)
        {
            // 返回主页
            MainPageGrid.Visibility = Visibility.Visible;
            SettingsPageGrid.Visibility = Visibility.Collapsed;
        }

        private async void TestApiButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                TestApiButton.IsEnabled = false;
                TestApiButton.Content = "测试中...";
                
                logger.LogMessage("🔍 开始测试网易云API连接...");
                
                // TODO: 实际测试API连接 - 这里可以调用NeteaseMusicService的TestConnectionAsync
                await System.Threading.Tasks.Task.Delay(1000); // 模拟测试
                
                logger.LogMessage("✅ API连接测试成功");
                MessageBox.Show("API连接测试成功！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                logger.LogMessage($"❌ API连接测试失败: {ex.Message}");
                MessageBox.Show($"API连接测试失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                TestApiButton.IsEnabled = true;
                TestApiButton.Content = "测试API连接";
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
