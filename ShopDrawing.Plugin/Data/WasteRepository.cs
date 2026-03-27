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
                    INSERT INTO panels (panel_code, width_mm, length_mm, thick_mm, panel_spec, joint_left, joint_right, source_wall, project, status, source_type)
                    VALUES (@code, @width, @length, @thick, @spec, @jLeft, @jRight, @sWall, @project, @status, @srcType)
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
        /// Tìm tấm theo spec + thickness, width >= neededWidth, length >= neededLength,
        /// sort theo width gần nhất.
        /// Không filter theo joint (để WasteMatcher kiểm tra 2 chiều).
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

        /// <summary>Xóa hẳn tấm khỏi DB (hard delete).</summary>
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

        /// <summary>Xóa tất cả tấm lẻ thuộc một tường khi tường bị xóa khỏi bản vẽ.</summary>
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

        /// <summary>Xóa tất cả tấm lẻ có panel_code bắt đầu bằng prefix.</summary>
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

        // ─── Delivery Batch (Số đợt giao hàng) ────────────────────

        /// <summary>Load toàn bộ batch map (panelKey → batchNo) — gọi 1 lần khi load data.</summary>
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

        /// <summary>Lưu hoặc cập nhật số đợt cho 1 panel key (UPSERT).</summary>
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

        /// <summary>Xóa batch assignment khi batchNo = 0 (chưa gán).</summary>
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
            var panel = new WastePanel
            {
                Id = reader.GetInt32(0),
                PanelCode = reader.GetString(1),
                WidthMm = reader.GetDouble(2),
                LengthMm = reader.GetDouble(3),
                ThickMm = reader.GetInt32(4),
                PanelSpec = reader.GetString(5),
                JointLeft = ParseJoint(reader.GetString(6)),
                JointRight = ParseJoint(reader.GetString(7)),
                SourceWall = reader.IsDBNull(8) ? "" : reader.GetString(8),
                Project = reader.IsDBNull(9) ? "" : reader.GetString(9),
                Status = reader.GetString(11)
            };
            // source_type column (index 12) — may not exist in old DBs
            try { if (!reader.IsDBNull(12)) panel.SourceType = reader.GetString(12); }
            catch { /* old schema without source_type */ }
            return panel;
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
