using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NPOI.SS.UserModel;
using NPOI.SS.Util;
using NPOI.XSSF.UserModel;
using ShopDrawing.Plugin.Data;
using ShopDrawing.Plugin.Models;
using ShopDrawing.Plugin.Modules.Accessories;

namespace ShopDrawing.Plugin.Core
{
    public class ExcelExporter
    {
        public void ExportBomToExcel(List<BomRow> rows, string filePath)
        {
            var calc = new ShopdrawingBomCalculator();
            var report = calc.CalculateReport(rows, new List<WastePanel>(), new List<ShopdrawingAccessorySummaryRow>());
            ExportFullBom(report, filePath);
        }

        public void ExportFullBom(ShopdrawingBomCalculator.ShopdrawingReport report, string filePath)
        {
            IWorkbook wb = new XSSFWorkbook();

            // === Styles ===
            var titleStyle = CreateTitleStyle(wb);
            var headerStyle = CreateHeaderStyle(wb);
            var dataStyle = CreateDataStyle(wb);
            var sumStyle = CreateSumStyle(wb);
            var sumIntegerStyle = CreateSumIntegerStyle(wb);
            var computedStyle = CreateComputedStyle(wb);
            var wrapStyle = CreateWrapStyle(wb);
            var sectionStyle = CreateColoredSectionStyle(wb, IndexedColors.DarkBlue.Index);
            var accessorySectionStyle = CreateColoredSectionStyle(wb, IndexedColors.DarkGreen.Index);

            // ────────────────────────────────────────────
            // SHEET 1: BOM CHI TIẾT
            // ────────────────────────────────────────────
            CreateBomSheet(wb, report.BomRows, headerStyle, dataStyle, sumStyle, sumIntegerStyle, computedStyle, titleStyle);

            // ────────────────────────────────────────────
            // SHEET 2: HAO HỤT
            // ────────────────────────────────────────────
            CreateWasteSheet(wb, report.WastePanels, headerStyle, dataStyle, sumStyle, computedStyle, titleStyle);

            // ────────────────────────────────────────────
            // SHEET 3: TỔNG HỢP + ĐẶT HÀNG NHÀ MÁY
            // ────────────────────────────────────────────
            CreateSummarySheet(wb, report, headerStyle, dataStyle, sumStyle, sumIntegerStyle, computedStyle, titleStyle, sectionStyle);

            // ────────────────────────────────────────────
            // SHEET 4: PHỤ KIỆN
            // ────────────────────────────────────────────
            CreateAccessorySheet(wb, report.AccessoryRows, headerStyle, dataStyle, sumStyle, computedStyle, titleStyle, accessorySectionStyle, wrapStyle);

            using (var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write))
            {
                wb.Write(fs);
            }
        }

