using System;

using System.Collections.Generic;

using System.IO;

using System.Linq;

using NPOI.SS.UserModel;

using NPOI.SS.Util;

using NPOI.XSSF.UserModel;

using ShopDrawing.Plugin.Data;

using ShopDrawing.Plugin.Models;



namespace ShopDrawing.Plugin.Core

{

    public class ExcelExporter

    {

        /// <summary>Xuất BOM cũ (tương thích ngược).</summary>

        public void ExportBomToExcel(List<BomRow> rows, string filePath)

        {

            ExportFullBom(rows, null, filePath);

        }



        /// <summary>

        /// Xuất BOM đầy đủ 3 sheet:

        ///   Sheet 1: BOM CHI TIẾT

        ///   Sheet 2: HAO HỤT

        ///   Sheet 3: TỔNG HỢP + ĐẶT HÀNG NHÀ MÁY

        /// </summary>

        public void ExportFullBom(List<BomRow> bomRows, WasteRepository? wasteRepo, string filePath)

        {

            IWorkbook wb = new XSSFWorkbook();



            // === Styles ===

            var headerStyle = CreateHeaderStyle(wb);

            var dataStyle = CreateDataStyle(wb);

            var sumStyle = CreateSumStyle(wb);

            var titleStyle = CreateTitleStyle(wb);



            // ────────────────────────────────────────────

            // SHEET 1: BOM CHI TIẾT

            // ────────────────────────────────────────────

            CreateBomSheet(wb, bomRows, headerStyle, dataStyle, sumStyle, titleStyle);



            // ────────────────────────────────────────────

            // SHEET 2: HAO HỤT

            // ────────────────────────────────────────────

            List<WastePanel> allWaste = new();

            if (wasteRepo != null)

            {

                try { allWaste = wasteRepo.GetAll(); } catch (System.Exception ex)

            {

                ShopDrawing.Plugin.Core.PluginLogger.Error("Suppressed exception in ExcelExporter.cs", ex);

            }

            }

            CreateWasteSheet(wb, allWaste, headerStyle, dataStyle, sumStyle, titleStyle);



            // ────────────────────────────────────────────

            // SHEET 3: TỔNG HỢP + ĐẶT HÀNG NHÀ MÁY

            // ────────────────────────────────────────────

            CreateSummarySheet(wb, bomRows, allWaste, headerStyle, dataStyle, sumStyle, titleStyle);



            using (var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write))

            {

                wb.Write(fs);

            }

        }



        // ═══════════════════════════════════════════════

        //  SHEET 1: BOM CHI TIẾT

        // ═══════════════════════════════════════════════

        private void CreateBomSheet(IWorkbook wb, List<BomRow> rows,

            ICellStyle headerStyle, ICellStyle dataStyle, ICellStyle sumStyle, ICellStyle titleStyle)

        {

            ISheet sh = wb.CreateSheet("BOM Chi Tiết");



            // Title

            var titleRow = sh.CreateRow(0);

            var titleCell = titleRow.CreateCell(0);

            titleCell.SetCellValue("BẢNG KÊ VẬT TƯ TẤM PANEL (BOM)");

            titleCell.CellStyle = titleStyle;

            sh.AddMergedRegion(new CellRangeAddress(0, 0, 0, 7));



            // Date

            var dateRow = sh.CreateRow(1);

            dateRow.CreateCell(0).SetCellValue($"Ngày xuất: {DateTime.Now:dd/MM/yyyy HH:mm}");



            // Headers

            string[] headers = { "STT", "M? T?M", "C?U T?O", "R?NG (mm)", "D?I (mm)", "NG?M T/P", "S? L??NG", "DI?N T?CH (m?)", "TR?NG TH?I", "M? V?NG" };

            IRow hdr = sh.CreateRow(3);

            for (int i = 0; i < headers.Length; i++)

            {

                var c = hdr.CreateCell(i);

                c.SetCellValue(headers[i]);

                c.CellStyle = headerStyle;

            }



            // Data

            int r = 4;

            double totalArea = 0;

            int totalQty = 0;

            foreach (var row in rows)

            {

                IRow dr = sh.CreateRow(r);

                SetCell(dr, 0, r - 3, dataStyle);

                SetCell(dr, 1, string.IsNullOrWhiteSpace(row.DisplayId) ? row.Id : row.DisplayId, dataStyle);

                SetCell(dr, 2, row.Spec, dataStyle);

                SetCell(dr, 3, row.WidthMm, dataStyle);

                SetCell(dr, 4, row.LengthMm, dataStyle);

                SetCell(dr, 5, $"{row.JointLeft}/{row.JointRight}", dataStyle);

                SetCell(dr, 6, row.Qty, dataStyle);

                double area = row.AreaM2 * row.Qty;

                SetCell(dr, 7, Math.Round(area, 3), dataStyle);

                SetCell(dr, 8, row.Status, dataStyle);

                SetCell(dr, 9, row.WallCode, dataStyle);

                totalArea += area;

                totalQty += row.Qty;

                r++;

            }



            // Summary row

            IRow sr = sh.CreateRow(r);

            SetCell(sr, 0, "TỔNG", sumStyle);

            sh.AddMergedRegion(new CellRangeAddress(r, r, 0, 5));

            SetCell(sr, 6, totalQty, sumStyle);

            SetCell(sr, 7, Math.Round(totalArea, 3), sumStyle);



            AutoSize(sh, headers.Length);

        }



