using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LyricSync.Windows.Models;
using LyricSync.Windows.Utils;

namespace LyricSync.Windows.Services
{
    public class UIService
    {
        private readonly ILogger logger;
        private readonly NeteaseMusicService neteaseService;
        
        // UI控件引用
        private readonly TextBlock songTitle;
        private readonly TextBlock artistName;
        private readonly TextBlock albumName;
        private readonly ProgressBar progressBar;
        private readonly TextBlock currentTime;
        private readonly TextBlock totalTime;
        private readonly TextBlock statusText;
        private readonly TextBlock statusDescription;
        private readonly TextBlock bottomStatusText;
        private readonly TextBlock matchedSongTitle;
        private readonly TextBlock matchedSongArtist;
        private readonly TextBlock matchedSongAlbum;
        private readonly TextBlock matchedSongDuration;
        private readonly TextBlock matchedSongId;
        private readonly TextBox jsonDisplayTextBox;
        private readonly Expander matchedSongExpander;
        private readonly Image albumCoverImage;
        private readonly MahApps.Metro.IconPacks.PackIconMaterial defaultMusicIcon;

        public UIService(
            ILogger logger,
            NeteaseMusicService neteaseService,
            TextBlock songTitle,
            TextBlock artistName,
            TextBlock albumName,
            ProgressBar progressBar,
            TextBlock currentTime,
            TextBlock totalTime,
            TextBlock statusText,
            TextBlock statusDescription,
            TextBlock bottomStatusText,
            TextBlock matchedSongTitle,
            TextBlock matchedSongArtist,
            TextBlock matchedSongAlbum,
            TextBlock matchedSongDuration,
            TextBlock matchedSongId,
            TextBox jsonDisplayTextBox,
            Expander matchedSongExpander,
            Image albumCoverImage,
            MahApps.Metro.IconPacks.PackIconMaterial defaultMusicIcon)
        {
            this.logger = logger;
            this.neteaseService = neteaseService;
            this.songTitle = songTitle;
            this.artistName = artistName;
            this.albumName = albumName;
            this.progressBar = progressBar;
            this.currentTime = currentTime;
            this.totalTime = totalTime;
            this.statusText = statusText;
            this.statusDescription = statusDescription;
            this.bottomStatusText = bottomStatusText;
            this.matchedSongTitle = matchedSongTitle;
            this.matchedSongArtist = matchedSongArtist;
            this.matchedSongAlbum = matchedSongAlbum;
            this.matchedSongDuration = matchedSongDuration;
            this.matchedSongId = matchedSongId;
            this.jsonDisplayTextBox = jsonDisplayTextBox;
            this.matchedSongExpander = matchedSongExpander;
            this.albumCoverImage = albumCoverImage;
            this.defaultMusicIcon = defaultMusicIcon;
        }

        /// <summary>
        /// 安全地更新UI控件，确保在主线程执行
        /// </summary>
        private void SafeUpdateUI(Action action)
        {
            if (songTitle.Dispatcher.CheckAccess())
            {
                action();
            }
            else
            {
                songTitle.Dispatcher.Invoke(action);
            }
        }

        public void UpdateMusicDisplay(MusicInfo music)
        {
            SafeUpdateUI(() =>
            {
                try
                {
                    songTitle.Text = music.Title ?? "未知歌曲";
                    artistName.Text = music.Artist ?? "未知艺术家";
                    albumName.Text = music.Album ?? "未知专辑";
                    
                    // 更新进度条
                    if (music.Position >= 0)
                    {
                        progressBar.Value = music.Position;
                    }
                    
                    // 更新当前时间显示
                    if (music.Position > 0)
                    {
                        currentTime.Text = TimeFormatter.FormatTime(music.Position);
                    }
                    
                    // 更新总时长（只在有有效时长时更新）
                    if (music.Duration > 0)
                    {
                        totalTime.Text = TimeFormatter.FormatTime(music.Duration);
                        
                        // 更新进度条最大值
                        progressBar.Maximum = music.Duration;
                    }
                }
                catch (Exception ex)
                {
                    logger.LogMessage($"❌ 更新音乐显示失败: {ex.Message}");
                }
            });
        }

