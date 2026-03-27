using Microsoft.Data.Sqlite;
using System.IO;

namespace ShopDrawing.Plugin.Data
{
    public static class DatabaseSchema
    {
        public static void Initialize(string dbPath)
        {
            // Đảm bảo thư mục tồn tại
            string? dir = Path.GetDirectoryName(dbPath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            using (var connection = new SqliteConnection($"Data Source={dbPath}"))
            {
                connection.Open();

                var command = connection.CreateCommand();
                command.CommandText = @"
                    CREATE TABLE IF NOT EXISTS panels (
                        id          INTEGER PRIMARY KEY AUTOINCREMENT,
                        panel_code  TEXT NOT NULL,
                        width_mm    REAL NOT NULL,
                        length_mm   REAL NOT NULL,
                        thick_mm    INTEGER NOT NULL,
                        panel_spec  TEXT NOT NULL,
                        joint_left  TEXT NOT NULL,   -- 'M' | 'F' | 'C'
                        joint_right TEXT NOT NULL,   -- 'M' | 'F' | 'C'
                        source_wall TEXT,
                        project     TEXT,
                        added_at    TEXT DEFAULT (datetime('now')),
                        status      TEXT DEFAULT 'available',
                        source_type TEXT DEFAULT 'REM'  -- REM | STEP | OPEN | TRIM
                    );
                ";
                command.ExecuteNonQuery();

                // Migration: thêm cột source_type nếu DB cũ chưa có
                try
                {
                    var migrate = connection.CreateCommand();
                    migrate.CommandText = "ALTER TABLE panels ADD COLUMN source_type TEXT DEFAULT 'REM'";
                    migrate.ExecuteNonQuery();
                }
                catch { /* Column already exists */ }

                // Table: delivery_batch — lưu số đợt giao hàng cho từng PanelId trong BOM Sản Xuất
                var batchCmd = connection.CreateCommand();
                batchCmd.CommandText = @"
                    CREATE TABLE IF NOT EXISTS delivery_batch (
                        panel_key  TEXT PRIMARY KEY,   -- PanelIds (vd: W13-01/Spec1)
                        batch_no   INTEGER DEFAULT 0   -- Số đợt giao (0 = chưa gán)
                    );
                ";
                batchCmd.ExecuteNonQuery();
            }
        }
    }
}
