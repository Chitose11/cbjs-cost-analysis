using System;
using System.Data.SQLite;

namespace CostAnalysis.App.Data
{
    internal sealed class OcrToolSettingsRepository
    {
        public OcrToolSettings Get()
        {
            using (var connection = new SQLiteConnection(DatabaseInitializer.ConnectionString))
            {
                connection.Open();
                using (var command = new SQLiteCommand(@"
SELECT poppler_directory, pdftotext_path, pdftoppm_path, tesseract_path, tesseract_language
FROM ocr_tool_settings
ORDER BY id DESC
LIMIT 1;", connection))
                using (var reader = command.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        return new OcrToolSettings
                        {
                            PopplerDirectory = ReadString(reader, "poppler_directory"),
                            PdftotextPath = ReadString(reader, "pdftotext_path"),
                            PdftoppmPath = ReadString(reader, "pdftoppm_path"),
                            TesseractPath = ReadString(reader, "tesseract_path"),
                            TesseractLanguage = ReadString(reader, "tesseract_language")
                        };
                    }
                }
            }

            return OcrToolSettings.Defaults();
        }

        public void Save(OcrToolSettings settings)
        {
            using (var connection = new SQLiteConnection(DatabaseInitializer.ConnectionString))
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    using (var clear = new SQLiteCommand("DELETE FROM ocr_tool_settings;", connection, transaction))
                    {
                        clear.ExecuteNonQuery();
                    }

                    using (var insert = new SQLiteCommand(@"
INSERT INTO ocr_tool_settings (
    poppler_directory, pdftotext_path, pdftoppm_path, tesseract_path, tesseract_language
) VALUES (
    @poppler_directory, @pdftotext_path, @pdftoppm_path, @tesseract_path, @tesseract_language
);", connection, transaction))
                    {
                        insert.Parameters.AddWithValue("@poppler_directory", settings.PopplerDirectory ?? string.Empty);
                        insert.Parameters.AddWithValue("@pdftotext_path", settings.PdftotextPath ?? string.Empty);
                        insert.Parameters.AddWithValue("@pdftoppm_path", settings.PdftoppmPath ?? string.Empty);
                        insert.Parameters.AddWithValue("@tesseract_path", settings.TesseractPath ?? string.Empty);
                        insert.Parameters.AddWithValue("@tesseract_language", string.IsNullOrWhiteSpace(settings.TesseractLanguage) ? "chi_sim+eng" : settings.TesseractLanguage.Trim());
                        insert.ExecuteNonQuery();
                    }

                    transaction.Commit();
                }
            }
        }

        private static string ReadString(SQLiteDataReader reader, string name)
        {
            return reader[name] == DBNull.Value ? string.Empty : Convert.ToString(reader[name]);
        }
    }

    internal sealed class OcrToolSettings
    {
        public string PopplerDirectory { get; set; }
        public string PdftotextPath { get; set; }
        public string PdftoppmPath { get; set; }
        public string TesseractPath { get; set; }
        public string TesseractLanguage { get; set; }

        public static OcrToolSettings Defaults()
        {
            return new OcrToolSettings
            {
                PopplerDirectory = string.Empty,
                PdftotextPath = string.Empty,
                PdftoppmPath = string.Empty,
                TesseractPath = string.Empty,
                TesseractLanguage = "chi_sim+eng"
            };
        }
    }
}
