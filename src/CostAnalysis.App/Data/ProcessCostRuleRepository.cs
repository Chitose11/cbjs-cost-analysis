using System;
using System.Collections.Generic;
using System.Data.SQLite;

namespace CostAnalysis.App.Data
{
    internal sealed class ProcessCostRuleRepository
    {
        public List<ProcessCostRule> GetAll()
        {
            var list = new List<ProcessCostRule>();
            using (var connection = new SQLiteConnection(DatabaseInitializer.ConnectionString))
            {
                connection.Open();
                using (var command = new SQLiteCommand(@"
SELECT id, keyword, cost_type, amount, is_enabled, remark
FROM process_cost_rules
ORDER BY id;", connection))
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        list.Add(new ProcessCostRule
                        {
                            Id = Convert.ToInt32(reader["id"]),
                            Keyword = ReadString(reader, "keyword"),
                            CostType = ReadString(reader, "cost_type"),
                            Amount = ReadDecimal(reader, "amount"),
                            IsEnabled = reader["is_enabled"] != DBNull.Value && Convert.ToInt32(reader["is_enabled"]) == 1,
                            Remark = ReadString(reader, "remark")
                        });
                    }
                }
            }

            return list;
        }

        public void SaveAll(IEnumerable<ProcessCostRule> rules)
        {
            using (var connection = new SQLiteConnection(DatabaseInitializer.ConnectionString))
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    using (var delete = new SQLiteCommand("DELETE FROM process_cost_rules;", connection, transaction))
                    {
                        delete.ExecuteNonQuery();
                    }

                    foreach (var rule in rules)
                    {
                        if (string.IsNullOrWhiteSpace(rule.Keyword) || string.IsNullOrWhiteSpace(rule.CostType))
                        {
                            continue;
                        }

                        using (var insert = new SQLiteCommand(@"
INSERT INTO process_cost_rules (keyword, cost_type, amount, is_enabled, remark)
VALUES (@keyword, @cost_type, @amount, @is_enabled, @remark);", connection, transaction))
                        {
                            insert.Parameters.AddWithValue("@keyword", rule.Keyword.Trim());
                            insert.Parameters.AddWithValue("@cost_type", rule.CostType.Trim());
                            insert.Parameters.AddWithValue("@amount", rule.Amount.HasValue ? (object)rule.Amount.Value : DBNull.Value);
                            insert.Parameters.AddWithValue("@is_enabled", rule.IsEnabled ? 1 : 0);
                            insert.Parameters.AddWithValue("@remark", rule.Remark ?? string.Empty);
                            insert.ExecuteNonQuery();
                        }
                    }

                    transaction.Commit();
                }
            }
        }

        public List<ProcessCostRule> FindMatches(string text)
        {
            var matches = new List<ProcessCostRule>();
            if (string.IsNullOrWhiteSpace(text))
            {
                return matches;
            }

            var normalizedText = Normalize(text);
            foreach (var rule in GetAll())
            {
                if (!rule.IsEnabled || string.IsNullOrWhiteSpace(rule.Keyword))
                {
                    continue;
                }

                if (normalizedText.IndexOf(Normalize(rule.Keyword), StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    matches.Add(rule);
                }
            }

            return matches;
        }

        private static string Normalize(string value)
        {
            return (value ?? string.Empty).Replace(" ", string.Empty).Trim().ToLowerInvariant();
        }

        private static string ReadString(SQLiteDataReader reader, string name)
        {
            return reader[name] == DBNull.Value ? string.Empty : Convert.ToString(reader[name]);
        }

        private static decimal? ReadDecimal(SQLiteDataReader reader, string name)
        {
            return reader[name] == DBNull.Value ? (decimal?)null : Convert.ToDecimal(reader[name]);
        }
    }

    internal sealed class ProcessCostRule
    {
        public int Id { get; set; }
        public string Keyword { get; set; }
        public string CostType { get; set; }
        public decimal? Amount { get; set; }
        public bool IsEnabled { get; set; }
        public string Remark { get; set; }
    }
}
