using System;
using System.IO;
using System.Text;

namespace CostAnalysis.App.Services
{
    internal static class ErrorLogService
    {
        public static string LogDirectory
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "CostAnalysis.App",
                    "logs");
            }
        }

        public static string LogFilePath
        {
            get { return Path.Combine(LogDirectory, "error.log"); }
        }

        public static void Write(string context, Exception exception)
        {
            try
            {
                Directory.CreateDirectory(LogDirectory);
                var builder = new StringBuilder();
                builder.AppendLine("==== " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " ====");
                builder.AppendLine(context ?? "未命名错误");
                builder.AppendLine(exception == null ? "无异常对象" : exception.ToString());
                builder.AppendLine();
                File.AppendAllText(LogFilePath, builder.ToString(), Encoding.UTF8);
            }
            catch
            {
                // Logging must never become a second crash source.
            }
        }
    }
}
