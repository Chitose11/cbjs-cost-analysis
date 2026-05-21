using System;
using System.Collections;
using System.Collections.Generic;
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

            var dataStartRow = GetInt(map, "data_start_row");
            if (dataStartRow <= 0)
            {
                dataStartRow = 1;
            }

            var preview = new QuoteImportPreview
            {
                SheetName = sheetName,
                Supplier = GetString(map, "supplier"),
                TemplateType = "本地学习模板-" + (template.Name ?? string.Empty),
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

            return preview;
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
