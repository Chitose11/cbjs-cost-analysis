using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace CostAnalysis.App.Services
{
    internal static class DocumentQuoteTextParser
    {
        private static readonly Regex MaterialCodeRegex =
            new Regex(@"(?<code>(?:\d+-)?\d{2}\.\d{2}\.[A-Za-z0-9xX]{4,}(?:[-.][A-Za-z0-9]+)*)", RegexOptions.Compiled);

        private static readonly Regex SizeRegex =
            new Regex(@"(?<size>\d+(?:\.\d+)?\s*[*xX×]\s*\d+(?:\.\d+)?(?:\s*[*xX×]\s*\d+(?:\.\d+)?)?\s*(?:mm|MM|毫米)?)", RegexOptions.Compiled);

        private static readonly Regex GramWeightRegex =
            new Regex(@"(?<weight>\d+(?:\.\d+)?\s*(?:g|G|克|#))", RegexOptions.Compiled);

        private static readonly Regex PriceTierRegex =
            new Regex(@"(?<label>\d{2,6}\s*(?:-|~|—|至)\s*\d{2,6}|\d{2,6}\s*\+)\D{0,12}(?<price>\d+(?:\.\d+)?)", RegexOptions.Compiled);

        public static List<QuoteImportItem> Parse(List<string> lines)
        {
            var result = new List<QuoteImportItem>();
            if (lines == null || lines.Count == 0)
            {
                return result;
            }

            foreach (var line in MergeLikelyItemLines(lines))
            {
                var item = ParseLine(line);
                if (item != null)
                {
                    result.Add(item);
                }
            }

            return result;
        }

        private static QuoteImportItem ParseLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                return null;
            }

            var codeMatch = MaterialCodeRegex.Match(line);
            var tiers = ExtractPriceTiers(line);
            if (!LooksLikeBusinessLine(line) || (!codeMatch.Success && tiers.Count == 0))
            {
                return null;
            }

            var code = codeMatch.Success ? codeMatch.Groups["code"].Value.Trim() : string.Empty;
            var name = ExtractName(line, code, tiers.Count > 0 ? tiers[0].Label : string.Empty);
            var process = ExtractProcess(line);
            var size = ExtractFirst(SizeRegex, line, "size");
            var gramWeight = JoinUnique(GramWeightRegex, line, "weight");

            return new QuoteImportItem
            {
                RawName = line,
                MaterialCode = code,
                MaterialName = name,
                FinishedSize = size,
                MaterialProcess = process,
                MaterialNameExtracted = ExtractMaterialName(process),
                GramWeight = gramWeight,
                PriceTiers = tiers
            };
        }

        private static List<string> MergeLikelyItemLines(List<string> lines)
        {
            var merged = new List<string>();
            var current = string.Empty;
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (MaterialCodeRegex.IsMatch(trimmed) || string.IsNullOrWhiteSpace(current))
                {
                    if (!string.IsNullOrWhiteSpace(current))
                    {
                        merged.Add(current);
                    }

                    current = trimmed;
                }
                else if (LooksLikeContinuation(trimmed))
                {
                    current += " " + trimmed;
                }
            }

            if (!string.IsNullOrWhiteSpace(current))
            {
                merged.Add(current);
            }

            return merged;
        }

        private static bool LooksLikeContinuation(string line)
        {
            return SizeRegex.IsMatch(line) ||
                   GramWeightRegex.IsMatch(line) ||
                   PriceTierRegex.IsMatch(line) ||
                   ContainsAny(line, "材质", "工艺", "尺寸", "规格", "单价", "报价", "用量");
        }

        private static bool LooksLikeBusinessLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                return false;
            }

            if (line.StartsWith("D:", StringComparison.OrdinalIgnoreCase) ||
                line.IndexOf("KONICA MINOLTA", StringComparison.OrdinalIgnoreCase) >= 0 ||
                line.IndexOf("bizhub", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return false;
            }

            return ContainsAny(line, "材质", "工艺", "尺寸", "规格", "单价", "报价", "用量", "物料", "纸", "盒", "箱", "卡", "胶", "膜") ||
                   MaterialCodeRegex.IsMatch(line);
        }

        private static string ExtractName(string line, string code, string firstTierLabel)
        {
            var value = line;
            if (!string.IsNullOrWhiteSpace(code))
            {
                value = value.Replace(code, " ");
            }

            if (!string.IsNullOrWhiteSpace(firstTierLabel))
            {
                var index = value.IndexOf(firstTierLabel, StringComparison.OrdinalIgnoreCase);
                if (index > 0)
                {
                    value = value.Substring(0, index);
                }
            }

            value = Regex.Replace(value, @"尺寸[:：]?\s*\d+(?:\.\d+)?\s*[*xX×]\s*\d+(?:\.\d+)?(?:\s*[*xX×]\s*\d+(?:\.\d+)?)?\s*(?:mm|MM|毫米)?", " ");
            value = Regex.Replace(value, @"材质[/／]工艺[:：]?", " ");
            value = Regex.Replace(value, @"\s+", " ").Trim(' ', '-', '_', '/', '\\', '，', ',', '；', ';', '：', ':');
            return value.Length > 80 ? value.Substring(0, 80) : value;
        }

        private static string ExtractProcess(string line)
        {
            var value = line;
            var processIndex = IndexOfAny(value, "材质/工艺", "材质", "工艺", "规格描述");
            if (processIndex >= 0)
            {
                value = value.Substring(processIndex);
            }

            return value.Trim();
        }

        private static List<PriceTier> ExtractPriceTiers(string line)
        {
            var tiers = new List<PriceTier>();
            foreach (Match match in PriceTierRegex.Matches(line ?? string.Empty))
            {
                decimal price;
                if (!decimal.TryParse(match.Groups["price"].Value, out price))
                {
                    continue;
                }

                var label = match.Groups["label"].Value.Replace(" ", string.Empty);
                int? min;
                int? max;
                ParseTierLabel(label, out min, out max);
                tiers.Add(new PriceTier
                {
                    Label = label,
                    MinQuantity = min,
                    MaxQuantity = max,
                    UnitPrice = price
                });
            }

            return tiers;
        }

        private static void ParseTierLabel(string label, out int? min, out int? max)
        {
            min = null;
            max = null;
            var normalized = (label ?? string.Empty).Replace("—", "-").Replace("~", "-").Replace("至", "-");
            if (normalized.EndsWith("+", StringComparison.OrdinalIgnoreCase))
            {
                int parsed;
                if (int.TryParse(normalized.TrimEnd('+'), out parsed))
                {
                    min = parsed;
                }
                return;
            }

            var parts = normalized.Split('-');
            int number;
            if (parts.Length > 0 && int.TryParse(parts[0], out number))
            {
                min = number;
            }

            if (parts.Length > 1 && int.TryParse(parts[1], out number))
            {
                max = number;
            }
        }

        private static string ExtractMaterialName(string process)
        {
            var matches = new List<string>();
            foreach (Match match in Regex.Matches(process ?? string.Empty, @"[\u4e00-\u9fa5A-Za-z0-9.]+(?:纸|纸板|双铜|双胶|白卡|黑卡|牛卡|灰板|双灰|PET|PVC|胶|膜|磁铁|海绵|EVA)[\u4e00-\u9fa5A-Za-z0-9.]*"))
            {
                var value = match.Value.Trim();
                if (!string.IsNullOrWhiteSpace(value) && !matches.Contains(value))
                {
                    matches.Add(value);
                }
            }

            return string.Join("；", matches.ToArray());
        }

        private static string ExtractFirst(Regex regex, string text, string groupName)
        {
            var match = regex.Match(text ?? string.Empty);
            return match.Success ? match.Groups[groupName].Value.Trim() : string.Empty;
        }

        private static string JoinUnique(Regex regex, string text, string groupName)
        {
            var values = new List<string>();
            foreach (Match match in regex.Matches(text ?? string.Empty))
            {
                var value = match.Groups[groupName].Value.Trim();
                if (!values.Contains(value))
                {
                    values.Add(value);
                }
            }

            return string.Join("；", values.ToArray());
        }

        private static int IndexOfAny(string value, params string[] needles)
        {
            var best = -1;
            foreach (var needle in needles)
            {
                var index = (value ?? string.Empty).IndexOf(needle, StringComparison.OrdinalIgnoreCase);
                if (index >= 0 && (best < 0 || index < best))
                {
                    best = index;
                }
            }

            return best;
        }

        private static bool ContainsAny(string value, params string[] needles)
        {
            return IndexOfAny(value, needles) >= 0;
        }
    }
}
