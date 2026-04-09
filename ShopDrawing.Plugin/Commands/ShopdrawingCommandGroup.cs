using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Runtime;
using ShopDrawing.Plugin.Runtime;

[assembly: CommandClass(typeof(ShopDrawing.Plugin.Commands.ShopdrawingCommandGroup))]

namespace ShopDrawing.Plugin.Commands
{
    public sealed class ShopdrawingCommandGroup
    {
        private readonly ShopdrawingCommandActions _actions = new();

        [CommandMethod("SD_PANEL")]
        public void TogglePalette()
        {
            _actions.TogglePalette();
        }

        [CommandMethod("_SD_WALL_QUICK")]
        public void CreateWallQuick()
        {
            _actions.CreateWallQuick();
        }

        [CommandMethod("_SD_CEILING_QUICK")]
        public void CreateCeilingQuick()
        {
            _actions.CreateCeilingQuick();
        }

        [CommandMethod("_SD_DETAIL")]
        public void PlaceDetail()
        {
            _actions.PlaceDetail();
        }

        [CommandMethod("_SD_PICK_T_HANGER")]
        public void PickTHangerPoints()
        {
            _actions.PickTHangerPoints();
        }

        [CommandMethod("_SD_PICK_MUSHROOM_HANGER")]
        public void PickMushroomHangerPoints()
        {
            _actions.PickMushroomHangerPoints();
        }

        [CommandMethod("_SD_PICK_OUTSIDE_CORNER")]
        public void PickOutsideCornerMarkers()
        {
            _actions.PickOutsideCornerMarkers();
        }

        [CommandMethod("_SD_PICK_INSIDE_CORNER")]
        public void PickInsideCornerMarkers()
        {
            _actions.PickInsideCornerMarkers();
        }

        /// <summary>
        /// Diagnostic command: in thông tin DB kho tấm tận dụng ra command line.
        /// Dùng lệnh _SD_WASTE_DIAG trong AutoCAD để debug.
        /// </summary>
        [CommandMethod("_SD_WASTE_DIAG")]
        public void WasteDiagnostic()
        {
            var ed = Application.DocumentManager.MdiActiveDocument?.Editor;
            if (ed == null) return;

            var repo = ShopDrawingRuntimeServices.WasteRepo;
            var dbPath = ShopDrawingRuntimeServices.WasteDbPath;

            ed.WriteMessage($"\n=== SD WASTE DIAGNOSTIC ===");
            ed.WriteMessage($"\nDB Path: {dbPath}");
            ed.WriteMessage($"\nDB Exists: {System.IO.File.Exists(dbPath)}");

            if (repo == null)
            {
                ed.WriteMessage("\nWasteRepo: NULL - chưa khởi tạo được!");
                return;
            }

            try
            {
                var all = repo.GetAll();
                ed.WriteMessage($"\nTổng số tấm trong DB: {all.Count}");

                int avail    = all.Count(w => w.Status == "available");
                int used     = all.Count(w => w.Status == "used");
                int discard  = all.Count(w => w.Status == "discarded");

                ed.WriteMessage($"\n  - Sẵn sàng (available): {avail} tấm");
                ed.WriteMessage($"\n  - Đã dùng (used):       {used} tấm");
                ed.WriteMessage($"\n  - Đã bỏ (discarded):    {discard} tấm");

                double totalArea = all.Sum(w => w.WidthMm * w.LengthMm / 1_000_000.0);
                int zeroWidth    = all.Count(w => w.WidthMm <= 0);
                int zeroLength   = all.Count(w => w.LengthMm <= 0);

                ed.WriteMessage($"\nTổng diện tích: {totalArea:F3} m²");
                ed.WriteMessage($"\nTấm có Width=0: {zeroWidth}, Length=0: {zeroLength}");

                // In 5 tấm đầu tiên
                var sample = all.Take(5).ToList();
                foreach (var p in sample)
                {
                    ed.WriteMessage($"\n  [{p.Id}] {p.PanelCode} | W={p.WidthMm:F0} L={p.LengthMm:F0} T={p.ThickMm} | {p.Status} | {p.SourceType} | DienTich={p.DienTich:F3}m²");
                }
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\nLỗi khi đọc DB: {ex.Message}");
            }

            ed.WriteMessage($"\n=== END DIAGNOSTIC ===");
        }
    }
}
