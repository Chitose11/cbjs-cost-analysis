using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Windows.Forms;
using System.Web.Script.Serialization;
using CostAnalysis.App.Services;

namespace CostAnalysis.App.Data
{
    internal sealed class CostAnalysisRepository
    {
        public int SaveFromGrid(CostAnalysisHeader header, DataGridView grid)
        {
            var now = DateTime.Now;
            var analysisNo = string.IsNullOrWhiteSpace(header.AnalysisNo)
                ? "CA-" + now.ToString("yyyyMMdd-HHmmss")
                : header.AnalysisNo.Trim();

            using (var connection = new SQLiteConnection(DatabaseInitializer.ConnectionString))
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    var analysisId = InsertAnalysis(connection, transaction, analysisNo, header, now);
                    InsertItems(connection, transaction, analysisId, grid);
                    transaction.Commit();
                    return analysisId;
                }
            }
        }

        public List<CostAnalysisSummary> GetRecentAnalyses()
        {
            var list = new List<CostAnalysisSummary>();
            using (var connection = new SQLiteConnection(DatabaseInitializer.ConnectionString))
            {
                connection.Open();
                using (var command = new SQLiteCommand(@"
SELECT ca.id, ca.analysis_no, ca.customer_name, ca.project_name, ca.created_at, COUNT(i.id) AS item_count
FROM cost_analysis ca
LEFT JOIN cost_analysis_items i ON i.cost_analysis_id = ca.id
GROUP BY ca.id, ca.analysis_no, ca.customer_name, ca.project_name, ca.created_at
ORDER BY ca.id DESC
LIMIT 100;", connection))
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        list.Add(new CostAnalysisSummary
                        {
                            Id = Convert.ToInt32(reader["id"]),
                            AnalysisNo = Convert.ToString(reader["analysis_no"]),
                            CustomerName = Convert.ToString(reader["customer_name"]),
                            ProjectName = Convert.ToString(reader["project_name"]),
                            CreatedAt = Convert.ToString(reader["created_at"]),
                            ItemCount = Convert.ToInt32(reader["item_count"])
                        });
                    }
                }
            }

            return list;
        }

        public SavedCostAnalysis GetAnalysis(int analysisId)
        {
            var analysis = new SavedCostAnalysis
            {
                Header = new CostAnalysisHeader(),
                Items = new List<SavedCostAnalysisItem>()
            };

            using (var connection = new SQLiteConnection(DatabaseInitializer.ConnectionString))
            {
                connection.Open();
                using (var command = new SQLiteCommand(@"
SELECT analysis_no, customer_name, project_name, analysis_date, tax_note, freight_note, remark
FROM cost_analysis
WHERE id = @id;", connection))
                {
                    command.Parameters.AddWithValue("@id", analysisId);
                    using (var reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            analysis.Header.AnalysisNo = ReadString(reader, "analysis_no");
                            analysis.Header.CustomerName = ReadString(reader, "customer_name");
                            analysis.Header.ProjectName = ReadString(reader, "project_name");
                            analysis.Header.AnalysisDate = ReadString(reader, "analysis_date");
                            analysis.Header.TaxNote = ReadString(reader, "tax_note");
                            analysis.Header.FreightNote = ReadString(reader, "freight_note");
                            analysis.Header.Remark = ReadString(reader, "remark");
                        }
                    }
                }
            }

            analysis.Items = GetItems(analysisId);
            return analysis;
        }

        public List<CostHistoryItem> SearchCostHistory(string materialCode, string materialName, int limit)
        {
            var list = new List<CostHistoryItem>();
            var code = (materialCode ?? string.Empty).Trim();
            var name = (materialName ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(code) && string.IsNullOrWhiteSpace(name))
            {
                return list;
            }

            using (var connection = new SQLiteConnection(DatabaseInitializer.ConnectionString))
            {
                connection.Open();
                using (var command = new SQLiteCommand(@"
SELECT ca.id AS analysis_id, ca.analysis_no, ca.customer_name, ca.project_name, ca.analysis_date, ca.created_at,
       i.row_no, i.material_code, i.material_name, i.material_description, i.supplier, i.base_material_name,
       i.material_vendor, i.material_unit_price, i.gram_weight, i.expanded_size, i.material_cost,
       i.printing_cost, i.post_process_cost, i.other_cost, i.purchase_unit_price,
       i.total_quantity, i.total_price, i.price_tiers_json
FROM cost_analysis_items i
INNER JOIN cost_analysis ca ON ca.id = i.cost_analysis_id
WHERE (@code <> '' AND i.material_code = @code)
   OR (@name <> '' AND i.material_name LIKE @name_like)
ORDER BY
    CASE WHEN @code <> '' AND i.material_code = @code THEN 0 ELSE 1 END,
    ca.id DESC,
    i.row_no
LIMIT @limit;", connection))
                {
                    command.Parameters.AddWithValue("@code", code);
                    command.Parameters.AddWithValue("@name", name);
                    command.Parameters.AddWithValue("@name_like", "%" + name + "%");
                    command.Parameters.AddWithValue("@limit", limit <= 0 ? 50 : limit);
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            list.Add(new CostHistoryItem
                            {
                                AnalysisId = ReadInt(reader, "analysis_id"),
                                AnalysisNo = ReadString(reader, "analysis_no"),
                                CustomerName = ReadString(reader, "customer_name"),
                                ProjectName = ReadString(reader, "project_name"),
                                AnalysisDate = ReadString(reader, "analysis_date"),
                                CreatedAt = ReadString(reader, "created_at"),
                                No = ReadInt(reader, "row_no"),
                                MaterialCode = ReadString(reader, "material_code"),
                                MaterialName = ReadString(reader, "material_name"),
                                MaterialDescription = ReadString(reader, "material_description"),
                                Supplier = ReadString(reader, "supplier"),
                                BaseMaterialName = ReadString(reader, "base_material_name"),
                                MaterialVendor = ReadString(reader, "material_vendor"),
                                MaterialUnitPrice = ReadDecimal(reader, "material_unit_price"),
                                GramWeight = ReadString(reader, "gram_weight"),
                                ExpandedSize = ReadString(reader, "expanded_size"),
                                MaterialCost = ReadDecimal(reader, "material_cost"),
                                PrintingCost = ReadDecimal(reader, "printing_cost"),
                                PostProcessCost = ReadDecimal(reader, "post_process_cost"),
                                OtherCost = ReadDecimal(reader, "other_cost"),
                                PurchaseUnitPrice = ReadDecimal(reader, "purchase_unit_price"),
                                TotalQuantity = ReadDecimal(reader, "total_quantity"),
                                TotalPrice = ReadDecimal(reader, "total_price"),
                                PriceTiers = DeserializePriceTiers(ReadString(reader, "price_tiers_json"))
                            });
                        }
                    }
                }
            }

            return list;
        }

        public List<CostHistoryItem> GetRecentCostHistory(int limit)
        {
            var list = new List<CostHistoryItem>();
            using (var connection = new SQLiteConnection(DatabaseInitializer.ConnectionString))
            {
                connection.Open();
                using (var command = new SQLiteCommand(@"
SELECT ca.id AS analysis_id, ca.analysis_no, ca.customer_name, ca.project_name, ca.analysis_date, ca.created_at,
       i.row_no, i.material_code, i.material_name, i.material_description, i.supplier, i.base_material_name,
       i.material_vendor, i.material_unit_price, i.gram_weight, i.expanded_size, i.material_cost,
       i.printing_cost, i.post_process_cost, i.other_cost, i.purchase_unit_price,
       i.total_quantity, i.total_price, i.price_tiers_json
FROM cost_analysis_items i
INNER JOIN cost_analysis ca ON ca.id = i.cost_analysis_id
WHERE i.purchase_unit_price IS NOT NULL AND i.purchase_unit_price > 0
ORDER BY ca.id DESC, i.row_no
LIMIT @limit;", connection))
                {
                    command.Parameters.AddWithValue("@limit", limit <= 0 ? 200 : limit);
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            list.Add(new CostHistoryItem
                            {
                                AnalysisId = ReadInt(reader, "analysis_id"),
                                AnalysisNo = ReadString(reader, "analysis_no"),
                                CustomerName = ReadString(reader, "customer_name"),
                                ProjectName = ReadString(reader, "project_name"),
                                AnalysisDate = ReadString(reader, "analysis_date"),
                                CreatedAt = ReadString(reader, "created_at"),
                                No = ReadInt(reader, "row_no"),
                                MaterialCode = ReadString(reader, "material_code"),
                                MaterialName = ReadString(reader, "material_name"),
                                MaterialDescription = ReadString(reader, "material_description"),
                                Supplier = ReadString(reader, "supplier"),
                                BaseMaterialName = ReadString(reader, "base_material_name"),
                                MaterialVendor = ReadString(reader, "material_vendor"),
                                MaterialUnitPrice = ReadDecimal(reader, "material_unit_price"),
                                GramWeight = ReadString(reader, "gram_weight"),
                                ExpandedSize = ReadString(reader, "expanded_size"),
                                MaterialCost = ReadDecimal(reader, "material_cost"),
                                PrintingCost = ReadDecimal(reader, "printing_cost"),
                                PostProcessCost = ReadDecimal(reader, "post_process_cost"),
                                OtherCost = ReadDecimal(reader, "other_cost"),
                                PurchaseUnitPrice = ReadDecimal(reader, "purchase_unit_price"),
                                TotalQuantity = ReadDecimal(reader, "total_quantity"),
                                TotalPrice = ReadDecimal(reader, "total_price"),
                                PriceTiers = DeserializePriceTiers(ReadString(reader, "price_tiers_json"))
                            });
                        }
                    }
                }
            }

            return list;
        }

        private List<SavedCostAnalysisItem> GetItems(int analysisId)
        {
            var list = new List<SavedCostAnalysisItem>();
            using (var connection = new SQLiteConnection(DatabaseInitializer.ConnectionString))
            {
                connection.Open();
                using (var command = new SQLiteCommand(@"
SELECT row_no, material_code, material_name, material_description, supplier, base_material_name,
       material_vendor, material_unit_price, gram_weight, expanded_size, material_cost, printing_cost,
       post_process_cost, other_cost, purchase_unit_price, total_quantity, total_price, price_tiers_json
FROM cost_analysis_items
WHERE cost_analysis_id = @id
ORDER BY row_no, id;", connection))
                {
                    command.Parameters.AddWithValue("@id", analysisId);
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            list.Add(new SavedCostAnalysisItem
                            {
                                No = ReadInt(reader, "row_no"),
                                MaterialCode = ReadString(reader, "material_code"),
                                MaterialName = ReadString(reader, "material_name"),
                                MaterialDescription = ReadString(reader, "material_description"),
                                Supplier = ReadString(reader, "supplier"),
                                BaseMaterialName = ReadString(reader, "base_material_name"),
                                MaterialVendor = ReadString(reader, "material_vendor"),
                                MaterialUnitPrice = ReadDecimal(reader, "material_unit_price"),
                                GramWeight = ReadString(reader, "gram_weight"),
                                ExpandedSize = ReadString(reader, "expanded_size"),
                                MaterialCost = ReadDecimal(reader, "material_cost"),
                                PrintingCost = ReadDecimal(reader, "printing_cost"),
                                PostProcessCost = ReadDecimal(reader, "post_process_cost"),
                                OtherCost = ReadDecimal(reader, "other_cost"),
                                PurchaseUnitPrice = ReadDecimal(reader, "purchase_unit_price"),
                                TotalQuantity = ReadDecimal(reader, "total_quantity"),
                                TotalPrice = ReadDecimal(reader, "total_price"),
                                PriceTiers = DeserializePriceTiers(ReadString(reader, "price_tiers_json"))
                            });
                        }
                    }
                }
            }

            return list;
        }

        private static int InsertAnalysis(SQLiteConnection connection, SQLiteTransaction transaction, string analysisNo, CostAnalysisHeader header, DateTime now)
        {
            using (var command = new SQLiteCommand(@"
INSERT INTO cost_analysis (analysis_no, customer_name, project_name, analysis_date, tax_note, freight_note, status, remark, created_at, updated_at)
VALUES (@analysis_no, @customer_name, @project_name, @analysis_date, @tax_note, @freight_note, @status, @remark, @created_at, @updated_at);
SELECT last_insert_rowid();", connection, transaction))
            {
                command.Parameters.AddWithValue("@analysis_no", analysisNo);
                command.Parameters.AddWithValue("@customer_name", header.CustomerName ?? string.Empty);
                command.Parameters.AddWithValue("@project_name", header.ProjectName ?? string.Empty);
                command.Parameters.AddWithValue("@analysis_date", string.IsNullOrWhiteSpace(header.AnalysisDate) ? now.ToString("yyyy-MM-dd") : header.AnalysisDate);
                command.Parameters.AddWithValue("@tax_note", header.TaxNote ?? string.Empty);
                command.Parameters.AddWithValue("@freight_note", header.FreightNote ?? string.Empty);
                command.Parameters.AddWithValue("@status", "草稿");
                command.Parameters.AddWithValue("@remark", header.Remark ?? string.Empty);
                command.Parameters.AddWithValue("@created_at", now.ToString("yyyy-MM-dd HH:mm:ss"));
                command.Parameters.AddWithValue("@updated_at", now.ToString("yyyy-MM-dd HH:mm:ss"));
                return Convert.ToInt32((long)command.ExecuteScalar());
            }
        }

        private static void InsertItems(SQLiteConnection connection, SQLiteTransaction transaction, int analysisId, DataGridView grid)
        {
            foreach (DataGridViewRow row in grid.Rows)
            {
                if (row.IsNewRow || RowIsEmpty(row))
                {
                    continue;
                }

                using (var command = new SQLiteCommand(@"
INSERT INTO cost_analysis_items (
    cost_analysis_id, row_no, material_code, material_name, material_description, supplier,
    base_material_name, material_vendor, material_unit_price, gram_weight, expanded_size,
    material_cost, printing_cost, post_process_cost, other_cost, purchase_unit_price,
    total_quantity, total_price, price_tiers_json
) VALUES (
    @cost_analysis_id, @row_no, @material_code, @material_name, @material_description, @supplier,
    @base_material_name, @material_vendor, @material_unit_price, @gram_weight, @expanded_size,
    @material_cost, @printing_cost, @post_process_cost, @other_cost, @purchase_unit_price,
    @total_quantity, @total_price, @price_tiers_json
);", connection, transaction))
                {
                    command.Parameters.AddWithValue("@cost_analysis_id", analysisId);
                    command.Parameters.AddWithValue("@row_no", ReadCellInt(row, "No"));
                    command.Parameters.AddWithValue("@material_code", ReadCell(row, "MaterialCode"));
                    command.Parameters.AddWithValue("@material_name", ReadCell(row, "MaterialName"));
                    command.Parameters.AddWithValue("@material_description", ReadCell(row, "MaterialDescription"));
                    command.Parameters.AddWithValue("@supplier", ReadCell(row, "Supplier"));
                    command.Parameters.AddWithValue("@base_material_name", ReadCell(row, "BaseMaterialName"));
                    command.Parameters.AddWithValue("@material_vendor", ReadCell(row, "MaterialVendor"));
                    command.Parameters.AddWithValue("@material_unit_price", DbDecimal(ReadCellDecimal(row, "MaterialUnitPrice")));
                    command.Parameters.AddWithValue("@gram_weight", ReadCell(row, "GramWeight"));
                    command.Parameters.AddWithValue("@expanded_size", ReadCell(row, "ExpandedSize"));
                    command.Parameters.AddWithValue("@material_cost", DbDecimal(ReadCellDecimal(row, "MaterialCost")));
                    command.Parameters.AddWithValue("@printing_cost", DbDecimal(ReadCellDecimal(row, "PrintingCost")));
                    command.Parameters.AddWithValue("@post_process_cost", DbDecimal(ReadCellDecimal(row, "PostProcessCost")));
                    command.Parameters.AddWithValue("@other_cost", DbDecimal(ReadCellDecimal(row, "OtherCost")));
                    command.Parameters.AddWithValue("@purchase_unit_price", DbDecimal(ReadCellDecimal(row, "PurchaseUnitPrice")));
                    command.Parameters.AddWithValue("@total_quantity", DbDecimal(ReadCellDecimal(row, "TotalQuantity")));
                    command.Parameters.AddWithValue("@total_price", DbDecimal(ReadCellDecimal(row, "TotalPrice")));
                    command.Parameters.AddWithValue("@price_tiers_json", SerializePriceTiers(row.Tag as List<PriceTier>));
                    command.ExecuteNonQuery();
                }
            }
        }

        private static bool RowIsEmpty(DataGridViewRow row)
        {
            foreach (DataGridViewCell cell in row.Cells)
            {
                if (cell.OwningColumn != null && (cell.OwningColumn.Name == "Selected" || cell.OwningColumn.Name == "ValidationStatus"))
                {
                    continue;
                }

                if (cell.Value != null && !string.IsNullOrWhiteSpace(Convert.ToString(cell.Value)))
                {
                    return false;
                }
            }

            return true;
        }

        private static string ReadCell(DataGridViewRow row, string columnName)
        {
            var value = row.Cells[columnName].Value;
            return value == null ? string.Empty : Convert.ToString(value);
        }

        private static int? ReadCellInt(DataGridViewRow row, string columnName)
        {
            int value;
            return int.TryParse(ReadCell(row, columnName), out value) ? value : (int?)null;
        }

        private static decimal? ReadCellDecimal(DataGridViewRow row, string columnName)
        {
            decimal value;
            return decimal.TryParse(ReadCell(row, columnName), out value) ? value : (decimal?)null;
        }

        private static object DbDecimal(decimal? value)
        {
            return value.HasValue ? (object)value.Value : DBNull.Value;
        }

        private static string ReadString(SQLiteDataReader reader, string name)
        {
            return reader[name] == DBNull.Value ? string.Empty : Convert.ToString(reader[name]);
        }

        private static int ReadInt(SQLiteDataReader reader, string name)
        {
            return reader[name] == DBNull.Value ? 0 : Convert.ToInt32(reader[name]);
        }

        private static decimal? ReadDecimal(SQLiteDataReader reader, string name)
        {
            return reader[name] == DBNull.Value ? (decimal?)null : Convert.ToDecimal(reader[name]);
        }

        private static string SerializePriceTiers(List<PriceTier> tiers)
        {
            if (tiers == null || tiers.Count == 0)
            {
                return string.Empty;
            }

            var dto = new List<PriceTierDto>();
            foreach (var tier in tiers)
            {
                dto.Add(new PriceTierDto
                {
                    Label = tier.Label,
                    MinQuantity = tier.MinQuantity,
                    MaxQuantity = tier.MaxQuantity,
                    UnitPrice = tier.UnitPrice
                });
            }

            return new JavaScriptSerializer().Serialize(dto);
        }

        private static List<PriceTier> DeserializePriceTiers(string json)
        {
            var result = new List<PriceTier>();
            if (string.IsNullOrWhiteSpace(json))
            {
                return result;
            }

            try
            {
                var dto = new JavaScriptSerializer().Deserialize<List<PriceTierDto>>(json);
                if (dto == null)
                {
                    return result;
                }

                foreach (var tier in dto)
                {
                    result.Add(new PriceTier
                    {
                        Label = tier.Label,
                        MinQuantity = tier.MinQuantity,
                        MaxQuantity = tier.MaxQuantity,
                        UnitPrice = tier.UnitPrice
                    });
                }
            }
            catch
            {
                return new List<PriceTier>();
            }

            return result;
        }

        private sealed class PriceTierDto
        {
            public string Label { get; set; }
            public int? MinQuantity { get; set; }
            public int? MaxQuantity { get; set; }
            public decimal? UnitPrice { get; set; }
        }
    }

    internal sealed class CostAnalysisSummary
    {
        public int Id { get; set; }
        public string AnalysisNo { get; set; }
        public string CustomerName { get; set; }
        public string ProjectName { get; set; }
        public string CreatedAt { get; set; }
        public int ItemCount { get; set; }

        public string DisplayText
        {
            get
            {
                return AnalysisNo + "  " + CreatedAt + "  " + ItemCount + "条";
            }
        }
    }

    internal sealed class CostAnalysisHeader
    {
        public string AnalysisNo { get; set; }
        public string CustomerName { get; set; }
        public string ProjectName { get; set; }
        public string AnalysisDate { get; set; }
        public string TaxNote { get; set; }
        public string FreightNote { get; set; }
        public string Remark { get; set; }
    }

    internal sealed class SavedCostAnalysis
    {
        public CostAnalysisHeader Header { get; set; }
        public List<SavedCostAnalysisItem> Items { get; set; }
    }

    internal class SavedCostAnalysisItem
    {
        public int No { get; set; }
        public string MaterialCode { get; set; }
        public string MaterialName { get; set; }
        public string MaterialDescription { get; set; }
        public string Supplier { get; set; }
        public string BaseMaterialName { get; set; }
        public string MaterialVendor { get; set; }
        public decimal? MaterialUnitPrice { get; set; }
        public string GramWeight { get; set; }
        public string ExpandedSize { get; set; }
        public decimal? MaterialCost { get; set; }
        public decimal? PrintingCost { get; set; }
        public decimal? PostProcessCost { get; set; }
        public decimal? OtherCost { get; set; }
        public decimal? PurchaseUnitPrice { get; set; }
        public decimal? TotalQuantity { get; set; }
        public decimal? TotalPrice { get; set; }
        public List<PriceTier> PriceTiers { get; set; }
    }

    internal sealed class CostHistoryItem : SavedCostAnalysisItem
    {
        public int AnalysisId { get; set; }
        public string AnalysisNo { get; set; }
        public string CustomerName { get; set; }
        public string ProjectName { get; set; }
        public string AnalysisDate { get; set; }
        public string CreatedAt { get; set; }
    }
}
