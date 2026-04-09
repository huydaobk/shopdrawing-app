using Microsoft.Data.Sqlite;
using ShopDrawing.Plugin.Models;
using System;
using System.Collections.Generic;

namespace ShopDrawing.Plugin.Data
{
    public class WasteRepository
    {
        private readonly string _connectionString;

        public WasteRepository(string dbPath)
        {
            // Khi chay trong AutoCAD, SQLitePCLRaw khong tu tim duoc native DLL.
            // Them thu muc chua e_sqlite3.dll vao DLL search path
            var assemblyDir = System.IO.Path.GetDirectoryName(
                System.Reflection.Assembly.GetExecutingAssembly().Location);
            if (string.IsNullOrEmpty(assemblyDir))
                assemblyDir = System.IO.Path.GetDirectoryName(dbPath) ?? @"c:\my_project\shopdrawing-app\public";

            var nativePath = System.IO.Path.Combine(assemblyDir, "runtimes", "win-x64", "native");
            if (System.IO.Directory.Exists(nativePath))
                SetDllDirectory(nativePath);

            _connectionString = $"Data Source={dbPath}";
            DatabaseSchema.Initialize(dbPath);
        }

        [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetDllDirectory(string lpPathName);

        public void AddPanel(WastePanel panel)
        {
            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();
                var command = connection.CreateCommand();
                command.CommandText = @"
                    INSERT INTO panels (
                        panel_code, width_mm, length_mm, thick_mm, panel_spec,
                        joint_left, joint_right, source_wall, project, status, source_type,
                        source_panel_x, source_panel_y)
                    VALUES (
                        @code, @width, @length, @thick, @spec,
                        @jLeft, @jRight, @sWall, @project, @status, @srcType,
                        @sourceX, @sourceY)
                ";
                command.Parameters.AddWithValue("@code", panel.PanelCode);
                command.Parameters.AddWithValue("@width", panel.WidthMm);
                command.Parameters.AddWithValue("@length", panel.LengthMm);
                command.Parameters.AddWithValue("@thick", panel.ThickMm);
                command.Parameters.AddWithValue("@spec", panel.PanelSpec);
                command.Parameters.AddWithValue("@jLeft", panel.JointLeft.ToString()[0].ToString());
                command.Parameters.AddWithValue("@jRight", panel.JointRight.ToString()[0].ToString());
                command.Parameters.AddWithValue("@sWall", panel.SourceWall);
                command.Parameters.AddWithValue("@project", panel.Project);
                command.Parameters.AddWithValue("@status", panel.Status);
                command.Parameters.AddWithValue("@srcType", panel.SourceType);
                command.Parameters.AddWithValue("@sourceX", (object?)panel.SourcePanelX ?? DBNull.Value);
                command.Parameters.AddWithValue("@sourceY", (object?)panel.SourcePanelY ?? DBNull.Value);
                command.ExecuteNonQuery();
            }
        }

