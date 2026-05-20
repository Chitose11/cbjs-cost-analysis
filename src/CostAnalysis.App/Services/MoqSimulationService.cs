using System;
using System.Collections.Generic;
using System.Linq;

namespace CostAnalysis.App.Services
{
    internal sealed class MoqSimulationService
    {
        public List<MoqSimulationResult> Simulate(List<MoqSimulationLine> lines)
        {
            var usableLines = (lines ?? new List<MoqSimulationLine>())
                .Where(line => line != null && line.CurrentQuantity.HasValue && line.CurrentQuantity.Value > 0)
                .ToList();

            var currentTotal = usableLines.Sum(CalculateCurrentTotal);
            var thresholds = usableLines
                .SelectMany(line => line.PriceTiers ?? new List<PriceTier>())
                .Where(tier => tier.MinQuantity.HasValue && tier.MinQuantity.Value > 0 && tier.UnitPrice.HasValue && tier.UnitPrice.Value > 0)
                .Select(tier => (decimal)tier.MinQuantity.Value)
                .Distinct()
                .OrderBy(value => value)
                .Take(80)
                .ToList();

            var results = new List<MoqSimulationResult>();
            foreach (var threshold in thresholds)
            {
                var result = BuildResult(usableLines, threshold, currentTotal);
                if (result == null)
                {
                    continue;
                }

                results.Add(result);
            }

            return results
                .OrderByDescending(result => result.SavingAmount)
                .ThenBy(result => result.TargetQuantity)
                .ToList();
        }

        private static MoqSimulationResult BuildResult(List<MoqSimulationLine> lines, decimal targetQuantity, decimal currentTotal)
        {
            var proposedTotal = 0M;
            var quantityIncrease = 0M;
            var affectedRows = 0;
            var cheaperRows = 0;
            var rowNotes = new List<string>();

            foreach (var line in lines)
            {
                var currentQuantity = line.CurrentQuantity.Value;
                var currentUnitPrice = GetCurrentUnitPrice(line);
                if (!currentUnitPrice.HasValue)
                {
                    continue;
                }

                var proposedQuantity = currentQuantity < targetQuantity ? targetQuantity : currentQuantity;
                var proposedUnitPrice = GetMatchedUnitPrice(line, proposedQuantity) ?? currentUnitPrice.Value;
                proposedTotal += proposedQuantity * proposedUnitPrice;

                var changedQuantity = proposedQuantity > currentQuantity;
                var cheaperPrice = proposedUnitPrice < currentUnitPrice.Value;
                if (changedQuantity || cheaperPrice)
                {
                    affectedRows++;
                    if (cheaperPrice)
                    {
                        cheaperRows++;
                    }

                    quantityIncrease += proposedQuantity - currentQuantity;
                    rowNotes.Add(BuildRowNote(line, currentQuantity, currentUnitPrice.Value, proposedQuantity, proposedUnitPrice));
                }
            }

            if (affectedRows == 0)
            {
                return null;
            }

            var saving = currentTotal - proposedTotal;
            return new MoqSimulationResult
            {
                TargetQuantity = targetQuantity,
                CurrentTotal = currentTotal,
                ProposedTotal = proposedTotal,
                SavingAmount = saving,
                QuantityIncrease = quantityIncrease,
                AffectedRowCount = affectedRows,
                CheaperRowCount = cheaperRows,
                Recommendation = BuildRecommendation(targetQuantity, saving, quantityIncrease, affectedRows, cheaperRows),
                Detail = string.Join("；", rowNotes.ToArray())
            };
        }

        private static decimal CalculateCurrentTotal(MoqSimulationLine line)
        {
            var unitPrice = GetCurrentUnitPrice(line);
            return unitPrice.HasValue ? unitPrice.Value * line.CurrentQuantity.GetValueOrDefault() : 0M;
        }

        private static decimal? GetCurrentUnitPrice(MoqSimulationLine line)
        {
            if (!line.CurrentQuantity.HasValue)
            {
                return line.CurrentUnitPrice;
            }

            return GetMatchedUnitPrice(line, line.CurrentQuantity.Value) ?? line.CurrentUnitPrice;
        }

        private static decimal? GetMatchedUnitPrice(MoqSimulationLine line, decimal quantity)
        {
            var tiers = line.PriceTiers ?? new List<PriceTier>();
            var matched = ExcelQuoteImportService.MatchTier(tiers, quantity);
            return matched != null && matched.UnitPrice.HasValue ? matched.UnitPrice : null;
        }

        private static string BuildRowNote(MoqSimulationLine line, decimal currentQuantity, decimal currentUnitPrice, decimal proposedQuantity, decimal proposedUnitPrice)
        {
            var name = string.IsNullOrWhiteSpace(line.MaterialName) ? "第" + line.No + "行" : line.MaterialName.Trim();
            return name + " " +
                   Format(currentQuantity) + "@" + Format(currentUnitPrice) + " -> " +
                   Format(proposedQuantity) + "@" + Format(proposedUnitPrice);
        }

        private static string BuildRecommendation(decimal targetQuantity, decimal saving, decimal quantityIncrease, int affectedRows, int cheaperRows)
        {
            if (saving > 0)
            {
                return "建议：把起步数量提高到 " + Format(targetQuantity) + "，影响 " + affectedRows + " 行，整单预计节省 " + Format(saving) + "。";
            }

            if (cheaperRows > 0)
            {
                return "观察：提高到 " + Format(targetQuantity) + " 后有 " + cheaperRows + " 行单价下降，但整单总成本增加 " + Format(Math.Abs(saving)) + "。";
            }

            return "不建议：提高到 " + Format(targetQuantity) + " 会增加总用量 " + Format(quantityIncrease) + "，整单成本增加 " + Format(Math.Abs(saving)) + "。";
        }

        private static string Format(decimal value)
        {
            return value.ToString("0.####");
        }
    }

    internal sealed class MoqSimulationLine
    {
        public int No { get; set; }
        public string MaterialCode { get; set; }
        public string MaterialName { get; set; }
        public decimal? CurrentQuantity { get; set; }
        public decimal? CurrentUnitPrice { get; set; }
        public List<PriceTier> PriceTiers { get; set; }
    }

    internal sealed class MoqSimulationResult
    {
        public decimal TargetQuantity { get; set; }
        public decimal CurrentTotal { get; set; }
        public decimal ProposedTotal { get; set; }
        public decimal SavingAmount { get; set; }
        public decimal QuantityIncrease { get; set; }
        public int AffectedRowCount { get; set; }
        public int CheaperRowCount { get; set; }
        public string Recommendation { get; set; }
        public string Detail { get; set; }
    }
}
