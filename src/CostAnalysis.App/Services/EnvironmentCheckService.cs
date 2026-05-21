using System;
using System.Collections.Generic;
using Microsoft.Win32;

namespace CostAnalysis.App.Services
{
    internal sealed class EnvironmentCheckService
    {
        public EnvironmentCheckReport Check()
        {
            var items = new List<EnvironmentCheckItem>();

            var os = Environment.OSVersion;
            var isWin7 = os.Platform == PlatformID.Win32NT && os.Version.Major == 6 && os.Version.Minor == 1;
            var isWin7OrNewer = os.Platform == PlatformID.Win32NT &&
                                (os.Version.Major > 6 || (os.Version.Major == 6 && os.Version.Minor >= 1));

            items.Add(new EnvironmentCheckItem
            {
                Name = "操作系统",
                Status = isWin7OrNewer ? EnvironmentCheckStatus.Pass : EnvironmentCheckStatus.Fail,
                Value = os.VersionString,
                Message = isWin7OrNewer ? "系统版本满足最低要求。" : "需要 Windows 7 SP1 或更高版本。"
            });

            var servicePack = os.ServicePack ?? string.Empty;
            items.Add(new EnvironmentCheckItem
            {
                Name = "Windows 7 SP1",
                Status = !isWin7 || servicePack.IndexOf("Service Pack 1", StringComparison.OrdinalIgnoreCase) >= 0
                    ? EnvironmentCheckStatus.Pass
                    : EnvironmentCheckStatus.Fail,
                Value = string.IsNullOrWhiteSpace(servicePack) ? "未检测到 Service Pack" : servicePack,
                Message = isWin7 ? "Windows 7 必须安装 SP1 才能稳定运行 .NET Framework 4.8。" : "当前不是 Windows 7，跳过 SP1 专项检查。"
            });

            var release = ReadNet48Release();
            items.Add(new EnvironmentCheckItem
            {
                Name = ".NET Framework 4.8",
                Status = release >= 528040 ? EnvironmentCheckStatus.Pass : EnvironmentCheckStatus.Fail,
                Value = release > 0 ? "Release=" + release : "未检测到",
                Message = release >= 528040 ? ".NET Framework 版本满足 net48 运行要求。" : "需要安装 .NET Framework 4.8。Windows 7 最高支持到 4.8，不支持 4.8.1。"
            });

            items.Add(new EnvironmentCheckItem
            {
                Name = "系统位数",
                Status = Environment.Is64BitOperatingSystem ? EnvironmentCheckStatus.Pass : EnvironmentCheckStatus.Warning,
                Value = Environment.Is64BitOperatingSystem ? "64 位" : "32 位",
                Message = Environment.Is64BitOperatingSystem ? "内置 OCR 工具为 64 位，可正常使用。" : "主程序可运行，但内置 Poppler/Tesseract 为 64 位，32 位系统无法使用 OCR。"
            });

            items.Add(new EnvironmentCheckItem
            {
                Name = "程序位数",
                Status = Environment.Is64BitProcess ? EnvironmentCheckStatus.Pass : EnvironmentCheckStatus.Warning,
                Value = Environment.Is64BitProcess ? "64 位进程" : "32 位进程",
                Message = Environment.Is64BitProcess ? "当前进程为 64 位。" : "当前进程为 32 位；如需 OCR，建议在 64 位 Windows 上运行。"
            });

            AddOcrItems(items);

            return new EnvironmentCheckReport(items);
        }

        private static void AddOcrItems(List<EnvironmentCheckItem> items)
        {
            var status = new ExternalTextExtractionService().GetToolStatusText();
            AddToolItem(items, status, "pdftotext");
            AddToolItem(items, status, "pdftoppm");
            AddToolItem(items, status, "tesseract");
        }

        private static void AddToolItem(List<EnvironmentCheckItem> items, string status, string toolName)
        {
            var value = FindStatusValue(status, toolName);
            var found = !string.IsNullOrWhiteSpace(value) && value.IndexOf("未找到", StringComparison.OrdinalIgnoreCase) < 0;
            items.Add(new EnvironmentCheckItem
            {
                Name = "OCR 工具：" + toolName,
                Status = found ? EnvironmentCheckStatus.Pass : EnvironmentCheckStatus.Warning,
                Value = string.IsNullOrWhiteSpace(value) ? "未找到" : value,
                Message = found ? "已找到内置或配置的 OCR 工具。" : "未找到该工具；PDF 图片识别能力会受影响。"
            });
        }

        private static string FindStatusValue(string status, string toolName)
        {
            if (string.IsNullOrWhiteSpace(status))
            {
                return string.Empty;
            }

            var lines = status.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            foreach (var line in lines)
            {
                if (!line.StartsWith(toolName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var index = line.IndexOf('：');
                if (index < 0)
                {
                    index = line.IndexOf(':');
                }

                return index >= 0 ? line.Substring(index + 1).Trim() : line.Trim();
            }

            return string.Empty;
        }

        private static int ReadNet48Release()
        {
            try
            {
                using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full"))
                {
                    if (key == null)
                    {
                        return 0;
                    }

                    var value = key.GetValue("Release");
                    return value == null ? 0 : Convert.ToInt32(value);
                }
            }
            catch
            {
                return 0;
            }
        }
    }

    internal sealed class EnvironmentCheckReport
    {
        public EnvironmentCheckReport(List<EnvironmentCheckItem> items)
        {
            Items = items ?? new List<EnvironmentCheckItem>();
        }

        public List<EnvironmentCheckItem> Items { get; private set; }

        public bool HasFailure
        {
            get { return Items.Exists(item => item.Status == EnvironmentCheckStatus.Fail); }
        }

        public bool HasWarning
        {
            get { return Items.Exists(item => item.Status == EnvironmentCheckStatus.Warning); }
        }

        public string BuildSummary()
        {
            if (HasFailure)
            {
                return "当前运行环境存在必须处理的问题。";
            }

            if (HasWarning)
            {
                return "当前运行环境可启动，但部分功能可能受限。";
            }

            return "当前运行环境满足要求。";
        }
    }

    internal sealed class EnvironmentCheckItem
    {
        public string Name { get; set; }
        public EnvironmentCheckStatus Status { get; set; }
        public string Value { get; set; }
        public string Message { get; set; }
    }

    internal enum EnvironmentCheckStatus
    {
        Pass,
        Warning,
        Fail
    }
}