        public void UpdateProgressBarValueOnly(MusicInfo music)
        {
            SafeUpdateUI(() =>
            {
                try
                {
                    // 只更新进度条的值和当前时间，不覆盖总时长
                    if (music.Position >= 0)
                    {
                        progressBar.Value = music.Position;
                    }
                    
                    if (music.Position > 0)
                    {
                        currentTime.Text = TimeFormatter.FormatTime(music.Position);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogMessage($"❌ 更新进度条失败: {ex.Message}");
                }
            });
        }

        public void ResetMusicDisplay()
        {
            SafeUpdateUI(() =>
            {
                try
                {
                    songTitle.Text = "未播放";
                    artistName.Text = "";
                    albumName.Text = "";
                    progressBar.Value = 0;
                    progressBar.Maximum = 100; // 重置进度条最大值
                    currentTime.Text = "0:00";
                    totalTime.Text = "0:00";
                    
                    // 清除匹配歌曲信息
                    ClearMatchedSongDisplay();
                }
                catch (Exception ex)
                {
                    logger.LogMessage($"❌ 重置音乐显示失败: {ex.Message}");
                }
            });
        }

        public void UpdateConnectionStatus(bool connected, string description = null)
        {
            SafeUpdateUI(() =>
            {
                try
                {
                    if (connected)
                    {
                        statusText.Text = "正在监听";
                        statusText.Foreground = Brushes.Green;
                        statusDescription.Text = description ?? "正在监听安卓端日志";
                    }
                    else
                    {
                        statusText.Text = "未监听";
                        statusText.Foreground = Brushes.Red;
                        statusDescription.Text = description ?? "请点击开始监听按钮";
                    }
                }
                catch (Exception ex)
                {
                    logger.LogMessage($"❌ 更新连接状态失败: {ex.Message}");
                }
            });
        }

        public void UpdateBottomStatus(string status)
        {
            SafeUpdateUI(() =>
            {
                try
                {
                    bottomStatusText.Text = status;
                }
                catch (Exception ex)
                {
                    logger.LogMessage($"❌ 更新底部状态失败: {ex.Message}");
                }
            });
        }

        /// <summary>
        /// 更新匹配歌曲信息显示
        /// </summary>
        public void UpdateMatchedSongDisplay(NeteaseSong matchedSong)
        {
            SafeUpdateUI(() =>
            {
                try
                {
                    if (matchedSong != null)
                    {
                        // 显示匹配的歌曲信息
                        matchedSongTitle.Text = $"🎵 {matchedSong.Name}";
                        matchedSongArtist.Text = $"👤 艺术家: {string.Join(", ", matchedSong.Artists?.Select(a => a.Name) ?? new List<string>())}";
                        matchedSongAlbum.Text = $"💿 专辑: {matchedSong.Album?.Name ?? "未知"}";
                        matchedSongDuration.Text = $"⏱️ 时长: {TimeFormatter.FormatTime(matchedSong.Duration)}";
                        matchedSongId.Text = $"🆔 歌曲ID: {matchedSong.Id}";
                        
                        // 更新专辑封面
                        UpdateAlbumCover(matchedSong);
                        
                        // 显示格式化的JSON数据
                        try
                        {
                            var formattedJson = JsonFormatter.FormatJson(matchedSong.ToString());
                            jsonDisplayTextBox.Text = formattedJson;
                        }
                        catch
                        {
                            jsonDisplayTextBox.Text = matchedSong.ToString();
                        }
                        
                        // 展开匹配信息区域
                        matchedSongExpander.IsExpanded = true;
                        
                        logger.LogMessage($"🎯 匹配歌曲信息显示完成: {matchedSong.Name}");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"更新匹配歌曲显示失败: {ex.Message}");
                    logger.LogMessage($"❌ 更新匹配歌曲显示失败: {ex.Message}");
                }
            });
        }

        /// <summary>
        /// 清除匹配歌曲信息显示
        /// </summary>
        public void ClearMatchedSongDisplay()
        {
            SafeUpdateUI(() =>
            {
                try
                {
                    matchedSongTitle.Text = "未找到匹配歌曲";
                    matchedSongArtist.Text = "";
                    matchedSongAlbum.Text = "";
                    matchedSongDuration.Text = "";
                    matchedSongId.Text = "";
                    jsonDisplayTextBox.Text = "";
                    
                    // 重置封面显示
                    SetDefaultCover();
                    
                    // 收起匹配信息区域
                    matchedSongExpander.IsExpanded = false;
                    
                    logger.LogMessage("🧹 已清除匹配歌曲显示");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"清除匹配歌曲显示失败: {ex.Message}");
                }
            });
        }

