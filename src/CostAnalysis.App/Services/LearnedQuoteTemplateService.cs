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
            var seed = new QuoteImportPreview
            {
                SheetName = sheetName,
                RawSheet = QuoteRawSheetPreview.FromCells(cells, rows, cols),
                Items = new List<QuoteImportItem>()
            };

            foreach (var template in new QuoteTemplateRepository().FindMatches(seed, 5))
            {
                var applied = TryApplyTemplate(template, sheetName, cells, rows, cols);
                if (applied != null && applied.Items.Count > 0)
                {
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

            var preview = new QuoteImportPreview
            {
                SheetName = sheetName,
                Supplier = GetString(map, "supplier"),
                TemplateType = "鏈湴瀛︿範妯℃澘-" + (template.Name ?? string.Empty),
                Confidence = 0.75,
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
                if (string.IsNullOrWhiteSpace(rawName))
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
                    MaterialCode = Get(cells, row, GetInt(columns, "code")),
                    MaterialName = rawName,
                    FinishedSize = Get(cells, row, GetInt(columns, "size")),
                    MaterialProcess = Get(cells, row, GetInt(columns, "process")),
                    MaterialNameExtracted = Get(cells, row, GetInt(columns, "material_name")),
                    GramWeight = Get(cells, row, GetInt(columns, "gram_weight")),
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
            var hasBusinessField = !string.IsNullOrWhiteSpace(item.MaterialCode) ||
                                   !string.IsNullOrWhiteSpace(item.FinishedSize) ||
                                   !string.IsNullOrWhiteSpace(item.MaterialProcess);
            return hasPrice && hasBusinessField;
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

        private static bool HasUsefulContent(QuoteImportItem item)
        {
            return item != null &&
                   (!string.IsNullOrWhiteSpace(item.MaterialCode) ||
                    !string.IsNullOrWhiteSpace(item.MaterialProcess) ||
                    !string.IsNullOrWhiteSpace(item.FinishedSize));
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

        private static string GetString(Dictionary<string, object> map, string key)
        {
            object value;
            return map != null && map.TryGetValue(key, out value) && value != null ? Convert.ToString(value) : string.Empty;
        }
    }
}
