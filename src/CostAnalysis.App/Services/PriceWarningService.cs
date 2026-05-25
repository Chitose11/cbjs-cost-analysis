using System;
using System.Collections.Generic;
using System.Linq;
using CostAnalysis.App.Data;

namespace CostAnalysis.App.Services
{
    internal sealed class PriceWarningService
    {
        private const int RecentHistoryLimit = 300;

        public PriceWarningResult EvaluateQuoteItem(string supplier, QuoteImportItem item)
        {
            var settings = new PriceWarningSettingsRepository().Get();
            var currentPrice = GetCurrentUnitPrice(item);
            if (!currentPrice.HasValue || currentPrice.Value <= 0)
            {
                return PriceWarningResult.Empty;
            }

            var materialCode = item == null ? string.Empty : item.MaterialCode;
            var materialName = item == null ? string.Empty : item.MaterialName;
            if (string.IsNullOrWhiteSpace(materialName) && item != null)
            {
                materialName = item.RawName;
            }

            var repository = new CostAnalysisRepository();
            var history = repository.SearchCostHistory(materialCode, materialName, 80);
            var similarHistory = FindSimilarHistory(repository.GetRecentCostHistory(RecentHistoryLimit), item);
            foreach (var similar in similarHistory)
            {
                if (!history.Any(existing => existing.AnalysisId == similar.AnalysisId && existing.No == similar.No))
                {
                    history.Add(similar);
                }
            }

            history = FilterByHistoryMonths(history, settings.HistoryMonths);
            similarHistory = FilterByHistoryMonths(similarHistory, settings.HistoryMonths);

            if (history.Count == 0)
            {
                return PriceWarningResult.Empty;
            }

            var messages = new List<string>();
            var evidence = new List<string>();
            var severity = PriceWarningSeverity.None;
            var sameSupplier = FindLatestSameSupplier(history, supplier);
            if (sameSupplier != null && sameSupplier.PurchaseUnitPrice.HasValue && sameSupplier.PurchaseUnitPrice.Value > 0)
            {
                var oldPrice = sameSupplier.PurchaseUnitPrice.Value;
                var increaseRate = (currentPrice.Value - oldPrice) / oldPrice;
                if (increaseRate >= settings.SameSupplierYellowRate)
                {
                    severity = Max(severity, increaseRate >= settings.SameSupplierRedRate ? PriceWarningSeverity.Red : PriceWarningSeverity.Yellow);
                    messages.Add("同供应商上浮 " + FormatPercent(increaseRate) + "，历史价 " + FormatPrice(oldPrice));
                    evidence.Add("同供应商历史：" + FormatHistoryEvidence(sameSupplier));
                }
            }

            var lowerSupplier = FindLowerOtherSupplier(history, supplier, currentPrice.Value);
            if (lowerSupplier != null && lowerSupplier.PurchaseUnitPrice.HasValue && lowerSupplier.PurchaseUnitPrice.Value > 0)
            {
                var lowerPrice = lowerSupplier.PurchaseUnitPrice.Value;
                var gapRate = (currentPrice.Value - lowerPrice) / lowerPrice;
                if (gapRate >= settings.LowerSupplierYellowRate)
                {
                    severity = Max(severity, gapRate >= settings.LowerSupplierRedRate ? PriceWarningSeverity.Red : PriceWarningSeverity.Yellow);
                    messages.Add("其他供应商更低：" + SafeSupplier(lowerSupplier.Supplier) + " " + FormatPrice(lowerPrice) + "，差 " + FormatPercent(gapRate));
                    evidence.Add("更低供应商历史：" + FormatHistoryEvidence(lowerSupplier));
                }
            }

            var similarLower = FindSimilarLowerHistory(similarHistory, supplier, currentPrice.Value);
            if (similarLower != null && similarLower.PurchaseUnitPrice.HasValue)
            {
                var gapRate = (currentPrice.Value - similarLower.PurchaseUnitPrice.Value) / similarLower.PurchaseUnitPrice.Value;
                if (gapRate >= settings.LowerSupplierYellowRate)
                {
                    severity = Max(severity, gapRate >= settings.LowerSupplierRedRate ? PriceWarningSeverity.Red : PriceWarningSeverity.Yellow);
                    messages.Add("同类型规格历史更低：" + SafeSupplier(similarLower.Supplier) + " " + FormatPrice(similarLower.PurchaseUnitPrice.Value) + "，差 " + FormatPercent(gapRate));
                    evidence.Add("同类型规格历史：" + FormatHistoryEvidence(similarLower));
                }
            }

            return messages.Count == 0
                ? PriceWarningResult.Empty
                : new PriceWarningResult(
                    severity,
                    string.Join("；", messages.Distinct().ToArray()),
                    string.Join("\r\n", evidence.Distinct().ToArray()));
        }

