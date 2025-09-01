using Newtonsoft.Json;

namespace LyricSync.Windows.Models
{
    public class LyricLine
    {
        [JsonProperty("t")]
        public double TimeSeconds { get; set; }

        [JsonProperty("l")]
        public string Text { get; set; }
    }
}


