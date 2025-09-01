using Newtonsoft.Json;

namespace LyricSync.Windows.Models
{
    public class MusicInfo
    {
        [JsonProperty("title")]
        public string Title { get; set; }
        
        [JsonProperty("artist")]
        public string Artist { get; set; }
        
        [JsonProperty("album")]
        public string Album { get; set; }
        
        [JsonProperty("position")]
        public long Position { get; set; }
        
        [JsonProperty("state")]
        public bool IsPlaying { get; set; }
        
        [JsonProperty("duration")]
        public long Duration { get; set; } = 0;
        
        // 网易云API匹配的歌曲信息
        public NeteaseSong MatchedSong { get; set; }
        
        // 完整的API响应JSON
        public string SearchResponseJson { get; set; }
    }
}