        private static List<CostHistoryItem> FilterByHistoryMonths(List<CostHistoryItem> history, int historyMonths)
        {
            if (history == null)
            {
                return new List<CostHistoryItem>();
            }

            if (historyMonths <= 0)
            {
                return history;
            }

            var threshold = DateTime.Today.AddMonths(-historyMonths);
            return history
                .Where(item =>
                {
                    var date = ParseDate(item.AnalysisDate, item.CreatedAt);
                    return date == DateTime.MinValue || date >= threshold;
                })
                .ToList();
        }

        private static decimal? GetCurrentUnitPrice(QuoteImportItem item)
        {
            if (item == null || item.PriceTiers == null)
            {
                return null;
            }

            foreach (var tier in item.PriceTiers.OrderBy(t => t.MinQuantity.HasValue ? t.MinQuantity.Value : int.MaxValue))
            {
                if (tier.UnitPrice.HasValue)
                {
                    return tier.UnitPrice.Value;
                }
            }

            return null;
        }

        private static CostHistoryItem FindLatestSameSupplier(List<CostHistoryItem> history, string supplier)
        {
            var normalizedSupplier = Normalize(supplier);
            if (string.IsNullOrWhiteSpace(normalizedSupplier))
            {
                return null;
            }

            return history
                .Where(item => Normalize(item.Supplier) == normalizedSupplier && item.PurchaseUnitPrice.HasValue)
                .OrderByDescending(item => ParseDate(item.AnalysisDate, item.CreatedAt))
                .FirstOrDefault();
        }

        private static CostHistoryItem FindLowerOtherSupplier(List<CostHistoryItem> history, string supplier, decimal currentPrice)
        {
            var normalizedSupplier = Normalize(supplier);
            return history
                .Where(item => item.PurchaseUnitPrice.HasValue &&
                               item.PurchaseUnitPrice.Value > 0 &&
                               item.PurchaseUnitPrice.Value < currentPrice &&
                               Normalize(item.Supplier) != normalizedSupplier)
                .OrderBy(item => item.PurchaseUnitPrice.Value)
                .FirstOrDefault();
        }

        private static CostHistoryItem FindSimilarLowerHistory(List<CostHistoryItem> history, string supplier, decimal currentPrice)
        {
            var normalizedSupplier = Normalize(supplier);
            return (history ?? new List<CostHistoryItem>())
                .Where(item => item.PurchaseUnitPrice.HasValue &&
                               item.PurchaseUnitPrice.Value > 0 &&
                               item.PurchaseUnitPrice.Value < currentPrice &&
                               Normalize(item.Supplier) != normalizedSupplier)
                .OrderBy(item => item.PurchaseUnitPrice.Value)
                .FirstOrDefault();
        }

        private static List<CostHistoryItem> FindSimilarHistory(List<CostHistoryItem> history, QuoteImportItem item)
        {
            var result = new List<CostHistoryItem>();
            if (item == null || history == null || history.Count == 0)
            {
                return result;
            }

            foreach (var candidate in history)
            {
                if (IsSimilarItem(candidate, item))
                {
                    result.Add(candidate);
                }
            }

            return result.Take(80).ToList();
        }

        private static bool IsSimilarItem(CostHistoryItem history, QuoteImportItem item)
        {
            var score = 0;
            var currentName = NormalizeText(item.MaterialName + " " + item.RawName);
            var historyName = NormalizeText(history.MaterialName + " " + history.MaterialDescription);
            if (!string.IsNullOrWhiteSpace(currentName) &&
                !string.IsNullOrWhiteSpace(historyName) &&
                HasSharedKeyword(currentName, historyName))
            {
                score += 2;
            }

            var currentSize = NormalizeSize(item.FinishedSize);
            var historySize = NormalizeSize(history.ExpandedSize);
            if (!string.IsNullOrWhiteSpace(currentSize) && currentSize == historySize)
            {
                score += 3;
            }

            if (SameNormalized(item.MaterialNameExtracted, history.BaseMaterialName))
            {
                score += 2;
            }

            if (SameNormalized(item.GramWeight, history.GramWeight))
            {
                score += 1;
            }

            return score >= 3;
        }