        private void CreateBomSheet(IWorkbook wb, List<BomRow> rows,
            ICellStyle headerStyle, ICellStyle dataStyle, ICellStyle sumStyle, ICellStyle sumIntegerStyle, ICellStyle computedStyle, ICellStyle titleStyle)
        {
            ISheet sh = wb.CreateSheet("BOM Chi Tiết");
            sh.DefaultRowHeightInPoints = 20;

            var titleRow = sh.CreateRow(0);
            titleRow.HeightInPoints = 25;
            var titleCell = titleRow.CreateCell(0);
            titleCell.SetCellValue("BẢNG KÊ VẬT TƯ TẤM PANEL (BOM)");
            titleCell.CellStyle = titleStyle;
            sh.AddMergedRegion(new CellRangeAddress(0, 0, 0, 7));

            var dateRow = sh.CreateRow(1);
            dateRow.CreateCell(0).SetCellValue($"Ngày xuất: {DateTime.Now:dd/MM/yyyy HH:mm}");

            string[] headers = { "STT", "MÃ TẤM", "CẤU TẠO", "RỘNG (mm)", "DÀI (mm)", "NGÀM T/P", "SỐ LƯỢNG", "DIỆN TÍCH (m²)", "TRẠNG THÁI", "MÃ VÁCH" };
            int headerRowIdx = 3;
            IRow hdr = sh.CreateRow(headerRowIdx);
            hdr.HeightInPoints = 22;
            for (int i = 0; i < headers.Length; i++)
            {
                SetCell(hdr, i, headers[i], headerStyle);
            }

            int r = headerRowIdx + 1;
            int dataStart = r;
            foreach (var row in rows)
            {
                IRow dr = sh.CreateRow(r);
                SetCell(dr, 0, r - headerRowIdx, dataStyle);
                SetCell(dr, 1, string.IsNullOrWhiteSpace(row.DisplayId) ? row.Id : row.DisplayId, dataStyle);
                SetCell(dr, 2, row.Spec, dataStyle);
                SetCell(dr, 3, row.WidthMm, dataStyle);
                SetCell(dr, 4, row.LengthMm, dataStyle);
                SetCell(dr, 5, $"{row.JointLeft}/{row.JointRight}", dataStyle);
                SetCell(dr, 6, row.Qty, dataStyle);
                
                // Use Excel formula for Area
                // Area = (Width * Length) / 1000000 * Qty
                SetFormulaCell(dr, 7, $"{CellRef(r, 3)}*{CellRef(r, 4)}/1000000*{CellRef(r, 6)}", computedStyle);
                
                SetCell(dr, 8, row.Status, dataStyle);
                SetCell(dr, 9, row.WallCode, dataStyle);
                r++;
            }

            // Summary row using formulas
            IRow sr = sh.CreateRow(r);
            SetCell(sr, 0, "TỔNG", sumStyle);
            for (int i = 1; i <= 5; i++) SetCell(sr, i, "", sumStyle);
            sh.AddMergedRegion(new CellRangeAddress(r, r, 0, 5));
            
            if (r > dataStart)
            {
                SetFormulaCell(sr, 6, $"SUM({CellRef(dataStart, 6)}:{CellRef(r - 1, 6)})", sumIntegerStyle);
                SetFormulaCell(sr, 7, $"SUM({CellRef(dataStart, 7)}:{CellRef(r - 1, 7)})", sumStyle);
            }
            else
            {
                SetCell(sr, 6, 0, sumIntegerStyle);
                SetCell(sr, 7, 0, sumStyle);
            }
            SetCell(sr, 8, "", sumStyle);
            SetCell(sr, 9, "", sumStyle);

            ApplyColumnWidths(sh, new[] { 6, 12, 12, 12, 12, 12, 10, 14, 15, 15 });
            sh.CreateFreezePane(0, headerRowIdx + 1);
            SetZoom(sh, 90);
        }

