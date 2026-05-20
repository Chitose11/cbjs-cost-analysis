using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using CostAnalysis.App.Data;

namespace CostAnalysis.App.Services
{
    internal sealed class ExternalTextExtractionService
    {
        private readonly OcrToolSettings _settings;

        public ExternalTextExtractionService()
            : this(new OcrToolSettingsRepository().Get())
        {
        }

        public ExternalTextExtractionService(OcrToolSettings settings)
        {
            _settings = settings ?? OcrToolSettings.Defaults();
        }

        public List<string> ExtractPdfTextLines(string filePath)
        {
            var lines = TryReadSidecarText(filePath);
            if (lines.Count > 0)
            {
                return lines;
            }

            lines = FilterReadableLines(SplitLines(RunTool("pdftotext", "-layout -nopgbrk -enc UTF-8 " + Quote(filePath) + " -", 20000)));
            if (LooksUseful(lines))
            {
                return lines;
            }

            lines = FilterReadableLines(SplitLines(RunTool("pdftotext", "-raw -nopgbrk -enc UTF-8 " + Quote(filePath) + " -", 20000)));
            if (LooksUseful(lines))
            {
                return lines;
            }

            lines = TryOcrPdfPages(filePath);
            return LooksUseful(lines) ? lines : new List<string>();
        }

        public List<string> ExtractImageTextLines(string filePath)
        {
            var lines = TryReadSidecarText(filePath);
            if (lines.Count > 0)
            {
                return lines;
            }

            var language = string.IsNullOrWhiteSpace(_settings.TesseractLanguage) ? "chi_sim+eng" : _settings.TesseractLanguage.Trim();
            lines = FilterReadableLines(SplitLines(RunTool("tesseract", Quote(filePath) + " stdout -l " + language + " --psm 6", 30000)));
            if (LooksUseful(lines))
            {
                return lines;
            }

            return FilterReadableLines(SplitLines(RunTool("tesseract", Quote(filePath) + " stdout -l eng --psm 6", 30000)));
        }

        public string GetToolStatusText()
        {
            var pdftotext = ResolveToolPath("pdftotext");
            var pdftoppm = ResolveToolPath("pdftoppm");
            var tesseract = ResolveToolPath("tesseract");

            var sb = new StringBuilder();
            sb.AppendLine("pdftotext：" + (string.IsNullOrWhiteSpace(pdftotext) ? "未找到" : pdftotext));
            sb.AppendLine("pdftoppm：" + (string.IsNullOrWhiteSpace(pdftoppm) ? "未找到" : pdftoppm));
            sb.AppendLine("tesseract：" + (string.IsNullOrWhiteSpace(tesseract) ? "未找到" : tesseract));
            sb.AppendLine("OCR语言：" + (string.IsNullOrWhiteSpace(_settings.TesseractLanguage) ? "chi_sim+eng" : _settings.TesseractLanguage.Trim()));
            return sb.ToString().TrimEnd();
        }

        private List<string> TryOcrPdfPages(string filePath)
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), "cbjs_pdf_ocr_" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(tempRoot);
                var prefix = Path.Combine(tempRoot, "page");
                RunTool("pdftoppm", "-r 200 -png -f 1 -l 3 " + Quote(filePath) + " " + Quote(prefix), 60000);
                var images = Directory.GetFiles(tempRoot, "page-*.png").OrderBy(path => path).ToList();
                var lines = new List<string>();
                var language = string.IsNullOrWhiteSpace(_settings.TesseractLanguage) ? "chi_sim+eng" : _settings.TesseractLanguage.Trim();
                foreach (var image in images)
                {
                    var pageLines = FilterReadableLines(SplitLines(RunTool("tesseract", Quote(image) + " stdout -l " + language + " --psm 6", 45000)));
                    lines.AddRange(pageLines);
                    if (lines.Count >= 120)
                    {
                        break;
                    }
                }

