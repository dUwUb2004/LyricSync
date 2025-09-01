using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using LyricSync.Windows.Models;
using LyricSync.Windows.Services;
using LyricSync.Windows.Utils;
using Newtonsoft.Json;

namespace LyricSync.Windows.ViewModels
{
    public class MainViewModel : IDisposable
    {
        private readonly AdbService adbService;
        private readonly NeteaseMusicService neteaseService;
        private readonly UIService uiService;
        private readonly ILogger logger;
        private readonly DispatcherTimer progressTimer;
        
        private bool isListening = false;
        private MusicInfo currentMusic;
        private string lastSearchedTitle = null; // 记录上一次搜索的歌曲名称

        public MainViewModel(ILogger logger, UIService uiService)
        {
            this.logger = logger;
            this.uiService = uiService;
            this.adbService = new AdbService(logger);
            this.neteaseService = new NeteaseMusicService(logger);
            
            // 初始化定时器
            progressTimer = new DispatcherTimer();
            progressTimer.Interval = TimeSpan.FromSeconds(1);
            progressTimer.Tick += ProgressTimer_Tick;
        }

        public async Task<bool> InitializeAsync()
        {
            try
            {
                // 初始化ADB服务
                bool adbInitialized = await adbService.InitializeAsync();
                if (!adbInitialized)
                {
                    return false;
                }

                // 测试网易云API连接
                await neteaseService.TestConnectionAsync();
                
                return true;
            }
            catch (Exception ex)
            {
                logger.LogMessage($"❌ 初始化失败: {ex.Message}");
                return false;
            }
        }

        public async Task StartListeningAsync()
        {
            try
            {
                logger.LogMessage("正在启动ADB日志监听...");
                
                // 启动ADB logcat监听
                await adbService.StartLogcatAsync(OnLogcatOutput, OnLogcatError);
                
                isListening = true;
                logger.LogMessage("ADB日志监听已启动，等待音乐信息...");
            }
            catch (Exception ex)
            {
                logger.LogMessage($"启动监听失败: {ex.Message}");
                throw;
            }
        }

        public event Action<MusicInfo> OnMusicInfoUpdated;

        public void StopListening()
        {
            try
            {
                adbService.StopLogcat();
                isListening = false;
                logger.LogMessage("已停止监听");
                
                // 停止进度条更新
                progressTimer.Stop();
                currentMusic = null;
                
                // 重置上一次搜索的标题
                lastSearchedTitle = null;
                logger.LogMessage("🔄 已重置搜索状态");
            }
            catch (Exception ex)
            {
                logger.LogMessage($"停止监听时出错: {ex.Message}");
            }
        }

        public bool IsListening => isListening;

        private void ProgressTimer_Tick(object sender, EventArgs e)
        {
            if (currentMusic != null && currentMusic.IsPlaying)
            {
                currentMusic.Position += 1000; // 增加1秒
            }
        }