        private void CreateWasteSheet(IWorkbook wb, List<WastePanel> allWaste,
            ICellStyle headerStyle, ICellStyle dataStyle, ICellStyle sumStyle, ICellStyle computedStyle, ICellStyle titleStyle)
        {
            ISheet sh = wb.CreateSheet("Hao Hụt");
            sh.DefaultRowHeightInPoints = 20;

            var titleRow = sh.CreateRow(0);
            titleRow.HeightInPoints = 25;
            var titleCell = titleRow.CreateCell(0);
            titleCell.SetCellValue("THỐNG KÊ HAO HỤT");
            titleCell.CellStyle = titleStyle;
            sh.AddMergedRegion(new CellRangeAddress(0, 0, 0, 7));

            string[] headers = { "STT", "MÃ TẤM", "CẤU TẠO", "RỘNG (mm)", "DÀI (mm)", "DIỆN TÍCH (m²)", "NGUỒN", "TRẠNG THÁI", "MÃ VÁCH" };
            int headerRowIdx = 2;
            IRow hdr = sh.CreateRow(headerRowIdx);
            hdr.HeightInPoints = 22;
            for (int i = 0; i < headers.Length; i++)
            {
                SetCell(hdr, i, headers[i], headerStyle);
            }

            int r = headerRowIdx + 1;
            int dataStart = r;
            foreach (var w in allWaste)
            {
                IRow dr = sh.CreateRow(r);
                SetCell(dr, 0, r - headerRowIdx, dataStyle);
                SetCell(dr, 1, w.PanelCode, dataStyle);
                SetCell(dr, 2, w.PanelSpec, dataStyle);
                SetCell(dr, 3, w.WidthMm, dataStyle);
                SetCell(dr, 4, w.LengthMm, dataStyle);
                
                // Formula: Area = (Width * Length) / 1000000
                SetFormulaCell(dr, 5, $"{CellRef(r, 3)}*{CellRef(r, 4)}/1000000", computedStyle);
                
                SetCell(dr, 6, w.SourceTypeDisplay, dataStyle);
                SetCell(dr, 7, w.StatusDisplay, dataStyle);
                SetCell(dr, 8, w.SourceWall, dataStyle);
                r++;
            }

            // Note: Since calculating sum conditionally based on another column's string value 
            // is tricky with simple formulas, we can rely on SUMIF
            r++;
            IRow s1 = sh.CreateRow(r);
            SetCell(s1, 0, "Tổng m² Đã bỏ:", sumStyle);
            for(int i=1;i<=4;i++) SetCell(s1, i, "", sumStyle);
            sh.AddMergedRegion(new CellRangeAddress(r, r, 0, 4));
            
            if (r > dataStart + 1)
            {
                SetFormulaCell(s1, 5, $"SUMIF({CellRef(dataStart, 7)}:{CellRef(dataStart + allWaste.Count - 1, 7)},\"discarded\",{CellRef(dataStart, 5)}:{CellRef(dataStart + allWaste.Count - 1, 5)})", sumStyle);
            }
            else
            {
                SetCell(s1, 5, 0, sumStyle);
            }
            for(int i=6;i<=8;i++) SetCell(s1, i, "", sumStyle);

            IRow s2 = sh.CreateRow(r + 1);
            SetCell(s2, 0, "Tổng m² Sẵn sàng:", sumStyle);
            for(int i=1;i<=4;i++) SetCell(s2, i, "", sumStyle);
            sh.AddMergedRegion(new CellRangeAddress(r + 1, r + 1, 0, 4));
            
            if (r > dataStart + 1)
            {
                SetFormulaCell(s2, 5, $"SUMIF({CellRef(dataStart, 7)}:{CellRef(dataStart + allWaste.Count - 1, 7)},\"available\",{CellRef(dataStart, 5)}:{CellRef(dataStart + allWaste.Count - 1, 5)})", sumStyle);
            }
            else
            {
                SetCell(s2, 5, 0, sumStyle);
            }
            for(int i=6;i<=8;i++) SetCell(s2, i, "", sumStyle);

            ApplyColumnWidths(sh, new[] { 6, 12, 12, 12, 12, 14, 15, 15, 15 });
            sh.CreateFreezePane(0, headerRowIdx + 1);
            SetZoom(sh, 90);
        }

