using System;
using System.Text.RegularExpressions;

namespace CostAnalysis.App.Services
{
    internal sealed class AreaMaterialCostService
    {
        private static readonly Regex SizeRegex =
            new Regex(@"(?<w>\d+(?:\.\d+)?)\s*[*xX×]\s*(?<h>\d+(?:\.\d+)?)(?:\s*(?<unit>mm|MM|毫米|cm|CM|厘米))?", RegexOptions.Compiled);

        public AreaMaterialCostResult TryCalculateStickerCost(string text, string sizeText, decimal? materialUnitPrice)
        {
            if (!materialUnitPrice.HasValue || materialUnitPrice.Value <= 0)
            {
                return AreaMaterialCostResult.Empty;
            }

            var source = ((text ?? string.Empty) + " " + (sizeText ?? string.Empty)).Trim();
            if (!LooksLikeSticker(source))
            {
                return AreaMaterialCostResult.Empty;
            }

            var area = TryGetAreaSquareMeter(source);
            if (!area.HasValue || area.Value <= 0)
            {
                return AreaMaterialCostResult.Empty;
            }

            var amount = area.Value * materialUnitPrice.Value;
            return new AreaMaterialCostResult
            {
                Amount = amount,
                Evidence = "贴纸/板贴面积材料费：" + Format(area.Value) + "m2 * 材料单价 " + Format(materialUnitPrice.Value)
            };
        }

        private static bool LooksLikeSticker(string text)
        {
            var value = text ?? string.Empty;
            return value.IndexOf("贴纸", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   value.IndexOf("板贴", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   value.IndexOf("标签", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   value.IndexOf("标贴", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   value.IndexOf("胶贴", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   value.IndexOf("不干胶", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   value.IndexOf("PET", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   value.IndexOf("PVC", StringComparison.OrdinalIgnoreCase) >= 0;
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
                !decimal.TryParse(match.Groups["h"].Value, out height) ||
                width <= 0 ||
                height <= 0)
            {
                return null;
            }

            var unit = match.Groups["unit"].Success ? match.Groups["unit"].Value : string.Empty;
            if (unit.Equals("cm", StringComparison.OrdinalIgnoreCase) || unit == "厘米")
            {
                return width * height / 10000m;
            }

            return width * height / 1000000m;
        }

        private static string Format(decimal value)
        {
            return value.ToString("0.####");
        }
    }

    internal sealed class AreaMaterialCostResult
    {
        public static readonly AreaMaterialCostResult Empty = new AreaMaterialCostResult();

        public decimal? Amount { get; set; }
        public string Evidence { get; set; }
    }
}
