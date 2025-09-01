using System;
using System.Linq;
using System.Windows;
using System.Windows.Threading;

namespace LyricSync.Windows.Utils
{
    public class Logger : ILogger
    {
        private readonly System.Windows.Controls.TextBox logTextBox;
        private readonly System.Windows.Controls.TextBlock logStatusText;
        private readonly Dispatcher dispatcher;
        
        // 日志管理相关字段 - 固定配置，无需用户自定义
        private const int MAX_LOG_LINES = 1000;        // 最大日志行数
        private const int LOG_CLEANUP_THRESHOLD = 800; // 清理阈值
        private const int LOG_CLEANUP_COUNT = 200;     // 每次清理的行数

        public Logger(System.Windows.Controls.TextBox logTextBox, System.Windows.Controls.TextBlock logStatusText, Dispatcher dispatcher)
        {
            this.logTextBox = logTextBox;
            this.logStatusText = logStatusText;
            this.dispatcher = dispatcher;
        }

        public void LogMessage(string message)
        {
            string timestamp = DateTime.Now.ToString("HH:mm:ss");
            string logEntry = $"[{timestamp}] {message}";
            
            dispatcher.Invoke(() =>
            {
                // 检查日志行数是否超过限制
                int currentLineCount = logTextBox.Text.Split('\n').Length;
                
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
                logTextBox.AppendText(logEntry + Environment.NewLine);
                logTextBox.ScrollToEnd();
                
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
                var lines = logTextBox.Text.Split('\n');
                if (lines.Length > LOG_CLEANUP_THRESHOLD)
                {
                    // 保留最新的日志，删除旧的
                    var newLines = lines.Skip(lines.Length - LOG_CLEANUP_THRESHOLD + LOG_CLEANUP_COUNT).ToArray();
                    logTextBox.Text = string.Join("\n", newLines);
                    
                    // 记录清理信息
                    logTextBox.AppendText($"[{DateTime.Now:HH:mm:ss}] 🧹 已清理 {LOG_CLEANUP_COUNT} 行旧日志，当前保留 {newLines.Length} 行" + Environment.NewLine);
                    logTextBox.ScrollToEnd();
                    
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
                var lines = logTextBox.Text.Split('\n');
                if (lines.Length > MAX_LOG_LINES)
                {
                    // 保留最新的日志，删除超出的部分
                    var newLines = lines.Skip(lines.Length - MAX_LOG_LINES + 100).ToArray();
                    logTextBox.Text = string.Join("\n", newLines);
                    
                    // 记录清理信息
                    logTextBox.AppendText($"[{DateTime.Now:HH:mm:ss}] 🧹 日志数量超限，已清理至 {newLines.Length} 行" + Environment.NewLine);
                    logTextBox.ScrollToEnd();
                    
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
                var lines = logTextBox.Text.Split('\n');
                int currentLines = lines.Length;
                
                if (currentLines > MAX_LOG_LINES)
                {
                    logStatusText.Text = $"日志状态: 超限 ({currentLines}/{MAX_LOG_LINES})";
                    logStatusText.Foreground = System.Windows.Media.Brushes.Red;
                }
                else if (currentLines > LOG_CLEANUP_THRESHOLD)
                {
                    logStatusText.Text = $"日志状态: 接近限制 ({currentLines}/{MAX_LOG_LINES})";
                    logStatusText.Foreground = System.Windows.Media.Brushes.Orange;
                }
                else
                {
                    logStatusText.Text = $"日志状态: 正常 ({currentLines}/{MAX_LOG_LINES})";
                    logStatusText.Foreground = System.Windows.Media.Brushes.Green;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"更新日志状态失败: {ex.Message}");
            }
        }

        public void ClearLogs()
        {
            try
            {
                // 清空日志文本框
                logTextBox.Text = "";
                
                // 添加清理记录
                string timestamp = DateTime.Now.ToString("HH:mm:ss");
                logTextBox.Text = $"[{timestamp}] 🧹 日志已手动清空" + Environment.NewLine;
                
                // 更新日志状态
                UpdateLogStatus();
                
                LogMessage("✅ 日志已手动清空");
            }
            catch (Exception ex)
            {
                LogMessage($"❌ 清理日志失败: {ex.Message}");
            }
        }

        public string GetLogInfo()
        {
            try
            {
                // 显示日志统计信息
                var lines = logTextBox.Text.Split('\n');
                int totalLines = lines.Length;
                int nonEmptyLines = lines.Count(line => !string.IsNullOrWhiteSpace(line));
                
                string info = $"📊 日志统计信息:\n" +
                             $"   当前行数: {totalLines}\n" +
                             $"   非空行数: {nonEmptyLines}\n" +
                             $"   状态: {(totalLines > MAX_LOG_LINES ? "超限" : totalLines > LOG_CLEANUP_THRESHOLD ? "接近限制" : "正常")}";
                
                return info;
            }
            catch (Exception ex)
            {
                return $"❌ 获取日志信息失败: {ex.Message}";
            }
        }
    }
}