        private void CreateSummarySheet(IWorkbook wb, ShopdrawingBomCalculator.ShopdrawingReport report,
            ICellStyle headerStyle, ICellStyle dataStyle, ICellStyle sumStyle, ICellStyle sumIntegerStyle, ICellStyle computedStyle, ICellStyle titleStyle, ICellStyle sectionStyle)
        {
            ISheet sh = wb.CreateSheet("Tổng Hợp Panel");
            sh.DefaultRowHeightInPoints = 20;

            int r = 0;
            var titleRow = sh.CreateRow(r);
            titleRow.HeightInPoints = 25;
            var titleCell = titleRow.CreateCell(0);
            titleCell.SetCellValue("A. TỔNG HỢP DỰ ÁN");
            titleCell.CellStyle = titleStyle;
            sh.AddMergedRegion(new CellRangeAddress(r, r, 0, 2));
            r+=2;

            var summaryData = new (string Label, string Value)[]
            {
                ("Tổng số tấm", $"{report.Summary.TotalPanelQty} tấm"),
                ("Tổng m² panel", $"{report.Summary.TotalPanelArea:F2} m²"),
                ("", ""),
                ("Tổng m² hao hụt (Đã bỏ)", $"{report.Summary.DiscardedArea:F3} m²"),
                ("TỶ LỆ HAO HỤT", $"{report.Summary.WastePercent:F1} %"),
                ("", ""),
                ("Chi tiết - Tấm lẻ (REM)", $"{report.Summary.RemArea:F3} m²"),
                ("Chi tiết - Bậc thang (STEP)", $"{report.Summary.StepArea:F3} m²"),
                ("Chi tiết - Lỗ mở (OPEN)", $"{report.Summary.OpenArea:F3} m²"),
                ("Chi tiết - Cắt tận dụng (TRIM)", $"{report.Summary.TrimArea:F3} m²"),
                ("", ""),
                ("Tấm lẻ còn dùng được", $"{report.Summary.AvailableQty} tấm ({report.Summary.AvailableArea:F3} m²)")
            };

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

            r += 2;
            var factTitle = sh.CreateRow(r);
            factTitle.HeightInPoints = 22;
            var factCell = factTitle.CreateCell(0);
            factCell.SetCellValue("B. ĐẶT HÀNG NHÀ MÁY (gộp theo kích thước)");
            factCell.CellStyle = sectionStyle;
            for(int i=1;i<=7;i++) SetCell(factTitle, i, "", sectionStyle);
            sh.AddMergedRegion(new CellRangeAddress(r, r, 0, 7));
            r++;

            string[] fHeaders = { "STT", "CẤU TẠO", "ĐỘ DÀY (mm)", "RỘNG (mm)", "DÀI (mm)", "SỐ LƯỢNG", "DIỆN TÍCH (m²)", "GHI CHÚ" };
            int headerRowIdx = r;
            IRow fhdr = sh.CreateRow(headerRowIdx);
            fhdr.HeightInPoints = 22;
            for (int i = 0; i < fHeaders.Length; i++)
            {
                SetCell(fhdr, i, fHeaders[i], headerStyle);
            }
            r++;

            int stt = 1;
            int dataStart = r;
            foreach (var order in report.FactoryOrders)
            {
                IRow dr = sh.CreateRow(r);
                SetCell(dr, 0, stt++, dataStyle);
                SetCell(dr, 1, order.Spec, dataStyle);
                SetCell(dr, 2, order.ThickMm, dataStyle);
                SetCell(dr, 3, order.WidthMm, dataStyle);
                SetCell(dr, 4, order.LengthMm, dataStyle);
                SetCell(dr, 5, order.Qty, dataStyle);
                
                // Area = (Width * Length) / 1000000 * Qty
                SetFormulaCell(dr, 6, $"{CellRef(r, 3)}*{CellRef(r, 4)}/1000000*{CellRef(r, 5)}", computedStyle);
                
                SetCell(dr, 7, order.Note, dataStyle);
                r++;
            }

            IRow fs = sh.CreateRow(r);
            SetCell(fs, 0, "TỔNG ĐẶT HÀNG", sumStyle);
            for(int i=1;i<=4;i++) SetCell(fs, i, "", sumStyle);
            sh.AddMergedRegion(new CellRangeAddress(r, r, 0, 4));
            
            if (r > dataStart)
            {
                SetFormulaCell(fs, 5, $"SUM({CellRef(dataStart, 5)}:{CellRef(r - 1, 5)})", sumIntegerStyle);
                SetFormulaCell(fs, 6, $"SUM({CellRef(dataStart, 6)}:{CellRef(r - 1, 6)})", sumStyle);
            }
            else
            {
                SetCell(fs, 5, 0, sumIntegerStyle);
                SetCell(fs, 6, 0, sumStyle);
            }
            SetCell(fs, 7, "", sumStyle);

            ApplyColumnWidths(sh, new[] { 6, 20, 12, 12, 12, 12, 15, 35 });
            sh.CreateFreezePane(0, headerRowIdx + 1);
            SetZoom(sh, 90);
        }
        