        /// <summary>
        /// 显示等待搜索状态
        /// </summary>
        public void ShowWaitingForSearchStatus()
        {
            SafeUpdateUI(() =>
            {
                try
                {
                    // 检查当前是否已经是等待搜索状态，避免重复设置
                    if (matchedSongTitle.Text == "⏳ 等待搜索...")
                    {
                        return; // 已经是等待搜索状态，不需要重复设置
                    }
                    
                    // 重要：如果当前显示的是匹配信息，绝对不要覆盖
                    if (matchedSongTitle.Text.StartsWith("🎵"))
                    {
                        logger.LogMessage($"🛡️ 状态保护：当前显示匹配信息，不覆盖为等待搜索状态");
                        return;
                    }
                    
                    matchedSongTitle.Text = "⏳ 等待搜索...";
                    matchedSongArtist.Text = "等待网易云音乐搜索完成";
                    matchedSongAlbum.Text = "";
                    matchedSongDuration.Text = "";
                    matchedSongId.Text = "";
                    jsonDisplayTextBox.Text = "搜索进行中，请稍候...";
                    
                    // 设置默认封面
                    SetDefaultCover();
                    
                    // 展开匹配信息区域，显示等待搜索状态
                    matchedSongExpander.IsExpanded = true;
                    
                    logger.LogMessage("⏳ 显示等待搜索状态");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"显示等待搜索状态失败: {ex.Message}");
                    logger.LogMessage($"❌ 显示等待搜索状态失败: {ex.Message}");
                }
            });
        }

