using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using LyricSync.Windows.Models;

namespace LyricSync.Windows
{
    public partial class DesktopLyricWindow : Window, INotifyPropertyChanged
    {
        // 由外部注入的控制委托（例如调用 ADB 控制）
        public Func<int, System.Threading.Tasks.Task> SendControlAsync { get; set; }
        private ObservableCollection<LyricLine> _lyricLines = new ObservableCollection<LyricLine>();
        private int _currentLineIndex = -1;
        private bool _showTranslation = true;
        private double _opacityPercent = 90; // 0-100
        private double _backgroundOpacityPercent = 40; // 背景透明度 0-100
        private bool _isLocked = false; // 锁定后不可拖动
        
        // 样式配置属性
        private string _mainLyricFontFamily = "Microsoft YaHei UI"; // 主歌词字体
        private double _mainLyricFontSize = 28; // 主歌词字体大小
        private string _mainLyricColor = "White"; // 主歌词颜色
        private string _mainLyricStrokeColor = "#000000"; // 主歌词描边颜色
        private string _translationFontFamily = "Microsoft YaHei UI"; // 翻译歌词字体
        private double _translationFontSize = 20; // 翻译歌词字体大小
        private string _translationColor = "#CCDDDDDD"; // 翻译歌词颜色
        private string _translationStrokeColor = "#000000"; // 翻译歌词描边颜色
        private string _backgroundColor = "#66000000"; // 背景颜色
        private double _cornerRadius = 12; // 圆角半径
        private double _padding = 16; // 内边距

        public ObservableCollection<LyricLine> LyricLines
        {
            get => _lyricLines;
            set { _lyricLines = value; OnPropertyChanged(); }
        }

        public LyricLine CurrentLine
        {
            get
            {
                if (_currentLineIndex >= 0 && _currentLineIndex < _lyricLines.Count)
                {
                    return _lyricLines[_currentLineIndex];
                }
                return null;
            }
        }

        public int CurrentLineIndex
        {
            get => _currentLineIndex;
            set { _currentLineIndex = value; OnPropertyChanged(); OnPropertyChanged(nameof(CurrentLine)); }
        }

        public bool ShowTranslation
        {
            get => _showTranslation;
            set { _showTranslation = value; OnPropertyChanged(); }
        }

        public double OpacityPercent
        {
            get => _opacityPercent;
            set
            {
                _opacityPercent = value;
                this.Opacity = System.Math.Max(0.05, System.Math.Min(1.0, _opacityPercent / 100.0));
                OnPropertyChanged();
            }
        }

        public bool IsLocked
        {
            get => _isLocked;
            set { _isLocked = value; OnPropertyChanged(); }
        }

        public double BackgroundOpacityPercent
        {
            get => _backgroundOpacityPercent;
            set
            {
                _backgroundOpacityPercent = value;
                OnPropertyChanged();
                UpdateBackgroundOpacity();
            }
        }

        // 样式配置属性
        public string MainLyricFontFamily
        {
            get => _mainLyricFontFamily;
            set { _mainLyricFontFamily = value; OnPropertyChanged(); ApplyStyleChanges(); }
        }

        public double MainLyricFontSize
        {
            get => _mainLyricFontSize;
            set { _mainLyricFontSize = value; OnPropertyChanged(); ApplyStyleChanges(); }
        }

        public string MainLyricColor
        {
            get => _mainLyricColor;
            set { _mainLyricColor = value; OnPropertyChanged(); ApplyStyleChanges(); }
        }

        public string MainLyricStrokeColor
        {
            get => _mainLyricStrokeColor;
            set { _mainLyricStrokeColor = value; OnPropertyChanged(); ApplyStyleChanges(); }
        }

        public string TranslationFontFamily
        {
            get => _translationFontFamily;
            set { _translationFontFamily = value; OnPropertyChanged(); ApplyStyleChanges(); }
        }

        public double TranslationFontSize
        {
            get => _translationFontSize;
            set { _translationFontSize = value; OnPropertyChanged(); ApplyStyleChanges(); }
        }

