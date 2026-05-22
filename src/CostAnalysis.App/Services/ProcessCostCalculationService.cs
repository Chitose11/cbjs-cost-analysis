using System;
using System.Text.RegularExpressions;
using CostAnalysis.App.Data;

namespace CostAnalysis.App.Services
{
    internal sealed class ProcessCostCalculationService
    {
        private static readonly Regex AreaFormulaRegex =
            new Regex(@"(?:area|面积|平方|长\*宽)\s*[*xX×]\s*(?<rate>\d+(?:\.\d+)?)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex PerimeterFormulaRegex =
            new Regex(@"(?:perimeter|周长|边长)\s*[*xX×]\s*(?<rate>\d+(?:\.\d+)?)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex ColorFormulaRegex =
            new Regex(@"(?:color|colors|色数|印色)\s*[*xX×]\s*(?<rate>\d+(?:\.\d+)?)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex AddonRegex =
            new Regex(@"\+\s*(?<amount>\d+(?:\.\d+)?)", RegexOptions.Compiled);

        private static readonly Regex MinimumRegex =
            new Regex(@"(?:min|minimum|最低|起步|保底)\s*(?<amount>\d+(?:\.\d+)?)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex SizeRegex =
            new Regex(@"(?<w>\d+(?:\.\d+)?)\s*[*xX×]\s*(?<h>\d+(?:\.\d+)?)", RegexOptions.Compiled);

        private static readonly Regex ColorCountRegex =
            new Regex(@"(?<count>\d+)\s*(?:C|c|色)", RegexOptions.Compiled);

        public ProcessCostCalculationResult Calculate(ProcessCostRule rule, string text, string sizeText)
        {
            if (rule == null)
            {
                return ProcessCostCalculationResult.Empty;
            }

            if (rule.Amount.HasValue)
            {
                return new ProcessCostCalculationResult
                {
                    Amount = rule.Amount.Value,
                    Evidence = "固定金额 " + Format(rule.Amount.Value) + "，规则：" + rule.Keyword
                };
            }

            var formula = rule.Remark ?? string.Empty;
            var sourceText = (text ?? string.Empty) + " " + (sizeText ?? string.Empty);

            var areaMatch = AreaFormulaRegex.Match(formula);
            if (areaMatch.Success)
            {
                decimal rate;
                if (decimal.TryParse(areaMatch.Groups["rate"].Value, out rate))
                {
                    var area = TryGetAreaSquareMeter(sourceText);
                    if (area.HasValue)
                    {
                        var amount = ApplyFormulaAdjustments(area.Value * rate, formula);
                        return new ProcessCostCalculationResult
                        {
                            Amount = amount,
                            Evidence = "面积公式 " + Format(area.Value) + "m2 * " + Format(rate) + BuildAdjustmentEvidence(formula) + "，规则：" + rule.Keyword
                        };
                    }
                }
            }

            var perimeterMatch = PerimeterFormulaRegex.Match(formula);
            if (perimeterMatch.Success)
            {
                decimal rate;
                if (decimal.TryParse(perimeterMatch.Groups["rate"].Value, out rate))
                {
                    var perimeter = TryGetPerimeterMeter(sourceText);
                    if (perimeter.HasValue)
                    {
                        var amount = ApplyFormulaAdjustments(perimeter.Value * rate, formula);
                        return new ProcessCostCalculationResult
                        {
                            Amount = amount,
                            Evidence = "周长公式 " + Format(perimeter.Value) + "m * " + Format(rate) + BuildAdjustmentEvidence(formula) + "，规则：" + rule.Keyword
                        };
                    }
                }
            }

            var colorMatch = ColorFormulaRegex.Match(formula);
            if (colorMatch.Success)
            {
                decimal rate;
                if (decimal.TryParse(colorMatch.Groups["rate"].Value, out rate))
                {
                    var colorCount = TryGetColorCount(sourceText);
                    if (colorCount.HasValue)
                    {
                        var amount = ApplyFormulaAdjustments(colorCount.Value * rate, formula);
                        return new ProcessCostCalculationResult
                        {
                            Amount = amount,
                            Evidence = "色数公式 " + colorCount.Value + " * " + Format(rate) + BuildAdjustmentEvidence(formula) + "，规则：" + rule.Keyword
                        };
                    }
                }
            }

            return ProcessCostCalculationResult.Empty;
        }

        private static decimal? TryGetAreaSquareMeter(string text)
        {
            var match = SizeRegex.Match(text ?? string.Empty);
            if (!match.Success)
            {
                return null;
            }

            decimal width;
            decimal height;
            if (!decimal.TryParse(match.Groups["w"].Value, out width) ||
                !decimal.TryParse(match.Groups["h"].Value, out height))
            {
                return null;
            }

            if (width <= 0 || height <= 0)
            {
                return null;
            }

            return width * height / 1000000m;
        }

        private static decimal? TryGetPerimeterMeter(string text)
        {
            var match = SizeRegex.Match(text ?? string.Empty);
            if (!match.Success)
            {
                return null;
            }

            decimal width;
            decimal height;
            if (!decimal.TryParse(match.Groups["w"].Value, out width) ||
                !decimal.TryParse(match.Groups["h"].Value, out height) ||
                width <= 0 ||
                height <= 0)
            {
                return null;
            }

            return (width + height) * 2 / 1000m;
        }

        private static int? TryGetColorCount(string text)
        {
            var match = ColorCountRegex.Match(text ?? string.Empty);
            if (!match.Success)
            {
                return null;
            }

            int count;
            return int.TryParse(match.Groups["count"].Value, out count) && count > 0 ? count : (int?)null;
        }

        private static string Format(decimal value)
        {
            return value.ToString("0.####");
        }

        private static decimal ApplyFormulaAdjustments(decimal amount, string formula)
        {
            var adjusted = amount;
            var addon = AddonRegex.Match(formula ?? string.Empty);
            if (addon.Success)
            {
                decimal addonAmount;
                if (decimal.TryParse(addon.Groups["amount"].Value, out addonAmount))
                {
                    adjusted += addonAmount;
                }
            }

            var minimum = MinimumRegex.Match(formula ?? string.Empty);
            if (minimum.Success)
            {
                decimal minimumAmount;
                if (decimal.TryParse(minimum.Groups["amount"].Value, out minimumAmount) && adjusted < minimumAmount)
                {
                    adjusted = minimumAmount;
                }
            }

            return adjusted;
        }

        private static string BuildAdjustmentEvidence(string formula)
        {
            var parts = string.Empty;
            var addon = AddonRegex.Match(formula ?? string.Empty);
            if (addon.Success)
            {
                parts += " + " + addon.Groups["amount"].Value;
            }

            var minimum = MinimumRegex.Match(formula ?? string.Empty);
            if (minimum.Success)
            {
                parts += "，最低 " + minimum.Groups["amount"].Value;
            }

            return parts;
        }
    }

    internal sealed class ProcessCostCalculationResult
    {
        public static readonly ProcessCostCalculationResult Empty = new ProcessCostCalculationResult();

        public decimal? Amount { get; set; }
        public string Evidence { get; set; }
    }
}