        private static bool HasSharedKeyword(string left, string right)
        {
            foreach (var token in SplitTokens(left))
            {
                if (token.Length >= 2 && right.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static IEnumerable<string> SplitTokens(string value)
        {
            return (value ?? string.Empty)
                .Split(new[] { ' ', '；', ';', ',', '，', '/', '\\', '-', '_', '(', ')', '（', '）' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(token => token.Trim())
                .Where(token => token.Length >= 2);
        }

        private static bool SameNormalized(string left, string right)
        {
            var normalizedLeft = NormalizeText(left);
            var normalizedRight = NormalizeText(right);
            return !string.IsNullOrWhiteSpace(normalizedLeft) && normalizedLeft == normalizedRight;
        }

        private static string NormalizeSize(string value)
        {
            return NormalizeText(value)
                .Replace("毫米", "mm")
                .Replace("ｍｍ", "mm")
                .Replace("×", "*")
                .Replace("x", "*");
        }

        private static string NormalizeText(string value)
        {
            return (value ?? string.Empty)
                .Replace(" ", string.Empty)
                .Replace("\t", string.Empty)
                .Trim()
                .ToLowerInvariant();
        }

        private static DateTime ParseDate(string analysisDate, string createdAt)
        {
            DateTime parsed;
            if (DateTime.TryParse(analysisDate, out parsed))
            {
                return parsed;
            }

            if (DateTime.TryParse(createdAt, out parsed))
            {
                return parsed;
            }

            return DateTime.MinValue;
        }

        private static PriceWarningSeverity Max(PriceWarningSeverity left, PriceWarningSeverity right)
        {
            return (PriceWarningSeverity)Math.Max((int)left, (int)right);
        }

        private static string Normalize(string value)
        {
            return (value ?? string.Empty).Replace(" ", string.Empty).Trim().ToLowerInvariant();
        }

        private static string SafeSupplier(string supplier)
        {
            return string.IsNullOrWhiteSpace(supplier) ? "未记录供应商" : supplier.Trim();
        }

        private static string FormatPrice(decimal price)
        {
            return price.ToString("0.####");
        }

        private static string FormatPercent(decimal rate)
        {
            return (rate * 100m).ToString("0.#") + "%";
        }

        private static string FormatHistoryEvidence(CostHistoryItem item)
        {
            if (item == null)
            {
                return string.Empty;
            }

            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(item.AnalysisNo))
            {
                parts.Add("单号 " + item.AnalysisNo.Trim());
            }

            var date = !string.IsNullOrWhiteSpace(item.AnalysisDate) ? item.AnalysisDate : item.CreatedAt;
            if (!string.IsNullOrWhiteSpace(date))
            {
                parts.Add("日期 " + date.Trim());
            }

            parts.Add("供应商 " + SafeSupplier(item.Supplier));

            if (item.PurchaseUnitPrice.HasValue)
            {
                parts.Add("采购价 " + FormatPrice(item.PurchaseUnitPrice.Value));
            }

            var material = string.IsNullOrWhiteSpace(item.MaterialCode)
                ? item.MaterialName
                : item.MaterialCode + " " + item.MaterialName;
            if (!string.IsNullOrWhiteSpace(material))
            {
                parts.Add("物料 " + material.Trim());
            }

            if (!string.IsNullOrWhiteSpace(item.ExpandedSize))
            {
                parts.Add("尺寸 " + item.ExpandedSize.Trim());
            }

            return string.Join("，", parts.ToArray());
        }
    }

    internal sealed class PriceWarningResult
    {
        public static readonly PriceWarningResult Empty = new PriceWarningResult(PriceWarningSeverity.None, string.Empty, string.Empty);

        public PriceWarningResult(PriceWarningSeverity severity, string message)
            : this(severity, message, string.Empty)
        {
        }

        public PriceWarningResult(PriceWarningSeverity severity, string message, string evidence)
        {
            Severity = severity;
            Message = message;
            Evidence = evidence;
        }

        public PriceWarningSeverity Severity { get; private set; }
        public string Message { get; private set; }
        public string Evidence { get; private set; }
    }

    internal enum PriceWarningSeverity
    {
        None = 0,
        Yellow = 1,
        Red = 2
    }
}
