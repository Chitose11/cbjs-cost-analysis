using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using CostAnalysis.App.Data;

namespace CostAnalysis.App.Services
{
    internal sealed class LearnedQuoteTemplateService
    {
        public QuoteImportPreview TryApply(string sheetName, string[,] cells, int rows, int cols)
        {
            var repository = new QuoteTemplateRepository();
            var seed = new QuoteImportPreview
            {
                SheetName = sheetName,
                RawSheet = QuoteRawSheetPreview.FromCells(cells, rows, cols),
                Items = new List<QuoteImportItem>()
            };

            foreach (var template in repository.FindMatches(seed, 5))
            {
                var applied = TryApplyTemplate(template, sheetName, cells, rows, cols);
                if (applied != null && applied.Items.Count > 0)
                {
                    repository.MarkTemplateUsed(template.Id);
                    return applied;
                }
            }

            return null;
        }

        private static QuoteImportPreview TryApplyTemplate(QuoteTemplateRecord template, string sheetName, string[,] cells, int rows, int cols)
        {
            var map = ParseMap(template.FieldMapJson);
            var columns = GetDictionary(map, "columns");
            if (columns == null)
            {
                return null;
            }

            var nameColumn = GetInt(columns, "name");
            if (nameColumn <= 0)
            {
                return null;
            }

            if (!HasReliableColumnMap(columns))
            {
                return null;
            }

            var dataStartRow = GetInt(map, "data_start_row");
            if (dataStartRow <= 0)
            {
                dataStartRow = 1;
            }

            dataStartRow = DetectDataStartRow(map, columns, cells, rows, cols, dataStartRow);

            var preview = new QuoteImportPreview
            {
                SheetName = sheetName,
                Supplier = FindSupplier(cells, rows, cols, GetString(map, "supplier")),
                TemplateType = "本地规则-" + (template.Name ?? string.Empty),
                Confidence = Math.Max(0.72, Math.Min(0.95, GetDouble(map, "quality_score"))),
                HeaderRow = GetInt(map, "header_row"),
                QuantityRow = GetInt(map, "quantity_row"),
                DataStartRow = dataStartRow,
                RawSheet = QuoteRawSheetPreview.FromCells(cells, rows, cols),
                Items = new List<QuoteImportItem>()
            };

            var blankCount = 0;
            for (var row = dataStartRow; row <= rows; row++)
            {
                var rawName = Get(cells, row, nameColumn);
                var code = Get(cells, row, GetInt(columns, "code"));
                var process = Get(cells, row, GetInt(columns, "process"));
                if (string.IsNullOrWhiteSpace(rawName) && string.IsNullOrWhiteSpace(code) && string.IsNullOrWhiteSpace(process))
                {
                    blankCount++;
                    if (blankCount >= 8)
                    {
                        break;
                    }

                    continue;
                }

                blankCount = 0;
                var item = new QuoteImportItem
                {
                    RawName = rawName,
                    MaterialCode = code,
                    MaterialName = string.IsNullOrWhiteSpace(rawName) ? code : rawName,
                    FinishedSize = NormalizeLearnedSize(Get(cells, row, GetInt(columns, "size"))),
                    MaterialProcess = NormalizeLearnedProcess(process),
                    MaterialNameExtracted = NormalizeLearnedMaterial(Get(cells, row, GetInt(columns, "material_name"))),
                    GramWeight = NormalizeLearnedGramWeight(Get(cells, row, GetInt(columns, "gram_weight"))),
                    UsageQuantity = ParseDecimal(Get(cells, row, GetInt(columns, "usage"))),
                    PriceTiers = ReadPriceTiers(map, cells, row)
                };

                if (item.PriceTiers.Count > 0 || HasUsefulContent(item))
                {
                    preview.Items.Add(item);
                }
            }

            return HasReliableItems(preview.Items) ? preview : null;
        }

        private static bool HasReliableColumnMap(Dictionary<string, object> columns)
        {
            var importantKeys = new[] { "code", "name", "size", "process", "material_name", "gram_weight", "usage" };
            var mapped = importantKeys
                .Select(key => GetInt(columns, key))
                .Where(column => column > 0)
                .ToList();
            if (mapped.Count < 2)
            {
                return false;
            }

            if (mapped.Distinct().Count() < Math.Min(2, mapped.Count))
            {
                return false;
            }

            var nameColumn = GetInt(columns, "name");
            var sizeColumn = GetInt(columns, "size");
            var processColumn = GetInt(columns, "process");
            return nameColumn > 0 && (sizeColumn > 0 || processColumn > 0 || GetInt(columns, "code") > 0);
        }

