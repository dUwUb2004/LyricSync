using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Newtonsoft.Json;

namespace LyricSync.Windows
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        private Process adbProcess;
        private bool isListening = false;
        private DispatcherTimer progressTimer;
        private MusicInfo currentMusic;
        private string adbPath;
        private HttpClient httpClient;
        private const string NETEASE_API_BASE = "http://localhost:3000";
        private string lastSearchedTitle = null; // 记录上一次搜索的歌曲名称
        
        public MainWindow()
        {
            InitializeComponent();
            InitializeTimer();
            UpdateConnectionStatus(false);
            InitializeAdbPath();
            InitializeHttpClient();
        }
        
        private void InitializeTimer()
        {
            progressTimer = new DispatcherTimer();
            progressTimer.Interval = TimeSpan.FromSeconds(1);
            progressTimer.Tick += ProgressTimer_Tick;
        }
        
        private void InitializeAdbPath()
        {
            try
            {
                // 从嵌入式资源中提取ADB工具
                string tempDir = Path.Combine(Path.GetTempPath(), "LyricSync_ADB");
                if (!Directory.Exists(tempDir))
                {
                    Directory.CreateDirectory(tempDir);
                }
                
                string adbExePath = Path.Combine(tempDir, "adb.exe");
                string adbApiPath = Path.Combine(tempDir, "AdbWinApi.dll");
                string adbUsbApiPath = Path.Combine(tempDir, "AdbWinUsbApi.dll");
                
                // 检查是否需要提取文件
                bool needExtract = !File.Exists(adbExePath) || !File.Exists(adbApiPath) || !File.Exists(adbUsbApiPath);
                
                if (needExtract)
                {
                    LogMessage("🔧 正在从嵌入式资源中提取ADB工具...");
                    
                    // 提取adb.exe
                    ExtractEmbeddedResource("adb.exe", adbExePath);
                    
                    // 提取AdbWinApi.dll
                    ExtractEmbeddedResource("AdbWinApi.dll", adbApiPath);
                    
                    // 提取AdbWinUsbApi.dll
                    ExtractEmbeddedResource("AdbWinUsbApi.dll", adbUsbApiPath);
                    
                    LogMessage("✅ ADB工具提取完成");
                }
                
                adbPath = adbExePath;
                LogMessage("✅ 内置ADB工具已就绪，路径: " + adbPath);
                LogMessage("📱 可以开始连接Android设备");
            }
            catch (Exception ex)
            {
                LogMessage("❌ 初始化ADB工具失败: " + ex.Message);
                adbPath = null;
            }
        }
        
        private void InitializeHttpClient()
        {
            try
            {
                httpClient = new HttpClient();
                httpClient.Timeout = TimeSpan.FromSeconds(10);
                LogMessage("✅ HTTP客户端已初始化，网易云API地址: " + NETEASE_API_BASE);
                
                // 异步测试API连接
                _ = Task.Run(async () => await TestNeteaseApiConnection());
            }
            catch (Exception ex)
            {
                LogMessage("❌ 初始化HTTP客户端失败: " + ex.Message);
                httpClient = null;
            }
        }
        
        private async Task TestNeteaseApiConnection()
        {
            try
            {
                LogMessage("🔍 正在测试网易云API连接...");
                var response = await httpClient.GetAsync($"{NETEASE_API_BASE}/");
                
                if (response.IsSuccessStatusCode)
                {
                    LogMessage("✅ 网易云API连接测试成功");
                }
                else
                {
                    LogMessage($"⚠️ 网易云API连接测试失败: {response.StatusCode}");
                    LogMessage("💡 请确保API服务器正在运行");
                }
            }
            catch (Exception ex)
            {
                LogMessage($"❌ 网易云API连接测试失败: {ex.Message}");
                LogMessage("💡 请检查API服务器是否启动，地址是否正确");
            }
        }
        
        private void ExtractEmbeddedResource(string resourceName, string outputPath)
        {
            try
            {
                // 获取当前程序集
                Assembly assembly = Assembly.GetExecutingAssembly();
                
                // 构建完整的资源名称（包含命名空间）
                string fullResourceName = $"LyricSync.Windows.Tools.{resourceName}";
                
                // 从嵌入式资源中读取数据
                using (Stream resourceStream = assembly.GetManifestResourceStream(fullResourceName))
                {
                    if (resourceStream == null)
                    {
                        throw new Exception($"找不到嵌入式资源: {fullResourceName}");
                    }
                    
                    // 写入到临时文件
                    using (FileStream fileStream = new FileStream(outputPath, FileMode.Create))
                    {
                        resourceStream.CopyTo(fileStream);
                    }
                }
                
                LogMessage($"✅ 已提取: {resourceName}");
            }
            catch (Exception ex)
            {
                throw new Exception($"提取资源 {resourceName} 失败: {ex.Message}");
            }
        }
        
        private void ProgressTimer_Tick(object sender, EventArgs e)
        {
            if (currentMusic != null && currentMusic.IsPlaying)
            {
                currentMusic.Position += 1000; // 增加1秒
                UpdateProgressBar();
            }
        }
        
        private async void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            if (!isListening)
            {
                await StartListening();
            }
            else
            {
                StopListening();
            }
        }
        
        private async Task StartListening()
        {
            try
            {
                LogMessage("正在启动ADB日志监听...");
                UpdateConnectionStatus(false, "启动中...");
                
                // 检查ADB是否可用
                if (!await CheckAdbAvailable())
                {
                    MessageBox.Show("未找到内置ADB工具！\n\n请按以下步骤操作：\n1. 运行 download_adb_tools.bat 脚本下载ADB工具\n2. 或者手动将ADB工具复制到 Tools 目录\n3. 重新启动应用程序", "ADB工具缺失", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                
                // 启动ADB logcat监听
                await StartAdbLogcat();
                
                isListening = true;
                UpdateConnectionStatus(true, "正在监听安卓端日志");
                ConnectButton.Content = "停止监听";
                LogMessage("ADB日志监听已启动，等待音乐信息...");
                
                BottomStatusText.Text = "正在监听安卓端日志，请确保安卓端已启动并播放音乐";
            }
            catch (Exception ex)
            {
                LogMessage($"启动监听失败: {ex.Message}");
                UpdateConnectionStatus(false, "启动失败");
                MessageBox.Show($"启动监听失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void StopListening()
        {
            try
            {
                StopAdbLogcat();
                isListening = false;
                UpdateConnectionStatus(false);
                ConnectButton.Content = "开始监听";
                LogMessage("已停止监听");
                BottomStatusText.Text = "准备就绪";
                
                // 停止进度条更新
                progressTimer.Stop();
                currentMusic = null;
                ResetMusicDisplay();
                
                // 重置上一次搜索的标题
                lastSearchedTitle = null;
                LogMessage("🔄 已重置搜索状态");
            }
            catch (Exception ex)
            {
                LogMessage($"停止监听时出错: {ex.Message}");
            }
        }
        
        private async Task<bool> CheckAdbAvailable()
        {
            // 检查ADB路径是否已设置
            if (string.IsNullOrEmpty(adbPath))
            {
                LogMessage("❌ ADB工具路径未设置，请先下载ADB工具");
                return false;
            }
            
            try
            {
                LogMessage("🔍 正在检测内置ADB工具...");
                
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = adbPath,
                        Arguments = "version",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    }
                };
                
                process.Start();
                process.WaitForExit();
                
                if (process.ExitCode == 0)
                {
                    LogMessage($"✅ 内置ADB工具检测成功: {adbPath}");
                    LogMessage("🚀 ADB工具已就绪，可以开始连接设备");
                    return true;
                }
                else
                {
                    LogMessage($"❌ 内置ADB工具检测失败，退出码: {process.ExitCode}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                LogMessage($"❌ 检测内置ADB工具时发生错误: {ex.Message}");
                LogMessage("💡 请确保ADB工具文件完整且可执行");
                return false;
            }
        }
        
        private async Task StartAdbLogcat()
        {
            // 检查ADB路径是否已设置
            if (string.IsNullOrEmpty(adbPath))
            {
                LogMessage("❌ 无法启动ADB logcat：ADB工具路径未设置");
                throw new InvalidOperationException("ADB工具路径未设置");
            }
            
            try
            {
                LogMessage("🧹 清理之前的ADB日志...");
                // 先清理之前的日志
                await ExecuteAdbCommand("logcat -c");
                
                LogMessage("📡 启动ADB logcat监听进程...");
                // 启动logcat监听，过滤USB_MUSIC标签
                adbProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = adbPath,
                        Arguments = "logcat -s USB_MUSIC:D",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                        StandardOutputEncoding = Encoding.UTF8
                    }
                };
                
                adbProcess.OutputDataReceived += OnLogcatOutput;
                adbProcess.ErrorDataReceived += OnLogcatError;
                
                adbProcess.Start();
                adbProcess.BeginOutputReadLine();
                adbProcess.BeginErrorReadLine();
                
                LogMessage("✅ ADB logcat进程已启动，正在监听USB_MUSIC标签");
                LogMessage("🎵 请在Android设备上播放音乐，音乐信息将自动同步");
            }
            catch (Exception ex)
            {
                LogMessage($"❌ 启动ADB logcat失败: {ex.Message}");
                throw;
            }
        }
        
        private void StopAdbLogcat()
        {
            try
            {
                if (adbProcess != null && !adbProcess.HasExited)
                {
                    adbProcess.Kill();
                    adbProcess.Dispose();
                    adbProcess = null;
                }
                LogMessage("ADB logcat进程已停止");
            }
            catch (Exception ex)
            {
                LogMessage($"停止ADB logcat失败: {ex.Message}");
            }
        }
        
        private void OnLogcatOutput(object sender, DataReceivedEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                ProcessLogcatLine(e.Data);
            }
        }
        
        private void OnLogcatError(object sender, DataReceivedEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                LogMessage($"ADB错误: {e.Data}");
            }
        }
        
        private void ProcessLogcatLine(string line)
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
                    LogMessage($"📋 发现JSON数据: {jsonData.Substring(0, Math.Min(100, jsonData.Length))}...");
                    ProcessMusicData(jsonData);
                }
                else
                {
                    // 记录非JSON日志行（可选，用于调试）
                    if (line.Contains("USB_MUSIC") || line.Contains("music") || line.Contains("song"))
                    {
                        LogMessage($"📝 相关日志行: {line}");
                    }
                }
            }
            catch (Exception ex)
            {
                LogMessage($"❌ 处理日志行失败: {ex.Message}");
                LogMessage($"💡 问题日志行: {line}");
            }
        }
        
        private void ProcessMusicData(string data)
        {
            try
            {
                LogMessage($"📥 收到原始数据: {data}");
                
                // 尝试解析JSON数据
                var musicInfo = JsonConvert.DeserializeObject<MusicInfo>(data);
                if (musicInfo != null)
                {
                    // 验证音乐信息的完整性
                    if (string.IsNullOrEmpty(musicInfo.Title) && string.IsNullOrEmpty(musicInfo.Artist) && string.IsNullOrEmpty(musicInfo.Album))
                    {
                        LogMessage("⚠️ 警告：音乐信息不完整，所有字段都为空");
                        LogMessage("💡 这可能是Android端数据格式问题或音乐播放器未正确发送信息");
                    }
                    else
                    {
                        LogMessage($"✅ 音乐信息解析成功");
                    }
                    
                    Dispatcher.Invoke(() =>
                    {
                        // 检查音乐信息是否真的发生了变化
                        bool titleChanged = currentMusic?.Title != musicInfo.Title;
                        bool artistChanged = currentMusic?.Artist != musicInfo.Artist;
                        bool albumChanged = currentMusic?.Album != musicInfo.Album;
                        
                        // 只有在音乐信息真正变化时才清除匹配信息
                        if ((titleChanged || artistChanged || albumChanged) && 
                            (currentMusic?.MatchedSong != null || !string.IsNullOrEmpty(currentMusic?.SearchResponseJson)))
                        {
                            LogMessage($"🔄 检测到音乐信息变化，清除旧匹配信息");
                            LogMessage($"  标题: {currentMusic?.Title} → {musicInfo.Title}");
                            LogMessage($"  艺术家: {currentMusic?.Artist} → {musicInfo.Artist}");
                            LogMessage($"  专辑: {currentMusic?.Album} → {musicInfo.Album}");
                            
                            // 清除匹配信息
                            currentMusic.MatchedSong = null;
                            currentMusic.SearchResponseJson = null;
                        }
                        else if (currentMusic?.MatchedSong != null)
                        {
                            LogMessage($"✅ 音乐信息未变化，保留现有匹配信息: {currentMusic.MatchedSong.Name}");
                        }
                        
                        // 更新当前音乐信息
                        currentMusic = musicInfo;
                        
                        UpdateMusicDisplay(currentMusic);
                        LogMessage($"收到音乐信息: {musicInfo.Title ?? "未知标题"} - {musicInfo.Artist ?? "未知艺术家"}");
                        
                        if (musicInfo.IsPlaying)
                        {
                            progressTimer.Start();
                        }
                        else
                        {
                            progressTimer.Stop();
                        }
                    });
                    
                    // 检查歌曲名称是否发生变化，只有变化时才搜索
                    if (HasTitleChanged(musicInfo.Title))
                    {
                        lastSearchedTitle = musicInfo.Title;
                        LogMessage($"🔄 歌曲名称发生变化，开始搜索: '{musicInfo.Title}'");
                        // 异步搜索网易云音乐信息
                        _ = Task.Run(async () => await SearchNeteaseMusic(musicInfo));
                    }
                    else
                    {
                        LogMessage($"⏭️ 歌曲名称未变化，跳过搜索: '{musicInfo.Title}'");
                    }
                }
                else
                {
                    LogMessage("❌ 音乐信息解析失败：返回null");
                }
            }
            catch (JsonException ex)
            {
                LogMessage($"❌ 解析音乐数据失败: {ex.Message}");
                LogMessage($"💡 原始数据: {data}");
            }
            catch (Exception ex)
            {
                LogMessage($"❌ 处理音乐数据时发生未知错误: {ex.Message}");
                LogMessage($"💡 原始数据: {data}");
            }
        }
        
        private async Task SearchNeteaseMusic(MusicInfo musicInfo)
        {
            if (httpClient == null)
            {
                LogMessage("❌ HTTP客户端未初始化，无法搜索网易云音乐");
                return;
            }
            
            try
            {
                // 构建搜索关键词
                string searchKeywords = BuildSearchKeywords(musicInfo);
                
                // 检查搜索关键词是否有效
                if (string.IsNullOrWhiteSpace(searchKeywords))
                {
                    LogMessage("❌ 搜索关键词为空，跳过搜索");
                    return;
                }
                
                LogMessage($"🔍 正在搜索网易云音乐: '{searchKeywords}'");
                
                // 使用已验证有效的 'keywords' 参数进行搜索
                string encodedKeywords = Uri.EscapeDataString(searchKeywords);
                var searchUrl = $"{NETEASE_API_BASE}/search?keywords={encodedKeywords}&type=1&limit=20&offset=0";
                
                LogMessage($"📡 发送搜索请求: {searchUrl}");
                
                var response = await httpClient.GetAsync(searchUrl);
                
                if (response.IsSuccessStatusCode)
                {
                    LogMessage("✅ 搜索请求成功");
                    await ProcessSearchResponse(response, musicInfo);
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    LogMessage($"❌ 搜索请求失败，状态码: {response.StatusCode}");
                    LogMessage($"💡 错误响应: {errorContent}");
                }
            }
            catch (Exception ex)
            {
                LogMessage($"❌ 搜索网易云音乐失败: {ex.Message}");
                LogMessage($"💡 请检查网络连接和API服务器状态");
            }
        }
        
        private async Task ProcessSearchResponse(HttpResponseMessage response, MusicInfo musicInfo)
        {
            try
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                LogMessage($"📡 API响应: {responseContent.Substring(0, Math.Min(200, responseContent.Length))}...");
                
                var searchResponse = JsonConvert.DeserializeObject<NeteaseSearchResponse>(responseContent);
                
                if (searchResponse?.Result?.Songs != null && searchResponse.Result.Songs.Count > 0)
                {
                    LogMessage($"🎵 搜索到 {searchResponse.Result.Songs.Count} 首歌曲");
                    
                    // 匹配最佳结果
                    var bestMatch = FindBestMatch(musicInfo, searchResponse.Result.Songs);
                    
                    if (bestMatch != null)
                    {
                        LogMessage($"✅ 找到匹配歌曲: {bestMatch.Name} - {string.Join(", ", bestMatch.Artists?.Select(a => a.Name) ?? new List<string>())}");
                        LogMessage($"🎵 歌曲ID: {bestMatch.Id}");
                        LogMessage($"💿 专辑: {bestMatch.Album?.Name ?? "未知"}");
                        LogMessage($"⏱️ 时长: {FormatTime(bestMatch.Duration)}");
                        
                        // 保存匹配的歌曲信息到当前音乐对象
                        SaveMatchedSongInfo(bestMatch, responseContent);
                        
                        // 立即更新UI显示匹配的歌曲信息
                        UpdateMatchedSongDisplay(bestMatch, responseContent);
                        
                        // 重要：强制更新音乐显示，确保状态稳定
                        if (currentMusic != null)
                        {
                            LogMessage($"🔄 强制更新音乐显示，确保匹配信息状态稳定");
                            UpdateMusicDisplay(currentMusic);
                            
                            // 再次验证状态是否正确
                            if (HasMatchedSongInfo())
                            {
                                LogMessage($"✅ 状态验证成功：匹配信息已稳定保存");
                            }
                            else
                            {
                                LogMessage($"⚠️ 状态验证失败：匹配信息未正确保存");
                            }
                        }
                        
                        // 显示所有搜索结果供参考
                        LogMessage("📋 所有搜索结果:");
                        for (int i = 0; i < Math.Min(3, searchResponse.Result.Songs.Count); i++)
                        {
                            var song = searchResponse.Result.Songs[i];
                            LogMessage($"  {i + 1}. {song.Name} - {string.Join(", ", song.Artists?.Select(a => a.Name) ?? new List<string>())} (ID: {song.Id})");
                        }
                    }
                    else
                    {
                        LogMessage("⚠️ 未找到完全匹配的歌曲");
                        // 清除之前的匹配信息
                        if (currentMusic != null)
                        {
                            currentMusic.MatchedSong = null;
                            currentMusic.SearchResponseJson = null;
                        }
                        ClearMatchedSongDisplay();
                    }
                }
                else
                {
                    LogMessage("❌ 网易云API返回空结果");
                    LogMessage($"💡 响应内容: {responseContent}");
                    // 清除匹配信息
                    if (currentMusic != null)
                    {
                        currentMusic.MatchedSong = null;
                        currentMusic.SearchResponseJson = null;
                    }
                    ClearMatchedSongDisplay();
                }
            }
            catch (Exception ex)
            {
                LogMessage($"❌ 处理API响应失败: {ex.Message}");
            }
        }
        
        private string BuildSearchKeywords(MusicInfo musicInfo)
        {
            // 只搜索歌曲名称，不搜索艺术家和专辑
            if (string.IsNullOrEmpty(musicInfo.Title))
            {
                LogMessage("⚠️ 歌曲名称为空，无法搜索");
                return null;
            }
            
            // 移除英文翻译部分，只保留中文标题
            string title = musicInfo.Title;
            int englishStart = title.IndexOf('(');
            if (englishStart > 0)
            {
                title = title.Substring(0, englishStart).Trim();
            }
            
            LogMessage($"🔍 构建搜索关键词 - 只搜索歌曲名称: '{title}'");
            return title;
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
                LogMessage($"🔄 标题变化检测: '{cleanLastTitle ?? "无"}' -> '{cleanNewTitle}'");
            }
            
            return hasChanged;
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
            
            LogMessage($"🎯 开始匹配歌曲: '{cleanTitle}' - '{musicInfo.Artist}'");
            
            // 1. 完全匹配标题和艺术家
            var exactMatch = songs.FirstOrDefault(s => 
                string.Equals(s.Name, cleanTitle, StringComparison.OrdinalIgnoreCase) &&
                s.Artists?.Any(a => string.Equals(a.Name, musicInfo.Artist, StringComparison.OrdinalIgnoreCase)) == true);
            
            if (exactMatch != null)
            {
                LogMessage("🎯 找到完全匹配的歌曲");
                return exactMatch;
            }
            
            // 2. 标题完全匹配，艺术家部分匹配
            var titleExactArtistPartial = songs.FirstOrDefault(s => 
                string.Equals(s.Name, cleanTitle, StringComparison.OrdinalIgnoreCase) &&
                s.Artists?.Any(a => musicInfo.Artist.Contains(a.Name) || a.Name.Contains(musicInfo.Artist)) == true);
            
            if (titleExactArtistPartial != null)
            {
                LogMessage("🎯 找到标题完全匹配，艺术家部分匹配的歌曲");
                return titleExactArtistPartial;
            }
            
            // 3. 标题完全匹配
            var titleMatch = songs.FirstOrDefault(s => 
                string.Equals(s.Name, cleanTitle, StringComparison.OrdinalIgnoreCase));
            
            if (titleMatch != null)
            {
                LogMessage("🎯 找到标题匹配的歌曲");
                return titleMatch;
            }
            
            // 4. 标题包含匹配
            var titleContains = songs.FirstOrDefault(s => 
                s.Name.Contains(cleanTitle) || cleanTitle.Contains(s.Name));
            
            if (titleContains != null)
            {
                LogMessage("🎯 找到标题包含匹配的歌曲");
                return titleContains;
            }
            
            // 5. 返回第一个结果
            LogMessage("🎯 未找到精确匹配，返回第一个搜索结果");
            return songs[0];
        }
        
        private async Task ExecuteAdbCommand(string arguments)
        {
            // 检查ADB路径是否已设置
            if (string.IsNullOrEmpty(adbPath))
            {
                LogMessage("❌ 无法执行ADB命令：ADB工具路径未设置");
                return;
            }
            
            try
            {
                LogMessage($"🔧 执行ADB命令: {arguments}");
                
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = adbPath,
                        Arguments = arguments,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    }
                };
                
                process.Start();
                process.WaitForExit();
                
                if (process.ExitCode != 0)
                {
                    LogMessage($"❌ ADB命令执行失败: {arguments}，退出码: {process.ExitCode}");
                }
                else
                {
                    LogMessage($"✅ ADB命令执行成功: {arguments}");
                }
            }
            catch (Exception ex)
            {
                LogMessage($"❌ 执行ADB命令时发生错误: {ex.Message}");
            }
        }
        
        private void UpdateMusicDisplay(MusicInfo music)
        {
            SongTitle.Text = music.Title ?? "未知歌曲";
            ArtistName.Text = music.Artist ?? "未知艺术家";
            AlbumName.Text = music.Album ?? "未知专辑";
            
            UpdateProgressBar();
            
            // 严格的状态保护：有匹配信息时绝对不覆盖
            if (HasMatchedSongInfo())
            {
                // 有匹配信息就显示，并且强制保护状态
                LogMessage($"🛡️ 状态保护：保持匹配信息显示 - {music.MatchedSong.Name}");
                UpdateMatchedSongDisplay(music.MatchedSong, music.SearchResponseJson);
                return; // 重要：有匹配信息时直接返回，不执行后续逻辑
            }
            
            // 没有匹配信息时的处理
            if (string.IsNullOrEmpty(music.Title) || music.Title == "未播放")
            {
                ClearMatchedSongDisplay();
            }
            else
            {
                // 只有在确实没有匹配信息且音乐正在播放时才显示等待搜索状态
                // 额外检查：确保当前UI不是显示匹配信息状态
                string currentState = GetCurrentDisplayState();
                if (currentState != "matched")
                {
                    ShowWaitingForSearchStatus();
                }
                else
                {
                    LogMessage($"⚠️ 状态冲突：UI显示匹配信息但数据中没有，保持当前显示");
                }
            }
        }
        
        private void UpdateProgressBar()
        {
            if (currentMusic != null)
            {
                ProgressBar.Value = currentMusic.Position;
                CurrentTime.Text = FormatTime(currentMusic.Position);
                TotalTime.Text = FormatTime(currentMusic.Duration);
            }
        }
        
        private void ResetMusicDisplay()
        {
            SongTitle.Text = "未播放";
            ArtistName.Text = "";
            AlbumName.Text = "";
            ProgressBar.Value = 0;
            CurrentTime.Text = "0:00";
            TotalTime.Text = "0:00";
            
            // 清除匹配歌曲信息
            ClearMatchedSongDisplay();
        }
        
        private string FormatTime(long milliseconds)
        {
            if (milliseconds <= 0) return "0:00";
            
            TimeSpan time = TimeSpan.FromMilliseconds(milliseconds);
            return $"{(int)time.TotalMinutes}:{time.Seconds:D2}";
        }
        
        private void UpdateConnectionStatus(bool connected, string description = null)
        {
            if (connected)
            {
                StatusText.Text = "正在监听";
                StatusText.Foreground = System.Windows.Media.Brushes.Green;
                StatusDescription.Text = description ?? "正在监听安卓端日志";
            }
            else
            {
                StatusText.Text = "未监听";
                StatusText.Foreground = System.Windows.Media.Brushes.Red;
                StatusDescription.Text = description ?? "请点击开始监听按钮";
            }
        }
        
        // 日志管理相关字段 - 固定配置，无需用户自定义
        private const int MAX_LOG_LINES = 1000;        // 最大日志行数
        private const int LOG_CLEANUP_THRESHOLD = 800; // 清理阈值
        private const int LOG_CLEANUP_COUNT = 200;     // 每次清理的行数
        
        private void LogMessage(string message)
        {
            string timestamp = DateTime.Now.ToString("HH:mm:ss");
            string logEntry = $"[{timestamp}] {message}";
            
            Dispatcher.Invoke(() =>
            {
                // 检查日志行数是否超过限制
                int currentLineCount = LogTextBox.Text.Split('\n').Length;
                
                if (currentLineCount > MAX_LOG_LINES)
                {
                    // 超过最大行数，进行智能清理
                    CleanupLogs();
                }
                else if (currentLineCount > LOG_CLEANUP_THRESHOLD)
                {
                    // 超过清理阈值，清理旧日志
                    CleanupOldLogs();
                }
                
                // 添加新日志
                LogTextBox.AppendText(logEntry + Environment.NewLine);
                LogTextBox.ScrollToEnd();
                
                // 更新日志状态
                UpdateLogStatus();
            });
        }
        
        /// <summary>
        /// 清理旧日志，保留最新的日志
        /// 系统自动执行，无需用户干预
        /// </summary>
        private void CleanupOldLogs()
        {
            try
            {
                var lines = LogTextBox.Text.Split('\n');
                if (lines.Length > LOG_CLEANUP_THRESHOLD)
                {
                    // 保留最新的日志，删除旧的
                    var newLines = lines.Skip(lines.Length - LOG_CLEANUP_THRESHOLD + LOG_CLEANUP_COUNT).ToArray();
                    LogTextBox.Text = string.Join("\n", newLines);
                    
                    // 记录清理信息
                    LogTextBox.AppendText($"[{DateTime.Now:HH:mm:ss}] 🧹 已清理 {LOG_CLEANUP_COUNT} 行旧日志，当前保留 {newLines.Length} 行" + Environment.NewLine);
                    LogTextBox.ScrollToEnd();
                    
                    // 更新日志状态
                    UpdateLogStatus();
                }
            }
            catch (Exception ex)
            {
                // 清理失败时，记录错误但不影响正常日志记录
                System.Diagnostics.Debug.WriteLine($"日志清理失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 强制清理日志，保留最新的日志
        /// 当日志超过最大限制时自动执行
        /// </summary>
        private void CleanupLogs()
        {
            try
            {
                var lines = LogTextBox.Text.Split('\n');
                if (lines.Length > MAX_LOG_LINES)
                {
                    // 保留最新的日志，删除超出的部分
                    var newLines = lines.Skip(lines.Length - MAX_LOG_LINES + 100).ToArray();
                    LogTextBox.Text = string.Join("\n", newLines);
                    
                    // 记录清理信息
                    LogTextBox.AppendText($"[{DateTime.Now:HH:mm:ss}] 🧹 日志数量超限，已清理至 {newLines.Length} 行" + Environment.NewLine);
                    LogTextBox.ScrollToEnd();
                    
                    // 更新日志状态
                    UpdateLogStatus();
                }
            }
            catch (Exception ex)
            {
                // 清理失败时，记录错误但不影响正常日志记录
                System.Diagnostics.Debug.WriteLine($"日志清理失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 更新日志状态显示
        /// </summary>
        private void UpdateLogStatus()
        {
            try
            {
                var lines = LogTextBox.Text.Split('\n');
                int currentLines = lines.Length;
                
                Dispatcher.Invoke(() =>
                {
                    if (currentLines > MAX_LOG_LINES)
                    {
                        LogStatusText.Text = $"日志状态: 超限 ({currentLines}/{MAX_LOG_LINES})";
                        LogStatusText.Foreground = System.Windows.Media.Brushes.Red;
                    }
                    else if (currentLines > LOG_CLEANUP_THRESHOLD)
                    {
                        LogStatusText.Text = $"日志状态: 接近限制 ({currentLines}/{MAX_LOG_LINES})";
                        LogStatusText.Foreground = System.Windows.Media.Brushes.Orange;
                    }
                    else
                    {
                        LogStatusText.Text = $"日志状态: 正常 ({currentLines}/{MAX_LOG_LINES})";
                        LogStatusText.Foreground = System.Windows.Media.Brushes.Green;
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"更新日志状态失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 更新匹配歌曲信息显示
        /// </summary>
        private void UpdateMatchedSongDisplay(NeteaseSong matchedSong, string jsonResponse)
        {
            try
            {
                Dispatcher.Invoke(() =>
                {
                    if (matchedSong != null)
                    {
                        // 显示匹配的歌曲信息
                        MatchedSongTitle.Text = $"🎵 {matchedSong.Name}";
                        MatchedSongArtist.Text = $"👤 艺术家: {string.Join(", ", matchedSong.Artists?.Select(a => a.Name) ?? new List<string>())}";
                        MatchedSongAlbum.Text = $"💿 专辑: {matchedSong.Album?.Name ?? "未知"}";
                        MatchedSongDuration.Text = $"⏱️ 时长: {FormatTime(matchedSong.Duration)}";
                        MatchedSongId.Text = $"🆔 歌曲ID: {matchedSong.Id}";
                        
                        // 显示格式化的JSON数据
                        try
                        {
                            var formattedJson = FormatJson(jsonResponse);
                            JsonDisplayTextBox.Text = formattedJson;
                        }
                        catch
                        {
                            JsonDisplayTextBox.Text = jsonResponse;
                        }
                        
                        // 展开匹配信息区域
                        MatchedSongExpander.IsExpanded = true;
                        
                        LogMessage($"🎯 匹配歌曲信息显示完成: {matchedSong.Name}");
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"更新匹配歌曲显示失败: {ex.Message}");
                LogMessage($"❌ 更新匹配歌曲显示失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 清除匹配歌曲信息显示
        /// </summary>
        private void ClearMatchedSongDisplay()
        {
            try
            {
                Dispatcher.Invoke(() =>
                {
                    MatchedSongTitle.Text = "未找到匹配歌曲";
                    MatchedSongArtist.Text = "";
                    MatchedSongAlbum.Text = "";
                    MatchedSongDuration.Text = "";
                    MatchedSongId.Text = "";
                    JsonDisplayTextBox.Text = "";
                    
                    // 收起匹配信息区域
                    MatchedSongExpander.IsExpanded = false;
                    
                    LogMessage("🧹 已清除匹配歌曲显示");
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"清除匹配歌曲显示失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 显示等待搜索状态
        /// </summary>
        private void ShowWaitingForSearchStatus()
        {
            try
            {
                Dispatcher.Invoke(() =>
                {
                    // 检查当前是否已经是等待搜索状态，避免重复设置
                    if (MatchedSongTitle.Text == "⏳ 等待搜索...")
                    {
                        return; // 已经是等待搜索状态，不需要重复设置
                    }
                    
                    // 重要：如果当前显示的是匹配信息，绝对不要覆盖
                    if (MatchedSongTitle.Text.StartsWith("🎵"))
                    {
                        LogMessage($"🛡️ 状态保护：当前显示匹配信息，不覆盖为等待搜索状态");
                        return;
                    }
                    
                    // 额外检查：如果当前音乐对象有匹配信息，也不应该显示等待搜索
                    if (HasMatchedSongInfo())
                    {
                        LogMessage($"🛡️ 状态保护：当前音乐有匹配信息，不显示等待搜索状态");
                        return;
                    }
                    
                    MatchedSongTitle.Text = "⏳ 等待搜索...";
                    MatchedSongArtist.Text = "等待网易云音乐搜索完成";
                    MatchedSongAlbum.Text = "";
                    MatchedSongDuration.Text = "";
                    MatchedSongId.Text = "";
                    JsonDisplayTextBox.Text = "搜索进行中，请稍候...";
                    
                    // 展开匹配信息区域，显示等待搜索状态
                    MatchedSongExpander.IsExpanded = true;
                    
                    LogMessage("⏳ 显示等待搜索状态");
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"显示等待搜索状态失败: {ex.Message}");
                LogMessage($"❌ 显示等待搜索状态失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 格式化JSON字符串，使其更易读
        /// </summary>
        private string FormatJson(string json)
        {
            try
            {
                var obj = JsonConvert.DeserializeObject(json);
                return JsonConvert.SerializeObject(obj, Formatting.Indented);
            }
            catch
            {
                return json;
            }
        }
        
        /// <summary>
        /// 保存匹配的歌曲信息到当前音乐对象
        /// </summary>
        private void SaveMatchedSongInfo(NeteaseSong matchedSong, string jsonResponse)
        {
            try
            {
                if (currentMusic != null && matchedSong != null)
                {
                    // 清除旧的匹配信息
                    if (currentMusic.MatchedSong != null)
                    {
                        LogMessage($"🔄 更新匹配信息: {currentMusic.MatchedSong.Name} → {matchedSong.Name}");
                    }
                    else
                    {
                        LogMessage($"💾 新增匹配信息: {matchedSong.Name}");
                    }
                    
                    currentMusic.MatchedSong = matchedSong;
                    currentMusic.SearchResponseJson = jsonResponse;
                    
                    LogMessage($"✅ 匹配歌曲信息已保存: {matchedSong.Name} (ID: {matchedSong.Id})");
                }
            }
            catch (Exception ex)
            {
                LogMessage($"❌ 保存匹配歌曲信息失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 检查当前音乐是否已有匹配信息
        /// </summary>
        private bool HasMatchedSongInfo()
        {
            return currentMusic?.MatchedSong != null && 
                   !string.IsNullOrEmpty(currentMusic.SearchResponseJson);
        }
        
        /// <summary>
        /// 检查当前UI显示状态
        /// </summary>
        private string GetCurrentDisplayState()
        {
            try
            {
                return Dispatcher.Invoke(() =>
                {
                    if (MatchedSongTitle.Text.StartsWith("🎵"))
                    {
                        return "matched"; // 显示匹配信息
                    }
                    else if (MatchedSongTitle.Text == "⏳ 等待搜索...")
                    {
                        return "waiting_for_search"; // 等待搜索
                    }
                    else if (MatchedSongTitle.Text == "未找到匹配歌曲")
                    {
                        return "not_found"; // 未找到
                    }
                    else
                    {
                        return "unknown"; // 未知状态
                    }
                });
            }
            catch
            {
                return "unknown";
            }
        }
        
        private async void PlayPauseButton_Click(object sender, RoutedEventArgs e)
        {
            if (isListening)
            {
                await SendControlCommand(85); // 播放/暂停
            }
        }
        
        private async void PreviousButton_Click(object sender, RoutedEventArgs e)
        {
            if (isListening)
            {
                await SendControlCommand(88); // 上一首
            }
        }
        
        private async void NextButton_Click(object sender, RoutedEventArgs e)
        {
            if (isListening)
            {
                await SendControlCommand(87); // 下一首
            }
        }
        
        private async void TestSearchButton_Click(object sender, RoutedEventArgs e)
        {
            await TestManualSearch();
        }
        
        private void ClearLogButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 清空日志文本框
                LogTextBox.Text = "";
                
                // 添加清理记录
                string timestamp = DateTime.Now.ToString("HH:mm:ss");
                LogTextBox.Text = $"[{timestamp}] 🧹 日志已手动清空" + Environment.NewLine;
                
                // 更新日志状态
                UpdateLogStatus();
                
                LogMessage("✅ 日志已手动清空");
            }
            catch (Exception ex)
            {
                LogMessage($"❌ 清理日志失败: {ex.Message}");
            }
        }
        
        private void LogInfoButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 显示日志统计信息
                var lines = LogTextBox.Text.Split('\n');
                int totalLines = lines.Length;
                int nonEmptyLines = lines.Count(line => !string.IsNullOrWhiteSpace(line));
                
                string info = $"📊 日志统计信息:\n" +
                             $"   当前行数: {totalLines}\n" +
                             $"   非空行数: {nonEmptyLines}\n" +
                             $"   状态: {(totalLines > MAX_LOG_LINES ? "超限" : totalLines > LOG_CLEANUP_THRESHOLD ? "接近限制" : "正常")}";
                
                MessageBox.Show(info, "日志信息", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                LogMessage($"❌ 获取日志信息失败: {ex.Message}");
            }
        }
        
        private async Task TestManualSearch()
        {
            try
            {
                LogMessage("🧪 开始手动测试搜索功能...");
                
                // 创建一个测试用的音乐信息
                var testMusicInfo = new MusicInfo
                {
                    Title = "测试歌曲",
                    Artist = "测试艺术家",
                    Album = "测试专辑"
                };
                
                LogMessage($"🧪 测试音乐信息: {testMusicInfo.Title} - {testMusicInfo.Artist}");
                
                // 手动测试时，清除旧的匹配信息并显示等待搜索状态
                if (currentMusic != null)
                {
                    currentMusic.MatchedSong = null;
                    currentMusic.SearchResponseJson = null;
                    LogMessage("🧪 测试模式：清除旧匹配信息");
                }
                
                // 强制更新搜索状态
                lastSearchedTitle = null;
                
                // 显示等待搜索状态
                ShowWaitingForSearchStatus();
                
                // 执行搜索
                await SearchNeteaseMusic(testMusicInfo);
                
                // 验证匹配信息是否保存
                if (HasMatchedSongInfo())
                {
                    LogMessage("✅ 测试完成，匹配信息已保存");
                }
                else
                {
                    LogMessage("⚠️ 测试完成，但匹配信息未保存");
                }
                
                LogMessage("🧪 手动测试搜索完成");
            }
            catch (Exception ex)
            {
                LogMessage($"❌ 手动测试搜索失败: {ex.Message}");
            }
        }
        
        private async Task SendControlCommand(int keyCode)
        {
            // 检查ADB路径是否已设置
            if (string.IsNullOrEmpty(adbPath))
            {
                LogMessage("❌ 无法发送控制命令：ADB工具路径未设置");
                return;
            }
            
            try
            {
                // 通过ADB发送按键事件到安卓端
                string command = $"shell input keyevent {keyCode}";
                await ExecuteAdbCommand(command);
                
                string action;
                switch (keyCode)
                {
                    case 85:
                        action = "播放/暂停";
                        break;
                    case 87:
                        action = "下一首";
                        break;
                    case 88:
                        action = "上一首";
                        break;
                    default:
                        action = "未知命令";
                        break;
                }
                
                LogMessage($"🎮 发送音乐控制命令: {action}");
            }
            catch (Exception ex)
            {
                LogMessage($"❌ 发送控制命令失败: {ex.Message}");
            }
        }
        
        protected override void OnClosed(EventArgs e)
        {
            StopListening();
            CleanupTempFiles();
            CleanupHttpClient();
            base.OnClosed(e);
        }
        
        private void CleanupTempFiles()
        {
            try
            {
                // 清理临时ADB工具文件
                string tempDir = Path.Combine(Path.GetTempPath(), "LyricSync_ADB");
                if (Directory.Exists(tempDir))
                {
                    // 停止所有ADB进程
                    if (adbProcess != null && !adbProcess.HasExited)
                    {
                        try
                        {
                            adbProcess.Kill();
                            adbProcess.Dispose();
                        }
                        catch { }
                    }
                    
                    // 等待一下让进程完全退出
                    System.Threading.Thread.Sleep(1000);
                    
                    // 删除临时文件
                    try
                    {
                        Directory.Delete(tempDir, true);
                        LogMessage("🧹 临时ADB工具文件已清理");
                    }
                    catch (Exception ex)
                    {
                        LogMessage($"⚠️ 清理临时文件时出错: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                LogMessage($"⚠️ 清理临时文件时出错: {ex.Message}");
            }
        }
        
        private void CleanupHttpClient()
        {
            try
            {
                if (httpClient != null)
                {
                    httpClient.Dispose();
                    httpClient = null;
                    LogMessage("🧹 HTTP客户端已清理");
                }
            }
            catch (Exception ex)
            {
                LogMessage($"⚠️ 清理HTTP客户端时出错: {ex.Message}");
            }
        }
    }
    
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
    }
    
    public class NeteaseArtist
    {
        [JsonProperty("id")]
        public long Id { get; set; }
        
        [JsonProperty("name")]
        public string Name { get; set; }
    }
    
    public class NeteaseAlbum
    {
        [JsonProperty("id")]
        public long Id { get; set; }
        
        [JsonProperty("name")]
        public string Name { get; set; }
        
        [JsonProperty("artist")]
        public NeteaseArtist Artist { get; set; }
    }
}