        public string TranslationColor
        {
            get => _translationColor;
            set { _translationColor = value; OnPropertyChanged(); ApplyStyleChanges(); }
        }

        public string TranslationStrokeColor
        {
            get => _translationStrokeColor;
            set { _translationStrokeColor = value; OnPropertyChanged(); ApplyStyleChanges(); }
        }

        public string BackgroundColor
        {
            get => _backgroundColor;
            set { _backgroundColor = value; OnPropertyChanged(); ApplyStyleChanges(); }
        }

        public double CornerRadius
        {
            get => _cornerRadius;
            set { _cornerRadius = value; OnPropertyChanged(); ApplyStyleChanges(); }
        }

        public new double Padding
        {
            get => _padding;
            set { _padding = value; OnPropertyChanged(); ApplyStyleChanges(); }
        }

        public DesktopLyricWindow()
        {
            InitializeComponent();
            DataContext = this;
            LoadSettings();
        }

        public void SetLyrics(ObservableCollection<LyricLine> lines)
        {
            LyricLines = lines;
        }

        public void HighlightLine(int index)
        {
            if (index >= 0 && index < LyricLines.Count)
            {
                CurrentLineIndex = index;
            }
        }

        public void SetShowTranslation(bool show)
        {
            ShowTranslation = show;
        }