        public List<WastePanel> FindMatches(
            double widthMm, double lengthMm, int thickMm,
            string spec, JointType jointLeft, JointType jointRight,
            double toleranceMm = 20.0)
        {
            var results = new List<WastePanel>();
            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();
                var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT * FROM panels
                    WHERE panel_spec = @spec
                      AND thick_mm = @thick
                      AND joint_left  = @jLeft
                      AND joint_right = @jRight
                      AND status = 'available'
                      AND width_mm  BETWEEN (@width  - @tol) AND (@width  + @tol)
                      AND length_mm BETWEEN (@length - @tol) AND (@length + @tol)
                    ORDER BY ABS(width_mm - @width) ASC, ABS(length_mm - @length) ASC
                ";
                command.Parameters.AddWithValue("@spec", spec);
                command.Parameters.AddWithValue("@thick", thickMm);
                command.Parameters.AddWithValue("@width", widthMm);
                command.Parameters.AddWithValue("@length", lengthMm);
                command.Parameters.AddWithValue("@tol", toleranceMm);
                command.Parameters.AddWithValue("@jLeft", jointLeft.ToString()[0].ToString());
                command.Parameters.AddWithValue("@jRight", jointRight.ToString()[0].ToString());

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        results.Add(MapFromReader(reader));
                    }
                }
            }
            return results;
        }

        /// <summary>
        /// T├¼m tß║Ñm theo spec + thickness, width >= neededWidth, length >= neededLength,
        /// sort theo width gß║ºn nhß║Ñt.
        /// Kh├┤ng filter theo joint (─æß╗â WasteMatcher kiß╗âm tra 2 chiß╗üu).
        /// </summary>
        public List<WastePanel> FindMatchesBySpec(string spec, int thickMm, double neededWidth, double neededLength = 0)
        {
            var results = new List<WastePanel>();
            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();
                var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT * FROM panels
                    WHERE panel_spec = @spec
                      AND thick_mm = @thick
                      AND width_mm >= @neededWidth
                      AND (length_mm + 5) >= @neededLength
                      AND status = 'available'
                    ORDER BY ABS(width_mm - @neededWidth) ASC
                ";
                command.Parameters.AddWithValue("@spec", spec);
                command.Parameters.AddWithValue("@thick", thickMm);
                command.Parameters.AddWithValue("@neededWidth", neededWidth);
                command.Parameters.AddWithValue("@neededLength", neededLength);

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        results.Add(MapFromReader(reader));
                    }
                }
            }
            return results;
        }

        public void MarkAsUsed(int id)
        {
            UpdateStatus(id, "used");
        }

        public void Discard(int id)
        {
            UpdateStatus(id, "discarded");
        }

        /// <summary>X├│a hß║│n tß║Ñm khß╗Åi DB (hard delete).</summary>
        public void HardDelete(int id)
        {
            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();
                var command = connection.CreateCommand();
                command.CommandText = "DELETE FROM panels WHERE id = @id";
                command.Parameters.AddWithValue("@id", id);
                command.ExecuteNonQuery();
            }
        }

        /// <summary>X├│a tß║Ñt cß║ú tß║Ñm lß║╗ thuß╗Öc mß╗Öt t╞░ß╗¥ng khi t╞░ß╗¥ng bß╗ï x├│a khß╗Åi bß║ún vß║╜.</summary>
        public int DeleteBySourceWall(string wallCode)
        {
            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();
                var command = connection.CreateCommand();
                command.CommandText = "DELETE FROM panels WHERE source_wall = @wall";
                command.Parameters.AddWithValue("@wall", wallCode);
                return command.ExecuteNonQuery();
            }
        }

        public int DeleteGeneratedBySourceWall(string wallCode)
        {
            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();
                var command = connection.CreateCommand();
                command.CommandText = @"
                    DELETE FROM panels
                    WHERE source_wall = @wall
                      AND (
                            panel_code LIKE @remPattern
                         OR panel_code LIKE @stepPattern
                         OR panel_code LIKE @openPattern
                         OR source_type = 'TRIM'
                      )
                ";
                command.Parameters.AddWithValue("@wall", wallCode);
                command.Parameters.AddWithValue("@remPattern", wallCode + "-%-REM");
                command.Parameters.AddWithValue("@stepPattern", wallCode + "-%-STEP");
                command.Parameters.AddWithValue("@openPattern", wallCode + "-%-OPEN");
                return command.ExecuteNonQuery();
            }
        }

        /// <summary>X├│a tß║Ñt cß║ú tß║Ñm lß║╗ c├│ panel_code bß║»t ─æß║ºu bß║▒ng prefix.</summary>
        public int DeleteByPanelCodePrefix(string prefix)
        {
            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();
                var command = connection.CreateCommand();
                command.CommandText = "DELETE FROM panels WHERE panel_code LIKE @prefix";
                command.Parameters.AddWithValue("@prefix", prefix + "%");
                return command.ExecuteNonQuery();
            }
        }

        public void UpdateStatus(int id, string status)
        {
            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();
                var command = connection.CreateCommand();
                command.CommandText = "UPDATE panels SET status = @status WHERE id = @id";
                command.Parameters.AddWithValue("@status", status);
                command.Parameters.AddWithValue("@id", id);
                command.ExecuteNonQuery();
            }
        }

        public void UpdatePanel(WastePanel panel)
        {
            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();
                var command = connection.CreateCommand();
                command.CommandText = @"
                    UPDATE panels
                    SET panel_code = @code,
                        width_mm = @width,
                        length_mm = @length,
                        thick_mm = @thick,
                        panel_spec = @spec,
                        joint_left = @jLeft,
                        joint_right = @jRight,
                        source_wall = @sWall,
                        project = @project,
                        status = @status,
                        source_type = @srcType,
                        source_panel_x = @sourceX,
                        source_panel_y = @sourceY
                    WHERE id = @id
                ";
                command.Parameters.AddWithValue("@id", panel.Id);
                command.Parameters.AddWithValue("@code", panel.PanelCode);
                command.Parameters.AddWithValue("@width", panel.WidthMm);
                command.Parameters.AddWithValue("@length", panel.LengthMm);
                command.Parameters.AddWithValue("@thick", panel.ThickMm);
                command.Parameters.AddWithValue("@spec", panel.PanelSpec);
                command.Parameters.AddWithValue("@jLeft", panel.JointLeft.ToString()[0].ToString());
                command.Parameters.AddWithValue("@jRight", panel.JointRight.ToString()[0].ToString());
                command.Parameters.AddWithValue("@sWall", panel.SourceWall);
                command.Parameters.AddWithValue("@project", panel.Project);
                command.Parameters.AddWithValue("@status", panel.Status);
                command.Parameters.AddWithValue("@srcType", panel.SourceType);
                command.Parameters.AddWithValue("@sourceX", (object?)panel.SourcePanelX ?? DBNull.Value);
                command.Parameters.AddWithValue("@sourceY", (object?)panel.SourcePanelY ?? DBNull.Value);
                command.ExecuteNonQuery();
            }
        }

        /// <summary>Lay tat ca (moi trang thai) de hien thi trong UI quan ly kho.</summary>
        public List<WastePanel> GetAll()
        {
            var results = new List<WastePanel>();
            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();
                var command = connection.CreateCommand();
                command.CommandText = "SELECT * FROM panels ORDER BY id DESC";
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                        results.Add(MapFromReader(reader));
                }
            }
            return results;
        }

        public List<WastePanel> GetAll(string statusFilter)
        {
            var results = new List<WastePanel>();
            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();
                var command = connection.CreateCommand();
                command.CommandText = "SELECT * FROM panels WHERE status = @status ORDER BY id DESC";
                command.Parameters.AddWithValue("@status", statusFilter);
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                        results.Add(MapFromReader(reader));
                }
            }
            return results;
        }

        // ΓöÇΓöÇΓöÇ Delivery Batch (Sß╗æ ─æß╗út giao h├áng) ΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇ

        /// <summary>Load to├án bß╗Ö batch map (panelKey ΓåÆ batchNo) ΓÇö gß╗ìi 1 lß║ºn khi load data.</summary>
        public Dictionary<string, int> GetAllBatches()
        {
            var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();
                var cmd = connection.CreateCommand();
                cmd.CommandText = "SELECT panel_key, batch_no FROM delivery_batch";
                using (var reader = cmd.ExecuteReader())
                    while (reader.Read())
                        result[reader.GetString(0)] = reader.GetInt32(1);
            }
            return result;
        }

        /// <summary>L╞░u hoß║╖c cß║¡p nhß║¡t sß╗æ ─æß╗út cho 1 panel key (UPSERT).</summary>
        public void SetBatch(string panelKey, int batchNo)
        {
            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();
                var cmd = connection.CreateCommand();
                cmd.CommandText = @"
                    INSERT INTO delivery_batch (panel_key, batch_no)
                    VALUES (@key, @no)
                    ON CONFLICT(panel_key) DO UPDATE SET batch_no = excluded.batch_no
                ";
                cmd.Parameters.AddWithValue("@key", panelKey);
                cmd.Parameters.AddWithValue("@no", batchNo);
                cmd.ExecuteNonQuery();
            }
        }

        /// <summary>X├│a batch assignment khi batchNo = 0 (ch╞░a g├ín).</summary>
        public void ClearBatch(string panelKey)
        {
            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();
                var cmd = connection.CreateCommand();
                cmd.CommandText = "DELETE FROM delivery_batch WHERE panel_key = @key";
                cmd.Parameters.AddWithValue("@key", panelKey);
                cmd.ExecuteNonQuery();
            }
        }


        private WastePanel MapFromReader(SqliteDataReader reader)
        {
            return new WastePanel
            {
                Id         = reader.GetInt32(reader.GetOrdinal("id")),
                PanelCode  = reader.GetString(reader.GetOrdinal("panel_code")),
                WidthMm    = reader.GetDouble(reader.GetOrdinal("width_mm")),
                LengthMm   = reader.GetDouble(reader.GetOrdinal("length_mm")),
                ThickMm    = reader.GetInt32(reader.GetOrdinal("thick_mm")),
                PanelSpec  = reader.GetString(reader.GetOrdinal("panel_spec")),
                JointLeft  = ParseJoint(reader.GetString(reader.GetOrdinal("joint_left"))),
                JointRight = ParseJoint(reader.GetString(reader.GetOrdinal("joint_right"))),
                SourceWall = SafeGetString(reader, "source_wall"),
                Project    = SafeGetString(reader, "project"),
                Status     = reader.GetString(reader.GetOrdinal("status")),
                SourceType = SafeGetString(reader, "source_type", "REM"),
                SourcePanelX = SafeGetNullableDouble(reader, "source_panel_x"),
                SourcePanelY = SafeGetNullableDouble(reader, "source_panel_y"),
            };
        }

        private static string SafeGetString(SqliteDataReader reader, string column, string defaultValue = "")
        {
            try
            {
                int ord = reader.GetOrdinal(column);
                return reader.IsDBNull(ord) ? defaultValue : reader.GetString(ord);
            }
            catch { return defaultValue; }
        }

        private static double? SafeGetNullableDouble(SqliteDataReader reader, string column)
        {
            try
            {
                int ord = reader.GetOrdinal(column);
                return reader.IsDBNull(ord) ? null : reader.GetDouble(ord);
            }
            catch { return null; }
        }

        private JointType ParseJoint(string j)
        {
            return j switch
            {
                "M" => JointType.Male,
                "F" => JointType.Female,
                _ => JointType.Cut
            };
        }
    }
}
