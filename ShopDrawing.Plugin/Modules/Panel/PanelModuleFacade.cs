using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Windows;
using Microsoft.Win32;
using ShopDrawing.Plugin.Core;
using ShopDrawing.Plugin.Data;
using ShopDrawing.Plugin.Models;
using ShopDrawing.Plugin.UI;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;

namespace ShopDrawing.Plugin.Modules.Panel
{
    internal sealed class PanelModuleFacade
    {
        private PaletteSet? _paletteSet;
        private WasteManagerDialog? _wasteDialog;
        private readonly QuickWallCommandService _quickWallCommandService = new();
        private readonly QuickCeilingCommandService _quickCeilingCommandService = new();
        private readonly PlaceDetailCommandService _placeDetailCommandService = new();

        public void TogglePalette()
        {
            if (_paletteSet == null || _paletteSet.IsDisposed)
            {
                _paletteSet = new PaletteSet("ShopDrawing Tools");
                _paletteSet.Style = PaletteSetStyles.ShowCloseButton
                    | PaletteSetStyles.ShowPropertiesMenu
                    | PaletteSetStyles.ShowAutoHideButton;
                _paletteSet.MinimumSize = new System.Drawing.Size(250, 400);
                _paletteSet.AddVisual("Tools", new MainPaletteControl());
            }

            _paletteSet.Visible = !_paletteSet.Visible;
        }

        public void ManageSpecs(SpecConfigManager specManager)
        {
            try
            {
                var dialog = new SpecManagerDialog(specManager);
                Application.ShowModalWindow(dialog);
            }
            catch (Exception ex)
            {
                string msg = ex.InnerException?.Message ?? ex.Message;
                Application.DocumentManager.MdiActiveDocument?.Editor.WriteMessage($"\n❌ Lỗi mở Spec Manager: {msg}");
            }
        }

        public void ExportBom(BomManager bomManager, WasteRepository? wasteRepo)
        {
            try
            {
                Document? doc = Application.DocumentManager.MdiActiveDocument;
                if (doc == null) return;

                var dlg = new Microsoft.Win32.SaveFileDialog
                {
                    Title = "Chọn nơi lưu file BOM Excel",
                    Filter = "Excel Files (*.xlsx)|*.xlsx",
                    FileName = $"ShopDrawing_BOM_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx",
                    InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
                };
                if (dlg.ShowDialog() != true) return;

                List<BomRow> rows;
                using (doc.LockDocument())
                using (var tr = doc.Database.TransactionManager.StartTransaction())
                {
                    rows = bomManager.ScanDocumentForPanels(tr, doc.Database);
                    tr.Commit();
                }

                if (rows.Count == 0)
                {
                    doc.Editor.WriteMessage("\n⚠️ Không tìm thấy tấm Panel nào để xuất Excel.");
                    return;
                }

                var exporter = new ExcelExporter();
                exporter.ExportFullBom(rows, wasteRepo, dlg.FileName);
                doc.Editor.WriteMessage($"\n✅ Xuất BOM thành công ra: {dlg.FileName}");
            }
            catch (Exception ex)
            {
                Application.DocumentManager.MdiActiveDocument?.Editor.WriteMessage($"\n❌ Lỗi xuất BOM: {ex.Message}");
            }
        }

        public void ShowWaste()
        {
            try
            {
                if (_wasteDialog != null && _wasteDialog.IsLoaded)
                {
                    _wasteDialog.Activate();
                    return;
                }

                _wasteDialog = new WasteManagerDialog();
                _wasteDialog.Closed += (_, _) => _wasteDialog = null;
                Application.ShowModelessWindow(_wasteDialog);
            }
            catch (Exception ex)
            {
                string msg = ex.InnerException?.Message ?? ex.Message;
                Application.DocumentManager.MdiActiveDocument?.Editor.WriteMessage($"\n❌ Lỗi mở Waste Manager: {msg}");
            }
        }

        public void CreateWallQuick(
            ShopDrawing.Plugin.Runtime.ShopDrawingRuntimeSettings settings,
            LayoutEngine layoutEngine,
            WasteRepository? wasteRepo,
            BlockManager blockManager,
            BomManager bomManager)
        {
            _quickWallCommandService.Run(settings, layoutEngine, wasteRepo, blockManager, bomManager);
        }

        public void CreateCeilingQuick(
            ShopDrawing.Plugin.Runtime.ShopDrawingRuntimeSettings settings,
            LayoutEngine layoutEngine,
            WasteRepository? wasteRepo,
            BlockManager blockManager,
            BomManager bomManager)
        {
            _quickCeilingCommandService.Run(settings, layoutEngine, wasteRepo, blockManager, bomManager);
        }

        public void PlaceDetail(BlockManager blockManager, DetailType detailType)
        {
            _placeDetailCommandService.Run(blockManager, detailType);
        }
    }
}
