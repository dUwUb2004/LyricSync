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
        private ObservableCollection<LyricLine> _lyricLines = new ObservableCollection<LyricLine>();
        private int _currentLineIndex = -1;
        private bool _showTranslation = true;
        private double _opacityPercent = 90; // 0-100
        private bool _isLocked = false; // 锁定后不可拖动

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

        public DesktopLyricWindow()
        {
            InitializeComponent();
            DataContext = this;
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

        private void ClickThroughMenuItem_Checked(object sender, RoutedEventArgs e)
        {
            TrySetClickThrough(true);
        }

        private void ClickThroughMenuItem_Unchecked(object sender, RoutedEventArgs e)
        {
            TrySetClickThrough(false);
        }

        private void Opacity60_Click(object sender, RoutedEventArgs e)
        {
            OpacityPercent = 60;
        }

        private void Opacity80_Click(object sender, RoutedEventArgs e)
        {
            OpacityPercent = 80;
        }

        private void Opacity100_Click(object sender, RoutedEventArgs e)
        {
            OpacityPercent = 100;
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void TrySetClickThrough(bool enable)
        {
            // 通过切换窗口的命中测试实现点击穿透：当启用时禁用 IsHitTestVisible
            // 若需系统级穿透可使用 Win32 扩展样式 WS_EX_TRANSPARENT
            this.IsHitTestVisible = !enable;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}


