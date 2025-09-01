using System.Collections.ObjectModel;
using System.Windows;

namespace LyricSync.Windows
{
    public partial class LyricWindow : Window
    {
        public ObservableCollection<LyricLine> LyricLines { get; set; } = new ObservableCollection<LyricLine>();
        public int CurrentLineIndex { get; set; } = 0;

        public LyricWindow()
        {
            InitializeComponent();
            DataContext = this;
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
    }

    public class LyricLine
    {
        public string Text { get; set; }
        public double Time { get; set; } // 秒
    }
}
