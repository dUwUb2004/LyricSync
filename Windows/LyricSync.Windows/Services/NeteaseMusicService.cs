using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Text;
using System.Security.Cryptography;
using System.IO;
using LyricSync.Windows.Models;
using LyricSync.Windows.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LyricSync.Windows.Services
{
    public class NeteaseMusicService
    {
        private readonly HttpClient httpClient;
        private readonly ILogger logger;
        
        // 网易云音乐官方API地址
        private const string NETEASE_API_BASE = "https://music.163.com";
        private const string NETEASE_API_WEAPI = "https://music.163.com/weapi";

        public NeteaseMusicService(ILogger logger)
        {
            this.logger = logger;
            httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(10);
            
            // 设置默认请求头
            httpClient.DefaultRequestHeaders.Add("User-Agent", 
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            httpClient.DefaultRequestHeaders.Add("Referer", "https://music.163.com/");
            httpClient.DefaultRequestHeaders.Add("Origin", "https://music.163.com");
        }

        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                logger.LogMessage("🔍 正在测试网易云音乐API连接...");
                
                // 使用简单的搜索接口，不需要加密
                string searchUrl = "https://music.163.com/api/search/get/web?s=周杰伦&type=1&limit=1&offset=0";
                logger.LogMessage($"📡 测试搜索URL: {searchUrl}");
                
                var response = await httpClient.GetAsync(searchUrl);
                
                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    logger.LogMessage($"📡 搜索API响应: {responseContent.Substring(0, Math.Min(200, responseContent.Length))}...");
                    
                    if (!string.IsNullOrEmpty(responseContent))
                    {
                        try
                        {
                            var jsonResponse = JObject.Parse(responseContent);
                            var code = jsonResponse["code"]?.Value<int>();
                            
                            if (code == 200)
                            {
                                logger.LogMessage("✅ 网易云音乐API连接测试成功");
                                return true;
                            }
                            else
                            {
                                logger.LogMessage($"⚠️ API返回错误代码: {code}");
                                logger.LogMessage($"💡 响应内容: {responseContent}");
                                return false;
                            }
                        }
                        catch (Exception parseEx)
                        {
                            logger.LogMessage($"❌ 解析响应失败: {parseEx.Message}");
                            logger.LogMessage($"💡 原始响应: {responseContent}");
                            return false;
                        }
                    }
                    else
                    {
                        logger.LogMessage("❌ 响应内容为空");
                        return false;
                    }
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    logger.LogMessage($"⚠️ 搜索API请求失败: {response.StatusCode}");
                    logger.LogMessage($"💡 错误响应: {errorContent}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                logger.LogMessage($"❌ 网易云音乐API连接测试失败: {ex.Message}");
                logger.LogMessage($"💡 请检查网络连接");
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
                
                // 使用简单的搜索接口，不需要加密
                string encodedKeywords = Uri.EscapeDataString(searchKeywords);
                string searchUrl = $"https://music.163.com/api/search/get/web?s={encodedKeywords}&type=1&limit=20&offset=0";
                
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
                    
                    // 如果API搜索失败，尝试使用网页解析
                    logger.LogMessage("🔄 尝试使用网页解析方式搜索...");
                    return await SearchMusicByWebParsing(searchKeywords, musicInfo);
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
        /// 将网易云歌词JSON转换为LRC格式
        /// </summary>
        /// <param name="lyricResponse">歌词响应对象</param>
        /// <param name="includeTranslation">是否包含翻译歌词</param>
        /// <param name="includeRomalrc">是否包含罗马音歌词</param>
        /// <returns>LRC格式的歌词字符串</returns>
        public string ConvertToLrcFormat(NeteaseLyricResponse lyricResponse, bool includeTranslation = true, bool includeRomalrc = false)
        {
            try
            {
                if (lyricResponse == null)
                {
                    logger.LogMessage("❌ 歌词响应对象为空，无法转换");
                    return null;
                }

                logger.LogMessage("🔄 开始转换歌词为LRC格式...");

                var lrcBuilder = new System.Text.StringBuilder();
                
                // 添加LRC文件头信息
                lrcBuilder.AppendLine("[ti:歌曲标题]");
                lrcBuilder.AppendLine("[ar:艺术家]");
                lrcBuilder.AppendLine("[al:专辑]");
                lrcBuilder.AppendLine("[by:LyricSync]");
                lrcBuilder.AppendLine();

                // 处理原歌词
                if (!string.IsNullOrEmpty(lyricResponse.Lrc?.Lyric))
                {
                    logger.LogMessage("📝 处理原歌词...");
                    var originalLines = lyricResponse.Lrc.Lyric.Split('\n');
                    var processedLines = 0;

                    foreach (var line in originalLines)
                    {
                        if (string.IsNullOrWhiteSpace(line)) continue;

                        // 检查是否包含时间标签
                        if (line.Contains('[') && line.Contains(']'))
                        {
                            // 提取时间标签和歌词内容
                            var timeMatch = System.Text.RegularExpressions.Regex.Match(line, @"\[(\d{2}:\d{2}\.\d{2})\]");
                            if (timeMatch.Success)
                            {
                                var timeTag = timeMatch.Groups[1].Value;
                                var lyricContent = line.Substring(timeMatch.Index + timeMatch.Length).Trim();
                                
                                // 跳过纯时间标签行（没有歌词内容）
                                if (!string.IsNullOrWhiteSpace(lyricContent))
                                {
                                    lrcBuilder.AppendLine($"[{timeTag}]{lyricContent}");
                                    processedLines++;
                                }
                            }
                            else
                            {
                                // 处理其他格式的时间标签
                                lrcBuilder.AppendLine(line);
                                processedLines++;
                            }
                        }
                        else
                        {
                            // 没有时间标签的行，直接添加
                            lrcBuilder.AppendLine(line);
                            processedLines++;
                        }
                    }

                    logger.LogMessage($"✅ 原歌词处理完成，共处理 {processedLines} 行");
                }

                // 处理翻译歌词
                if (includeTranslation && !string.IsNullOrEmpty(lyricResponse.Tlyric?.Lyric))
                {
                    logger.LogMessage("🌐 处理翻译歌词...");
                    lrcBuilder.AppendLine();
                    lrcBuilder.AppendLine("[翻译歌词]");
                    
                    var translationLines = lyricResponse.Tlyric.Lyric.Split('\n');
                    var processedTranslationLines = 0;

                    foreach (var line in translationLines)
                    {
                        if (string.IsNullOrWhiteSpace(line)) continue;

                        // 检查是否包含时间标签
                        if (line.Contains('[') && line.Contains(']'))
                        {
                            var timeMatch = System.Text.RegularExpressions.Regex.Match(line, @"\[(\d{2}:\d{2}\.\d{2})\]");
                            if (timeMatch.Success)
                            {
                                var timeTag = timeMatch.Groups[1].Value;
                                var lyricContent = line.Substring(timeMatch.Index + timeMatch.Length).Trim();
                                
                                if (!string.IsNullOrWhiteSpace(lyricContent))
                                {
                                    lrcBuilder.AppendLine($"[{timeTag}]{lyricContent}");
                                    processedTranslationLines++;
                                }
                            }
                        }
                    }

                    logger.LogMessage($"✅ 翻译歌词处理完成，共处理 {processedTranslationLines} 行");
                }

                // 处理罗马音歌词
                if (includeRomalrc && !string.IsNullOrEmpty(lyricResponse.Romalrc?.Lyric))
                {
                    logger.LogMessage("🎵 处理罗马音歌词...");
                    lrcBuilder.AppendLine();
                    lrcBuilder.AppendLine("[罗马音歌词]");
                    
                    var romalrcLines = lyricResponse.Romalrc.Lyric.Split('\n');
                    var processedRomalrcLines = 0;

                    foreach (var line in romalrcLines)
                    {
                        if (string.IsNullOrWhiteSpace(line)) continue;

                        if (line.Contains('[') && line.Contains(']'))
                        {
                            var timeMatch = System.Text.RegularExpressions.Regex.Match(line, @"\[(\d{2}:\d{2}\.\d{2})\]");
                            if (timeMatch.Success)
                            {
                                var timeTag = timeMatch.Groups[1].Value;
                                var lyricContent = line.Substring(timeMatch.Index + timeMatch.Length).Trim();
                                
                                if (!string.IsNullOrWhiteSpace(lyricContent))
                                {
                                    lrcBuilder.AppendLine($"[{timeTag}]{lyricContent}");
                                    processedRomalrcLines++;
                                }
                            }
                        }
                    }

                    logger.LogMessage($"✅ 罗马音歌词处理完成，共处理 {processedRomalrcLines} 行");
                }

                var lrcContent = lrcBuilder.ToString();
                logger.LogMessage($"🎵 LRC格式转换完成，总长度: {lrcContent.Length} 字符");
                
                return lrcContent;
            }
            catch (Exception ex)
            {
                logger.LogMessage($"❌ 转换LRC格式失败: {ex.Message}");
                return null;
            }
        }



        /// <summary>
        /// 获取网易云歌曲/专辑封面直链（通过网页解析）
        /// </summary>
        /// <param name="id">数字ID</param>
        /// <param name="type">"song" 或 "album"</param>
        /// <returns>封面URL；失败返回 null</returns>
        public async Task<string> GetCoverUrlAsync(string id, string type = "song")
        {
            if (string.IsNullOrWhiteSpace(id) || (type != "song" && type != "album"))
                return null;

            try
            {
                string url;
                if (type == "song")
                {
                    url = $"https://music.163.com/song?id={id}";
                }
                else // album
                {
                    url = $"https://music.163.com/album?id={id}";
                }

                // 设置请求头，模拟浏览器访问
                httpClient.DefaultRequestHeaders.Clear();
                httpClient.DefaultRequestHeaders.Add("User-Agent", 
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
                httpClient.DefaultRequestHeaders.Add("Accept", 
                    "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
                httpClient.DefaultRequestHeaders.Add("Accept-Language", "zh-CN,zh;q=0.9");
                httpClient.DefaultRequestHeaders.Add("Connection", "keep-alive");

                // 使用GetByteArrayAsync避免字符集问题，然后智能解码
                var responseBytes = await httpClient.GetByteArrayAsync(url);
                string response = DetectAndDecodeString(responseBytes);
                
                if (type == "song")
                {
                    return ExtractSongCoverFromHtml(response);
                }
                else
                {
                    return ExtractAlbumCoverFromHtml(response);
                }
            }
            catch (Exception ex)
            {
                logger.LogMessage($"❌ 获取封面失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 智能检测并解码字符串，处理不同的字符编码
        /// </summary>
        private string DetectAndDecodeString(byte[] bytes)
        {
            try
            {
                // 首先尝试从HTML中检测charset
                string htmlStart = System.Text.Encoding.UTF8.GetString(bytes, 0, Math.Min(1024, bytes.Length));
                var charsetMatch = System.Text.RegularExpressions.Regex.Match(htmlStart, 
                    @"charset=[""']?([^""'>\s]+)[""']?", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                
                if (charsetMatch.Success)
                {
                    string charset = charsetMatch.Groups[1].Value.ToLower();
                    try
                    {
                        var encoding = System.Text.Encoding.GetEncoding(charset);
                        return encoding.GetString(bytes);
                    }
                    catch
                    {
                        // 如果指定的编码不支持，继续尝试其他方法
                    }
                }
                
                // 尝试常见的中文编码
                var encodings = new[]
                {
                    System.Text.Encoding.UTF8,
                    System.Text.Encoding.GetEncoding("GB2312"),
                    System.Text.Encoding.GetEncoding("GBK"),
                    System.Text.Encoding.GetEncoding("GB18030"),
                    System.Text.Encoding.ASCII
                };
                
                foreach (var encoding in encodings)
                {
                    try
                    {
                        string result = encoding.GetString(bytes);
                        // 简单检查是否包含中文字符，验证解码是否正确
                        if (result.Contains("网易云音乐") || result.Contains("music.163.com"))
                        {
                            return result;
                        }
                    }
                    catch
                    {
                        continue;
                    }
                }
                
                // 如果所有方法都失败，使用UTF-8作为默认
                return System.Text.Encoding.UTF8.GetString(bytes);
            }
            catch
            {
                // 最后的保险，使用UTF-8
                return System.Text.Encoding.UTF8.GetString(bytes);
            }
        }

        /// <summary>
        /// 从歌曲页面HTML中提取封面URL
        /// </summary>
        private string ExtractSongCoverFromHtml(string html)
        {
            try
            {
                // 查找 og:image meta 标签
                var ogImageMatch = System.Text.RegularExpressions.Regex.Match(html, 
                    @"<meta\s+property=""og:image""\s+content=""([^""]+)""");
                
                if (ogImageMatch.Success)
                {
                    var imageUrl = ogImageMatch.Groups[1].Value;
                    // 移除URL参数，只保留基础URL
                    return imageUrl.Split('?')[0];
                }

                logger.LogMessage("⚠️ 未在歌曲页面找到封面图片");
                return null;
            }
            catch (Exception ex)
            {
                logger.LogMessage($"❌ 解析歌曲页面HTML失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 从专辑页面HTML中提取封面URL
        /// </summary>
        private string ExtractAlbumCoverFromHtml(string html)
        {
            try
            {
                // 查找专辑封面图片
                var coverMatch = System.Text.RegularExpressions.Regex.Match(html, 
                    @"<div\s+class=""cover\s+u-cover\s+u-cover-alb"">.*?<img[^>]+data-src=""([^""]+)""");
                
                if (coverMatch.Success)
                {
                    var imageUrl = coverMatch.Groups[1].Value;
                    // 移除URL参数，只保留基础URL
                    return imageUrl.Split('?')[0];
                }

                // 如果上面的方法失败，尝试查找 og:image
                var ogImageMatch = System.Text.RegularExpressions.Regex.Match(html, 
                    @"<meta\s+property=""og:image""\s+content=""([^""]+)""");
                
                if (ogImageMatch.Success)
                {
                    var imageUrl = ogImageMatch.Groups[1].Value;
                    return imageUrl.Split('?')[0];
                }

                logger.LogMessage("⚠️ 未在专辑页面找到封面图片");
                return null;
            }
            catch (Exception ex)
            {
                logger.LogMessage($"❌ 解析专辑页面HTML失败: {ex.Message}");
                return null;
            }
        }



        #region 网易云音乐API加密和签名方法

        /// <summary>
        /// 网易云音乐API加密方法
        /// </summary>
        private string EncryptRequest(string text, string key)
        {
            try
            {
                // 使用AES加密
                using (var aes = Aes.Create())
                {
                    aes.Mode = CipherMode.CBC;
                    aes.Padding = PaddingMode.PKCS7;
                    aes.Key = Encoding.UTF8.GetBytes(key);
                    aes.IV = Encoding.UTF8.GetBytes("0102030405060708"); // 固定IV

                    using (var encryptor = aes.CreateEncryptor())
                    using (var msEncrypt = new MemoryStream())
                    using (var csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                    using (var swEncrypt = new StreamWriter(csEncrypt))
                    {
                        swEncrypt.Write(text);
                        swEncrypt.Close();
                        return Convert.ToBase64String(msEncrypt.ToArray());
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogMessage($"❌ 加密请求失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 生成网易云音乐API签名
        /// </summary>
        private string GenerateSignature(string text, string key)
        {
            try
            {
                using (var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key)))
                {
                    byte[] hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(text));
                    return Convert.ToBase64String(hashBytes);
                }
            }
            catch (Exception ex)
            {
                logger.LogMessage($"❌ 生成签名失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 创建网易云音乐API请求参数
        /// </summary>
        private FormUrlEncodedContent CreateWeApiRequest(object data)
        {
            try
            {
                // 将数据转换为JSON
                string jsonData = JsonConvert.SerializeObject(data);
                logger.LogMessage($"🔐 原始请求数据: {jsonData}");
                
                // 使用网易云音乐的标准密钥
                string secretKey = "0CoJUm6Qyw8W8jud";
                string encSecKey = "257348aecb5e556c066de214e531faadd1c55d814f9be95fd06d6bff9f4c7a41f831f6394d5a3fd2e3881736d94a02ca919d952872e7d0a50ebfa1769a7a62d512f5f1ca21aec60bc3819a9c3ffca5eca9a0dba6d6f724209b403c3b38fe99d635f74566507b1519ad1681e39f872e8342540d2a3b7d1d1d0bfcafed9d4d06671";
                
                // 第一次加密
                string firstEncrypt = EncryptRequest(jsonData, secretKey);
                if (string.IsNullOrEmpty(firstEncrypt)) 
                {
                    logger.LogMessage("❌ 第一次加密失败");
                    return null;
                }
                logger.LogMessage($"🔐 第一次加密结果: {firstEncrypt.Substring(0, Math.Min(50, firstEncrypt.Length))}...");
                
                // 第二次加密
                string secondEncrypt = EncryptRequest(firstEncrypt, secretKey);
                if (string.IsNullOrEmpty(secondEncrypt)) 
                {
                    logger.LogMessage("❌ 第二次加密失败");
                    return null;
                }
                logger.LogMessage($"🔐 第二次加密结果: {secondEncrypt.Substring(0, Math.Min(50, secondEncrypt.Length))}...");
                
                // 创建请求参数
                var parameters = new List<KeyValuePair<string, string>>
                {
                    new KeyValuePair<string, string>("params", secondEncrypt),
                    new KeyValuePair<string, string>("encSecKey", encSecKey)
                };
                
                logger.LogMessage("✅ API请求参数创建成功");
                return new FormUrlEncodedContent(parameters);
            }
            catch (Exception ex)
            {
                logger.LogMessage($"❌ 创建API请求参数失败: {ex.Message}");
                return null;
            }
        }

        #endregion

        #region 网易云音乐API方法实现

        /// <summary>
        /// 获取歌曲详情
        /// </summary>
        public async Task<NeteaseSong> GetSongDetailAsync(long songId)
        {
            try
            {
                logger.LogMessage($"🎵 获取歌曲详情: {songId}");
                
                var requestData = new
                {
                    ids = $"[{songId}]",
                    c = "[]"
                };
                
                var content = CreateWeApiRequest(requestData);
                if (content == null)
                {
                    logger.LogMessage("❌ 创建歌曲详情请求失败");
                    return null;
                }
                
                var response = await httpClient.PostAsync($"{NETEASE_API_WEAPI}/v3/song/detail", content);
                
                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var jsonResponse = JObject.Parse(responseContent);
                    
                    if (jsonResponse["code"]?.Value<int>() == 200)
                    {
                        var songs = jsonResponse["songs"]?.ToObject<List<NeteaseSong>>();
                        if (songs != null && songs.Count > 0)
                        {
                            logger.LogMessage($"✅ 获取歌曲详情成功: {songs[0].Name}");
                            return songs[0];
                        }
                    }
                }
                
                logger.LogMessage($"❌ 获取歌曲详情失败: {songId}");
                return null;
            }
            catch (Exception ex)
            {
                logger.LogMessage($"❌ 获取歌曲详情异常: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 获取歌词（返回字符串）
        /// </summary>
        public async Task<string> GetLyricAsync(long songId)
        {
            try
            {
                logger.LogMessage($"🎵 获取歌词: {songId}");
                
                // 使用简单的GET请求获取歌词
                string lyricUrl = $"https://music.163.com/api/song/lyric?id={songId}&lv=-1&kv=-1&tv=-1";
                logger.LogMessage($"📡 歌词请求URL: {lyricUrl}");
                
                var response = await httpClient.GetAsync(lyricUrl);
                
                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    logger.LogMessage($"📡 歌词API响应: {responseContent.Substring(0, Math.Min(200, responseContent.Length))}...");
                    
                    var jsonResponse = JObject.Parse(responseContent);
                    
                    if (jsonResponse["code"]?.Value<int>() == 200)
                    {
                        var lrc = jsonResponse["lrc"]?["lyric"]?.Value<string>();
                        if (!string.IsNullOrEmpty(lrc))
                        {
                            logger.LogMessage($"✅ 获取歌词成功");
                            return lrc;
                        }
                    }
                }
                
                logger.LogMessage($"❌ 获取歌词失败: {songId}");
                return null;
            }
            catch (Exception ex)
            {
                logger.LogMessage($"❌ 获取歌词异常: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 获取歌词（返回完整响应对象）
        /// </summary>
        public async Task<NeteaseLyricResponse> GetLyricResponseAsync(long songId)
        {
            try
            {
                logger.LogMessage($"🎵 获取歌词响应: {songId}");
                
                // 使用简单的GET请求获取歌词
                string lyricUrl = $"https://music.163.com/api/song/lyric?id={songId}&lv=-1&kv=-1&tv=-1";
                logger.LogMessage($"📡 歌词请求URL: {lyricUrl}");
                
                var response = await httpClient.GetAsync(lyricUrl);
                
                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    logger.LogMessage($"📡 歌词API响应: {responseContent.Substring(0, Math.Min(200, responseContent.Length))}...");
                    
                    var jsonResponse = JObject.Parse(responseContent);
                    
                    if (jsonResponse["code"]?.Value<int>() == 200)
                    {
                        var lyricResponse = jsonResponse.ToObject<NeteaseLyricResponse>();
                        if (lyricResponse != null)
                        {
                            logger.LogMessage($"✅ 获取歌词响应成功");
                            return lyricResponse;
                        }
                    }
                }
                
                logger.LogMessage($"❌ 获取歌词响应失败: {songId}");
                return null;
            }
            catch (Exception ex)
            {
                logger.LogMessage($"❌ 获取歌词响应异常: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 获取专辑详情
        /// </summary>
        public async Task<NeteaseAlbum> GetAlbumDetailAsync(long albumId)
        {
            try
            {
                logger.LogMessage($"💿 获取专辑详情: {albumId}");
                
                var requestData = new
                {
                    id = albumId
                };
                
                var content = CreateWeApiRequest(requestData);
                if (content == null)
                {
                    logger.LogMessage("❌ 创建专辑详情请求失败");
                    return null;
                }
                
                var response = await httpClient.PostAsync($"{NETEASE_API_WEAPI}/v1/album/{albumId}", content);
                
                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var jsonResponse = JObject.Parse(responseContent);
                    
                    if (jsonResponse["code"]?.Value<int>() == 200)
                    {
                        var album = jsonResponse["album"]?.ToObject<NeteaseAlbum>();
                        if (album != null)
                        {
                            logger.LogMessage($"✅ 获取专辑详情成功: {album.Name}");
                            return album;
                        }
                    }
                }
                
                logger.LogMessage($"❌ 获取专辑详情失败: {albumId}");
                return null;
            }
            catch (Exception ex)
            {
                logger.LogMessage($"❌ 获取专辑详情异常: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 获取艺术家详情
        /// </summary>
        public async Task<NeteaseArtist> GetArtistDetailAsync(long artistId)
        {
            try
            {
                logger.LogMessage($"🎤 获取艺术家详情: {artistId}");
                
                var requestData = new
                {
                    id = artistId
                };
                
                var content = CreateWeApiRequest(requestData);
                if (content == null)
                {
                    logger.LogMessage("❌ 创建艺术家详情请求失败");
                    return null;
                }
                
                var response = await httpClient.PostAsync($"{NETEASE_API_WEAPI}/v1/artist/{artistId}", content);
                
                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var jsonResponse = JObject.Parse(responseContent);
                    
                    if (jsonResponse["code"]?.Value<int>() == 200)
                    {
                        var artist = jsonResponse["artist"]?.ToObject<NeteaseArtist>();
                        if (artist != null)
                        {
                            logger.LogMessage($"✅ 获取艺术家详情成功: {artist.Name}");
                            return artist;
                        }
                    }
                }
                
                logger.LogMessage($"❌ 获取艺术家详情失败: {artistId}");
                return null;
            }
            catch (Exception ex)
            {
                logger.LogMessage($"❌ 获取艺术家详情异常: {ex.Message}");
                return null;
            }
        }

        #endregion

        #region 网页解析搜索方法

        /// <summary>
        /// 通过网页解析方式搜索音乐
        /// </summary>
        private async Task<NeteaseSong> SearchMusicByWebParsing(string searchKeywords, MusicInfo musicInfo)
        {
            try
            {
                logger.LogMessage($"🌐 使用网页解析搜索: '{searchKeywords}'");
                
                // 构建搜索URL
                string encodedKeywords = Uri.EscapeDataString(searchKeywords);
                string searchUrl = $"https://music.163.com/#/search/m/?s={encodedKeywords}&type=1";
                
                // 尝试使用移动端搜索接口
                string mobileSearchUrl = $"https://music.163.com/api/search/get/web?s={encodedKeywords}&type=1&limit=20&offset=0";
                
                logger.LogMessage($"📡 发送网页搜索请求: {mobileSearchUrl}");
                
                var response = await httpClient.GetAsync(mobileSearchUrl);
                
                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    logger.LogMessage($"📡 网页搜索响应: {responseContent.Substring(0, Math.Min(200, responseContent.Length))}...");
                    
                    // 尝试解析响应
                    var jsonResponse = JObject.Parse(responseContent);
                    
                    if (jsonResponse["code"]?.Value<int>() == 200)
                    {
                        var result = jsonResponse["result"];
                        var songs = result["songs"]?.ToObject<List<NeteaseSong>>();
                        
                        if (songs != null && songs.Count > 0)
                        {
                            logger.LogMessage($"🎵 网页搜索找到 {songs.Count} 首歌曲");
                            
                            // 匹配最佳结果
                            var bestMatch = FindBestMatch(musicInfo, songs);
                            
                            if (bestMatch != null)
                            {
                                logger.LogMessage($"✅ 网页搜索找到匹配歌曲: {bestMatch.Name}");
                                return bestMatch;
                            }
                        }
                    }
                }
                
                logger.LogMessage("❌ 网页解析搜索失败");
                return null;
            }
            catch (Exception ex)
            {
                logger.LogMessage($"❌ 网页解析搜索异常: {ex.Message}");
                return null;
            }
        }

        #endregion

        public void Dispose()
        {
            httpClient?.Dispose();
        }
    }
}
