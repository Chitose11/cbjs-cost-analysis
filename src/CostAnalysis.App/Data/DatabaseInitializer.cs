using System.Data.SQLite;
using System.IO;

namespace CostAnalysis.App.Data
{
    internal static class DatabaseInitializer
    {
        public static string ConnectionString
        {
            get
            {
                return "Data Source=" + AppPaths.DatabasePath + ";Version=3;";
            }
        }

        public static void Initialize()
        {
            if (!File.Exists(AppPaths.DatabasePath))
            {
                SQLiteConnection.CreateFile(AppPaths.DatabasePath);
            }

            using (var connection = new SQLiteConnection(ConnectionString))
            {
                connection.Open();
                Execute(connection, @"
CREATE TABLE IF NOT EXISTS suppliers (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    name TEXT NOT NULL,
    short_name TEXT,
    address TEXT,
    phone TEXT,
    fax TEXT,
    contact TEXT,
    remark TEXT
);");

                Execute(connection, @"
CREATE TABLE IF NOT EXISTS materials (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    name TEXT NOT NULL,
    aliases TEXT,
    category TEXT,
    vendor TEXT,
    spec TEXT,
    unit TEXT,
    tax_unit_price REAL,
    includes_freight INTEGER DEFAULT 0,
    effective_date TEXT,
    expire_date TEXT,
    remark TEXT
);");

                Execute(connection, @"
CREATE TABLE IF NOT EXISTS quote_templates (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    name TEXT NOT NULL,
    template_type TEXT,
    supplier_id INTEGER,
    header_keywords TEXT,
    header_row_rule TEXT,
    quantity_row_rule TEXT,
    data_start_rule TEXT,
    field_map_json TEXT,
    is_enabled INTEGER DEFAULT 1,
    usage_count INTEGER DEFAULT 0,
    last_used_at TEXT,
    remark TEXT
);");

                Execute(connection, @"
CREATE TABLE IF NOT EXISTS cost_analysis (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    analysis_no TEXT,
    customer_name TEXT,
    project_name TEXT,
    supplier_id INTEGER,
    analysis_date TEXT,
    tax_note TEXT,
    freight_note TEXT,
    status TEXT,
    remark TEXT,
    created_at TEXT NOT NULL,
    updated_at TEXT NOT NULL
);");

                Execute(connection, @"
CREATE TABLE IF NOT EXISTS cost_analysis_items (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    cost_analysis_id INTEGER NOT NULL,
    row_no INTEGER,
    material_code TEXT,
    material_name TEXT,
    material_description TEXT,
    supplier TEXT,
    base_material_name TEXT,
    material_vendor TEXT,
    material_unit_price REAL,
    gram_weight TEXT,
    expanded_size TEXT,
    material_cost REAL,
    printing_cost REAL,
    post_process_cost REAL,
    other_cost REAL,
    purchase_unit_price REAL,
    total_quantity REAL,
    total_price REAL
);");

                Execute(connection, @"
CREATE TABLE IF NOT EXISTS cost_analysis_item_meta (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    item_id INTEGER NOT NULL,
    quote_import_id INTEGER,
    source_sheet TEXT,
    source_row INTEGER,
    raw_text TEXT,
    confidence REAL,
    is_manual_modified INTEGER DEFAULT 0,
    change_log_json TEXT
);");

                Execute(connection, @"
CREATE TABLE IF NOT EXISTS ai_settings (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    is_enabled INTEGER DEFAULT 0,
    api_url TEXT,
    api_key_encrypted TEXT,
    model_name TEXT,
    timeout_seconds INTEGER DEFAULT 60,
    confirm_before_call INTEGER DEFAULT 1,
    allow_customer_name INTEGER DEFAULT 0,
    allow_supplier_name INTEGER DEFAULT 1,
    allow_price INTEGER DEFAULT 1
);");
            }
        }

        private static void Execute(SQLiteConnection connection, string sql)
        {
            using (var command = new SQLiteCommand(sql, connection))
            {
                command.ExecuteNonQuery();
            }
        }
    }
}
