using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using LyricSync.Windows.Models;
using LyricSync.Windows.Utils;
using Newtonsoft.Json;

namespace LyricSync.Windows.Services
{
    public class NeteaseMusicService
    {
        private readonly HttpClient httpClient;
        private readonly ILogger logger;
        
        // 网易云API服务器地址配置
        // 如果你的API服务器运行在其他地址，请修改这里
        // 例如：http://localhost:8080 或 http://192.168.1.100:3000
        private const string NETEASE_API_BASE = "http://localhost:3000";

        public NeteaseMusicService(ILogger logger)
        {
            this.logger = logger;
            httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(10);
        }

        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                logger.LogMessage("🔍 正在测试网易云API连接...");
                
                // 测试搜索路径而不是根路径，因为根路径可能没有处理程序
                var response = await httpClient.GetAsync($"{NETEASE_API_BASE}/search?keywords=test&type=1&limit=1&offset=0");
                
                if (response.IsSuccessStatusCode)
                {
                    logger.LogMessage("✅ 网易云API连接测试成功");
                    return true;
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    logger.LogMessage($"⚠️ 网易云API连接测试失败: {response.StatusCode}");
                    logger.LogMessage($"💡 错误响应: {errorContent}");
                    logger.LogMessage("💡 请确保API服务器正在运行");
                    return false;
                }
            }
            catch (Exception ex)
            {
                logger.LogMessage($"❌ 网易云API连接测试失败: {ex.Message}");
                logger.LogMessage($"💡 请检查API服务器是否启动，地址是否正确");
                logger.LogMessage($"💡 当前配置的API地址: {NETEASE_API_BASE}");
                return false;
            }
        }

        public async Task<NeteaseSong> SearchMusicAsync(MusicInfo musicInfo)
        {
            try
            {
                // 构建搜索关键词
                string searchKeywords = BuildSearchKeywords(musicInfo);
                
                // 检查搜索关键词是否有效
                if (string.IsNullOrWhiteSpace(searchKeywords))
                {
                    logger.LogMessage("❌ 搜索关键词为空，跳过搜索");
                    return null;
                }
                
                logger.LogMessage($"🔍 正在搜索网易云音乐: '{searchKeywords}'");
                
                // 使用已验证有效的 'keywords' 参数进行搜索
                string encodedKeywords = Uri.EscapeDataString(searchKeywords);
                var searchUrl = $"{NETEASE_API_BASE}/search?keywords={encodedKeywords}&type=1&limit=20&offset=0";
                
                logger.LogMessage($"📡 发送搜索请求: {searchUrl}");
                
                var response = await httpClient.GetAsync(searchUrl);
                
                if (response.IsSuccessStatusCode)
                {
                    logger.LogMessage("✅ 搜索请求成功");
                    return await ProcessSearchResponseAsync(response, musicInfo);
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    logger.LogMessage($"❌ 搜索请求失败，状态码: {response.StatusCode}");
                    logger.LogMessage($"💡 错误响应: {errorContent}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                logger.LogMessage($"❌ 搜索网易云音乐失败: {ex.Message}");
                logger.LogMessage($"💡 请检查网络连接和API服务器状态");
                return null;
            }
        }

        private string BuildSearchKeywords(MusicInfo musicInfo)
        {
            // 只搜索歌曲名称，不搜索艺术家和专辑
            if (string.IsNullOrEmpty(musicInfo.Title))
            {
                logger.LogMessage("⚠️ 歌曲名称为空，无法搜索");
                return null;
            }
            
            // 移除英文翻译部分，只保留中文标题
            string title = musicInfo.Title;
            int englishStart = title.IndexOf('(');
            if (englishStart > 0)
            {
                title = title.Substring(0, englishStart).Trim();
            }
            
            logger.LogMessage($"🔍 构建搜索关键词 - 只搜索歌曲名称: '{title}'");
            return title;
        }

        private async Task<NeteaseSong> ProcessSearchResponseAsync(HttpResponseMessage response, MusicInfo musicInfo)
        {
            try
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                logger.LogMessage($"📡 API响应: {responseContent.Substring(0, Math.Min(200, responseContent.Length))}...");
                
                var searchResponse = JsonConvert.DeserializeObject<NeteaseSearchResponse>(responseContent);
                
                if (searchResponse?.Result?.Songs != null && searchResponse.Result.Songs.Count > 0)
                {
                    logger.LogMessage($"🎵 搜索到 {searchResponse.Result.Songs.Count} 首歌曲");
                    
                    // 匹配最佳结果
                    var bestMatch = FindBestMatch(musicInfo, searchResponse.Result.Songs);
                    
                    if (bestMatch != null)
                    {
                        logger.LogMessage($"✅ 找到匹配歌曲: {bestMatch.Name} - {string.Join(", ", bestMatch.Artists?.Select(a => a.Name) ?? new List<string>())}");
                        logger.LogMessage($"🎵 歌曲ID: {bestMatch.Id}");
                        logger.LogMessage($"💿 专辑: {bestMatch.Album?.Name ?? "未知"}");
                        logger.LogMessage($"⏱️ 时长: {FormatTime(bestMatch.Duration)}");
                        
                        // 显示所有搜索结果供参考
                        logger.LogMessage("📋 所有搜索结果:");
                        for (int i = 0; i < Math.Min(3, searchResponse.Result.Songs.Count); i++)
                        {
                            var song = searchResponse.Result.Songs[i];
                            logger.LogMessage($"  {i + 1}. {song.Name} - {string.Join(", ", song.Artists?.Select(a => a.Name) ?? new List<string>())} (ID: {song.Id})");
                        }
                        
                        // 自动获取歌词
                        logger.LogMessage("🎵 开始自动获取歌词...");
                        var lyricResponse = await GetLyricAsync(bestMatch.Id);
                        if (lyricResponse != null)
                        {
                            logger.LogMessage("✅ 歌词获取完成");
                        }
                        else
                        {
                            logger.LogMessage("⚠️ 歌词获取失败，但歌曲匹配成功");
                        }
                        
                        return bestMatch;
                    }
                    else
                    {
                        logger.LogMessage("⚠️ 未找到完全匹配的歌曲");
                        return null;
                    }
                }
                else
                {
                    logger.LogMessage("❌ 网易云API返回空结果");
                    logger.LogMessage($"💡 响应内容: {responseContent}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                logger.LogMessage($"❌ 处理API响应失败: {ex.Message}");
                return null;
            }
        }

        private NeteaseSong FindBestMatch(MusicInfo musicInfo, List<NeteaseSong> songs)
        {
            if (songs == null || songs.Count == 0) return null;
            
            // 清理标题，移除英文翻译
            string cleanTitle = musicInfo.Title;
            int englishStart = cleanTitle.IndexOf('(');
            if (englishStart > 0)
            {
                cleanTitle = cleanTitle.Substring(0, englishStart).Trim();
            }
            
            logger.LogMessage($"🎯 开始匹配歌曲: '{cleanTitle}' - '{musicInfo.Artist}'");
            
            // 1. 完全匹配标题和艺术家
            var exactMatch = songs.FirstOrDefault(s => 
                string.Equals(s.Name, cleanTitle, StringComparison.OrdinalIgnoreCase) &&
                s.Artists?.Any(a => string.Equals(a.Name, musicInfo.Artist, StringComparison.OrdinalIgnoreCase)) == true);
            
            if (exactMatch != null)
            {
                logger.LogMessage("🎯 找到完全匹配的歌曲");
                return exactMatch;
            }
            
            // 2. 标题完全匹配，艺术家部分匹配
            var titleExactArtistPartial = songs.FirstOrDefault(s => 
                string.Equals(s.Name, cleanTitle, StringComparison.OrdinalIgnoreCase) &&
                s.Artists?.Any(a => musicInfo.Artist.Contains(a.Name) || a.Name.Contains(musicInfo.Artist)) == true);
            
            if (titleExactArtistPartial != null)
            {
                logger.LogMessage("🎯 找到标题完全匹配，艺术家部分匹配的歌曲");
                return titleExactArtistPartial;
            }
            
            // 3. 标题完全匹配
            var titleMatch = songs.FirstOrDefault(s => 
                string.Equals(s.Name, cleanTitle, StringComparison.OrdinalIgnoreCase));
            
            if (titleMatch != null)
            {
                logger.LogMessage("🎯 找到标题匹配的歌曲");
                return titleMatch;
            }
            
            // 4. 标题包含匹配
            var titleContains = songs.FirstOrDefault(s => 
                s.Name.Contains(cleanTitle) || cleanTitle.Contains(s.Name));
            
            if (titleContains != null)
            {
                logger.LogMessage("🎯 找到标题包含匹配的歌曲");
                return titleContains;
            }
            
            // 5. 返回第一个结果
            logger.LogMessage("🎯 未找到精确匹配，返回第一个搜索结果");
            return songs[0];
        }

        private string FormatTime(long milliseconds)
        {
            if (milliseconds <= 0) return "0:00";
            
            TimeSpan time = TimeSpan.FromMilliseconds(milliseconds);
            return $"{(int)time.TotalMinutes}:{time.Seconds:D2}";
        }

        /// <summary>
        /// 根据歌曲ID获取歌词
        /// </summary>
        /// <param name="songId">歌曲ID</param>
        /// <returns>歌词响应对象，如果获取失败返回null</returns>
        public async Task<NeteaseLyricResponse> GetLyricAsync(long songId)
        {
            try
            {
                logger.LogMessage($"🎵 正在获取歌曲ID {songId} 的歌词...");
                
                string lyricUrl = $"{NETEASE_API_BASE}/lyric?id={songId}";
                logger.LogMessage($"📡 发送歌词请求: {lyricUrl}");
                
                var response = await httpClient.GetAsync(lyricUrl);
                
                if (response.IsSuccessStatusCode)
                {
                    logger.LogMessage("✅ 歌词请求成功");
                    
                    var responseContent = await response.Content.ReadAsStringAsync();
                    logger.LogMessage($"📡 歌词API响应: {responseContent.Substring(0, Math.Min(200, responseContent.Length))}...");
                    
                    var lyricResponse = JsonConvert.DeserializeObject<NeteaseLyricResponse>(responseContent);
                    
                    if (lyricResponse != null)
                    {
                        if (lyricResponse.Code == 200)
                        {
                            // 检查是否有歌词内容
                            bool hasLyric = !string.IsNullOrEmpty(lyricResponse.Lrc?.Lyric);
                            bool hasTranslation = !string.IsNullOrEmpty(lyricResponse.Tlyric?.Lyric);
                            bool hasRomalrc = !string.IsNullOrEmpty(lyricResponse.Romalrc?.Lyric);
                            
                            logger.LogMessage($"🎵 歌词获取成功:");
                            logger.LogMessage($"   - 原歌词: {(hasLyric ? "✅ 有" : "❌ 无")}");
                            logger.LogMessage($"   - 翻译歌词: {(hasTranslation ? "✅ 有" : "❌ 无")}");
                            logger.LogMessage($"   - 罗马音歌词: {(hasRomalrc ? "✅ 有" : "❌ 无")}");
                            
                            if (hasLyric)
                            {
                                // 统计歌词行数
                                var lyricLines = lyricResponse.Lrc.Lyric.Split('\n')
                                    .Where(line => !string.IsNullOrWhiteSpace(line) && line.Contains(']'))
                                    .Count();
                                logger.LogMessage($"   - 歌词行数: {lyricLines} 行");
                            }
                            
                            return lyricResponse;
                        }
                        else
                        {
                            logger.LogMessage($"❌ 歌词API返回错误代码: {lyricResponse.Code}");
                            return null;
                        }
                    }
                    else
                    {
                        logger.LogMessage("❌ 歌词响应解析失败");
                        return null;
                    }
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    logger.LogMessage($"❌ 歌词请求失败，状态码: {response.StatusCode}");
                    logger.LogMessage($"💡 错误响应: {errorContent}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                logger.LogMessage($"❌ 获取歌词失败: {ex.Message}");
                logger.LogMessage($"💡 请检查网络连接和API服务器状态");
                return null;
            }
        }

        public void Dispose()
        {
            httpClient?.Dispose();
        }
    }
}