        private void OnLogcatOutput(string line)
        {
            try
            {
                // 过滤掉空行和无关日志
                if (string.IsNullOrWhiteSpace(line))
                {
                    return;
                }
                
                // 查找JSON数据
                int jsonStart = line.IndexOf('{');
                if (jsonStart >= 0)
                {
                    string jsonData = line.Substring(jsonStart);
                    logger.LogMessage($"📋 发现JSON数据: {jsonData.Substring(0, Math.Min(100, jsonData.Length))}...");
                    ProcessMusicData(jsonData);
                }
                else
                {
                    // 记录非JSON日志行（可选，用于调试）
                    if (line.Contains("USB_MUSIC") || line.Contains("music") || line.Contains("song"))
                    {
                        logger.LogMessage($"📝 相关日志行: {line}");
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogMessage($"❌ 处理日志行失败: {ex.Message}");
                logger.LogMessage($"💡 问题日志行: {line}");
            }
        }

        private void OnLogcatError(string line)
        {
            if (!string.IsNullOrEmpty(line))
            {
                logger.LogMessage($"ADB错误: {line}");
            }
        }

        private void ProcessMusicData(string data)
        {
            try
            {
                logger.LogMessage($"📥 收到原始数据: {data}");
                
                // 尝试解析JSON数据
                var musicInfo = JsonConvert.DeserializeObject<MusicInfo>(data);
                if (musicInfo != null)
                {
                    // 验证音乐信息的完整性
                    if (string.IsNullOrEmpty(musicInfo.Title) && string.IsNullOrEmpty(musicInfo.Artist) && string.IsNullOrEmpty(musicInfo.Album))
                    {
                        logger.LogMessage("⚠️ 警告：音乐信息不完整，所有字段都为空");
                        logger.LogMessage("💡 这可能是Android端数据格式问题或音乐播放器未正确发送信息");
                    }
                    else
                    {
                        logger.LogMessage($"✅ 音乐信息解析成功");
                    }
                    
                    // 检查音乐信息是否真的发生了变化
                    bool titleChanged = currentMusic?.Title != musicInfo.Title;
                    bool artistChanged = currentMusic?.Artist != musicInfo.Artist;
                    bool albumChanged = currentMusic?.Album != musicInfo.Album;
                    
                    // 只有在音乐信息真正变化时才清除匹配信息
                    if ((titleChanged || artistChanged || albumChanged) && 
                        (currentMusic?.MatchedSong != null || !string.IsNullOrEmpty(currentMusic?.SearchResponseJson)))
                    {
                        logger.LogMessage($"🔄 检测到音乐信息变化，清除旧匹配信息");
                        logger.LogMessage($"  标题: {currentMusic?.Title} → {musicInfo.Title}");
                        logger.LogMessage($"  艺术家: {currentMusic?.Artist} → {musicInfo.Artist}");
                        logger.LogMessage($"  专辑: {currentMusic?.Album} → {musicInfo.Album}");
                        
                        // 清除匹配信息
                        currentMusic.MatchedSong = null;
                        currentMusic.SearchResponseJson = null;
                    }
                    else if (currentMusic?.MatchedSong != null)
                    {
                        logger.LogMessage($"✅ 音乐信息未变化，保留现有匹配信息: {currentMusic.MatchedSong.Name}");
                    }
                    
                    // 重要：保护API获取的时长信息，避免被Android端数据覆盖
                    if (currentMusic != null && currentMusic.Duration > 0)
                    {
                        logger.LogMessage($"🛡️ 保护现有时长信息: {TimeFormatter.FormatTime(currentMusic.Duration)}");
                        musicInfo.Duration = currentMusic.Duration; // 将API时长复制到新数据中
                    }
                    
                    // 保护匹配信息
                    if (currentMusic != null)
                    {
                        musicInfo.MatchedSong = currentMusic.MatchedSong;
                        musicInfo.SearchResponseJson = currentMusic.SearchResponseJson;
                    }
                    
                    // 更新当前音乐信息
                    currentMusic = musicInfo;
                    
                    logger.LogMessage($"收到音乐信息: {musicInfo.Title ?? "未知标题"} - {musicInfo.Artist ?? "未知艺术家"}");
                    
                    if (musicInfo.IsPlaying)
                    {
                        progressTimer.Start();
                    }
                    else
                    {
                        progressTimer.Stop();
                    }
                    
                    // 通知UI更新
                    OnMusicInfoUpdated?.Invoke(currentMusic);
                    
                    // 检查歌曲名称是否发生变化，只有变化时才搜索
                    if (HasTitleChanged(musicInfo.Title))
                    {
                        lastSearchedTitle = musicInfo.Title;
                        logger.LogMessage($"🔄 歌曲名称发生变化，开始搜索: '{musicInfo.Title}'");
                        
                        // 检查网易云API连接
                        _ = Task.Run(async () => 
                        {
                            try
                            {
                                // 先测试API连接
                                bool apiConnected = await neteaseService.TestConnectionAsync();
                                if (apiConnected)
                                {
                                    logger.LogMessage("✅ 网易云API连接正常，开始搜索...");
                                    await SearchNeteaseMusic(musicInfo);
                                }
                                else
                                {
                                    logger.LogMessage("❌ 网易云API连接失败，无法进行搜索");
                                    logger.LogMessage("💡 请确保网易云API服务器正在运行 (http://localhost:3000)");
                                }
                            }
                            catch (Exception ex)
                            {
                                logger.LogMessage($"❌ 搜索过程中发生错误: {ex.Message}");
                            }
                        });
                    }
                    else
                    {
                        logger.LogMessage($"⏭️ 歌曲名称未变化，跳过搜索: '{musicInfo.Title}'");
                        logger.LogMessage($"💡 上次搜索标题: '{lastSearchedTitle ?? "无"}'");
                    }
                }
                else
                {
                    logger.LogMessage("❌ 音乐信息解析失败：返回null");
                }
            }
            catch (JsonException ex)
            {
                logger.LogMessage($"❌ 解析音乐数据失败: {ex.Message}");
                logger.LogMessage($"💡 原始数据: {data}");
            }
            catch (Exception ex)
            {
                logger.LogMessage($"❌ 处理音乐数据时发生未知错误: {ex.Message}");
                logger.LogMessage($"💡 原始数据: {data}");
            }
        }

        private async Task SearchNeteaseMusic(MusicInfo musicInfo)
        {
            try
            {
                var matchedSong = await neteaseService.SearchMusicAsync(musicInfo);
                
                if (matchedSong != null)
                {
                    // 重要：将API返回的歌曲时长设置到当前音乐对象
                    if (currentMusic != null && matchedSong.Duration > 0)
                    {
                        currentMusic.Duration = matchedSong.Duration;
                        logger.LogMessage($"🔄 已更新歌曲时长: {TimeFormatter.FormatTime(currentMusic.Duration)}");
                    }
                    
                    // 保存匹配的歌曲信息到当前音乐对象
                    SaveMatchedSongInfo(matchedSong);
                    
                    logger.LogMessage($"✅ 匹配歌曲信息已保存: {matchedSong.Name} (ID: {matchedSong.Id})");
                }
                else
                {
                    // 清除之前的匹配信息
                    if (currentMusic != null)
                    {
                        currentMusic.MatchedSong = null;
                        currentMusic.SearchResponseJson = null;
                    }
                    logger.LogMessage("❌ 未找到匹配的歌曲信息");
                }
            }
            catch (Exception ex)
            {
                logger.LogMessage($"❌ 搜索网易云音乐失败: {ex.Message}");
            }
        }

        private bool HasTitleChanged(string newTitle)
        {
            if (string.IsNullOrEmpty(newTitle))
            {
                return false;
            }
            
            // 清理新标题（移除英文翻译部分）
            string cleanNewTitle = newTitle;
            int englishStart = cleanNewTitle.IndexOf('(');
            if (englishStart > 0)
            {
                cleanNewTitle = cleanNewTitle.Substring(0, englishStart).Trim();
            }
            
            // 清理上一次搜索的标题
            string cleanLastTitle = lastSearchedTitle;
            if (!string.IsNullOrEmpty(cleanLastTitle))
            {
                int lastEnglishStart = cleanLastTitle.IndexOf('(');
                if (lastEnglishStart > 0)
                {
                    cleanLastTitle = cleanLastTitle.Substring(0, lastEnglishStart).Trim();
                }
            }
            
            // 比较清理后的标题
            bool hasChanged = !string.Equals(cleanNewTitle, cleanLastTitle, StringComparison.OrdinalIgnoreCase);
            
            if (hasChanged)
            {
                logger.LogMessage($"🔄 标题变化检测: '{cleanLastTitle ?? "无"}' -> '{cleanNewTitle}'");
            }
            
            return hasChanged;
        }

        private void SaveMatchedSongInfo(NeteaseSong matchedSong)
        {
            try
            {
                if (currentMusic != null && matchedSong != null)
                {
                    // 清除旧的匹配信息
                    if (currentMusic.MatchedSong != null)
                    {
                        logger.LogMessage($"🔄 更新匹配信息: {currentMusic.MatchedSong.Name} → {matchedSong.Name}");
                    }
                    else
                    {
                        logger.LogMessage($"💾 新增匹配信息: {matchedSong.Name}");
                    }
                    
                    currentMusic.MatchedSong = matchedSong;
                    
                    // 重要：同步更新歌曲时长
                    if (matchedSong.Duration > 0)
                    {
                        currentMusic.Duration = matchedSong.Duration;
                        logger.LogMessage($"🔄 同步更新歌曲时长: {TimeFormatter.FormatTime(currentMusic.Duration)}");
                    }
                    
                    logger.LogMessage($"✅ 匹配歌曲信息已保存: {matchedSong.Name} (ID: {matchedSong.Id})");
                    
                    // 重要：立即更新UI显示匹配信息
                    uiService.UpdateMatchedSongDisplay(matchedSong);
                    logger.LogMessage($"🎯 UI已更新匹配歌曲显示: {matchedSong.Name}");
                }
            }
            catch (Exception ex)
            {
                logger.LogMessage($"❌ 保存匹配歌曲信息失败: {ex.Message}");
            }
        }

        public async Task SendControlCommandAsync(int keyCode)
        {
            if (isListening)
            {
                await adbService.SendControlCommandAsync(keyCode);
            }
        }

        public async Task TestManualSearchAsync()
        {
            try
            {
                logger.LogMessage("🧪 开始手动测试搜索功能...");
                
                // 创建一个测试用的音乐信息
                var testMusicInfo = new MusicInfo
                {
                    Title = "测试歌曲",
                    Artist = "测试艺术家",
                    Album = "测试专辑",
                    Position = 0,
                    IsPlaying = true,
                    Duration = 0 // 初始时长为0，等待API返回
                };
                
                logger.LogMessage($"🧪 测试音乐信息: {testMusicInfo.Title} - {testMusicInfo.Artist}");
                
                // 重要：将测试音乐信息设置为当前音乐，这样时长信息才能正确同步
                currentMusic = testMusicInfo;
                
                // 手动测试时，清除旧的匹配信息
                currentMusic.MatchedSong = null;
                currentMusic.SearchResponseJson = null;
                logger.LogMessage("🧪 测试模式：清除旧匹配信息");
                
                // 强制更新搜索状态
                lastSearchedTitle = null;
                
                // 执行搜索
                await SearchNeteaseMusic(testMusicInfo);
                
                // 验证匹配信息是否保存
                if (HasMatchedSongInfo())
                {
                    logger.LogMessage("✅ 测试完成，匹配信息已保存");
                    logger.LogMessage($"🎯 测试歌曲时长: {TimeFormatter.FormatTime(currentMusic.Duration)}");
                }
                else
                {
                    logger.LogMessage("⚠️ 测试完成，但匹配信息未保存");
                }
                
                logger.LogMessage("🧪 手动测试搜索完成");
            }
            catch (Exception ex)
            {
                logger.LogMessage($"❌ 手动测试搜索失败: {ex.Message}");
            }
        }

        private bool HasMatchedSongInfo()
        {
            return currentMusic?.MatchedSong != null;
        }

        public MusicInfo CurrentMusic => currentMusic;

        public void Dispose()
        {
            try
            {
                progressTimer?.Stop();
                adbService?.Cleanup();
                neteaseService?.Dispose();
            }
            catch (Exception ex)
            {
                logger.LogMessage($"⚠️ 清理资源时出错: {ex.Message}");
            }
        }
    }
}
