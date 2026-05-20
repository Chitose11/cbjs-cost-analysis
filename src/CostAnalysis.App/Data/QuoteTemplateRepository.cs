using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using System.Text;
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

        private List<QuoteTemplateRecord> GetAllEnabled()
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
            var endRow = Math.Min(preview.RawSheet.Rows, startRow + 4);
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
                learned_at = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            });
        }

        private static string Safe(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "空" : value.Trim();
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
