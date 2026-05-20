using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace CostAnalysis.App.Services
{
    internal sealed class ExternalTextExtractionService
    {
        public List<string> ExtractPdfTextLines(string filePath)
        {
            var lines = TryReadSidecarText(filePath);
            if (lines.Count > 0)
            {
                return lines;
            }

            var output = RunTool("pdftotext", "-layout -enc UTF-8 " + Quote(filePath) + " -", 20000);
            return SplitLines(output);
        }

        public List<string> ExtractImageTextLines(string filePath)
        {
            var lines = TryReadSidecarText(filePath);
            if (lines.Count > 0)
            {
                return lines;
            }

            var output = RunTool("tesseract", Quote(filePath) + " stdout -l chi_sim+eng --psm 6", 30000);
            return SplitLines(output);
        }

        private static List<string> TryReadSidecarText(string filePath)
        {
            var sidecarPath = Path.ChangeExtension(filePath, ".txt");
            if (!File.Exists(sidecarPath))
            {
                return new List<string>();
            }

            return SplitLines(File.ReadAllText(sidecarPath, Encoding.UTF8));
        }

        private static string RunTool(string toolName, string arguments, int timeoutMilliseconds)
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = toolName,
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

        private static string Quote(string value)
        {
            return "\"" + (value ?? string.Empty).Replace("\"", "\\\"") + "\"";
        }
    }
}
