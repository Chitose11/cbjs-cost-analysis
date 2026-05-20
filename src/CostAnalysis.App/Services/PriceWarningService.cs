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

            var history = new CostAnalysisRepository().SearchCostHistory(materialCode, materialName, 80);
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

            return messages.Count == 0
                ? PriceWarningResult.Empty
                : new PriceWarningResult(severity, string.Join("；", messages.ToArray()));
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