        // ═══════════════════════════════════════════════

        //  SHEET 2: HAO HỤT

        // ═══════════════════════════════════════════════

        private void CreateWasteSheet(IWorkbook wb, List<WastePanel> allWaste,

            ICellStyle headerStyle, ICellStyle dataStyle, ICellStyle sumStyle, ICellStyle titleStyle)

        {

            ISheet sh = wb.CreateSheet("Hao Hụt");



            // Title

            var titleRow = sh.CreateRow(0);

            var titleCell = titleRow.CreateCell(0);

            titleCell.SetCellValue("THỐNG KÊ HAO HỤT");

            titleCell.CellStyle = titleStyle;

            sh.AddMergedRegion(new CellRangeAddress(0, 0, 0, 7));



            // Headers

            string[] headers = { "STT", "M? T?M", "C?U T?O", "R?NG (mm)", "D?I (mm)", "DI?N T?CH (m?)", "NGU?N", "TR?NG TH?I", "M? V?NG" };

            IRow hdr = sh.CreateRow(2);

            for (int i = 0; i < headers.Length; i++)

            {

                var c = hdr.CreateCell(i);

                c.SetCellValue(headers[i]);

                c.CellStyle = headerStyle;

            }



            // Data — tất cả waste

            int r = 3;

            double totalDiscarded = 0;

            double totalAvailable = 0;

            foreach (var w in allWaste)

            {

                IRow dr = sh.CreateRow(r);

                double area = (w.WidthMm * w.LengthMm) / 1_000_000.0;

                SetCell(dr, 0, r - 2, dataStyle);

                SetCell(dr, 1, w.PanelCode, dataStyle);

                SetCell(dr, 2, w.PanelSpec, dataStyle);

                SetCell(dr, 3, w.WidthMm, dataStyle);

                SetCell(dr, 4, w.LengthMm, dataStyle);

                SetCell(dr, 5, Math.Round(area, 4), dataStyle);

                SetCell(dr, 6, w.SourceTypeDisplay, dataStyle);

                SetCell(dr, 7, w.StatusDisplay, dataStyle);

                SetCell(dr, 8, w.SourceWall, dataStyle);



                if (w.Status == "discarded") totalDiscarded += area;

                if (w.Status == "available") totalAvailable += area;

                r++;

            }



            // Summary

            r++;

            IRow s1 = sh.CreateRow(r);

            SetCell(s1, 0, "Tổng m² Đã bỏ:", sumStyle);

            sh.AddMergedRegion(new CellRangeAddress(r, r, 0, 4));

            SetCell(s1, 5, Math.Round(totalDiscarded, 3), sumStyle);



            IRow s2 = sh.CreateRow(r + 1);

            SetCell(s2, 0, "Tổng m² Sẵn sàng:", sumStyle);

            sh.AddMergedRegion(new CellRangeAddress(r + 1, r + 1, 0, 4));

            SetCell(s2, 5, Math.Round(totalAvailable, 3), sumStyle);



            AutoSize(sh, headers.Length);

        }



        // ═══════════════════════════════════════════════

        //  SHEET 3: TỔNG HỢP + ĐẶT HÀNG NHÀ MÁY

        // ═══════════════════════════════════════════════

        private void CreateSummarySheet(IWorkbook wb, List<BomRow> bomRows, List<WastePanel> allWaste,

            ICellStyle headerStyle, ICellStyle dataStyle, ICellStyle sumStyle, ICellStyle titleStyle)

