using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using LyricSync.Windows.Models;

namespace LyricSync.Windows.Utils
{
    public static class LrcParser
    {
        private static readonly Regex TimeTagRegex = new Regex("\\[(?<min>\\d{2}):(?<sec>\\d{2})(?:\\.(?<centi>\\d{2,3}))?\\]", RegexOptions.Compiled);

        public static List<LyricLine> Parse(string lrcContent)
        {
            var result = new List<LyricLine>();
            if (string.IsNullOrWhiteSpace(lrcContent))
            {
                return result;
            }

            var lines = lrcContent.Replace("\r", string.Empty).Split('\n');
            foreach (var raw in lines)
            {
                var line = raw?.Trim();
                if (string.IsNullOrEmpty(line)) continue;

                var matches = TimeTagRegex.Matches(line);
                if (matches.Count == 0)
                {
                    continue; // 跳过无时间标签行
                }

                // 去掉所有时间标签后的文本
                var text = TimeTagRegex.Replace(line, string.Empty).Trim();
                if (string.IsNullOrEmpty(text))
                {
                    continue; // 纯时间标签行，不加入
                }

                foreach (Match m in matches)
                {
                    int minutes = int.Parse(m.Groups["min"].Value);
                    int seconds = int.Parse(m.Groups["sec"].Value);
                    string centiStr = m.Groups["centi"].Success ? m.Groups["centi"].Value : "0";

                    // 将毫秒或厘秒统一为毫秒
                    int centiOrMilli = 0;
                    if (centiStr.Length == 2)
                    {
                        // 像 xx -> 转为毫秒 (xx * 10)
                        centiOrMilli = int.Parse(centiStr) * 10;
                    }
                    else if (centiStr.Length == 3)
                    {
                        centiOrMilli = int.Parse(centiStr);
                    }

                    double totalSeconds = minutes * 60 + seconds + centiOrMilli / 1000.0;
                    result.Add(new LyricLine { TimeSeconds = totalSeconds, Text = text });
                }
            }

            // 排序
            result.Sort((a, b) => a.TimeSeconds.CompareTo(b.TimeSeconds));
            return result;
        }
    }
}


