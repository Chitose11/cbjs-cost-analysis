using System;
using System.Collections.Generic;
using System.Data.SQLite;

namespace CostAnalysis.App.Data
{
    internal sealed class MaterialRepository
    {
        public List<MaterialRecord> GetAll()
        {
            var list = new List<MaterialRecord>();
            using (var connection = new SQLiteConnection(DatabaseInitializer.ConnectionString))
            {
                connection.Open();
                using (var command = new SQLiteCommand(@"
SELECT id, name, aliases, category, vendor, spec, unit, tax_unit_price, includes_freight, remark
FROM materials
ORDER BY name;", connection))
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        list.Add(new MaterialRecord
                        {
                            Id = Convert.ToInt32(reader["id"]),
                            Name = ReadString(reader, "name"),
                            Aliases = ReadString(reader, "aliases"),
                            Category = ReadString(reader, "category"),
                            Vendor = ReadString(reader, "vendor"),
                            Spec = ReadString(reader, "spec"),
                            Unit = ReadString(reader, "unit"),
                            TaxUnitPrice = ReadDecimal(reader, "tax_unit_price"),
                            IncludesFreight = reader["includes_freight"] != DBNull.Value && Convert.ToInt32(reader["includes_freight"]) == 1,
                            Remark = ReadString(reader, "remark")
                        });
                    }
                }
            }

            return list;
        }

        public void SaveAll(IEnumerable<MaterialRecord> materials)
        {
            using (var connection = new SQLiteConnection(DatabaseInitializer.ConnectionString))
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    using (var delete = new SQLiteCommand("DELETE FROM materials;", connection, transaction))
                    {
                        delete.ExecuteNonQuery();
                    }

                    foreach (var material in materials)
                    {
                        if (string.IsNullOrWhiteSpace(material.Name))
                        {
                            continue;
                        }

                        using (var insert = new SQLiteCommand(@"
INSERT INTO materials (name, aliases, category, vendor, spec, unit, tax_unit_price, includes_freight, remark)
VALUES (@name, @aliases, @category, @vendor, @spec, @unit, @tax_unit_price, @includes_freight, @remark);", connection, transaction))
                        {
                            insert.Parameters.AddWithValue("@name", material.Name.Trim());
                            insert.Parameters.AddWithValue("@aliases", material.Aliases ?? string.Empty);
                            insert.Parameters.AddWithValue("@category", material.Category ?? string.Empty);
                            insert.Parameters.AddWithValue("@vendor", material.Vendor ?? string.Empty);
                            insert.Parameters.AddWithValue("@spec", material.Spec ?? string.Empty);
                            insert.Parameters.AddWithValue("@unit", material.Unit ?? string.Empty);
                            insert.Parameters.AddWithValue("@tax_unit_price", material.TaxUnitPrice.HasValue ? (object)material.TaxUnitPrice.Value : DBNull.Value);
                            insert.Parameters.AddWithValue("@includes_freight", material.IncludesFreight ? 1 : 0);
                            insert.Parameters.AddWithValue("@remark", material.Remark ?? string.Empty);
                            insert.ExecuteNonQuery();
                        }
                    }

                    transaction.Commit();
                }
            }
        }

        public MaterialRecord FindByNameOrAlias(string materialName)
        {
            if (string.IsNullOrWhiteSpace(materialName))
            {
                return null;
            }

            var normalized = Normalize(materialName);
            var materials = GetAll();
            foreach (var material in materials)
            {
                if (Normalize(material.Name) == normalized)
                {
                    return material;
                }

                var aliases = (material.Aliases ?? string.Empty).Split(new[] { ';', '；', ',', '，', '|', '/' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var alias in aliases)
                {
                    if (Normalize(alias) == normalized)
                    {
                        return material;
                    }
                }
            }

            foreach (var material in materials)
            {
                if (IsConservativeTextMatch(normalized, Normalize(material.Name)))
                {
                    return material;
                }

                var aliases = (material.Aliases ?? string.Empty).Split(new[] { ';', '；', ',', '，', '|', '/' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var alias in aliases)
                {
                    if (IsConservativeTextMatch(normalized, Normalize(alias)))
                    {
                        return material;
                    }
                }
            }

            return null;
        }

        private static bool IsConservativeTextMatch(string source, string target)
        {
            if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(target))
            {
                return false;
            }

            if (source.Length < 2 || target.Length < 2)
            {
                return false;
            }

            return source.IndexOf(target, StringComparison.OrdinalIgnoreCase) >= 0 ||
                   target.IndexOf(source, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string Normalize(string value)
        {
            return (value ?? string.Empty)
                .Replace(" ", string.Empty)
                .Replace("G", "g")
                .Trim()
                .ToLowerInvariant();
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

    internal sealed class MaterialRecord
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Aliases { get; set; }
        public string Category { get; set; }
        public string Vendor { get; set; }
        public string Spec { get; set; }
        public string Unit { get; set; }
        public decimal? TaxUnitPrice { get; set; }
        public bool IncludesFreight { get; set; }
        public string Remark { get; set; }
    }
}
