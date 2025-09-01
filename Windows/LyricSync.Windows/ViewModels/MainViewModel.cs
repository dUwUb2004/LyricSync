using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using LyricSync.Windows.Models;
using LyricSync.Windows.Services;
using LyricSync.Windows.Utils;
using LyricSync.Windows; // for LyricWindow
using Newtonsoft.Json;
using System.Collections.ObjectModel;

namespace LyricSync.Windows.ViewModels
{
    public class MainViewModel : IDisposable
    {
        private readonly AdbService adbService;
        private readonly NeteaseMusicService neteaseService;
        private readonly UIService uiService;
        private readonly ILogger logger;
        private readonly DispatcherTimer progressTimer;
        private LyricWindow lyricWindow;
        private DesktopLyricWindow desktopLyricWindow;
        private ObservableCollection<LyricLine> currentLyricLines = new ObservableCollection<LyricLine>();
        private int currentLyricIndex = -1;
        
        private bool isListening = false;
        private MusicInfo currentMusic;
        private string lastSearchedKey = null; // 记录上一次搜索使用的关键键(标题+歌手)

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
                
                // 重置上一次搜索的键
                lastSearchedKey = null;
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
                // 同步歌词高亮
                SyncLyricHighlight();
            }
        }

        private void SyncLyricHighlight()
        {
            try
            {
                if ((lyricWindow == null && desktopLyricWindow == null) || currentLyricLines == null || currentLyricLines.Count == 0)
                {
                    return;
                }

                double currentSeconds = currentMusic?.Position > 0 ? currentMusic.Position / 1000.0 : 0;

                int index = -1;
                for (int i = 0; i < currentLyricLines.Count; i++)
                {
                    if (currentLyricLines[i].TimeSeconds <= currentSeconds)
                    {
                        index = i;
                    }
                    else
                    {
                        break;
                    }
                }

                if (index != -1 && index != currentLyricIndex)
                {
                    currentLyricIndex = index;
                    if (lyricWindow != null)
                    {
                        lyricWindow.HighlightLine(index);
                    }
                    if (desktopLyricWindow != null)
                    {
                        desktopLyricWindow.HighlightLine(index);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogMessage($"❌ 同步歌词高亮失败: {ex.Message}");
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
                    
                    // 检查是否需要重新搜索（标题或歌手变更，或未曾搜索）
                    if (HasTrackChanged(musicInfo))
                    {
                        lastSearchedKey = BuildTrackKey(musicInfo);
                        logger.LogMessage($"🔄 轨道信息变化，开始搜索: '{lastSearchedKey}'");
                        
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
                        logger.LogMessage($"⏭️ 曲目信息未变化，跳过搜索: '{BuildTrackKey(musicInfo)}'");
                        logger.LogMessage($"💡 上次搜索键: '{lastSearchedKey ?? "无"}'");
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

                    // 如果歌词窗口已经打开，则在切歌后刷新为当前歌曲的歌词
                    if (lyricWindow != null)
                    {
                        logger.LogMessage("🔄 检测到切歌，正在刷新歌词窗口为当前歌曲...");
                        await RefreshLyricsForCurrentSongAsync();
                    }
                    if (desktopLyricWindow != null)
                    {
                        logger.LogMessage("🔄 检测到切歌，正在刷新桌面歌词窗口...");
                        await RefreshDesktopLyricsForCurrentSongAsync();
                    }
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

        private string BuildTrackKey(MusicInfo info)
        {
            if (info == null)
            {
                return null;
            }
            // 清理标题（移除括号内翻译等）
            string title = info.Title ?? string.Empty;
            int englishStart = title.IndexOf('(');
            if (englishStart > 0)
            {
                title = title.Substring(0, englishStart).Trim();
            }
            string artist = info.Artist ?? string.Empty;
            return $"{title.Trim()} - {artist.Trim()}";
        }

        private bool HasTrackChanged(MusicInfo newInfo)
        {
            // 构造当前键
            string newKey = BuildTrackKey(newInfo);
            // 尚未搜索过则需要搜索
            if (string.IsNullOrEmpty(lastSearchedKey))
            {
                logger.LogMessage($"🔎 尚未有上次搜索键，准备以 '{newKey}' 执行首次搜索");
                return true;
            }
            // 比较键是否变化
            bool changed = !string.Equals(newKey, lastSearchedKey, StringComparison.OrdinalIgnoreCase);
            if (changed)
            {
                logger.LogMessage($"🔄 曲目信息变化: '{lastSearchedKey}' -> '{newKey}'");
            }
            return changed;
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
                lastSearchedKey = null;
                
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

        /// <summary>
        /// 打开歌词窗口并加载当前歌曲的歌词
        /// </summary>
        public async Task<bool> OpenLyricWindowAsync()
        {
            try
            {
                if (!HasMatchedSongInfo())
                {
                    logger.LogMessage("❌ 没有匹配的歌曲信息，无法显示歌词");
                    return false;
                }

                // 如果窗口已存在，直接激活
                if (lyricWindow != null)
                {
                    lyricWindow.Activate();
                    return true;
                }

                // 获取歌词（JSON）
                var lyricResponse = await neteaseService.GetLyricAsync(currentMusic.MatchedSong.Id);
                if (lyricResponse == null || string.IsNullOrEmpty(lyricResponse.Lrc?.Lyric))
                {
                    logger.LogMessage("❌ 未获取到可用歌词");
                    return false;
                }

                // 使用新的解析方法，同时解析原文和翻译
                var parsed = LrcParser.ParseFromNeteaseResponse(lyricResponse);
                currentLyricLines = new System.Collections.ObjectModel.ObservableCollection<LyricLine>(parsed);
                currentLyricIndex = -1;

                // 检查是否有翻译
                bool hasTranslation = parsed.Any(line => line.HasTranslation);
                if (hasTranslation)
                {
                    logger.LogMessage("✅ 检测到翻译歌词，已加载");
                }
                else
                {
                    logger.LogMessage("ℹ️ 此歌曲没有翻译歌词");
                }

                // 打开窗口
                lyricWindow = new LyricWindow();
                lyricWindow.SetLyrics(currentLyricLines);
                lyricWindow.Closed += (s, e) => { lyricWindow = null; };
                
                // 如果有翻译，默认显示翻译
                if (hasTranslation)
                {
                    lyricWindow.SetShowTranslation(true);
                }
                
                lyricWindow.Show();

                // 立即同步一次高亮
                SyncLyricHighlight();

                logger.LogMessage("✅ 歌词窗口已打开");
                return true;
            }
            catch (Exception ex)
            {
                logger.LogMessage($"❌ 打开歌词窗口失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 打开桌面歌词窗口并加载当前歌曲歌词
        /// </summary>
        public async Task<bool> OpenDesktopLyricWindowAsync()
        {
            try
            {
                if (!HasMatchedSongInfo())
                {
                    logger.LogMessage("❌ 没有匹配的歌曲信息，无法显示桌面歌词");
                    return false;
                }

                if (desktopLyricWindow != null)
                {
                    desktopLyricWindow.Activate();
                    return true;
                }

                var lyricResponse = await neteaseService.GetLyricAsync(currentMusic.MatchedSong.Id);
                if (lyricResponse == null || string.IsNullOrEmpty(lyricResponse.Lrc?.Lyric))
                {
                    logger.LogMessage("❌ 未获取到可用歌词");
                    return false;
                }

                var parsed = LrcParser.ParseFromNeteaseResponse(lyricResponse);
                currentLyricLines = new System.Collections.ObjectModel.ObservableCollection<LyricLine>(parsed);
                currentLyricIndex = -1;

                desktopLyricWindow = new DesktopLyricWindow();
                desktopLyricWindow.SetLyrics(currentLyricLines);
                desktopLyricWindow.SendControlAsync = async (key) => await SendControlCommandAsync(key);
                desktopLyricWindow.Closed += (s, e) => { desktopLyricWindow = null; };
                desktopLyricWindow.Show();

                SyncLyricHighlight();

                logger.LogMessage("✅ 桌面歌词窗口已打开");
                return true;
            }
            catch (Exception ex)
            {
                logger.LogMessage($"❌ 打开桌面歌词窗口失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 刷新歌词窗口为当前匹配歌曲的歌词（含翻译）
        /// </summary>
        private async Task RefreshLyricsForCurrentSongAsync()
        {
            try
            {
                if (!HasMatchedSongInfo())
                {
                    return;
                }

                var lyricResponse = await neteaseService.GetLyricAsync(currentMusic.MatchedSong.Id);
                if (lyricResponse == null || string.IsNullOrEmpty(lyricResponse.Lrc?.Lyric))
                {
                    logger.LogMessage("⚠️ 当前歌曲未获取到可用歌词");
                    return;
                }

                // 解析原文+翻译
                var parsed = LrcParser.ParseFromNeteaseResponse(lyricResponse);
                currentLyricLines = new ObservableCollection<LyricLine>(parsed);
                currentLyricIndex = -1;

                if (lyricWindow != null)
                {
                    // 确保在UI线程刷新UI
                    await lyricWindow.Dispatcher.InvokeAsync(() =>
                    {
                        lyricWindow.SetLyrics(currentLyricLines);

                        // 如果有翻译，默认显示翻译
                        bool hasTranslation = parsed.Any(line => line.HasTranslation);
                        if (hasTranslation)
                        {
                            lyricWindow.SetShowTranslation(true);
                        }

                        // 同步高亮
                        SyncLyricHighlight();
                    });
                }

                logger.LogMessage("✅ 歌词已刷新为当前歌曲");
            }
            catch (Exception ex)
            {
                logger.LogMessage($"❌ 刷新当前歌曲歌词失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 刷新桌面歌词窗口为当前歌曲歌词
        /// </summary>
        private async Task RefreshDesktopLyricsForCurrentSongAsync()
        {
            try
            {
                if (desktopLyricWindow == null || !HasMatchedSongInfo())
                {
                    return;
                }

                var lyricResponse = await neteaseService.GetLyricAsync(currentMusic.MatchedSong.Id);
                if (lyricResponse == null || string.IsNullOrEmpty(lyricResponse.Lrc?.Lyric))
                {
                    logger.LogMessage("⚠️ 当前歌曲未获取到可用歌词");
                    return;
                }

                var parsed = LrcParser.ParseFromNeteaseResponse(lyricResponse);
                currentLyricLines = new System.Collections.ObjectModel.ObservableCollection<LyricLine>(parsed);
                currentLyricIndex = -1;

                await desktopLyricWindow.Dispatcher.InvokeAsync(() =>
                {
                    desktopLyricWindow.SetLyrics(currentLyricLines);
                    SyncLyricHighlight();
                });

                logger.LogMessage("✅ 桌面歌词已刷新为当前歌曲");
            }
            catch (Exception ex)
            {
                logger.LogMessage($"❌ 刷新桌面歌词失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 导出当前匹配歌曲的LRC歌词
        /// </summary>
        public async Task<bool> ExportLrcLyricAsync()
        {
            try
            {
                // 检查是否有匹配的歌曲
                if (!HasMatchedSongInfo())
                {
                    logger.LogMessage("❌ 没有匹配的歌曲信息，无法导出歌词");
                    return false;
                }

                var matchedSong = currentMusic.MatchedSong;
                logger.LogMessage($"🎵 开始导出歌曲 '{matchedSong.Name}' 的LRC歌词...");

                // 获取歌词
                var lyricResponse = await neteaseService.GetLyricAsync(matchedSong.Id);
                if (lyricResponse == null)
                {
                    logger.LogMessage("❌ 获取歌词失败，无法导出");
                    return false;
                }

                // 转换为LRC格式
                var lrcContent = neteaseService.ConvertToLrcFormat(lyricResponse, true, false);
                if (string.IsNullOrEmpty(lrcContent))
                {
                    logger.LogMessage("❌ 转换LRC格式失败，无法导出");
                    return false;
                }

                // 生成文件名
                string fileName = $"{matchedSong.Name} - {string.Join(", ", matchedSong.Artists?.Select(a => a.Name) ?? new List<string>())}.lrc";
                // 清理文件名中的非法字符
                fileName = System.IO.Path.GetInvalidFileNameChars().Aggregate(fileName, (current, c) => current.Replace(c, '_'));

                // 使用UIService显示保存对话框
                string filePath = uiService.ShowSaveFileDialog("保存LRC歌词文件", fileName, "LRC文件 (*.lrc)|*.lrc|所有文件 (*.*)|*.*");
                if (string.IsNullOrEmpty(filePath))
                {
                    logger.LogMessage("⚠️ 用户取消了保存操作");
                    return false;
                }

                // 保存文件
                System.IO.File.WriteAllText(filePath, lrcContent, System.Text.Encoding.UTF8);
                
                logger.LogMessage($"✅ LRC歌词导出成功: {filePath}");
                logger.LogMessage($"📄 文件大小: {new System.IO.FileInfo(filePath).Length} 字节");
                
                return true;
            }
            catch (Exception ex)
            {
                logger.LogMessage($"❌ 导出LRC歌词失败: {ex.Message}");
                return false;
            }
        }

        public MusicInfo CurrentMusic => currentMusic;

        /// <summary>
        /// 获取当前的桌面歌词窗口
        /// </summary>
        /// <returns>桌面歌词窗口实例，如果未打开返回null</returns>
        public DesktopLyricWindow GetDesktopLyricWindow()
        {
            return desktopLyricWindow;
        }

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