        {

            ISheet sh = wb.CreateSheet("Tổng Hợp");



            // ── PHẦN A: TỔNG HỢP DỰ ÁN ──

            var titleRow = sh.CreateRow(0);

            var titleCell = titleRow.CreateCell(0);

            titleCell.SetCellValue("A. TỔNG HỢP DỰ ÁN");

            titleCell.CellStyle = titleStyle;

            sh.AddMergedRegion(new CellRangeAddress(0, 0, 0, 2));



            double totalPanelArea = bomRows.Sum(r => r.AreaM2 * r.Qty);

            int totalPanelQty = bomRows.Sum(r => r.Qty);

            double discardedArea = allWaste.Where(w => w.Status == "discarded")

                .Sum(w => (w.WidthMm * w.LengthMm) / 1_000_000.0);

            double availableArea = allWaste.Where(w => w.Status == "available")

                .Sum(w => (w.WidthMm * w.LengthMm) / 1_000_000.0);

            double wastePercent = totalPanelArea > 0 ? (discardedArea / totalPanelArea) * 100.0 : 0;



            // Step

            double stepArea = allWaste.Where(w => w.SourceType == "STEP")

                .Sum(w => (w.WidthMm * w.LengthMm) / 1_000_000.0);

            double openArea = allWaste.Where(w => w.SourceType == "OPEN")

                .Sum(w => (w.WidthMm * w.LengthMm) / 1_000_000.0);

            double trimArea = allWaste.Where(w => w.SourceType == "TRIM")

                .Sum(w => (w.WidthMm * w.LengthMm) / 1_000_000.0);

            double remArea = allWaste.Where(w => w.SourceType == "REM")

                .Sum(w => (w.WidthMm * w.LengthMm) / 1_000_000.0);



            var summaryData = new (string Label, string Value)[]

            {

                ("Tổng số tấm", $"{totalPanelQty} tấm"),

                ("Tổng m² panel", $"{totalPanelArea:F2} m²"),

                ("", ""),

                ("Tổng m² hao hụt (Đã bỏ)", $"{discardedArea:F3} m²"),

                ("TỶ LỆ HAO HỤT", $"{wastePercent:F1} %"),

                ("", ""),

                ("Chi tiết - Tấm lẻ (REM)", $"{remArea:F3} m²"),

                ("Chi tiết - Bậc thang (STEP)", $"{stepArea:F3} m²"),

                ("Chi tiết - Lỗ mở (OPEN)", $"{openArea:F3} m²"),

                ("Chi tiết - Cắt tận dụng (TRIM)", $"{trimArea:F3} m²"),

                ("", ""),

                ("Tấm lẻ còn dùng được", $"{allWaste.Count(w => w.Status == "available")} tấm ({availableArea:F3} m²)")

            };



            int r = 2;

            foreach (var (label, value) in summaryData)

            {

                if (string.IsNullOrEmpty(label)) { r++; continue; }

                IRow dr = sh.CreateRow(r);

                SetCell(dr, 0, label, dataStyle);

                SetCell(dr, 1, value, dataStyle);

                if (label == "TỶ LỆ HAO HỤT")

                {

                    dr.GetCell(0).CellStyle = sumStyle;

                    dr.GetCell(1).CellStyle = sumStyle;

                }

                r++;

            }



            // ── PHẦN B: ĐẶT HÀNG NHÀ MÁY ──

            r += 2;

            var factTitle = sh.CreateRow(r);

            var factCell = factTitle.CreateCell(0);

            factCell.SetCellValue("B. ĐẶT HÀNG NHÀ MÁY (gộp theo kích thước)");

            factCell.CellStyle = titleStyle;

            sh.AddMergedRegion(new CellRangeAddress(r, r, 0, 5));

            r++;



            // Headers

            string[] fHeaders = { "STT", "CẤU TẠO", "ĐỘ DÀY (mm)", "RỘNG (mm)", "DÀI (mm)", "SỐ LƯỢNG", "DIỆN TÍCH (m²)", "GHI CHÚ" };

            IRow fhdr = sh.CreateRow(r);

            for (int i = 0; i < fHeaders.Length; i++)

            {

                var c = fhdr.CreateCell(i);

                c.SetCellValue(fHeaders[i]);

                c.CellStyle = headerStyle;

            }

            r++;



            // Gộp tấm cùng Spec + Dims + chuẩn (non-cut)

            var factoryGroups = bomRows

                .GroupBy(b => new { b.Spec, b.WidthMm, b.LengthMm, b.ThickMm })

                .OrderBy(g => g.Key.Spec)

                .ThenByDescending(g => g.Key.LengthMm)

                .ThenBy(g => g.Key.WidthMm)

                .ToList();



            int stt = 1;

            double totalFactoryArea = 0;

            int totalFactoryQty = 0;

            foreach (var g in factoryGroups)

            {

                int qty = g.Sum(x => x.Qty);

                double area = (g.Key.WidthMm * g.Key.LengthMm) / 1_000_000.0 * qty;

                string note = "";



                // Ghi chú cắt nếu tấm không phải full

                bool hasCut = g.Any(x => x.Status == "✂ CẮT" || x.Status == "CẮT");

                bool hasReused = g.Any(x => x.Status.Contains("TÁI") || x.Status.Contains("♻"));

                if (hasCut) note += "Cắt tại công trường";

                if (hasReused) note += (note.Length > 0 ? " + " : "") + "Tận dụng kho";



                // Tường liên quan

                var walls = g.Select(x => x.WallCode).Where(w => !string.IsNullOrEmpty(w)).Distinct();

                if (walls.Any()) note += (note.Length > 0 ? " | " : "") + string.Join(",", walls);



                IRow dr = sh.CreateRow(r);

                SetCell(dr, 0, stt++, dataStyle);

                SetCell(dr, 1, g.Key.Spec, dataStyle);

                SetCell(dr, 2, g.Key.ThickMm, dataStyle);

                SetCell(dr, 3, g.Key.WidthMm, dataStyle);

                SetCell(dr, 4, g.Key.LengthMm, dataStyle);

                SetCell(dr, 5, qty, dataStyle);

                SetCell(dr, 6, Math.Round(area, 3), dataStyle);

                SetCell(dr, 7, note, dataStyle);



                totalFactoryArea += area;

                totalFactoryQty += qty;

                r++;

            }



            // Factory summary

            IRow fs = sh.CreateRow(r);

            SetCell(fs, 0, "TỔNG ĐẶT HÀNG", sumStyle);

            sh.AddMergedRegion(new CellRangeAddress(r, r, 0, 4));

            SetCell(fs, 5, totalFactoryQty, sumStyle);

            SetCell(fs, 6, Math.Round(totalFactoryArea, 3), sumStyle);



            AutoSize(sh, fHeaders.Length);

        }



