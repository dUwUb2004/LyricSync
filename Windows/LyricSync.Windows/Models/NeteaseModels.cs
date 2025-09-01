using Newtonsoft.Json;
using System.Collections.Generic;

namespace LyricSync.Windows.Models
{
    // 网易云音乐API数据模型
    public class NeteaseSearchRequest
    {
        [JsonProperty("keywords")]
        public string Keywords { get; set; }
        
        [JsonProperty("s")]
        public string S { get; set; }  // 标准搜索参数
        
        [JsonProperty("type")]
        public int Type { get; set; } = 1;  // 1: 单曲, 10: 专辑, 100: 歌手
        
        [JsonProperty("limit")]
        public int Limit { get; set; } = 20;  // 结果数量限制
        
        [JsonProperty("offset")]
        public int Offset { get; set; } = 0;  // 偏移量
    }
    
    public class NeteaseSearchResponse
    {
        [JsonProperty("code")]
        public int Code { get; set; }
        
        [JsonProperty("result")]
        public NeteaseSearchResult Result { get; set; }
        
        [JsonProperty("status")]
        public int Status { get; set; }
    }
    
    public class NeteaseSearchResult
    {
        [JsonProperty("hasMore")]
        public bool HasMore { get; set; }
        
        [JsonProperty("songCount")]
        public int SongCount { get; set; }
        
        [JsonProperty("songs")]
        public List<NeteaseSong> Songs { get; set; }
    }
    
    public class NeteaseSong
    {
        [JsonProperty("id")]
        public long Id { get; set; }
        
        [JsonProperty("name")]
        public string Name { get; set; }
        
        [JsonProperty("duration")]
        public long Duration { get; set; }
        
        [JsonProperty("artists")]
        public List<NeteaseArtist> Artists { get; set; }
        
        [JsonProperty("album")]
        public NeteaseAlbum Album { get; set; }
        
        [JsonProperty("transNames")]
        public List<string> TransNames { get; set; }
        
        /// <summary>
        /// 重写ToString方法，返回格式化的JSON数据
        /// </summary>
        public override string ToString()
        {
            try
            {
                // 使用Newtonsoft.Json进行序列化，确保数据完整性
                return JsonConvert.SerializeObject(this, Formatting.Indented);
            }
            catch
            {
                // 如果序列化失败，返回基本信息
                return $"{{ \"id\": {Id}, \"name\": \"{Name}\", \"duration\": {Duration} }}";
            }
        }
    }
    
    public class NeteaseArtist
    {
        [JsonProperty("id")]
        public long Id { get; set; }
        
        [JsonProperty("name")]
        public string Name { get; set; }
        
        [JsonProperty("img1v1Url")]
        public string Img1v1Url { get; set; }
        
        [JsonProperty("picUrl")]
        public string PicUrl { get; set; }
    }
    
    public class NeteaseAlbum
    {
        [JsonProperty("id")]
        public long Id { get; set; }
        
        [JsonProperty("name")]
        public string Name { get; set; }
        
        [JsonProperty("artist")]
        public NeteaseArtist Artist { get; set; }
        
        [JsonProperty("picUrl")]
        public string PicUrl { get; set; }
        
        [JsonProperty("cover")]
        public string Cover { get; set; }
        
        [JsonProperty("img1v1Url")]
        public string Img1v1Url { get; set; }
    }
}
