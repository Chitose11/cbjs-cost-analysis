using System;
using System.Text.RegularExpressions;
using CostAnalysis.App.Data;

namespace CostAnalysis.App.Services
{
    internal sealed class ProcessCostCalculationService
    {
        private static readonly Regex AreaFormulaRegex =
            new Regex(@"(?:area|面积|平方|长\*宽)\s*[*xX×]\s*(?<rate>\d+(?:\.\d+)?)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex ColorFormulaRegex =
            new Regex(@"(?:color|colors|色数|印色)\s*[*xX×]\s*(?<rate>\d+(?:\.\d+)?)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

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
                        var amount = area.Value * rate;
                        return new ProcessCostCalculationResult
                        {
                            Amount = amount,
                            Evidence = "面积公式 " + Format(area.Value) + "m2 * " + Format(rate) + "，规则：" + rule.Keyword
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
                        var amount = colorCount.Value * rate;
                        return new ProcessCostCalculationResult
                        {
                            Amount = amount,
                            Evidence = "色数公式 " + colorCount.Value + " * " + Format(rate) + "，规则：" + rule.Keyword
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
    }

    internal sealed class ProcessCostCalculationResult
    {
        public static readonly ProcessCostCalculationResult Empty = new ProcessCostCalculationResult();

        public decimal? Amount { get; set; }
        public string Evidence { get; set; }
    }
}
