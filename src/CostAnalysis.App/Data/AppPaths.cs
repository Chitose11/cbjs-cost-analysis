using System;
using System.IO;

namespace CostAnalysis.App.Data
{
    internal static class AppPaths
    {
        public static string DataDirectory
        {
            get
            {
                var appDirectory = Path.GetDirectoryName(typeof(AppPaths).Assembly.Location);
                if (string.IsNullOrWhiteSpace(appDirectory))
                {
                    appDirectory = AppDomain.CurrentDomain.BaseDirectory;
                }

                var path = Path.Combine(appDirectory, "data");
                Directory.CreateDirectory(path);
                return path;
            }
        }

        public static string DatabasePath
        {
            get { return Path.Combine(DataDirectory, "cost-analysis.db"); }
        }
    }
}
