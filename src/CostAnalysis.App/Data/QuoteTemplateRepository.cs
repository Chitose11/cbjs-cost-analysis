using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using CostAnalysis.App.Services;

namespace CostAnalysis.App.Data
{
    internal sealed class QuoteTemplateRepository
    {
        public void SaveLearnedTemplate(QuoteImportPreview preview, string sourceFileName, int itemCount)
        {
            if (preview == null || preview.Items == null || preview.Items.Count == 0)
            {
                return;
            }

            var keywords = BuildHeaderKeywords(preview);
            if (string.IsNullOrWhiteSpace(keywords))
            {
                return;
            }

            var now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            var name = BuildTemplateName(preview, sourceFileName);
            var fieldMapJson = BuildFieldMapJson(preview, itemCount);
            using (var connection = new SQLiteConnection(DatabaseInitializer.ConnectionString))
            {
                connection.Open();
                var existingId = FindExistingTemplateId(connection, preview.TemplateType, keywords);
                if (existingId > 0)
                {
                    using (var command = new SQLiteCommand(@"
UPDATE quote_templates
SET header_row_rule = @header_row_rule,
    quantity_row_rule = @quantity_row_rule,
    data_start_rule = @data_start_rule,
    field_map_json = @field_map_json,
    usage_count = usage_count + 1,
    last_used_at = @last_used_at,
    remark = @remark
WHERE id = @id;", connection))
                    {
                        command.Parameters.AddWithValue("@id", existingId);
                        command.Parameters.AddWithValue("@header_row_rule", preview.HeaderRow > 0 ? preview.HeaderRow.ToString() : string.Empty);
                        command.Parameters.AddWithValue("@quantity_row_rule", preview.QuantityRow > 0 ? preview.QuantityRow.ToString() : string.Empty);
                        command.Parameters.AddWithValue("@data_start_rule", preview.DataStartRow > 0 ? preview.DataStartRow.ToString() : string.Empty);
                        command.Parameters.AddWithValue("@field_map_json", fieldMapJson);
                        command.Parameters.AddWithValue("@last_used_at", now);
                        command.Parameters.AddWithValue("@remark", "模板复扫更新：" + (sourceFileName ?? string.Empty));
                        command.ExecuteNonQuery();
                    }

                    return;
                }

                using (var command = new SQLiteCommand(@"
INSERT INTO quote_templates
    (name, template_type, header_keywords, header_row_rule, quantity_row_rule, data_start_rule, field_map_json, is_enabled, usage_count, last_used_at, remark)
VALUES
    (@name, @template_type, @header_keywords, @header_row_rule, @quantity_row_rule, @data_start_rule, @field_map_json, 1, 1, @last_used_at, @remark);", connection))
                {
                    command.Parameters.AddWithValue("@name", name);
                    command.Parameters.AddWithValue("@template_type", preview.TemplateType ?? string.Empty);
                    command.Parameters.AddWithValue("@header_keywords", keywords);
                    command.Parameters.AddWithValue("@header_row_rule", preview.HeaderRow > 0 ? preview.HeaderRow.ToString() : string.Empty);
                    command.Parameters.AddWithValue("@quantity_row_rule", preview.QuantityRow > 0 ? preview.QuantityRow.ToString() : string.Empty);
                    command.Parameters.AddWithValue("@data_start_rule", preview.DataStartRow > 0 ? preview.DataStartRow.ToString() : string.Empty);
                    command.Parameters.AddWithValue("@field_map_json", fieldMapJson);
                    command.Parameters.AddWithValue("@last_used_at", now);
                    command.Parameters.AddWithValue("@remark", "AI清洗学习：" + (sourceFileName ?? string.Empty));
                    command.ExecuteNonQuery();
                }
            }
        }

        public void MarkTemplateUsed(int templateId)
        {
            if (templateId <= 0)
            {
                return;
            }

            using (var connection = new SQLiteConnection(DatabaseInitializer.ConnectionString))
            {
                connection.Open();
                using (var command = new SQLiteCommand(@"
UPDATE quote_templates
SET usage_count = usage_count + 1,
    last_used_at = @last_used_at
WHERE id = @id;", connection))
                {
                    command.Parameters.AddWithValue("@id", templateId);
                    command.Parameters.AddWithValue("@last_used_at", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                    command.ExecuteNonQuery();
                }
            }
        }

        public List<QuoteTemplateRecord> FindMatches(QuoteImportPreview preview, int limit)
        {
            var records = GetAllEnabled();
            var currentTokens = BuildHeaderTokenSet(preview);
            if (currentTokens.Count == 0)
            {
                return new List<QuoteTemplateRecord>();
            }

            return records
                .Select(record => new { Record = record, Score = Score(record, currentTokens, preview) })
                .Where(pair => pair.Score >= 2)
                .OrderByDescending(pair => pair.Score)
                .ThenByDescending(pair => pair.Record.UsageCount)
                .Take(limit <= 0 ? 3 : limit)
                .Select(pair => pair.Record)
                .ToList();
        }

        public string BuildAiTemplateHint(QuoteImportPreview preview)
        {
            var matches = FindMatches(preview, 3);
            if (matches.Count == 0)
            {
                return string.Empty;
            }

            var sb = new StringBuilder();
            sb.AppendLine("本地已学习的相似模板参考：");
            foreach (var match in matches)
            {
                sb.AppendLine("- 模板名：" + Safe(match.Name) +
                              "；类型：" + Safe(match.TemplateType) +
                              "；表头行：" + Safe(match.HeaderRowRule) +
                              "；数据起始行：" + Safe(match.DataStartRule) +
                              "；关键词：" + Safe(match.HeaderKeywords));
            }

            sb.AppendLine("请只把这些作为辅助参考，最终仍以当前原始预览内容为准。");
            return sb.ToString();
        }

        public List<QuoteTemplateRecord> GetAllEnabled()
        {
            var records = new List<QuoteTemplateRecord>();
            using (var connection = new SQLiteConnection(DatabaseInitializer.ConnectionString))
            {
                connection.Open();
                using (var command = new SQLiteCommand(@"
SELECT id, name, template_type, header_keywords, header_row_rule, quantity_row_rule,
       data_start_rule, field_map_json, usage_count, last_used_at, remark
FROM quote_templates
WHERE is_enabled = 1
ORDER BY usage_count DESC, id DESC
LIMIT 200;", connection))
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        records.Add(new QuoteTemplateRecord
                        {
                            Id = Convert.ToInt32(reader["id"]),
                            Name = Convert.ToString(reader["name"]),
                            TemplateType = Convert.ToString(reader["template_type"]),
                            HeaderKeywords = Convert.ToString(reader["header_keywords"]),
                            HeaderRowRule = Convert.ToString(reader["header_row_rule"]),
                            QuantityRowRule = Convert.ToString(reader["quantity_row_rule"]),
                            DataStartRule = Convert.ToString(reader["data_start_rule"]),
                            FieldMapJson = Convert.ToString(reader["field_map_json"]),
                            UsageCount = reader["usage_count"] == DBNull.Value ? 0 : Convert.ToInt32(reader["usage_count"]),
                            LastUsedAt = Convert.ToString(reader["last_used_at"]),
                            Remark = Convert.ToString(reader["remark"])
                        });
                    }
                }
            }

            return records;
        }

        private static int FindExistingTemplateId(SQLiteConnection connection, string templateType, string keywords)
        {
            using (var command = new SQLiteCommand(@"
SELECT id
FROM quote_templates
WHERE is_enabled = 1
  AND IFNULL(template_type, '') = @template_type
  AND IFNULL(header_keywords, '') = @header_keywords
ORDER BY id DESC
LIMIT 1;", connection))
            {
                command.Parameters.AddWithValue("@template_type", templateType ?? string.Empty);
                command.Parameters.AddWithValue("@header_keywords", keywords ?? string.Empty);
                var value = command.ExecuteScalar();
                return value == null || value == DBNull.Value ? 0 : Convert.ToInt32(value);
            }
        }

        private static int Score(QuoteTemplateRecord record, HashSet<string> currentTokens, QuoteImportPreview preview)
        {
            var score = 0;
            foreach (var token in SplitKeywords(record.HeaderKeywords))
            {
                if (currentTokens.Contains(NormalizeToken(token)))
                {
                    score++;
                }
            }

            if (!string.IsNullOrWhiteSpace(record.TemplateType) &&
                !string.IsNullOrWhiteSpace(preview.TemplateType) &&
                preview.TemplateType.IndexOf(record.TemplateType, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                score += 2;
            }

            return score;
        }

        private static string BuildHeaderKeywords(QuoteImportPreview preview)
        {
            return string.Join("；", BuildHeaderTokenSet(preview).Take(24).ToArray());
        }

        private static HashSet<string> BuildHeaderTokenSet(QuoteImportPreview preview)
        {
            var tokens = new HashSet<string>();
            if (preview == null || preview.RawSheet == null || preview.RawSheet.Cells == null)
            {
                return tokens;
            }

            var startRow = preview.HeaderRow > 0 ? preview.HeaderRow : 1;
            var endRow = preview.HeaderRow > 0 ? Math.Min(preview.RawSheet.Rows, startRow + 4) : Math.Min(preview.RawSheet.Rows, 20);
            for (var row = startRow; row <= endRow; row++)
            {
                for (var col = 1; col <= preview.RawSheet.Columns; col++)
                {
                    foreach (var token in SplitKeywords(preview.RawSheet.Cells[row, col]))
                    {
                        var normalized = NormalizeToken(token);
                        if (!string.IsNullOrWhiteSpace(normalized))
                        {
                            tokens.Add(normalized);
                        }
                    }
                }
            }

            return tokens;
        }

        private static IEnumerable<string> SplitKeywords(string value)
        {
            return (value ?? string.Empty)
                .Split(new[] { ' ', '\t', '\r', '\n', '；', ';', ',', '，', '/', '\\', '|', ':', '：', '(', ')', '（', '）' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(token => token.Trim())
                .Where(token => token.Length >= 2 && token.Length <= 30);
        }

        private static string NormalizeToken(string value)
        {
            return (value ?? string.Empty).Trim().ToLowerInvariant();
        }

        private static string BuildTemplateName(QuoteImportPreview preview, string sourceFileName)
        {
            var supplier = string.IsNullOrWhiteSpace(preview.Supplier) ? "未知供应商" : preview.Supplier.Trim();
            var type = string.IsNullOrWhiteSpace(preview.TemplateType) ? "AI模板" : preview.TemplateType.Trim();
            return supplier + "-" + type + "-" + DateTime.Now.ToString("MMddHHmmss");
        }

        private static string BuildFieldMapJson(QuoteImportPreview preview, int itemCount)
        {
            var serializer = new JavaScriptSerializer();
            return serializer.Serialize(new
            {
                template_type = preview.TemplateType,
                supplier = preview.Supplier,
                sheet_name = preview.SheetName,
                header_row = preview.HeaderRow,
                quantity_row = preview.QuantityRow,
                data_start_row = preview.DataStartRow,
                item_count = itemCount,
                learned_at = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                rule_version = 2,
                quality_score = CalculateRuleQuality(preview),
                columns = BuildColumnMap(preview),
                price_columns = BuildPriceColumns(preview)
            });
        }

        private static Dictionary<string, int> BuildColumnMap(QuoteImportPreview preview)
        {
            var map = new Dictionary<string, int>();
            if (preview == null || preview.RawSheet == null || preview.Items == null || preview.Items.Count == 0)
            {
                return map;
            }

            var votes = new Dictionary<string, Dictionary<int, int>>();
            foreach (var match in MatchItemRows(preview).Take(12))
            {
                Vote(votes, "code", FindColumnForValue(preview, match.Row, match.Item.MaterialCode));
                Vote(votes, "name", FindColumnForValue(preview, match.Row, match.Item.RawName, match.Item.MaterialName));
                Vote(votes, "size", FindColumnForValue(preview, match.Row, match.Item.FinishedSize));
                Vote(votes, "process", FindColumnForValue(preview, match.Row, match.Item.MaterialProcess));
                Vote(votes, "material_name", FindColumnForValue(preview, match.Row, match.Item.MaterialNameExtracted));
                Vote(votes, "gram_weight", FindColumnForValue(preview, match.Row, match.Item.GramWeight));
                Vote(votes, "usage", FindColumnForDecimal(preview, match.Row, match.Item.UsageQuantity));
            }

            AddBestColumn(map, votes, preview, "code", new[] { "物料编码", "料号", "产品型号", "型号", "编码" });
            AddBestColumn(map, votes, preview, "name", new[] { "物料名称", "产品名称", "品名", "名称" });
            AddBestColumn(map, votes, preview, "size", new[] { "成品尺寸", "尺寸", "规格", "规格描述" });
            AddBestColumn(map, votes, preview, "process", new[] { "材质/工艺", "材质", "工艺", "规格描述" });
            AddBestColumn(map, votes, preview, "material_name", new[] { "材料名称", "材质", "纸质", "材质类型" });
            AddBestColumn(map, votes, preview, "gram_weight", new[] { "克重", "原材料克重", "g/m2", "g/㎡" });
            AddBestColumn(map, votes, preview, "usage", new[] { "用量", "数量", "MOQ" });
            return map;
        }

        private static List<Dictionary<string, object>> BuildPriceColumns(QuoteImportPreview preview)
        {
            var result = new List<Dictionary<string, object>>();
            if (preview == null || preview.RawSheet == null || preview.Items == null || preview.Items.Count == 0)
            {
                return result;
            }

            var priceVotes = new Dictionary<int, LearnedPriceColumn>();
            foreach (var match in MatchItemRows(preview).Take(12))
            {
                if (match.Item.PriceTiers == null)
                {
                    continue;
                }

                foreach (var tier in match.Item.PriceTiers)
                {
                    var column = FindColumnForDecimal(preview, match.Row, tier.UnitPrice);
                    if (column <= 0)
                    {
                        continue;
                    }

                    LearnedPriceColumn learned;
                    if (!priceVotes.TryGetValue(column, out learned))
                    {
                        learned = new LearnedPriceColumn { Column = column };
                        priceVotes[column] = learned;
                    }

                    learned.Score++;
                    if (string.IsNullOrWhiteSpace(learned.Label))
                    {
                        learned.Label = tier.Label;
                    }

                    if (!learned.MinQuantity.HasValue)
                    {
                        learned.MinQuantity = tier.MinQuantity;
                    }

                    if (!learned.MaxQuantity.HasValue)
                    {
                        learned.MaxQuantity = tier.MaxQuantity;
                    }
                }
            }

            foreach (var headerColumn in FindPriceColumnsFromHeaders(preview))
            {
                LearnedPriceColumn learned;
                if (!priceVotes.TryGetValue(headerColumn.Column, out learned))
                {
                    priceVotes[headerColumn.Column] = headerColumn;
                }
                else
                {
                    learned.Score += 1;
                    if (string.IsNullOrWhiteSpace(learned.Label))
                    {
                        learned.Label = headerColumn.Label;
                    }
                }
            }

            foreach (var learned in priceVotes.Values.OrderBy(item => item.Column))
            {
                if (learned.Score <= 0)
                {
                    continue;
                }

                result.Add(new Dictionary<string, object>
                {
                    { "column", learned.Column },
                    { "label", learned.Label ?? string.Empty },
                    { "min_quantity", learned.MinQuantity.HasValue ? (object)learned.MinQuantity.Value : null },
                    { "max_quantity", learned.MaxQuantity.HasValue ? (object)learned.MaxQuantity.Value : null },
                    { "score", learned.Score }
                });
            }

            return result;
        }

        private static decimal CalculateRuleQuality(QuoteImportPreview preview)
        {
            if (preview == null || preview.Items == null || preview.Items.Count == 0)
            {
                return 0;
            }

            var matchedRows = MatchItemRows(preview).Count();
            var ratio = matchedRows / (decimal)Math.Max(1, preview.Items.Count);
            return Math.Min(1m, ratio);
        }

        private static void AddBestColumn(Dictionary<string, int> map, Dictionary<string, Dictionary<int, int>> votes, QuoteImportPreview preview, string key, string[] headerWords)
        {
            var column = BestVotedColumn(votes, key);
            if (column <= 0)
            {
                column = FindColumnByHeader(preview, headerWords);
            }

            if (column > 0 && !map.ContainsKey(key) && !map.ContainsValue(column))
            {
                map[key] = column;
            }
        }

        private static int BestVotedColumn(Dictionary<string, Dictionary<int, int>> votes, string key)
        {
            Dictionary<int, int> columns;
            if (!votes.TryGetValue(key, out columns) || columns.Count == 0)
            {
                return 0;
            }

            var best = columns.OrderByDescending(pair => pair.Value).ThenBy(pair => pair.Key).First();
            return best.Value >= 1 ? best.Key : 0;
        }

        private static void Vote(Dictionary<string, Dictionary<int, int>> votes, string key, int column)
        {
            if (column <= 0)
            {
                return;
            }

            Dictionary<int, int> columns;
            if (!votes.TryGetValue(key, out columns))
            {
                columns = new Dictionary<int, int>();
                votes[key] = columns;
            }

            columns[column] = columns.ContainsKey(column) ? columns[column] + 1 : 1;
        }

        private static IEnumerable<ItemRowMatch> MatchItemRows(QuoteImportPreview preview)
        {
            if (preview == null || preview.RawSheet == null || preview.Items == null)
            {
                yield break;
            }

            foreach (var item in preview.Items)
            {
                var row = FindRowForItem(preview, item);
                if (row > 0)
                {
                    yield return new ItemRowMatch { Item = item, Row = row };
                }
            }
        }

        private static int FindRowForItem(QuoteImportPreview preview, QuoteImportItem item)
        {
            var start = preview.DataStartRow > 0 ? Math.Max(1, preview.DataStartRow - 2) : 1;
            var end = Math.Min(preview.RawSheet.Rows, start + 80);
            var bestRow = 0;
            var bestScore = 0;
            for (var row = start; row <= end; row++)
            {
                var score = 0;
                if (RowContainsValue(preview, row, item.MaterialCode)) score += 4;
                if (RowContainsValue(preview, row, item.RawName)) score += 3;
                if (RowContainsValue(preview, row, item.MaterialName)) score += 3;
                if (RowContainsValue(preview, row, item.FinishedSize)) score += 2;
                if (RowContainsValue(preview, row, item.MaterialProcess)) score += 2;
                if (RowContainsDecimal(preview, row, item.UsageQuantity)) score += 1;

                if (item.PriceTiers != null)
                {
                    foreach (var tier in item.PriceTiers.Take(4))
                    {
                        if (RowContainsDecimal(preview, row, tier.UnitPrice))
                        {
                            score += 2;
                        }
                    }
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    bestRow = row;
                }
            }

            return bestScore >= 3 ? bestRow : 0;
        }

        private static bool RowContainsValue(QuoteImportPreview preview, int row, string value)
        {
            var target = NormalizeValue(value);
            if (string.IsNullOrWhiteSpace(target) || target.Length <= 1)
            {
                return false;
            }

            for (var col = 1; col <= preview.RawSheet.Columns; col++)
            {
                var cell = NormalizeValue(preview.RawSheet.Cells[row, col]);
                if (!string.IsNullOrWhiteSpace(cell) &&
                    (cell == target || cell.IndexOf(target, StringComparison.OrdinalIgnoreCase) >= 0 || target.IndexOf(cell, StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool RowContainsDecimal(QuoteImportPreview preview, int row, decimal? value)
        {
            return FindColumnForDecimal(preview, row, value) > 0;
        }

        private static int FindColumnByHeader(QuoteImportPreview preview, params string[] headerWords)
        {
            if (preview == null || preview.RawSheet == null || headerWords == null || headerWords.Length == 0)
            {
                return 0;
            }

            var startRow = preview.HeaderRow > 0 ? Math.Max(1, preview.HeaderRow - 2) : 1;
            var endRow = preview.DataStartRow > 0 ? Math.Min(preview.RawSheet.Rows, preview.DataStartRow - 1) : Math.Min(preview.RawSheet.Rows, 20);
            for (var row = startRow; row <= endRow; row++)
            {
                for (var col = 1; col <= preview.RawSheet.Columns; col++)
                {
                    var cell = NormalizeValue(preview.RawSheet.Cells[row, col]);
                    if (string.IsNullOrWhiteSpace(cell))
                    {
                        continue;
                    }

                    foreach (var word in headerWords)
                    {
                        if (cell.IndexOf(NormalizeValue(word), StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            return col;
                        }
                    }
                }
            }

            return 0;
        }

        private static IEnumerable<LearnedPriceColumn> FindPriceColumnsFromHeaders(QuoteImportPreview preview)
        {
            if (preview == null || preview.RawSheet == null)
            {
                yield break;
            }

            var startRow = preview.HeaderRow > 0 ? preview.HeaderRow : 1;
            var endRow = preview.DataStartRow > 0 ? Math.Min(preview.RawSheet.Rows, preview.DataStartRow - 1) : Math.Min(preview.RawSheet.Rows, startRow + 4);
            for (var row = startRow; row <= endRow; row++)
            {
                for (var col = 1; col <= preview.RawSheet.Columns; col++)
                {
                    var label = (preview.RawSheet.Cells[row, col] ?? string.Empty).Trim();
                    if (!LooksLikePriceTierLabel(label))
                    {
                        continue;
                    }

                    var parsed = ParsePriceLabel(label);
                    parsed.Column = col;
                    parsed.Score = 1;
                    yield return parsed;
                }
            }
        }

        private static bool LooksLikePriceTierLabel(string value)
        {
            var text = (value ?? string.Empty).Replace(" ", string.Empty);
            return Regex.IsMatch(text, @"^\d+\s*[-~至]\s*\d+$") ||
                   Regex.IsMatch(text, @"^\d+\s*(?:以上|\+)$") ||
                   Regex.IsMatch(text, @"^(MOQ)?\d+个?$", RegexOptions.IgnoreCase);
        }

        private static LearnedPriceColumn ParsePriceLabel(string label)
        {
            var result = new LearnedPriceColumn { Label = label ?? string.Empty };
            var normalized = (label ?? string.Empty).Replace("MOQ", string.Empty).Replace("个", string.Empty).Replace(" ", string.Empty);
            var range = Regex.Match(normalized, @"(?<min>\d+)\s*[-~至]\s*(?<max>\d+)");
            if (range.Success)
            {
                result.MinQuantity = int.Parse(range.Groups["min"].Value);
                result.MaxQuantity = int.Parse(range.Groups["max"].Value);
                return result;
            }

            var min = Regex.Match(normalized, @"(?<min>\d+)");
            if (min.Success)
            {
                result.MinQuantity = int.Parse(min.Groups["min"].Value);
            }

            return result;
        }

        private static int FindColumnForValue(QuoteImportPreview preview, int row, params string[] values)
        {
            if (preview == null || preview.RawSheet == null || preview.RawSheet.Cells == null || values == null)
            {
                return 0;
            }

            for (var col = 1; col <= preview.RawSheet.Columns; col++)
            {
                var cell = NormalizeValue(preview.RawSheet.Cells[row, col]);
                if (string.IsNullOrWhiteSpace(cell))
                {
                    continue;
                }

                foreach (var value in values)
                {
                    var target = NormalizeValue(value);
                    if (!string.IsNullOrWhiteSpace(target) &&
                        (cell == target || cell.IndexOf(target, StringComparison.OrdinalIgnoreCase) >= 0 || target.IndexOf(cell, StringComparison.OrdinalIgnoreCase) >= 0))
                    {
                        return col;
                    }
                }
            }

            return 0;
        }

        private static int FindColumnForDecimal(QuoteImportPreview preview, int row, decimal? value)
        {
            if (!value.HasValue || preview == null || preview.RawSheet == null || preview.RawSheet.Cells == null)
            {
                return 0;
            }

            for (var col = 1; col <= preview.RawSheet.Columns; col++)
            {
                decimal number;
                if (decimal.TryParse((preview.RawSheet.Cells[row, col] ?? string.Empty).Trim(), out number) &&
                    Math.Abs(number - value.Value) <= 0.0001m)
                {
                    return col;
                }
            }

            return 0;
        }

        private static string NormalizeValue(string value)
        {
            return (value ?? string.Empty).Replace(" ", string.Empty).Trim();
        }

        private static string Safe(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "空" : value.Trim();
        }

        private sealed class ItemRowMatch
        {
            public QuoteImportItem Item { get; set; }
            public int Row { get; set; }
        }

        private sealed class LearnedPriceColumn
        {
            public int Column { get; set; }
            public string Label { get; set; }
            public int? MinQuantity { get; set; }
            public int? MaxQuantity { get; set; }
            public int Score { get; set; }
        }
    }

    internal sealed class QuoteTemplateRecord
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string TemplateType { get; set; }
        public string HeaderKeywords { get; set; }
        public string HeaderRowRule { get; set; }
        public string QuantityRowRule { get; set; }
        public string DataStartRule { get; set; }
        public string FieldMapJson { get; set; }
        public int UsageCount { get; set; }
        public string LastUsedAt { get; set; }
        public string Remark { get; set; }
    }
}
