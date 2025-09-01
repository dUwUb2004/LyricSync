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

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}


