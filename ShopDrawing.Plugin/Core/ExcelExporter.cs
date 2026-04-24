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

            // ────────────────────────────────────────────
            // SHEET 1: LỆNH SẢN XUẤT (A4 Portrait)
            // ────────────────────────────────────────────
            CreateProductionSheet(wb, report.FactoryOrders, headerStyle, dataStyle, sumStyle, sumIntegerStyle, computedStyle, titleStyle, wrapStyle);

            // ────────────────────────────────────────────
            // SHEET 2: QUẢN LÝ SPEC (A4 Portrait)
            // ────────────────────────────────────────────
            CreateSpecSheet(wb, report.FactoryOrders, headerStyle, dataStyle, titleStyle);

            // ────────────────────────────────────────────
            // SHEET 3: ĐẶT HÀNG PHỤ KIỆN (A4 Landscape)
            // ────────────────────────────────────────────
            CreateAccessorySheet(wb, report.AccessoryRows, headerStyle, dataStyle, titleStyle, wrapStyle);

            using (var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write))
            {
                wb.Write(fs);
            }
        }

        private void CreateProductionSheet(IWorkbook wb, List<ShopdrawingBomCalculator.FactoryOrderRow> orders,
            ICellStyle headerStyle, ICellStyle dataStyle, ICellStyle sumStyle, ICellStyle sumIntegerStyle, ICellStyle computedStyle, ICellStyle titleStyle, ICellStyle wrapStyle)
        {
            ISheet sh = wb.CreateSheet("Lệnh Sản Xuất");
            sh.DefaultRowHeightInPoints = 20;

            // A4 Portrait, Fit All Columns on One Page
            sh.PrintSetup.PaperSize = (short)PaperSize.A4;
            sh.PrintSetup.Landscape = false;
            sh.PrintSetup.FitWidth = 1;
            sh.PrintSetup.FitHeight = 0;
            sh.FitToPage = true;

            var titleRow = sh.CreateRow(0);
            titleRow.HeightInPoints = 25;
            var titleCell = titleRow.CreateCell(0);
            titleCell.SetCellValue("LỆNH SẢN XUẤT");
            titleCell.CellStyle = titleStyle;
            sh.AddMergedRegion(new CellRangeAddress(0, 0, 0, 8));

            var dateRow = sh.CreateRow(1);
            dateRow.CreateCell(0).SetCellValue($"Ngày xuất: {DateTime.Now:dd/MM/yyyy HH:mm}");

            string[] headers = { "STT", "ƯU TIÊN", "CẤU TẠO", "DÀY (mm)", "RỘNG (mm)", "DÀI (mm)", "SỐ LƯỢNG", "DIỆN TÍCH (m²)", "GHI CHÚ" };
            int headerRowIdx = 3;
            IRow hdr = sh.CreateRow(headerRowIdx);
            hdr.HeightInPoints = 22;
            for (int i = 0; i < headers.Length; i++)
            {
                SetCell(hdr, i, headers[i], headerStyle);
            }

            int r = headerRowIdx + 1;
            int dataStart = r;
            int stt = 1;
            foreach (var order in orders)
            {
                IRow dr = sh.CreateRow(r);
                SetCell(dr, 0, stt++, dataStyle);
                SetCell(dr, 1, order.Priority ?? "", dataStyle);
                SetCell(dr, 2, order.Spec ?? "", wrapStyle);
                SetCell(dr, 3, order.ThickMm, dataStyle);
                SetCell(dr, 4, order.WidthMm, dataStyle);
                SetCell(dr, 5, order.LengthMm, dataStyle);
                SetCell(dr, 6, order.Qty, dataStyle);
                
                SetFormulaCell(dr, 7, $"{CellRef(r, 4)}*{CellRef(r, 5)}/1000000*{CellRef(r, 6)}", computedStyle);
                
                SetCell(dr, 8, order.Note ?? "", wrapStyle);
                r++;
            }

            IRow fs = sh.CreateRow(r);
            SetCell(fs, 0, "TỔNG ĐẶT HÀNG", sumStyle);
            for(int i=1;i<=5;i++) SetCell(fs, i, "", sumStyle);
            sh.AddMergedRegion(new CellRangeAddress(r, r, 0, 5));
            
            if (r > dataStart)
            {
                SetFormulaCell(fs, 6, $"SUM({CellRef(dataStart, 6)}:{CellRef(r - 1, 6)})", sumIntegerStyle);
                SetFormulaCell(fs, 7, $"SUM({CellRef(dataStart, 7)}:{CellRef(r - 1, 7)})", sumStyle);
            }
            else
            {
                SetCell(fs, 6, 0, sumIntegerStyle);
                SetCell(fs, 7, 0, sumStyle);
            }
            SetCell(fs, 8, "", sumStyle);

            ApplyColumnWidths(sh, new[] { 5, 10, 20, 10, 10, 10, 10, 14, 25 });
            sh.CreateFreezePane(0, headerRowIdx + 1);
            SetZoom(sh, 90);
        }

        private void CreateSpecSheet(IWorkbook wb, List<ShopdrawingBomCalculator.FactoryOrderRow> orders, ICellStyle headerStyle, ICellStyle dataStyle, ICellStyle titleStyle)
        {
            ISheet sh = wb.CreateSheet("Quản lý Spec");
            sh.DefaultRowHeightInPoints = 20;

            // A4 Portrait
            sh.PrintSetup.PaperSize = (short)PaperSize.A4;
            sh.PrintSetup.Landscape = false;
            sh.PrintSetup.FitWidth = 1;
            sh.PrintSetup.FitHeight = 0;
            sh.FitToPage = true;

            var titleRow = sh.CreateRow(0);
            titleRow.HeightInPoints = 25;
            var titleCell = titleRow.CreateCell(0);
            titleCell.SetCellValue("BẢNG QUẢN LÝ SPEC");
            titleCell.CellStyle = titleStyle;
            sh.AddMergedRegion(new CellRangeAddress(0, 0, 0, 6));

            string[] headers = { "STT", "MÃ SP", "LOẠI PANEL", "ĐỘ DÀY (mm)", "NGÀM", "TÔN MẶT NGOÀI", "TÔN MẶT TRONG" };
            int headerRowIdx = 2;
            IRow hdr = sh.CreateRow(headerRowIdx);
            hdr.HeightInPoints = 22;
            for (int i = 0; i < headers.Length; i++)
            {
                SetCell(hdr, i, headers[i], headerStyle);
            }

            var uniqueSpecs = orders.Select(o => o.Spec).Where(s => !string.IsNullOrEmpty(s)).Distinct().OrderBy(s => s).ToList();

            int r = headerRowIdx + 1;
            int stt = 1;
            foreach (var spec in uniqueSpecs)
            {
                var parts = spec.Split('|').Select(p => p.Trim()).ToArray();
                string loai = parts.Length > 0 ? parts[0] : "";
                string day = parts.Length > 1 ? parts[1].Replace("mm", "").Trim() : "";
                string ngam = parts.Length > 2 ? parts[2] : "";
                string outFace = parts.Length > 3 ? parts[3] : "";
                string inFace = parts.Length > 4 ? parts[4] : "";

                IRow dr = sh.CreateRow(r);
                SetCell(dr, 0, stt++, dataStyle);
                SetCell(dr, 1, spec, dataStyle); // MÃ SP
                SetCell(dr, 2, loai, dataStyle); // LOẠI PANEL
                SetCell(dr, 3, day, dataStyle);  // ĐỘ DÀY
                SetCell(dr, 4, ngam, dataStyle); // NGÀM
                SetCell(dr, 5, outFace, dataStyle); // TÔN MẶT NGOÀI
                SetCell(dr, 6, inFace, dataStyle); // TÔN MẶT TRONG
                r++;
            }

            ApplyColumnWidths(sh, new[] { 5, 20, 15, 12, 15, 25, 25 });
            sh.CreateFreezePane(0, headerRowIdx + 1);
            SetZoom(sh, 90);
        }

        private void CreateAccessorySheet(IWorkbook wb, List<ShopdrawingAccessorySummaryRow> accessoryRows, ICellStyle headerStyle, ICellStyle dataStyle, ICellStyle titleStyle, ICellStyle wrapStyle)
        {
            ISheet sh = wb.CreateSheet("Đặt Hàng Phụ Kiện");
            sh.DefaultRowHeightInPoints = 20;

            // A4 Landscape
            sh.PrintSetup.PaperSize = (short)PaperSize.A4;
            sh.PrintSetup.Landscape = true;
            sh.PrintSetup.FitWidth = 1;
            sh.PrintSetup.FitHeight = 0;
            sh.FitToPage = true;

            var titleRow = sh.CreateRow(0);
            titleRow.HeightInPoints = 25;
            var titleCell = titleRow.CreateCell(0);
            titleCell.SetCellValue("ĐẶT HÀNG PHỤ KIỆN");
            titleCell.CellStyle = titleStyle;
            sh.AddMergedRegion(new CellRangeAddress(0, 0, 0, 7));

            string[] headers = { "STT", "HẠNG MỤC", "VỊ TRÍ / ỨNG DỤNG", "TÊN PHỤ KIỆN", "QUY CÁCH", "ĐVT", "SỐ LƯỢNG", "GHI CHÚ" };
            int headerRowIdx = 2;
            IRow hdr = sh.CreateRow(headerRowIdx);
            hdr.HeightInPoints = 22;
            for (int i = 0; i < headers.Length; i++)
            {
                SetCell(hdr, i, headers[i], headerStyle);
            }

            int r = headerRowIdx + 1;
            int stt = 1;
            foreach (var acc in accessoryRows)
            {
                IRow dr = sh.CreateRow(r);
                SetCell(dr, 0, stt++, dataStyle);
                SetCell(dr, 1, acc.CategoryScope, dataStyle);
                SetCell(dr, 2, acc.Application, dataStyle);
                SetCell(dr, 3, acc.Name, wrapStyle);
                
                string quyCach = string.Join(" - ", new[] { acc.Material, acc.Position }.Where(x => !string.IsNullOrEmpty(x)));
                SetCell(dr, 4, quyCach, dataStyle);
                
                string dvtMua = acc.Unit;
                double slMua = acc.BasisValue * acc.Factor;
                
                if (acc.Unit?.ToLower() == "m")
                {
                    dvtMua = "Cây";
                    slMua = Math.Ceiling(slMua / 6.0);
                }
                else if (acc.Unit?.ToLower() == "cái")
                {
                    dvtMua = "Hộp";
                }
                
                SetCell(dr, 5, dvtMua, dataStyle);
                SetCell(dr, 6, slMua, dataStyle); 
                SetCell(dr, 7, acc.Note, wrapStyle);
                r++;
            }

            ApplyColumnWidths(sh, new[] { 5, 15, 20, 30, 25, 10, 12, 30 });
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