        // ═══════════════════════════════════════════════

        //  HELPER METHODS

        // ═══════════════════════════════════════════════

        private void SetCell(IRow row, int col, object value, ICellStyle style)

        {

            var c = row.CreateCell(col);

            if (value is int iVal) c.SetCellValue(iVal);

            else if (value is double dVal) c.SetCellValue(dVal);

            else c.SetCellValue(value?.ToString() ?? "");

            c.CellStyle = style;

        }



        private void AutoSize(ISheet sh, int cols)

        {

            for (int i = 0; i < cols; i++)

            {

                sh.AutoSizeColumn(i);

                sh.SetColumnWidth(i, sh.GetColumnWidth(i) + 512);

            }

        }



        private ICellStyle CreateHeaderStyle(IWorkbook wb)

        {

            var s = wb.CreateCellStyle();

            var f = wb.CreateFont();

            f.IsBold = true;

            f.FontHeightInPoints = 11;

            s.SetFont(f);

            s.Alignment = HorizontalAlignment.Center;

            s.VerticalAlignment = VerticalAlignment.Center;

            s.FillForegroundColor = NPOI.HSSF.Util.HSSFColor.Grey25Percent.Index;

            s.FillPattern = FillPattern.SolidForeground;

            s.BorderBottom = BorderStyle.Thin;

            s.BorderTop = BorderStyle.Thin;

            s.BorderLeft = BorderStyle.Thin;

            s.BorderRight = BorderStyle.Thin;

            return s;

        }



        private ICellStyle CreateDataStyle(IWorkbook wb)

        {

            var s = wb.CreateCellStyle();

            s.BorderBottom = BorderStyle.Thin;

            s.BorderTop = BorderStyle.Thin;

            s.BorderLeft = BorderStyle.Thin;

            s.BorderRight = BorderStyle.Thin;

            s.VerticalAlignment = VerticalAlignment.Center;

            return s;

        }



        private ICellStyle CreateSumStyle(IWorkbook wb)

        {

            var s = wb.CreateCellStyle();

            var f = wb.CreateFont();

            f.IsBold = true;

            f.FontHeightInPoints = 11;

            s.SetFont(f);

            s.Alignment = HorizontalAlignment.Center;

            s.VerticalAlignment = VerticalAlignment.Center;

            s.FillForegroundColor = NPOI.HSSF.Util.HSSFColor.LightYellow.Index;

            s.FillPattern = FillPattern.SolidForeground;

            s.BorderBottom = BorderStyle.Thin;

            s.BorderTop = BorderStyle.Thin;

            s.BorderLeft = BorderStyle.Thin;

            s.BorderRight = BorderStyle.Thin;

            return s;

        }



        private ICellStyle CreateTitleStyle(IWorkbook wb)

        {

            var s = wb.CreateCellStyle();

            var f = wb.CreateFont();

            f.IsBold = true;

            f.FontHeightInPoints = 14;

            s.SetFont(f);

            s.Alignment = HorizontalAlignment.Left;

            return s;

        }

    }

}