        private static int DetectDataStartRow(Dictionary<string, object> map, Dictionary<string, object> columns, string[,] cells, int rows, int cols, int learnedStartRow)
        {
            var start = Math.Max(1, learnedStartRow - 6);
            var end = Math.Min(rows, Math.Max(learnedStartRow + 30, 60));
            var nameColumn = GetInt(columns, "name");
            var codeColumn = GetInt(columns, "code");
            var sizeColumn = GetInt(columns, "size");
            var processColumn = GetInt(columns, "process");
            var priceColumns = GetList(map, "price_columns");

            for (var row = start; row <= end; row++)
            {
                var score = 0;
                var name = Get(cells, row, nameColumn);
                if (!string.IsNullOrWhiteSpace(name) && !IsPureSerial(name) && !LooksLikeFooterText(name))
                {
                    score += 3;
                }

                if (!string.IsNullOrWhiteSpace(Get(cells, row, codeColumn)))
                {
                    score += 2;
                }

                if (!string.IsNullOrWhiteSpace(Get(cells, row, sizeColumn)))
                {
                    score += 1;
                }

                if (!string.IsNullOrWhiteSpace(Get(cells, row, processColumn)))
                {
                    score += 1;
                }

                foreach (var priceColumn in ReadPriceColumnNumbers(priceColumns))
                {
                    if (ParseDecimal(Get(cells, row, priceColumn)).HasValue)
                    {
                        score += 2;
                        break;
                    }
                }

                if (score >= 5)
                {
                    return row;
                }
            }

            return learnedStartRow;
        }

        private static bool HasReliableItems(List<QuoteImportItem> items)
        {
            if (items == null || items.Count == 0)
            {
                return false;
            }

            var reliable = 0;
            foreach (var item in items)
            {
                if (LooksLikeRecognizedItem(item))
                {
                    reliable++;
                }
            }

            return reliable > 0 && reliable >= Math.Max(1, items.Count / 2);
        }

        private static bool LooksLikeRecognizedItem(QuoteImportItem item)
        {
            if (item == null)
            {
                return false;
            }

            var name = (item.MaterialName ?? item.RawName ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(name) || IsPureSerial(name) || LooksLikeFooterText(name))
            {
                return false;
            }

            var hasPrice = item.PriceTiers != null && item.PriceTiers.Count > 0;
            var hasStrongBusinessField = LooksLikeMaterialCode(item.MaterialCode) ||
                                         LooksLikeSize(item.FinishedSize) ||
                                         LooksLikeProcessOrMaterial(item.MaterialProcess) ||
                                         LooksLikeProcessOrMaterial(item.MaterialNameExtracted);
            return hasPrice && hasStrongBusinessField;
        }

        private static bool IsPureSerial(string value)
        {
            return Regex.IsMatch((value ?? string.Empty).Trim(), @"^\d{1,3}$");
        }

