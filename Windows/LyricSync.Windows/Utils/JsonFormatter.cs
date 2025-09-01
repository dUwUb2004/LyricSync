using Newtonsoft.Json;

namespace LyricSync.Windows.Utils
{
    public static class JsonFormatter
    {
        /// <summary>
        /// 格式化JSON字符串，使其更易读
        /// </summary>
        public static string FormatJson(string json)
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
    }
}
