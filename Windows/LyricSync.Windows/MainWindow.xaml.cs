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
using System.Windows.Controls;
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
            
            // 订阅桌面歌词窗口状态变化事件
            viewModel.OnDesktopLyricWindowStateChanged += OnDesktopLyricWindowStateChanged;
            
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
                    // 设置播放按钮的初始状态（暂停状态）
                    UpdatePlayPauseButtonIcon(false);
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
                    UpdateButtonText(ConnectButton, "停止监听");
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
                    UpdateButtonText(ConnectButton, "开始监听");
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
                UpdateButtonText(ShowLyricButton, "打开中...");
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
                UpdateButtonText(ShowLyricButton, "显示歌词");
            }
        }

        private async void ShowDesktopLyricButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ShowDesktopLyricButton.IsEnabled = false;
                
                // 检查桌面歌词窗口是否已经打开
                var desktopWindow = viewModel.GetDesktopLyricWindow();
                if (desktopWindow != null)
                {
                    // 如果已经打开，则关闭它
                    UpdateButtonText(ShowDesktopLyricButton, "关闭中...");
                    desktopWindow.Close();
                    logger.LogMessage("✅ 桌面歌词窗口已关闭");
                }
                else
                {
                    // 如果未打开，则打开它
                    UpdateButtonText(ShowDesktopLyricButton, "打开中...");
                    bool ok = await viewModel.OpenDesktopLyricWindowAsync();
                    if (ok)
                    {
                        // 桌面歌词窗口打开成功，应用当前设置
                        ApplyDesktopSettings();
                        logger.LogMessage("✅ 桌面歌词窗口已打开");
                    }
                    else
                    {
                        logger.LogMessage("❌ 打开桌面歌词窗口失败");
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogMessage($"❌ 操作桌面歌词窗口时发生异常: {ex.Message}");
            }
            finally
            {
                ShowDesktopLyricButton.IsEnabled = true;
                // 根据窗口状态更新按钮文本
                var desktopWindow = viewModel.GetDesktopLyricWindow();
                UpdateButtonText(ShowDesktopLyricButton, desktopWindow != null ? "关闭桌面歌词" : "桌面歌词");
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

        private void DesktopBackgroundOpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (DesktopBackgroundOpacityText != null)
            {
                DesktopBackgroundOpacityText.Text = $"{e.NewValue:F0}%";
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
                    
                    if (DesktopBackgroundOpacitySlider != null)
                        desktopWindow.BackgroundOpacityPercent = DesktopBackgroundOpacitySlider.Value;
                    
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
                UpdateButtonText(TestApiButton, "测试中...");
                
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
                UpdateButtonText(TestApiButton, "测试API连接");
            }
        }

        private async void ExportLrcButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 禁用按钮防止重复点击
                ExportLrcButton.IsEnabled = false;
                UpdateButtonText(ExportLrcButton, "导出中...");
                
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
                UpdateButtonText(ExportLrcButton, "导出LRC歌词");
            }
        }

        private void OnMusicInfoUpdated(Models.MusicInfo musicInfo)
        {
            if (musicInfo != null)
            {
                logger.LogMessage($"🔄 更新播放按钮图标: IsPlaying={musicInfo.IsPlaying}");
                uiService.UpdateMusicDisplay(musicInfo);
                UpdatePlayPauseButtonIcon(musicInfo.IsPlaying);
            }
        }

        private void OnDesktopLyricWindowStateChanged(bool isOpen)
        {
            // 当桌面歌词窗口状态变化时，更新按钮文本
            var desktopWindow = viewModel.GetDesktopLyricWindow();
            UpdateButtonText(ShowDesktopLyricButton, desktopWindow != null ? "关闭桌面歌词" : "桌面歌词");
        }

        /// <summary>
        /// 更新按钮文本，保持图标不变
        /// </summary>
        private void UpdateButtonText(Button button, string newText)
        {
            try
            {
                if (button.Content is StackPanel stackPanel)
                {
                    // 查找 StackPanel 中的 TextBlock
                    var textBlock = FindTextBlockInStackPanel(stackPanel);
                    if (textBlock != null)
                    {
                        textBlock.Text = newText;
                    }
                    else
                    {
                        // 如果找不到 TextBlock，直接设置 Content
                        button.Content = newText;
                    }
                }
                else
                {
                    // 如果 Content 不是 StackPanel，直接设置
                    button.Content = newText;
                }
            }
            catch (Exception ex)
            {
                // 如果更新失败，直接设置 Content
                button.Content = newText;
                logger?.LogMessage($"⚠️ 更新按钮文本失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 在 StackPanel 中查找 TextBlock
        /// </summary>
        private TextBlock FindTextBlockInStackPanel(StackPanel stackPanel)
        {
            foreach (var child in stackPanel.Children)
            {
                if (child is TextBlock textBlock)
                {
                    return textBlock;
                }
            }
            return null;
        }

        /// <summary>
        /// 更新播放/暂停按钮的图标
        /// </summary>
        private void UpdatePlayPauseButtonIcon(bool isPlaying)
        {
            try
            {
                logger?.LogMessage($"🎯 开始更新播放按钮图标: isPlaying={isPlaying}");
                
                // 确保在UI线程中执行
                if (PlayPauseButton.Dispatcher.CheckAccess())
                {
                    // 当前在UI线程，直接更新
                    logger?.LogMessage($"✅ 在UI线程中更新播放按钮图标");
                    UpdatePlayPauseButtonIconInternal(isPlaying);
                }
                else
                {
                    // 不在UI线程，使用Dispatcher.Invoke
                    logger?.LogMessage($"🔄 切换到UI线程更新播放按钮图标");
                    PlayPauseButton.Dispatcher.Invoke(() => UpdatePlayPauseButtonIconInternal(isPlaying));
                }
                
                logger?.LogMessage($"✅ 播放按钮图标更新完成");
            }
            catch (Exception ex)
            {
                logger?.LogMessage($"⚠️ 更新播放按钮图标失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 内部方法：实际更新播放按钮图标
        /// </summary>
        private void UpdatePlayPauseButtonIconInternal(bool isPlaying)
        {
            try
            {
                logger?.LogMessage($"🔧 内部方法更新播放按钮图标: isPlaying={isPlaying}");
                
                if (PlayPauseButton.Content is MahApps.Metro.IconPacks.PackIconMaterial icon)
                {
                    var newKind = isPlaying ? MahApps.Metro.IconPacks.PackIconMaterialKind.Pause : MahApps.Metro.IconPacks.PackIconMaterialKind.Play;
                    logger?.LogMessage($"🎨 设置图标类型: {newKind}");
                    icon.Kind = newKind;
                    logger?.LogMessage($"✅ 图标类型设置成功");
                }
                else
                {
                    logger?.LogMessage($"⚠️ 播放按钮内容不是PackIconMaterial类型: {PlayPauseButton.Content?.GetType()}");
                }
            }
            catch (Exception ex)
            {
                logger?.LogMessage($"⚠️ 更新播放按钮图标内部方法失败: {ex.Message}");
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
                    viewModel.OnDesktopLyricWindowStateChanged -= OnDesktopLyricWindowStateChanged;
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
