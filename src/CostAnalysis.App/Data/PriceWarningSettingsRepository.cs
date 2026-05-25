using System;
using System.Data.SQLite;

namespace CostAnalysis.App.Data
{
    internal sealed class PriceWarningSettingsRepository
    {
        public PriceWarningSettings Get()
        {
            using (var connection = new SQLiteConnection(DatabaseInitializer.ConnectionString))
            {
                connection.Open();
                using (var command = new SQLiteCommand(@"
SELECT same_supplier_yellow_rate, same_supplier_red_rate,
       lower_supplier_yellow_rate, lower_supplier_red_rate, history_months
FROM price_warning_settings
ORDER BY id DESC
LIMIT 1;", connection))
                using (var reader = command.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        return new PriceWarningSettings
                        {
                            SameSupplierYellowRate = ReadDecimal(reader, "same_supplier_yellow_rate", 0.0001m),
                            SameSupplierRedRate = ReadDecimal(reader, "same_supplier_red_rate", 0.10m),
                            LowerSupplierYellowRate = ReadDecimal(reader, "lower_supplier_yellow_rate", 0.03m),
                            LowerSupplierRedRate = ReadDecimal(reader, "lower_supplier_red_rate", 0.10m),
                            HistoryMonths = ReadInt(reader, "history_months", 0)
                        }.Normalize();
                    }
                }
            }

            return PriceWarningSettings.Defaults();
        }

        public void Save(PriceWarningSettings settings)
        {
            settings = (settings ?? PriceWarningSettings.Defaults()).Normalize();
            using (var connection = new SQLiteConnection(DatabaseInitializer.ConnectionString))
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    using (var clear = new SQLiteCommand("DELETE FROM price_warning_settings;", connection, transaction))
                    {
                        clear.ExecuteNonQuery();
                    }

                    using (var insert = new SQLiteCommand(@"
INSERT INTO price_warning_settings (
    same_supplier_yellow_rate, same_supplier_red_rate,
    lower_supplier_yellow_rate, lower_supplier_red_rate, history_months
) VALUES (
    @same_supplier_yellow_rate, @same_supplier_red_rate,
    @lower_supplier_yellow_rate, @lower_supplier_red_rate, @history_months
);", connection, transaction))
                    {
                        insert.Parameters.AddWithValue("@same_supplier_yellow_rate", settings.SameSupplierYellowRate);
                        insert.Parameters.AddWithValue("@same_supplier_red_rate", settings.SameSupplierRedRate);
                        insert.Parameters.AddWithValue("@lower_supplier_yellow_rate", settings.LowerSupplierYellowRate);
                        insert.Parameters.AddWithValue("@lower_supplier_red_rate", settings.LowerSupplierRedRate);
                        insert.Parameters.AddWithValue("@history_months", settings.HistoryMonths);
                        insert.ExecuteNonQuery();
                    }

                    transaction.Commit();
                }
            }
        }

        private static decimal ReadDecimal(SQLiteDataReader reader, string name, decimal defaultValue)
        {
            return reader[name] == DBNull.Value ? defaultValue : Convert.ToDecimal(reader[name]);
        }

        private static int ReadInt(SQLiteDataReader reader, string name, int defaultValue)
        {
            return reader[name] == DBNull.Value ? defaultValue : Convert.ToInt32(reader[name]);
        }
    }

    internal sealed class PriceWarningSettings
    {
        public decimal SameSupplierYellowRate { get; set; }
        public decimal SameSupplierRedRate { get; set; }
        public decimal LowerSupplierYellowRate { get; set; }
        public decimal LowerSupplierRedRate { get; set; }
        public int HistoryMonths { get; set; }

        public static PriceWarningSettings Defaults()
        {
            return new PriceWarningSettings
            {
                SameSupplierYellowRate = 0.0001m,
                SameSupplierRedRate = 0.10m,
                LowerSupplierYellowRate = 0.03m,
                LowerSupplierRedRate = 0.10m,
                HistoryMonths = 0
            };
        }

        public PriceWarningSettings Normalize()
        {
            if (SameSupplierYellowRate < 0) SameSupplierYellowRate = 0;
            if (SameSupplierRedRate < SameSupplierYellowRate) SameSupplierRedRate = SameSupplierYellowRate;
            if (LowerSupplierYellowRate < 0) LowerSupplierYellowRate = 0;
            if (LowerSupplierRedRate < LowerSupplierYellowRate) LowerSupplierRedRate = LowerSupplierYellowRate;
            if (HistoryMonths < 0) HistoryMonths = 0;
            return this;
        }
    }
}