                return lines;
            }
            finally
            {
                TryDeleteDirectory(tempRoot);
            }
        }

        private string RunTool(string toolName, string arguments, int timeoutMilliseconds)
        {
            var toolPath = ResolveToolPath(toolName);
            if (string.IsNullOrWhiteSpace(toolPath))
            {
                return string.Empty;
            }

            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = toolPath,
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };

                using (var process = Process.Start(startInfo))
                {
                    if (process == null)
                    {
                        return string.Empty;
                    }

                    var output = process.StandardOutput.ReadToEnd();
                    process.StandardError.ReadToEnd();
                    if (!process.WaitForExit(timeoutMilliseconds))
                    {
                        try
                        {
                            process.Kill();
                        }
                        catch
                        {
                            // Ignore cleanup errors.
                        }
                    }

                    return output;
                }
            }
            catch
            {
                return string.Empty;
            }
        }

        private string ResolveToolPath(string toolName)
        {
            var configured = GetConfiguredPath(toolName);
            if (File.Exists(configured))
            {
                return configured;
            }

            if ((toolName == "pdftotext" || toolName == "pdftoppm") && !string.IsNullOrWhiteSpace(_settings.PopplerDirectory))
            {
                var fromDirectory = FindExecutableInDirectory(_settings.PopplerDirectory, toolName);
                if (!string.IsNullOrWhiteSpace(fromDirectory))
                {
                    return fromDirectory;
                }
            }

            foreach (var candidateRoot in GetBundledToolRoots(toolName))
            {
                var bundled = FindExecutableInDirectory(candidateRoot, toolName);
                if (!string.IsNullOrWhiteSpace(bundled))
                {
                    return bundled;
                }
            }

            return FindOnPath(toolName);
        }

        private string GetConfiguredPath(string toolName)
        {
            if (toolName == "pdftotext")
            {
                return _settings.PdftotextPath;
            }

            if (toolName == "pdftoppm")
            {
                return _settings.PdftoppmPath;
            }

            if (toolName == "tesseract")
            {
                return _settings.TesseractPath;
            }

            return string.Empty;
        }

        private static IEnumerable<string> GetBundledToolRoots(string toolName)
        {
            var baseDirectory = Path.GetDirectoryName(typeof(ExternalTextExtractionService).Assembly.Location);
            if (string.IsNullOrWhiteSpace(baseDirectory))
            {
                baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            }

            if (toolName == "pdftotext" || toolName == "pdftoppm")
            {
                yield return Path.Combine(baseDirectory, "tools", "poppler", "bin");
                yield return Path.Combine(baseDirectory, "tools", "poppler");
            }
            else if (toolName == "tesseract")
            {
                yield return Path.Combine(baseDirectory, "tools", "tesseract");
                yield return Path.Combine(baseDirectory, "tools", "tesseract", "bin");
            }
        }

        private static string FindExecutableInDirectory(string directory, string toolName)
        {
            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            {
                return string.Empty;
            }

            var names = ExecutableNames(toolName);
            foreach (var name in names)
            {
                var direct = Path.Combine(directory, name);
                if (File.Exists(direct))
                {
                    return direct;
                }
            }

            string[] children;
            try
            {
                children = Directory.GetDirectories(directory);
            }
            catch
            {
                children = new string[0];
            }

            foreach (var child in children)
            {
                foreach (var name in names)
                {
                    var nested = Path.Combine(child, name);
                    if (File.Exists(nested))
                    {
                        return nested;
                    }
                }
            }

            return string.Empty;
        }

        private static string FindOnPath(string toolName)
        {
            var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            foreach (var directory in path.Split(Path.PathSeparator))
            {
                var found = FindExecutableInDirectory(directory, toolName);
                if (!string.IsNullOrWhiteSpace(found))
                {
                    return found;
                }
            }

            return string.Empty;
        }

        private static List<string> ExecutableNames(string toolName)
        {
            return new List<string>
            {
                toolName,
                toolName + ".exe"
            };
        }

        private static List<string> TryReadSidecarText(string filePath)
        {
            var sidecarPath = Path.ChangeExtension(filePath, ".txt");
            if (!File.Exists(sidecarPath))
            {
                return new List<string>();
            }

            return FilterReadableLines(SplitLines(ReadAllTextBestEffort(sidecarPath)));
        }

        private static string ReadAllTextBestEffort(string path)
        {
            try
            {
                return File.ReadAllText(path, Encoding.UTF8);
            }
            catch
            {
                return File.ReadAllText(path, Encoding.Default);
            }
        }

        private static List<string> SplitLines(string text)
        {
            var result = new List<string>();
            if (string.IsNullOrWhiteSpace(text))
            {
                return result;
            }

            var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            foreach (var line in lines)
            {
                var cleaned = (line ?? string.Empty).Trim();
                if (!string.IsNullOrWhiteSpace(cleaned))
                {
                    result.Add(cleaned);
                }
            }

            return result;
        }

        private static List<string> FilterReadableLines(List<string> lines)
        {
            var result = new List<string>();
            foreach (var line in lines ?? new List<string>())
            {
                var cleaned = NormalizeLine(line);
                if (cleaned.Length < 2 || LooksLikeNoise(cleaned))
                {
                    continue;
                }

                result.Add(cleaned);
                if (result.Count >= 160)
                {
                    break;
                }
            }

            return result;
        }

        private static string NormalizeLine(string line)
        {
            return (line ?? string.Empty)
                .Replace('\u00a0', ' ')
                .Replace("　", " ")
                .Trim();
        }

        private static bool LooksUseful(List<string> lines)
        {
            if (lines == null || lines.Count == 0)
            {
                return false;
            }

            var totalLength = lines.Sum(line => line.Length);
            if (totalLength < 12)
            {
                return false;
            }

            return lines.Any(ContainsBusinessSignal) || lines.Count >= 3;
        }

        private static bool ContainsBusinessSignal(string line)
        {
            var value = line ?? string.Empty;
            return value.IndexOf("报价", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   value.IndexOf("物料", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   value.IndexOf("规格", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   value.IndexOf("尺寸", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   value.IndexOf("单价", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   value.IndexOf("材质", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   value.IndexOf("quote", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   value.IndexOf("price", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   value.IndexOf("material", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool LooksLikeNoise(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return true;
            }

            var control = 0;
            var replacement = 0;
            var chinese = 0;
            var latin = 0;
            var digit = 0;
            var punctuation = 0;
            foreach (var ch in value)
            {
                if (char.IsControl(ch))
                {
                    control++;
                }
                else if (ch == '\ufffd')
                {
                    replacement++;
                }
                else if (ch >= 0x4e00 && ch <= 0x9fff)
                {
                    chinese++;
                }
                else if (char.IsLetter(ch))
                {
                    latin++;
                }
                else if (char.IsDigit(ch))
                {
                    digit++;
                }
                else if (char.IsPunctuation(ch) || char.IsSymbol(ch) || char.IsSeparator(ch))
                {
                    punctuation++;
                }
            }

            if (control > 0 || replacement > 0)
            {
                return true;
            }

            if (value.StartsWith("D:", StringComparison.OrdinalIgnoreCase) && value.Length >= 10)
            {
                return true;
            }

            var lower = value.ToLowerInvariant();
            if (lower.Contains("konica") ||
                lower.Contains("bizhub") ||
                lower.Contains("adobe") ||
                lower.Contains("acrobat") ||
                lower.Contains("creator") ||
                lower.Contains("producer"))
            {
                return true;
            }

            var readable = chinese + latin + digit;
            if (readable == 0)
            {
                return true;
            }

            return punctuation > readable * 3 && !ContainsBusinessSignal(value);
        }

        private static string Quote(string value)
        {
            return "\"" + (value ?? string.Empty).Replace("\"", "\\\"") + "\"";
        }

        private static void TryDeleteDirectory(string path)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, true);
                }
            }
            catch
            {
                // Ignore temporary OCR cleanup errors.
            }
        }
    }
}