        private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                if (!IsLocked)
                {
                    DragMove();
                }
            }
            catch
            {
            }
        }



        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }



        private async void PrevButton_Click(object sender, RoutedEventArgs e)
        {
            if (SendControlAsync != null)
            {
                try { await SendControlAsync(88); } catch { }
            }
        }

        private async void PlayPauseButton_Click(object sender, RoutedEventArgs e)
        {
            if (SendControlAsync != null)
            {
                try { await SendControlAsync(85); } catch { }
            }
        }

        private async void NextButton_Click(object sender, RoutedEventArgs e)
        {
            if (SendControlAsync != null)
            {
                try { await SendControlAsync(87); } catch { }
            }
        }

        /// <summary>
        /// 更新背景透明度
        /// </summary>
        private void UpdateBackgroundOpacity()
        {
            try
            {
                // 将百分比转换为十六进制透明度值
                int alphaValue = (int)(_backgroundOpacityPercent * 255 / 100);
                alphaValue = System.Math.Max(0, System.Math.Min(255, alphaValue)); // 确保在有效范围内
                
                // 创建新的背景颜色
                string hexAlpha = alphaValue.ToString("X2");
                string newBackgroundColor = $"#{hexAlpha}000000";
                
                // 查找并更新背景Border
                var border = FindName("BackgroundBorder") as System.Windows.Controls.Border;
                if (border != null)
                {
                    var brush = new System.Windows.Media.SolidColorBrush(
                        (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(newBackgroundColor));
                    border.Background = brush;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"更新背景透明度失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 应用样式变化
        /// </summary>
        private void ApplyStyleChanges()
        {
            try
            {
                // 更新背景Border样式
                var border = FindName("BackgroundBorder") as System.Windows.Controls.Border;
                if (border != null)
                {
                    // 更新背景颜色
                    var brush = new System.Windows.Media.SolidColorBrush(
                        (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(_backgroundColor));
                    border.Background = brush;
                    
                    // 更新圆角
                    border.CornerRadius = new System.Windows.CornerRadius(_cornerRadius);
                    
                    // 更新内边距
                    border.Padding = new System.Windows.Thickness(_padding);
                }

                // 更新主歌词样式
                var mainLyricTextBlock = FindName("MainLyricTextBlock") as System.Windows.Controls.TextBlock;
                if (mainLyricTextBlock != null)
                {
                    mainLyricTextBlock.FontFamily = new System.Windows.Media.FontFamily(_mainLyricFontFamily);
                    mainLyricTextBlock.FontSize = _mainLyricFontSize;
                    mainLyricTextBlock.Foreground = new System.Windows.Media.SolidColorBrush(
                        (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(_mainLyricColor));
                }

                // 更新主歌词描边样式
                for (int i = 1; i <= 8; i++)
                {
                    var strokeTextBlock = FindName($"MainLyricStroke{i}") as System.Windows.Controls.TextBlock;
                    if (strokeTextBlock != null)
                    {
                        strokeTextBlock.FontFamily = new System.Windows.Media.FontFamily(_mainLyricFontFamily);
                        strokeTextBlock.FontSize = _mainLyricFontSize;
                        strokeTextBlock.Foreground = new System.Windows.Media.SolidColorBrush(
                            (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(_mainLyricStrokeColor));
                    }
                }

                // 更新翻译歌词样式
                var translationTextBlock = FindName("TranslationTextBlock") as System.Windows.Controls.TextBlock;
                if (translationTextBlock != null)
                {
                    translationTextBlock.FontFamily = new System.Windows.Media.FontFamily(_translationFontFamily);
                    translationTextBlock.FontSize = _translationFontSize;
                    translationTextBlock.Foreground = new System.Windows.Media.SolidColorBrush(
                        (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(_translationColor));
                }

                // 更新翻译歌词描边样式
                for (int i = 1; i <= 8; i++)
                {
                    var strokeTextBlock = FindName($"TranslationStroke{i}") as System.Windows.Controls.TextBlock;
                    if (strokeTextBlock != null)
                    {
                        strokeTextBlock.FontFamily = new System.Windows.Media.FontFamily(_translationFontFamily);
                        strokeTextBlock.FontSize = _translationFontSize;
                        strokeTextBlock.Foreground = new System.Windows.Media.SolidColorBrush(
                            (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(_translationStrokeColor));
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"应用样式变化失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 加载保存的设置
        /// </summary>
        private void LoadSettings()
        {
            try
            {
                var properties = Application.Current.Properties;
                
                if (properties.Contains("MainLyricFontFamily"))
                    _mainLyricFontFamily = properties["MainLyricFontFamily"].ToString();
                if (properties.Contains("MainLyricFontSize"))
                    _mainLyricFontSize = Convert.ToDouble(properties["MainLyricFontSize"]);
                if (properties.Contains("MainLyricColor"))
                    _mainLyricColor = properties["MainLyricColor"].ToString();
                if (properties.Contains("MainLyricStrokeColor"))
                    _mainLyricStrokeColor = properties["MainLyricStrokeColor"].ToString();
                if (properties.Contains("TranslationFontFamily"))
                    _translationFontFamily = properties["TranslationFontFamily"].ToString();
                if (properties.Contains("TranslationFontSize"))
                    _translationFontSize = Convert.ToDouble(properties["TranslationFontSize"]);
                if (properties.Contains("TranslationColor"))
                    _translationColor = properties["TranslationColor"].ToString();
                if (properties.Contains("TranslationStrokeColor"))
                    _translationStrokeColor = properties["TranslationStrokeColor"].ToString();
                if (properties.Contains("BackgroundColor"))
                    _backgroundColor = properties["BackgroundColor"].ToString();
                if (properties.Contains("CornerRadius"))
                    _cornerRadius = Convert.ToDouble(properties["CornerRadius"]);
                if (properties.Contains("Padding"))
                    _padding = Convert.ToDouble(properties["Padding"]);
                
                // 应用加载的设置
                ApplyStyleChanges();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"加载设置失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 保存当前设置
        /// </summary>
        public void SaveSettings()
        {
            try
            {
                var properties = Application.Current.Properties;
                properties["MainLyricFontFamily"] = _mainLyricFontFamily;
                properties["MainLyricFontSize"] = _mainLyricFontSize;
                properties["MainLyricColor"] = _mainLyricColor;
                properties["MainLyricStrokeColor"] = _mainLyricStrokeColor;
                properties["TranslationFontFamily"] = _translationFontFamily;
                properties["TranslationFontSize"] = _translationFontSize;
                properties["TranslationColor"] = _translationColor;
                properties["TranslationStrokeColor"] = _translationStrokeColor;
                properties["BackgroundColor"] = _backgroundColor;
                properties["CornerRadius"] = _cornerRadius;
                properties["Padding"] = _padding;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"保存设置失败: {ex.Message}");
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}


