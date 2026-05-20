using System;
using System.Collections.Generic;
using System.Linq;
using CostAnalysis.App.Data;

namespace CostAnalysis.App.Services
{
    internal sealed class PriceWarningService
    {
        private const decimal YellowIncreaseRate = 0.0001m;
        private const decimal RedIncreaseRate = 0.10m;
        private const decimal YellowLowerSupplierRate = 0.03m;
        private const decimal RedLowerSupplierRate = 0.10m;
        private const int RecentHistoryLimit = 300;

        public PriceWarningResult EvaluateQuoteItem(string supplier, QuoteImportItem item)
        {
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

            if (history.Count == 0)
            {
                return PriceWarningResult.Empty;
            }

            var messages = new List<string>();
            var severity = PriceWarningSeverity.None;
            var sameSupplier = FindLatestSameSupplier(history, supplier);
            if (sameSupplier != null && sameSupplier.PurchaseUnitPrice.HasValue && sameSupplier.PurchaseUnitPrice.Value > 0)
            {
                var oldPrice = sameSupplier.PurchaseUnitPrice.Value;
                var increaseRate = (currentPrice.Value - oldPrice) / oldPrice;
                if (increaseRate >= YellowIncreaseRate)
                {
                    severity = Max(severity, increaseRate >= RedIncreaseRate ? PriceWarningSeverity.Red : PriceWarningSeverity.Yellow);
                    messages.Add("同供应商上浮 " + FormatPercent(increaseRate) + "，历史价 " + FormatPrice(oldPrice));
                }
            }

            var lowerSupplier = FindLowerOtherSupplier(history, supplier, currentPrice.Value);
            if (lowerSupplier != null && lowerSupplier.PurchaseUnitPrice.HasValue && lowerSupplier.PurchaseUnitPrice.Value > 0)
            {
                var lowerPrice = lowerSupplier.PurchaseUnitPrice.Value;
                var gapRate = (currentPrice.Value - lowerPrice) / lowerPrice;
                if (gapRate >= YellowLowerSupplierRate)
                {
                    severity = Max(severity, gapRate >= RedLowerSupplierRate ? PriceWarningSeverity.Red : PriceWarningSeverity.Yellow);
                    messages.Add("其他供应商更低：" + SafeSupplier(lowerSupplier.Supplier) + " " + FormatPrice(lowerPrice) + "，差 " + FormatPercent(gapRate));
                }
            }

            var similarLower = FindSimilarLowerHistory(similarHistory, supplier, currentPrice.Value);
            if (similarLower != null && similarLower.PurchaseUnitPrice.HasValue)
            {
                var gapRate = (currentPrice.Value - similarLower.PurchaseUnitPrice.Value) / similarLower.PurchaseUnitPrice.Value;
                if (gapRate >= YellowLowerSupplierRate)
                {
                    severity = Max(severity, gapRate >= RedLowerSupplierRate ? PriceWarningSeverity.Red : PriceWarningSeverity.Yellow);
                    messages.Add("同类型规格历史更低：" + SafeSupplier(similarLower.Supplier) + " " + FormatPrice(similarLower.PurchaseUnitPrice.Value) + "，差 " + FormatPercent(gapRate));
                }
            }

            return messages.Count == 0
                ? PriceWarningResult.Empty
                : new PriceWarningResult(severity, string.Join("；", messages.Distinct().ToArray()));
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
    }

    internal sealed class PriceWarningResult
    {
        public static readonly PriceWarningResult Empty = new PriceWarningResult(PriceWarningSeverity.None, string.Empty);

        public PriceWarningResult(PriceWarningSeverity severity, string message)
        {
            Severity = severity;
            Message = message;
        }

        public PriceWarningSeverity Severity { get; private set; }
        public string Message { get; private set; }
    }

    internal enum PriceWarningSeverity
    {
        None = 0,
        Yellow = 1,
        Red = 2
    }
}
