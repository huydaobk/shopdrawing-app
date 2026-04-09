using Microsoft.Data.Sqlite;
using System.IO;

namespace ShopDrawing.Plugin.Data
{
    public static class DatabaseSchema
    {
        public static void Initialize(string dbPath)
        {
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
                        id             INTEGER PRIMARY KEY AUTOINCREMENT,
                        panel_code     TEXT NOT NULL,
                        width_mm       REAL NOT NULL,
                        length_mm      REAL NOT NULL,
                        thick_mm       INTEGER NOT NULL,
                        panel_spec     TEXT NOT NULL,
                        joint_left     TEXT NOT NULL,
                        joint_right    TEXT NOT NULL,
                        source_wall    TEXT,
                        project        TEXT,
                        source_panel_x REAL,
                        source_panel_y REAL,
                        added_at       TEXT DEFAULT (datetime('now')),
                        status         TEXT DEFAULT 'available',
                        source_type    TEXT DEFAULT 'REM'
                    );
                ";
                command.ExecuteNonQuery();

                TryAddColumn(connection, "ALTER TABLE panels ADD COLUMN source_type TEXT DEFAULT 'REM'");
                TryAddColumn(connection, "ALTER TABLE panels ADD COLUMN source_panel_x REAL");
                TryAddColumn(connection, "ALTER TABLE panels ADD COLUMN source_panel_y REAL");

                var batchCmd = connection.CreateCommand();
                batchCmd.CommandText = @"
                    CREATE TABLE IF NOT EXISTS delivery_batch (
                        panel_key  TEXT PRIMARY KEY,
                        batch_no   INTEGER DEFAULT 0
                    );
                ";
                batchCmd.ExecuteNonQuery();
            }
        }

        private static void TryAddColumn(SqliteConnection connection, string sql)
        {
            try
            {
                var migrate = connection.CreateCommand();
                migrate.CommandText = sql;
                migrate.ExecuteNonQuery();
            }
            catch (System.Exception ex)
            {
                ShopDrawing.Plugin.Core.PluginLogger.Warn("Suppressed exception: " + ex.Message);
            }
        }
    }
}
