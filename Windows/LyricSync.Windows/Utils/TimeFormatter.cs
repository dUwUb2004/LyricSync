namespace LyricSync.Windows.Utils
{
    public static class TimeFormatter
    {
        public static string FormatTime(long milliseconds)
        {
            if (milliseconds <= 0) return "0:00";
            
            var time = System.TimeSpan.FromMilliseconds(milliseconds);
            return $"{(int)time.TotalMinutes}:{time.Seconds:D2}";
        }
    }
}
