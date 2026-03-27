using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;
using ShopDrawing.Plugin.Core;

[assembly: ExtensionApplication(typeof(ShopDrawing.Plugin.ShopDrawingApp))]

namespace ShopDrawing.Plugin
{
    public class ShopDrawingApp : IExtensionApplication
    {
        public void Initialize()
        {
            InitLayersForDocument(Application.DocumentManager.MdiActiveDocument);
            EnsurePlotStyleForDocument(Application.DocumentManager.MdiActiveDocument);
            HookAnnotationScaleForDocument(Application.DocumentManager.MdiActiveDocument);

            Application.DocumentManager.DocumentCreated   += (_, e) =>
            {
                InitLayersForDocument(e.Document);
                EnsurePlotStyleForDocument(e.Document);
                HookAnnotationScaleForDocument(e.Document);
            };
            Application.DocumentManager.DocumentActivated += (_, e) =>
            {
                InitLayersForDocument(e.Document);
                EnsurePlotStyleForDocument(e.Document);
                HookAnnotationScaleForDocument(e.Document);
                // Cập nhật palette ngay khi chuyển sang document khác
                SyncScaleFromDocument(e.Document);
            };

            // Đợi Ribbon sẵn sàng rồi mới tạo Tab + Panel + Buttons
            Application.Idle += OnAppIdleCreateRibbon;
        }

        private static void OnAppIdleCreateRibbon(object? sender, EventArgs e)
        {
            // Unhook ngay — chỉ cần chạy 1 lần
            Application.Idle -= OnAppIdleCreateRibbon;

            try
            {
                UI.RibbonInitializer.CreateRibbon();
            }
            catch (System.Exception ex)
            {
                try
                {
                    Application.DocumentManager.MdiActiveDocument?.Editor
                        .WriteMessage($"\n⚠️ Ribbon: {ex.Message}");
                }
                catch { }
            }
        }

        public void Terminate() { }

        // ───────────────────────── Layer Init ─────────────────────────
        private static void InitLayersForDocument(Document? doc)
        {
            if (doc == null) return;
            try
            {
                using var tr = doc.Database.TransactionManager.StartTransaction();
                Commands.ShopDrawingCommands.BlockManager.EnsureLayers(tr);
                tr.Commit();
                doc.Editor.WriteMessage("\n✅ [ShopDrawing] Plugin loaded. Layer SD_* đã sẵn sàng.");
            }
            catch (System.Exception ex)
            {
                try { doc.Editor.WriteMessage($"\n⚠️ ShopDrawing: {ex.Message}"); } catch { }
            }
        }

        // ───────────────────── Annotation Scale Sync ─────────────────────
        private static void EnsurePlotStyleForDocument(Document? doc)
        {
            if (doc == null) return;
            try
            {
                PlotStyleInstaller.EnsureInstalled(doc);
            }
            catch (System.Exception ex)
            {
                try { doc.Editor.WriteMessage($"\nâš ï¸ PlotStyle: {ex.Message}"); } catch { }
            }
        }

        private static bool _hookRegistered = false;

        private static void HookAnnotationScaleForDocument(Document? doc)
        {
            if (doc == null || _hookRegistered) return;
            // Application.SystemVariableChanged fires khi Cannoscale đổi (sysvar = "CANNOSCALE")
            Application.SystemVariableChanged -= OnSystemVariableChanged;
            Application.SystemVariableChanged += OnSystemVariableChanged;
            _hookRegistered = true;
        }

        private static void OnSystemVariableChanged(object? sender, SystemVariableChangedEventArgs e)
        {
            if (!e.Name.Equals("CANNOSCALE", System.StringComparison.OrdinalIgnoreCase)) return;
            try
            {
                // Đọc scale hiện tại từ document sau khi event fire
                var doc = Application.DocumentManager.MdiActiveDocument;
                if (doc == null) return;
                var acScale = doc.Database.Cannoscale;
                if (acScale == null) return;
                string scaleName = acScale.Name;
                if (!scaleName.StartsWith("1:") && acScale.DrawingUnits > 0)
                    scaleName = $"1:{(int)(acScale.DrawingUnits / acScale.PaperUnits)}";
                Commands.ShopDrawingCommands.FireAnnotationScaleChanged(scaleName);
            }
            catch { }
        }

        /// <summary>
        /// Đọc scale hiện tại của document và sync vào plugin ngay lập tức.
        /// </summary>
        private static void SyncScaleFromDocument(Document? doc)
        {
            if (doc == null) return;
            try
            {
                var acScale = doc.Database.Cannoscale;
                if (acScale == null) return;
                string scaleName = acScale.Name;
                if (!scaleName.StartsWith("1:") && acScale.DrawingUnits > 0)
                    scaleName = $"1:{(int)(acScale.DrawingUnits / acScale.PaperUnits)}";
                Commands.ShopDrawingCommands.FireAnnotationScaleChanged(scaleName);
            }
            catch { }
        }
    }
}