        private static bool LooksLikeFooterText(string value)
        {
            var text = value ?? string.Empty;
            return text.IndexOf("\u4ea4\u8d27\u671f", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   text.IndexOf("\u4ed8\u6b3e", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   text.IndexOf("\u8c22\u8c22", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   text.IndexOf("\u5907\u6ce8", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   text.IndexOf("\u62a5\u4ef7\u6709\u6548", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static List<PriceTier> ReadPriceTiers(Dictionary<string, object> map, string[,] cells, int row)
        {
            var tiers = new List<PriceTier>();
            var priceColumns = GetList(map, "price_columns");
            if (priceColumns == null)
            {
                return tiers;
            }

            foreach (var entry in priceColumns)
            {
                var priceMap = entry as Dictionary<string, object>;
                if (priceMap == null)
                {
                    continue;
                }

                var column = GetInt(priceMap, "column");
                var price = ParseDecimal(Get(cells, row, column));
                if (!price.HasValue)
                {
                    continue;
                }

                tiers.Add(new PriceTier
                {
                    Label = GetString(priceMap, "label"),
                    MinQuantity = GetNullableInt(priceMap, "min_quantity"),
                    MaxQuantity = GetNullableInt(priceMap, "max_quantity"),
                    UnitPrice = price
                });
            }

            return tiers;
        }

        private static IEnumerable<int> ReadPriceColumnNumbers(IEnumerable priceColumns)
        {
            if (priceColumns == null)
            {
                yield break;
            }

            foreach (var entry in priceColumns)
            {
                var priceMap = entry as Dictionary<string, object>;
                var column = GetInt(priceMap, "column");
                if (column > 0)
                {
                    yield return column;
                }
            }
        }

        private static bool HasUsefulContent(QuoteImportItem item)
        {
            return item != null &&
                   (LooksLikeMaterialCode(item.MaterialCode) ||
                    LooksLikeProcessOrMaterial(item.MaterialProcess) ||
                    LooksLikeSize(item.FinishedSize));
        }

        private static string NormalizeLearnedSize(string value)
        {
            var text = (value ?? string.Empty).Trim();
            return LooksLikeSize(text) ? text : string.Empty;
        }

        private static string NormalizeLearnedProcess(string value)
        {
            var text = (value ?? string.Empty).Trim();
            return LooksLikeProcessOrMaterial(text) ? text : string.Empty;
        }

        private static string NormalizeLearnedMaterial(string value)
        {
            var text = (value ?? string.Empty).Trim();
            return LooksLikeProcessOrMaterial(text) ? text : string.Empty;
        }

        private static string NormalizeLearnedGramWeight(string value)
        {
            var text = (value ?? string.Empty).Trim();
            return Regex.IsMatch(text, @"\d+(?:\.\d+)?\s*(?:g|G|克|#)") ? text : string.Empty;
        }

        private static bool LooksLikeMaterialCode(string value)
        {
            var text = (value ?? string.Empty).Trim();
            return Regex.IsMatch(text, @"(?:\d+-)?\d{2}\.\d{2}\.[A-Za-z0-9xX]{4,}(?:[-.][A-Za-z0-9]+)*");
        }

        private static bool LooksLikeSize(string value)
        {
            var text = (value ?? string.Empty).Replace(" ", string.Empty);
            return Regex.IsMatch(text, @"\d+(?:\.\d+)?[*xX×]\d+(?:\.\d+)?");
        }

        private static bool LooksLikeProcessOrMaterial(string value)
        {
            var text = value ?? string.Empty;
            if (string.IsNullOrWhiteSpace(text) || Regex.IsMatch(text.Trim(), @"^\d+(?:\.\d+)?$"))
            {
                return false;
            }

            return text.IndexOf("纸", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   text.IndexOf("卡", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   text.IndexOf("铜", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   text.IndexOf("胶", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   text.IndexOf("PET", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   text.IndexOf("PVC", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   text.IndexOf("印", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   text.IndexOf("UV", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   text.IndexOf("啤", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   text.IndexOf("粘", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   text.IndexOf("覆", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static Dictionary<string, object> ParseMap(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return new Dictionary<string, object>();
            }

            try
            {
                return new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(json) ?? new Dictionary<string, object>();
            }
            catch
            {
                return new Dictionary<string, object>();
            }
        }

        private static Dictionary<string, object> GetDictionary(Dictionary<string, object> map, string key)
        {
            object value;
            return map != null && map.TryGetValue(key, out value) ? value as Dictionary<string, object> : null;
        }

        private static IEnumerable GetList(Dictionary<string, object> map, string key)
        {
            object value;
            if (map == null || !map.TryGetValue(key, out value))
            {
                return null;
            }

            return value as IEnumerable;
        }

        private static string Get(string[,] cells, int row, int col)
        {
            if (cells == null || row <= 0 || col <= 0 || row >= cells.GetLength(0) || col >= cells.GetLength(1))
            {
                return string.Empty;
            }

            return cells[row, col] ?? string.Empty;
        }

        private static decimal? ParseDecimal(string value)
        {
            decimal result;
            return decimal.TryParse((value ?? string.Empty).Trim(), out result) ? result : (decimal?)null;
        }

        private static int GetInt(Dictionary<string, object> map, string key)
        {
            object value;
            if (map == null || !map.TryGetValue(key, out value) || value == null)
            {
                return 0;
            }

            int result;
            return int.TryParse(Convert.ToString(value), out result) ? result : 0;
        }

        private static int? GetNullableInt(Dictionary<string, object> map, string key)
        {
            var value = GetInt(map, key);
            return value > 0 ? value : (int?)null;
        }

        private static double GetDouble(Dictionary<string, object> map, string key)
        {
            object value;
            if (map == null || !map.TryGetValue(key, out value) || value == null)
            {
                return 0;
            }

            double result;
            return double.TryParse(Convert.ToString(value), out result) ? result : 0;
        }

        private static string GetString(Dictionary<string, object> map, string key)
        {
            object value;
            return map != null && map.TryGetValue(key, out value) && value != null ? Convert.ToString(value) : string.Empty;
        }

        private static string FindSupplier(string[,] cells, int rows, int cols, string learnedSupplier)
        {
            for (var row = 1; row <= Math.Min(rows, 8); row++)
            {
                for (var col = 1; col <= Math.Min(cols, 8); col++)
                {
                    var text = Get(cells, row, col);
                    if (text.IndexOf("有限公司", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return text.Trim();
                    }
                }
            }

            return learnedSupplier ?? string.Empty;
        }
    }
}
