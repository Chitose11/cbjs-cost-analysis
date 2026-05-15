using System;
using System.Data.SQLite;
using System.Security.Cryptography;
using System.Text;

namespace CostAnalysis.App.Data
{
    internal sealed class AiSettingsRepository
    {
        public AiSettings Get()
        {
            using (var connection = new SQLiteConnection(DatabaseInitializer.ConnectionString))
            {
                connection.Open();
                using (var command = new SQLiteCommand(@"
SELECT is_enabled, api_url, api_key_encrypted, model_name, timeout_seconds,
       confirm_before_call, allow_customer_name, allow_supplier_name, allow_price
FROM ai_settings
ORDER BY id DESC
LIMIT 1;", connection))
                using (var reader = command.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        return new AiSettings
                        {
                            IsEnabled = ReadBool(reader, "is_enabled"),
                            ApiUrl = ReadString(reader, "api_url"),
                            ApiKey = Unprotect(ReadString(reader, "api_key_encrypted")),
                            ModelName = ReadString(reader, "model_name"),
                            TimeoutSeconds = ReadInt(reader, "timeout_seconds", 60),
                            ConfirmBeforeCall = ReadBool(reader, "confirm_before_call"),
                            AllowCustomerName = ReadBool(reader, "allow_customer_name"),
                            AllowSupplierName = ReadBool(reader, "allow_supplier_name"),
                            AllowPrice = ReadBool(reader, "allow_price")
                        };
                    }
                }
            }

            return AiSettings.DeepSeekDefaults();
        }

        public void Save(AiSettings settings)
        {
            using (var connection = new SQLiteConnection(DatabaseInitializer.ConnectionString))
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    using (var clear = new SQLiteCommand("DELETE FROM ai_settings;", connection, transaction))
                    {
                        clear.ExecuteNonQuery();
                    }

                    using (var insert = new SQLiteCommand(@"
INSERT INTO ai_settings (
    is_enabled, api_url, api_key_encrypted, model_name, timeout_seconds,
    confirm_before_call, allow_customer_name, allow_supplier_name, allow_price
) VALUES (
    @is_enabled, @api_url, @api_key_encrypted, @model_name, @timeout_seconds,
    @confirm_before_call, @allow_customer_name, @allow_supplier_name, @allow_price
);", connection, transaction))
                    {
                        insert.Parameters.AddWithValue("@is_enabled", settings.IsEnabled ? 1 : 0);
                        insert.Parameters.AddWithValue("@api_url", settings.ApiUrl ?? string.Empty);
                        insert.Parameters.AddWithValue("@api_key_encrypted", Protect(settings.ApiKey));
                        insert.Parameters.AddWithValue("@model_name", settings.ModelName ?? string.Empty);
                        insert.Parameters.AddWithValue("@timeout_seconds", settings.TimeoutSeconds);
                        insert.Parameters.AddWithValue("@confirm_before_call", settings.ConfirmBeforeCall ? 1 : 0);
                        insert.Parameters.AddWithValue("@allow_customer_name", settings.AllowCustomerName ? 1 : 0);
                        insert.Parameters.AddWithValue("@allow_supplier_name", settings.AllowSupplierName ? 1 : 0);
                        insert.Parameters.AddWithValue("@allow_price", settings.AllowPrice ? 1 : 0);
                        insert.ExecuteNonQuery();
                    }

                    transaction.Commit();
                }
            }
        }

        private static string Protect(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            var bytes = Encoding.UTF8.GetBytes(value);
            var protectedBytes = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(protectedBytes);
        }

        private static string Unprotect(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            try
            {
                var bytes = Convert.FromBase64String(value);
                var unprotectedBytes = ProtectedData.Unprotect(bytes, null, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(unprotectedBytes);
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string ReadString(SQLiteDataReader reader, string name)
        {
            return reader[name] == DBNull.Value ? string.Empty : Convert.ToString(reader[name]);
        }

        private static int ReadInt(SQLiteDataReader reader, string name, int defaultValue)
        {
            return reader[name] == DBNull.Value ? defaultValue : Convert.ToInt32(reader[name]);
        }

        private static bool ReadBool(SQLiteDataReader reader, string name)
        {
            return reader[name] != DBNull.Value && Convert.ToInt32(reader[name]) == 1;
        }
    }

    internal sealed class AiSettings
    {
        public bool IsEnabled { get; set; }
        public string ApiUrl { get; set; }
        public string ApiKey { get; set; }
        public string ModelName { get; set; }
        public int TimeoutSeconds { get; set; }
        public bool ConfirmBeforeCall { get; set; }
        public bool AllowCustomerName { get; set; }
        public bool AllowSupplierName { get; set; }
        public bool AllowPrice { get; set; }

        public static AiSettings DeepSeekDefaults()
        {
            return new AiSettings
            {
                IsEnabled = false,
                ApiUrl = "https://api.deepseek.com",
                ApiKey = string.Empty,
                ModelName = "deepseek-v4-flash",
                TimeoutSeconds = 60,
                ConfirmBeforeCall = true,
                AllowCustomerName = false,
                AllowSupplierName = true,
                AllowPrice = true
            };
        }
    }
}