        /// <summary>
        /// 更新专辑封面显示
        /// </summary>
        public async void UpdateAlbumCover(NeteaseSong matchedSong)
        {
            try
            {
                if (matchedSong == null)
                {
                    logger.LogMessage("⚠️ 歌曲信息为空，使用默认封面");
                    SetDefaultCover();
                    return;
                }
                
                // 尝试获取封面URL，按优先级排序
                string coverUrl = null;
                string coverSource = "";
                
                // 1. 优先使用新的封面获取API
                if (matchedSong.Id > 0)
                {
                    try
                    {
                        var newCoverUrl = await neteaseService.GetCoverUrlAsync(matchedSong.Id.ToString(), "song");
                        if (!string.IsNullOrEmpty(newCoverUrl))
                        {
                            coverUrl = newCoverUrl;
                            coverSource = "网易云API直链";
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogMessage($"⚠️ 获取网易云API封面失败: {ex.Message}");
                    }
                }
                
                // 2. 如果新API失败，使用原有的专辑封面
                if (string.IsNullOrEmpty(coverUrl) && matchedSong.Album != null)
                {
                    if (!string.IsNullOrEmpty(matchedSong.Album.PicUrl))
                    {
                        coverUrl = matchedSong.Album.PicUrl;
                        coverSource = "专辑封面 (picUrl)";
                    }
                    else if (!string.IsNullOrEmpty(matchedSong.Album.Cover))
                    {
                        coverUrl = matchedSong.Album.Cover;
                        coverSource = "专辑封面 (cover)";
                    }
                    else if (!string.IsNullOrEmpty(matchedSong.Album.Img1v1Url))
                    {
                        coverUrl = matchedSong.Album.Img1v1Url;
                        coverSource = "专辑封面 (img1v1Url)";
                    }
                }
                
                // 3. 如果没有专辑封面，尝试使用艺术家头像
                if (string.IsNullOrEmpty(coverUrl) && matchedSong.Artists != null && matchedSong.Artists.Count > 0)
                {
                    var firstArtist = matchedSong.Artists[0];
                    if (!string.IsNullOrEmpty(firstArtist.Img1v1Url))
                    {
                        coverUrl = firstArtist.Img1v1Url;
                        coverSource = $"艺术家头像 ({firstArtist.Name})";
                    }
                    else if (!string.IsNullOrEmpty(firstArtist.PicUrl))
                    {
                        coverUrl = firstArtist.PicUrl;
                        coverSource = $"艺术家头像 ({firstArtist.Name})";
                    }
                }
                
                if (!string.IsNullOrEmpty(coverUrl))
                {
                    logger.LogMessage($"🖼️ 找到封面: {coverSource} - {coverUrl}");
                    await LoadAlbumCover(coverUrl);
                }
                else
                {
                    logger.LogMessage("⚠️ 未找到任何封面URL，使用默认封面");
                    logger.LogMessage($"💡 调试信息 - 专辑: {matchedSong.Album?.Name ?? "null"}, 艺术家: {string.Join(", ", matchedSong.Artists?.Select(a => a.Name) ?? new List<string>())}");
                    SetDefaultCover();
                }
            }
            catch (Exception ex)
            {
                logger.LogMessage($"❌ 更新专辑封面失败: {ex.Message}");
                SetDefaultCover();
            }
        }

        /// <summary>
        /// 加载专辑封面
        /// </summary>
        private async Task LoadAlbumCover(string coverUrl)
        {
            try
            {
                logger.LogMessage($"🔄 正在加载封面: {coverUrl}");
                
                using (var httpClient = new System.Net.Http.HttpClient())
                {
                    var imageBytes = await httpClient.GetByteArrayAsync(coverUrl);
                    
                    SafeUpdateUI(() =>
                    {
                        try
                        {
                            // 创建BitmapImage
                            var bitmap = new BitmapImage();
                            bitmap.BeginInit();
                            bitmap.CacheOption = BitmapCacheOption.OnLoad;
                            bitmap.StreamSource = new System.IO.MemoryStream(imageBytes);
                            bitmap.EndInit();
                            
                            // 设置封面图片
                            albumCoverImage.Source = bitmap;
                            
                            // 隐藏默认音符图标
                            defaultMusicIcon.Visibility = Visibility.Collapsed;
                            
                            logger.LogMessage($"✅ 封面加载成功");
                        }
                        catch (Exception ex)
                        {
                            logger.LogMessage($"❌ 设置封面图片失败: {ex.Message}");
                            SetDefaultCover();
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                logger.LogMessage($"❌ 下载封面失败: {ex.Message}");
                SetDefaultCover();
            }
        }

        /// <summary>
        /// 设置默认封面
        /// </summary>
        private void SetDefaultCover()
        {
            SafeUpdateUI(() =>
            {
                try
                {
                    // 清除封面图片
                    albumCoverImage.Source = null;
                    
                    // 显示默认音符图标
                    defaultMusicIcon.Visibility = Visibility.Visible;
                    
                    logger.LogMessage("🎵 已设置默认音符图标");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"设置默认封面失败: {ex.Message}");
                }
            });
        }

        /// <summary>
        /// 显示保存文件对话框
        /// </summary>
        /// <param name="title">对话框标题</param>
        /// <param name="defaultFileName">默认文件名</param>
        /// <param name="filter">文件过滤器</param>
        /// <returns>选择的文件路径，如果取消则返回null</returns>
        public string ShowSaveFileDialog(string title, string defaultFileName, string filter)
        {
            try
            {
                var saveFileDialog = new Microsoft.Win32.SaveFileDialog
                {
                    Title = title,
                    FileName = defaultFileName,
                    Filter = filter,
                    DefaultExt = "lrc",
                    AddExtension = true
                };

                bool? result = saveFileDialog.ShowDialog();
                if (result == true)
                {
                    logger.LogMessage($"📁 用户选择保存文件: {saveFileDialog.FileName}");
                    return saveFileDialog.FileName;
                }
                else
                {
                    logger.LogMessage("⚠️ 用户取消了保存文件对话框");
                    return null;
                }
            }
            catch (Exception ex)
            {
                logger.LogMessage($"❌ 显示保存文件对话框失败: {ex.Message}");
                return null;
            }
        }
    }
}