        private void CreateAccessorySheet(IWorkbook wb, List<ShopdrawingAccessorySummaryRow> accessoryRows,
            ICellStyle headerStyle, ICellStyle dataStyle, ICellStyle sumStyle, ICellStyle computedStyle, ICellStyle titleStyle, ICellStyle sectionStyle, ICellStyle wrapStyle)
        {
            ISheet sh = wb.CreateSheet("Phụ kiện");
            sh.DefaultRowHeightInPoints = 20;

            int r = 0;
            var titleRow = sh.CreateRow(r);
            titleRow.HeightInPoints = 25;
            var titleCell = titleRow.CreateCell(0);
            titleCell.SetCellValue("BẢNG TỔNG HỢP PHỤ KIỆN");
            titleCell.CellStyle = titleStyle;
            sh.AddMergedRegion(new CellRangeAddress(r, r, 0, 5));
            r += 2;

            var factTitle = sh.CreateRow(r);
            factTitle.HeightInPoints = 22;
            var factCell = factTitle.CreateCell(0);
            factCell.SetCellValue("CHI TIẾT PHỤ KIỆN");
            factCell.CellStyle = sectionStyle;
            for(int i=1;i<=10;i++) SetCell(factTitle, i, "", sectionStyle);
            sh.AddMergedRegion(new CellRangeAddress(r, r, 0, 10));
            r++;

            string[] headers = { "STT", "HẠNG MỤC", "ỨNG DỤNG", "VẬT LIỆU", "TÊN PHỤ KIỆN", "QUY CÁCH", "ĐVT", "KHỐI LƯỢNG", "HỆ SỐ", "TỔNG", "GHI CHÚ" };
            int headerRowIdx = r;
            IRow hdr = sh.CreateRow(headerRowIdx);
            hdr.HeightInPoints = 22;
            for (int i = 0; i < headers.Length; i++)
            {
                SetCell(hdr, i, headers[i], headerStyle);
            }
            r++;

            int stt = 1;
            foreach (var acc in accessoryRows)
            {
                IRow dr = sh.CreateRow(r);
                SetCell(dr, 0, stt++, dataStyle);
                SetCell(dr, 1, acc.CategoryScope, dataStyle);
                SetCell(dr, 2, acc.Application, dataStyle);
                SetCell(dr, 3, acc.Material, dataStyle);
                SetCell(dr, 4, acc.Name, wrapStyle);
                SetCell(dr, 5, acc.Position, dataStyle);
                SetCell(dr, 6, acc.Unit, dataStyle);
                SetCell(dr, 7, acc.BasisValue, computedStyle);
                SetCell(dr, 8, acc.Factor, computedStyle);
                
                // Final = Basis * Factor
                SetFormulaCell(dr, 9, $"{CellRef(r, 7)}*{CellRef(r, 8)}", computedStyle);
                
                SetCell(dr, 10, acc.Note, dataStyle);
                r++;
            }

            ApplyColumnWidths(sh, new[] { 6, 12, 15, 12, 35, 15, 8, 12, 10, 12, 30 });
            sh.CreateFreezePane(0, headerRowIdx + 1);
            SetZoom(sh, 90);
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

        private void SetFormulaCell(IRow row, int col, string formula, ICellStyle style)
        {
            var cell = row.CreateCell(col);
            cell.SetCellFormula(formula);
            cell.CellStyle = style;
        }

        private string CellRef(int rowIdx, int colIdx)
        {
            return new CellReference(rowIdx, colIdx).FormatAsString();
        }

        private void ApplyColumnWidths(ISheet sheet, int[] widths)
        {
            for (int i = 0; i < widths.Length; i++)
            {
                sheet.SetColumnWidth(i, widths[i] * 256);
            }
        }

        private void SetZoom(ISheet sheet, int scale)
        {
            if (sheet is XSSFSheet xssfSheet)
            {
                // NPOI takes a numerator/denominator for zoom, effectively zoom = (num/den)*100
                // We simplify by just sending scale and 100.
                xssfSheet.SetZoom(scale, 100);
            }
        }

        private ICellStyle CreateHeaderStyle(IWorkbook wb)
        {
            var s = wb.CreateCellStyle();
            var f = wb.CreateFont();
            f.IsBold = true;
            f.FontHeightInPoints = 11;
            f.FontName = "Arial";
            s.SetFont(f);
            s.Alignment = HorizontalAlignment.Center;
            s.VerticalAlignment = VerticalAlignment.Center;
            s.FillForegroundColor = NPOI.HSSF.Util.HSSFColor.Grey25Percent.Index;
            s.FillPattern = FillPattern.SolidForeground;
            SetBorders(s);
            return s;
        }

        private ICellStyle CreateDataStyle(IWorkbook wb)
        {
            var s = wb.CreateCellStyle();
            var f = wb.CreateFont();
            f.FontName = "Arial";
            f.FontHeightInPoints = 11;
            s.SetFont(f);
            s.VerticalAlignment = VerticalAlignment.Center;
            SetBorders(s);
            return s;
        }

        private ICellStyle CreateWrapStyle(IWorkbook wb)
        {
            var s = CreateDataStyle(wb);
            s.WrapText = true;
            return s;
        }

        private ICellStyle CreateComputedStyle(IWorkbook wb)
        {
            var s = CreateDataStyle(wb);
            s.DataFormat = wb.CreateDataFormat().GetFormat("#,##0.00");
            s.FillForegroundColor = NPOI.HSSF.Util.HSSFColor.LightTurquoise.Index;
            s.FillPattern = FillPattern.SolidForeground;
            return s;
        }

        private ICellStyle CreateSumStyle(IWorkbook wb)
        {
            var s = CreateDataStyle(wb);
            var f = wb.CreateFont();
            f.IsBold = true;
            f.FontName = "Arial";
            f.FontHeightInPoints = 11;
            s.SetFont(f);
            s.Alignment = HorizontalAlignment.Center;
            s.DataFormat = wb.CreateDataFormat().GetFormat("#,##0.00");
            s.FillForegroundColor = NPOI.HSSF.Util.HSSFColor.LightYellow.Index;
            s.FillPattern = FillPattern.SolidForeground;
            return s;
        }

        private ICellStyle CreateSumIntegerStyle(IWorkbook wb)
        {
            var s = CreateSumStyle(wb);
            s.DataFormat = wb.CreateDataFormat().GetFormat("#,##0");
            return s;
        }

        private ICellStyle CreateTitleStyle(IWorkbook wb)
        {
            var s = wb.CreateCellStyle();
            var f = wb.CreateFont();
            f.IsBold = true;
            f.FontHeightInPoints = 14;
            f.FontName = "Arial";
            s.SetFont(f);
            s.Alignment = HorizontalAlignment.Left;
            return s;
        }

        private ICellStyle CreateColoredSectionStyle(IWorkbook wb, short colorIndex)
        {
            var s = wb.CreateCellStyle();
            var f = wb.CreateFont();
            f.IsBold = true;
            f.Color = NPOI.HSSF.Util.HSSFColor.White.Index;
            f.FontName = "Arial";
            f.FontHeightInPoints = 12;
            s.SetFont(f);
            s.FillForegroundColor = colorIndex;
            s.FillPattern = FillPattern.SolidForeground;
            s.VerticalAlignment = VerticalAlignment.Center;
            SetBorders(s);
            return s;
        }

        private void SetBorders(ICellStyle s)
        {
            s.BorderBottom = BorderStyle.Thin;
            s.BorderTop = BorderStyle.Thin;
            s.BorderLeft = BorderStyle.Thin;
            s.BorderRight = BorderStyle.Thin;
        }
    }
}
