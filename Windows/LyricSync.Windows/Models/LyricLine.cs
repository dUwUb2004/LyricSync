using Newtonsoft.Json;

namespace LyricSync.Windows.Models
{
    public class LyricLine
    {
        [JsonProperty("t")]
        public double TimeSeconds { get; set; }

        [JsonProperty("l")]
        public string Text { get; set; }

        [JsonProperty("tl")]
        public string Translation { get; set; }

        /// <summary>
        /// 获取显示文本（包含翻译的话会组合显示）
        /// </summary>
        public string DisplayText
        {
            get
            {
                if (string.IsNullOrEmpty(Translation))
                    return Text;
                return $"{Text}\n{Translation}";
            }
        }

        /// <summary>
        /// 是否有翻译
        /// </summary>
        public bool HasTranslation => !string.IsNullOrEmpty(Translation);
    }
}


