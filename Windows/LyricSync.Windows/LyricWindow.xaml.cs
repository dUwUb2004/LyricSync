using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using LyricSync.Windows.Models;

namespace LyricSync.Windows
{
    public partial class LyricWindow : Window, INotifyPropertyChanged
    {
        private ObservableCollection<LyricLine> _lyricLines = new ObservableCollection<LyricLine>();
        private int _currentLineIndex = 0;
        private bool _showTranslation = true;
        private double _lyricFontSize = 20;
        private double _translationFontSize = 16;

        public ObservableCollection<LyricLine> LyricLines
        {
            get => _lyricLines;
            set
            {
                _lyricLines = value;
                OnPropertyChanged();
            }
        }

        public int CurrentLineIndex
        {
            get => _currentLineIndex;
            set
            {
                _currentLineIndex = value;
                OnPropertyChanged();
            }
        }

        public bool ShowTranslation
        {
            get => _showTranslation;
            set
            {
                _showTranslation = value;
                OnPropertyChanged();
            }
        }

        public double LyricFontSize
        {
            get => _lyricFontSize;
            set
            {
                _lyricFontSize = value;
                OnPropertyChanged();
                // 翻译字体大小跟随原文字体大小调整
                TranslationFontSize = value * 0.8;
            }
        }

        public double TranslationFontSize
        {
            get => _translationFontSize;
            set
            {
                _translationFontSize = value;
                OnPropertyChanged();
            }
        }

        public LyricWindow()
        {
            InitializeComponent();
            DataContext = this;
            
            // 设置初始翻译字体大小
            TranslationFontSize = LyricFontSize * 0.8;
        }

        public void SetLyrics(ObservableCollection<LyricLine> lines)
        {
            LyricLines = lines;
            LyricListBox.ItemsSource = LyricLines;
        }

        public void HighlightLine(int index)
        {
            if (index >= 0 && index < LyricLines.Count)
            {
                CurrentLineIndex = index;
                LyricListBox.SelectedIndex = index;
                LyricListBox.ScrollIntoView(LyricListBox.SelectedItem);
            }
        }

        /// <summary>
        /// 设置是否显示翻译
        /// </summary>
        /// <param name="show">是否显示翻译</param>
        public void SetShowTranslation(bool show)
        {
            ShowTranslation = show;
        }

        /// <summary>
        /// 检查当前歌词是否有翻译
        /// </summary>
        /// <returns>是否有翻译</returns>
        public bool HasAnyTranslation()
        {
            foreach (var line in LyricLines)
            {
                if (line.HasTranslation)
                    return true;
            }
            return false;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
