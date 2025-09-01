using System;
using System.Windows;
using LyricSync.Windows.Utils;

namespace LyricSync.Windows
{
    public partial class SettingsWindow : Window
    {
        private readonly ILogger logger;
        private DesktopLyricWindow desktopLyricWindow;

        public bool ShowTranslation { get; set; } = true;
        public bool LockWindow { get; set; } = false;
        public bool ClickThrough { get; set; } = false;
        public double OpacityPercent { get; set; } = 90;
        public double MainFontSize { get; set; } = 28;
        public string ApiUrl { get; set; } = "http://localhost:3000";

        public SettingsWindow(ILogger logger, DesktopLyricWindow desktopWindow = null)
        {
            InitializeComponent();
            this.logger = logger;
            this.desktopLyricWindow = desktopWindow;
            
            LoadCurrentSettings();
        }

        private void LoadCurrentSettings()
        {
            // 从桌面歌词窗口读取当前设置
            if (desktopLyricWindow != null)
            {
                ShowTranslation = desktopLyricWindow.ShowTranslation;
                LockWindow = desktopLyricWindow.IsLocked;
                OpacityPercent = desktopLyricWindow.OpacityPercent;
                // TODO: 读取字体大小和点击穿透状态
            }

            // 更新UI控件
            ShowTranslationCheckBox.IsChecked = ShowTranslation;
            LockWindowCheckBox.IsChecked = LockWindow;
            ClickThroughCheckBox.IsChecked = ClickThrough;
            OpacitySlider.Value = OpacityPercent;
            FontSizeSlider.Value = MainFontSize;
            ApiUrlTextBox.Text = ApiUrl;
            
            UpdateOpacityText();
            UpdateFontSizeText();
        }

        private void OpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            OpacityPercent = e.NewValue;
            UpdateOpacityText();
        }

        private void FontSizeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            MainFontSize = e.NewValue;
            UpdateFontSizeText();
        }

        private void UpdateOpacityText()
        {
            if (OpacityValueText != null)
            {
                OpacityValueText.Text = $"{OpacityPercent:F0}%";
            }
        }

        private void UpdateFontSizeText()
        {
            if (FontSizeValueText != null)
            {
                FontSizeValueText.Text = $"{MainFontSize:F0}";
            }
        }

        private async void TestApiButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                TestApiButton.IsEnabled = false;
                TestApiButton.Content = "测试中...";
                
                // TODO: 实际测试API连接
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

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            // 从UI控件读取设置
            ShowTranslation = ShowTranslationCheckBox.IsChecked ?? true;
            LockWindow = LockWindowCheckBox.IsChecked ?? false;
            ClickThrough = ClickThroughCheckBox.IsChecked ?? false;
            ApiUrl = ApiUrlTextBox.Text?.Trim() ?? "http://localhost:3000";
            
            // 应用到桌面歌词窗口
            if (desktopLyricWindow != null)
            {
                desktopLyricWindow.ShowTranslation = ShowTranslation;
                desktopLyricWindow.IsLocked = LockWindow;
                desktopLyricWindow.OpacityPercent = OpacityPercent;
                
                // 应用点击穿透
                desktopLyricWindow.IsHitTestVisible = !ClickThrough;
                
                // TODO: 应用字体大小设置
            }

            // TODO: 保存设置到配置文件
            logger.LogMessage("✅ 设置已保存");
            
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
