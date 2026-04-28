using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NPOI.SS.UserModel;
using NPOI.SS.Util;
using NPOI.XSSF.UserModel;
using ShopDrawing.Plugin.Commands;
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
            var profile = new ProjectProfileManager().LoadOrDefault();
            string projectName = string.IsNullOrWhiteSpace(profile.ProjectName) ? "Chưa khai báo" : profile.ProjectName;
            string projectAddress = string.IsNullOrWhiteSpace(profile.ProjectAddress) ? "Chưa khai báo" : profile.ProjectAddress;

            IWorkbook wb = new XSSFWorkbook();

            // === Styles ===
            var titleStyle = CreateTitleStyle(wb);
            var metadataStyle = CreateMetadataStyle(wb);
            var headerStyle = CreateHeaderStyle(wb);
            var dataStyle = CreateDataStyle(wb);
            var sumStyle = CreateSumStyle(wb);
            var sumIntegerStyle = CreateSumIntegerStyle(wb);
            var computedStyle = CreateComputedStyle(wb);
            var wrapStyle = CreateWrapStyle(wb);

            // ────────────────────────────────────────────
            // SHEET 1: LỆNH SẢN XUẤT (A4 Portrait)
            // ────────────────────────────────────────────
            CreateProductionSheet(wb, report.FactoryOrders, headerStyle, dataStyle, sumStyle, sumIntegerStyle, computedStyle, titleStyle, wrapStyle, metadataStyle, projectName, projectAddress);

            // ────────────────────────────────────────────
            // SHEET 2: QUẢN LÝ SPEC (A4 Portrait)
            // ────────────────────────────────────────────
            CreateSpecSheet(wb, report.FactoryOrders, headerStyle, dataStyle, titleStyle, metadataStyle, projectName, projectAddress);

            // ────────────────────────────────────────────
            // SHEET 3: ĐẶT HÀNG PHỤ KIỆN (A4 Landscape)
            // ────────────────────────────────────────────
            CreateAccessorySheet(wb, report.AccessoryRows, headerStyle, dataStyle, titleStyle, wrapStyle, sumStyle, sumIntegerStyle, metadataStyle, projectName, projectAddress);

            using (var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write))
            {
                wb.Write(fs);
            }
        }

        private void CreateProductionSheet(IWorkbook wb, List<ShopdrawingBomCalculator.FactoryOrderRow> orders,
            ICellStyle headerStyle, ICellStyle dataStyle, ICellStyle sumStyle, ICellStyle sumIntegerStyle, ICellStyle computedStyle, ICellStyle titleStyle, ICellStyle wrapStyle, ICellStyle metadataStyle, string projectName, string projectAddress)
        {
            ISheet sh = wb.CreateSheet("Lệnh Sản Xuất");
            sh.DefaultRowHeightInPoints = 20;

            // A4 Portrait, Fit All Columns on One Page
            sh.PrintSetup.PaperSize = 9; // 9 = A4
            sh.PrintSetup.Landscape = false;
            sh.PrintSetup.FitWidth = 1;
            sh.PrintSetup.FitHeight = 0;
            sh.FitToPage = true;

            var titleRow = sh.CreateRow(0);
            titleRow.HeightInPoints = 30;
            var titleCell = titleRow.CreateCell(0);
            titleCell.SetCellValue("LỆNH SẢN XUẤT");
            titleCell.CellStyle = titleStyle;
            sh.AddMergedRegion(new CellRangeAddress(0, 0, 0, 9));

            var projectRow = sh.CreateRow(1);
            projectRow.HeightInPoints = 24;
            var projectCell = projectRow.CreateCell(0);
            projectCell.SetCellValue($"Dự án: {projectName}");
            projectCell.CellStyle = metadataStyle;
            sh.AddMergedRegion(new CellRangeAddress(1, 1, 0, 9));

            var addressRow = sh.CreateRow(2);
            addressRow.HeightInPoints = 24;
            var addressCell = addressRow.CreateCell(0);
            addressCell.SetCellValue($"Địa chỉ: {projectAddress}");
            addressCell.CellStyle = metadataStyle;
            sh.AddMergedRegion(new CellRangeAddress(2, 2, 0, 9));

            var dateRow = sh.CreateRow(3);
            dateRow.HeightInPoints = 24;
            var dateCell = dateRow.CreateCell(0);
            dateCell.SetCellValue($"Ngày xuất: {DateTime.Now:dd/MM/yyyy HH:mm}");
            dateCell.CellStyle = metadataStyle;
            sh.AddMergedRegion(new CellRangeAddress(3, 3, 0, 9));

            string[] headers = { "STT", "ƯU TIÊN", "TẦNG", "CẤU TẠO", "KÝ HIỆU", "RỘNG (mm)", "DÀI (mm)", "SỐ LƯỢNG", "DIỆN TÍCH (m²)", "GHI CHÚ" };
            int headerRowIdx = 5;
            IRow hdr = sh.CreateRow(headerRowIdx);
            hdr.HeightInPoints = 33;
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
                SetCell(dr, 2, order.Level ?? "", dataStyle);
                SetCell(dr, 3, order.Spec ?? "", wrapStyle);
                SetCell(dr, 4, order.PanelIds ?? "", wrapStyle);
                SetCell(dr, 5, order.WidthMm, dataStyle);
                SetCell(dr, 6, order.LengthMm, dataStyle);
                SetCell(dr, 7, order.Qty, dataStyle);
                
                SetFormulaCell(dr, 8, $"{CellRef(r, 5)}*{CellRef(r, 6)}/1000000*{CellRef(r, 7)}", computedStyle);
                
                SetCell(dr, 9, order.Note ?? "", wrapStyle);
                r++;
            }

            IRow fs = sh.CreateRow(r);
            SetCell(fs, 0, "TỔNG ĐẶT HÀNG", sumStyle);
            for(int i=1;i<=6;i++) SetCell(fs, i, "", sumStyle);
            sh.AddMergedRegion(new CellRangeAddress(r, r, 0, 6));
            
            if (r > dataStart)
            {
                SetFormulaCell(fs, 7, $"SUM({CellRef(dataStart, 7)}:{CellRef(r - 1, 7)})", sumIntegerStyle);
                SetFormulaCell(fs, 8, $"SUM({CellRef(dataStart, 8)}:{CellRef(r - 1, 8)})", sumStyle);
            }
            else
            {
                SetCell(fs, 7, 0, sumIntegerStyle);
                SetCell(fs, 8, 0, sumStyle);
            }
            SetCell(fs, 9, "", sumStyle);

            // Min widths = header text length × 1.5 (Times New Roman 13pt vs Calibri 11pt)
            // Headers: STT(3), ƯU TIÊN(7), TẦNG(4), CẤU TẠO(7), KÝ HIỆU(7), RỘNG(mm)(9), DÀI(mm)(8), SỐ LƯỢNG(8), DIỆN TÍCH m²(14), GHI CHÚ
            AutoFitColumns(sh, 10,
                new[] { 6, 12,  8, 13, 13, 15, 14, 14, 20, 20 },
                new[] { 9, 18, 15, 22, 18, 20, 18, 18, 25, 55 });
            sh.CreateFreezePane(0, headerRowIdx + 1);
            SetZoom(sh, 90);
        }

        private void CreateSpecSheet(IWorkbook wb, List<ShopdrawingBomCalculator.FactoryOrderRow> orders, ICellStyle headerStyle, ICellStyle dataStyle, ICellStyle titleStyle, ICellStyle metadataStyle, string projectName, string projectAddress)
        {
            ISheet sh = wb.CreateSheet("Quản lý Spec");
            sh.DefaultRowHeightInPoints = 20;

            // A4 Landscape (18 columns)
            sh.PrintSetup.PaperSize = 9; // 9 = A4
            sh.PrintSetup.Landscape = true;
            sh.PrintSetup.FitWidth = 1;
            sh.PrintSetup.FitHeight = 0;
            sh.FitToPage = true;

            var titleRow = sh.CreateRow(0);
            titleRow.HeightInPoints = 30;
            var titleCell = titleRow.CreateCell(0);
            titleCell.SetCellValue("BẢNG QUẢN LÝ SPEC");
            titleCell.CellStyle = titleStyle;
            sh.AddMergedRegion(new CellRangeAddress(0, 0, 0, 17));

            var projectRow = sh.CreateRow(1);
            projectRow.HeightInPoints = 24;
            var projectCell = projectRow.CreateCell(0);
            projectCell.SetCellValue($"Dự án: {projectName}");
            projectCell.CellStyle = metadataStyle;
            sh.AddMergedRegion(new CellRangeAddress(1, 1, 0, 17));

            var addressRow = sh.CreateRow(2);
            addressRow.HeightInPoints = 24;
            var addressCell = addressRow.CreateCell(0);
            addressCell.SetCellValue($"Địa chỉ: {projectAddress}");
            addressCell.CellStyle = metadataStyle;
            sh.AddMergedRegion(new CellRangeAddress(2, 2, 0, 17));

            var dateRow = sh.CreateRow(3);
            dateRow.HeightInPoints = 24;
            var dateCell = dateRow.CreateCell(0);
            dateCell.SetCellValue($"Ngày xuất: {DateTime.Now:dd/MM/yyyy HH:mm}");
            dateCell.CellStyle = metadataStyle;
            sh.AddMergedRegion(new CellRangeAddress(3, 3, 0, 17));

            var listRow = sh.CreateRow(5);
            listRow.HeightInPoints = 22;
            var listCell = listRow.CreateCell(0);
            listCell.SetCellValue("DANH SÁCH SPEC DỰ ÁN");
            listCell.CellStyle = headerStyle;
            sh.AddMergedRegion(new CellRangeAddress(5, 5, 0, 17));

            string[] mainHeaders = { "STT", "Mã spec", "Khổ tấm (mm)", "Loại panel", "Tỷ trọng", "Chiều dày (mm)", "Chống cháy", "FM", "MẶT TRÊN", "", "", "", "", "MẶT DƯỚI", "", "", "", "" };
            string[] subHeaders = { "", "", "", "", "", "", "", "", "Màu sắc", "Vật liệu", "Độ mạ", "Dày tôn", "Profile", "Màu sắc", "Vật liệu", "Độ mạ", "Dày tôn", "Profile" };

            int headerRowIdx = 6;
            IRow hdr1 = sh.CreateRow(headerRowIdx);
            hdr1.HeightInPoints = 33;
            for (int i = 0; i < mainHeaders.Length; i++)
            {
                SetCell(hdr1, i, mainHeaders[i], headerStyle);
            }

            IRow hdr2 = sh.CreateRow(headerRowIdx + 1);
            hdr2.HeightInPoints = 33;
            for (int i = 0; i < subHeaders.Length; i++)
            {
                SetCell(hdr2, i, subHeaders[i], headerStyle);
            }

            for (int i = 0; i <= 7; i++)
            {
                sh.AddMergedRegion(new CellRangeAddress(headerRowIdx, headerRowIdx + 1, i, i));
            }
            sh.AddMergedRegion(new CellRangeAddress(headerRowIdx, headerRowIdx, 8, 12));
            sh.AddMergedRegion(new CellRangeAddress(headerRowIdx, headerRowIdx, 13, 17));

            var uniqueSpecs = orders.Select(o => o.Spec).Where(s => !string.IsNullOrEmpty(s)).Distinct().OrderBy(s => s).ToList();

            int r = headerRowIdx + 2;
            int stt = 1;
            foreach (var specStr in uniqueSpecs)
            {
                var parts = specStr.Split('|').Select(p => p.Trim()).ToArray();
                string specKey = parts.Length > 0 ? parts[0] : specStr;

                var specDef = ShopDrawingCommands.SpecManager?.GetByKey(specKey);

                IRow dr = sh.CreateRow(r);
                SetCell(dr, 0, stt++, dataStyle);
                SetCell(dr, 1, specKey, dataStyle);
                SetCell(dr, 2, specDef?.PanelWidth.ToString() ?? "", dataStyle);
                SetCell(dr, 3, specDef?.PanelType ?? "", dataStyle);
                SetCell(dr, 4, specDef?.Density ?? "", dataStyle);
                SetCell(dr, 5, specDef?.Thickness.ToString() ?? "", dataStyle);
                SetCell(dr, 6, specDef?.FireRating ?? "-", dataStyle);
                SetCell(dr, 7, (specDef?.FmApproved == true) ? "Có" : "Không", dataStyle);
                
                SetCell(dr, 8, specDef?.FacingColor ?? "", dataStyle);
                SetCell(dr, 9, specDef?.TopFacing ?? "", dataStyle);
                SetCell(dr, 10, specDef?.TopCoating ?? "", dataStyle);
                SetCell(dr, 11, specDef?.TopSteelThickness.ToString() ?? "", dataStyle);
                SetCell(dr, 12, specDef?.TopProfile ?? "", dataStyle);
                
                SetCell(dr, 13, specDef?.BottomFacingColor ?? "", dataStyle);
                SetCell(dr, 14, specDef?.BottomFacing ?? "", dataStyle);
                SetCell(dr, 15, specDef?.BottomCoating ?? "", dataStyle);
                SetCell(dr, 16, specDef?.BottomSteelThickness.ToString() ?? "", dataStyle);
                SetCell(dr, 17, specDef?.BottomProfile ?? "", dataStyle);
                
                r++;
            }

            // Headers: STT, Mã spec, Khổ tấm(mm)(12), Loại panel(10), Tỷ trọng(8), Chiều dày(mm)(14), Chống cháy(10), FM, MẶT TRÊN(8), sub-headers...
            AutoFitColumns(sh, 18,
                new[] { 6, 12, 18, 16, 14, 20, 16, 8, 13, 13, 10, 12, 12, 13, 13, 10, 12, 12 },
                new[] { 9, 22, 22, 22, 18, 24, 18, 12, 18, 18, 14, 16, 16, 18, 18, 14, 16, 16 });
            sh.CreateFreezePane(0, headerRowIdx + 2);
            SetZoom(sh, 90);
        }

        private void CreateAccessorySheet(IWorkbook wb, List<ShopdrawingAccessorySummaryRow> accessoryRows, ICellStyle headerStyle, ICellStyle dataStyle, ICellStyle titleStyle, ICellStyle wrapStyle, ICellStyle sumStyle, ICellStyle sumIntegerStyle, ICellStyle metadataStyle, string projectName, string projectAddress)
        {
            ISheet sh = wb.CreateSheet("Đặt Hàng Phụ Kiện");
            sh.DefaultRowHeightInPoints = 20;

            // A4 Landscape
            sh.PrintSetup.PaperSize = 9; // 9 = A4
            sh.PrintSetup.Landscape = true;
            sh.PrintSetup.FitWidth = 1;
            sh.PrintSetup.FitHeight = 0;
            sh.FitToPage = true;

            var titleRow = sh.CreateRow(0);
            titleRow.HeightInPoints = 30;
            var titleCell = titleRow.CreateCell(0);
            titleCell.SetCellValue("ĐẶT HÀNG PHỤ KIỆN");
            titleCell.CellStyle = titleStyle;
            sh.AddMergedRegion(new CellRangeAddress(0, 0, 0, 7));

            var projectRow = sh.CreateRow(1);
            projectRow.HeightInPoints = 24;
            var projectCell = projectRow.CreateCell(0);
            projectCell.SetCellValue($"Dự án: {projectName}");
            projectCell.CellStyle = metadataStyle;
            sh.AddMergedRegion(new CellRangeAddress(1, 1, 0, 7));

            var addressRow = sh.CreateRow(2);
            addressRow.HeightInPoints = 24;
            var addressCell = addressRow.CreateCell(0);
            addressCell.SetCellValue($"Địa chỉ: {projectAddress}");
            addressCell.CellStyle = metadataStyle;
            sh.AddMergedRegion(new CellRangeAddress(2, 2, 0, 7));

            var dateRow = sh.CreateRow(3);
            dateRow.HeightInPoints = 24;
            var dateCell = dateRow.CreateCell(0);
            dateCell.SetCellValue($"Ngày xuất: {DateTime.Now:dd/MM/yyyy HH:mm}");
            dateCell.CellStyle = metadataStyle;
            sh.AddMergedRegion(new CellRangeAddress(3, 3, 0, 7));

            string[] headers = { "STT", "HẠNG MỤC", "VỊ TRÍ / ỨNG DỤNG", "TÊN PHỤ KIỆN", "QUY CÁCH", "ĐVT", "SỐ LƯỢNG", "GHI CHÚ" };
            int headerRowIdx = 5;
            IRow hdr = sh.CreateRow(headerRowIdx);
            hdr.HeightInPoints = 33;
            for (int i = 0; i < headers.Length; i++)
            {
                SetCell(hdr, i, headers[i], headerStyle);
            }

            int r = headerRowIdx + 1;
            int dataStart = r;
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
                double slMua = acc.BasisValue * acc.Factor * (1.0 + acc.WasteFactor / 100.0);
                
                if (acc.Unit?.ToLower() == "m")
                {
                    dvtMua = "Cây";
                    slMua = Math.Ceiling(slMua / 6.0);
                }
                else
                {
                    slMua = Math.Ceiling(slMua);
                }
                
                SetCell(dr, 5, dvtMua, dataStyle);
                SetCell(dr, 6, slMua, dataStyle); 
                SetCell(dr, 7, acc.Note, wrapStyle);
                r++;
            }

            IRow fs = sh.CreateRow(r);
            SetCell(fs, 0, "TỔNG CỘNG", sumStyle);
            for(int i=1; i<=5; i++) SetCell(fs, i, "", sumStyle);
            sh.AddMergedRegion(new CellRangeAddress(r, r, 0, 5));
            
            if (r > dataStart)
            {
                SetFormulaCell(fs, 6, $"SUM({CellRef(dataStart, 6)}:{CellRef(r - 1, 6)})", sumIntegerStyle);
            }
            else
            {
                SetCell(fs, 6, 0, sumIntegerStyle);
            }
            SetCell(fs, 7, "", sumStyle);

            // Headers: STT, HẠNG MỤC(8), VỊ TRÍ/ỨNG DỤNG(17), TÊN PHỤ KIỆN(12), QUY CÁCH(8), ĐVT(3), SỐ LƯỢNG(8), GHI CHÚ(7)
            AutoFitColumns(sh, 8,
                new[] { 6, 14, 22, 18, 14,  8, 14, 20 },
                new[] { 9, 22, 30, 40, 35, 12, 16, 55 });
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

        /// <summary>
        /// Auto-sizes each column to fit its content, then enforces per-column min/max caps.
        /// This fixes the Times New Roman 13pt vs Calibri 11pt scaling mismatch where
        /// manual widths appear too narrow because SetColumnWidth units are based on
        /// the default Calibri 11pt font, not the actual cell font.
        /// </summary>
        private void AutoFitColumns(ISheet sheet, int colCount, int[] minWidths, int[] maxWidths)
        {
            for (int i = 0; i < colCount; i++)
            {
                sheet.AutoSizeColumn(i);
                int current = (int)sheet.GetColumnWidth(i);

                // Apply minimum width
                if (minWidths != null && i < minWidths.Length)
                {
                    int min = minWidths[i] * 256;
                    if (current < min) current = min;
                }

                // Apply maximum width (prevent runaway wide columns from long text)
                if (maxWidths != null && i < maxWidths.Length)
                {
                    int max = maxWidths[i] * 256;
                    if (current > max) current = max;
                }

                sheet.SetColumnWidth(i, current);
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
            f.FontHeightInPoints = 13;
            f.FontName = "Times New Roman";
            f.Color = NPOI.HSSF.Util.HSSFColor.White.Index;
            s.SetFont(f);
            s.Alignment = HorizontalAlignment.Center;
            s.VerticalAlignment = VerticalAlignment.Center;
            if (s is XSSFCellStyle xs)
            {
                var blueColor = new XSSFColor();
                blueColor.ARGBHex = "FF4F81BD";
                xs.SetFillForegroundColor(blueColor);
                s.FillPattern = FillPattern.SolidForeground;
            }
            SetBorders(s);
            return s;
        }

        private ICellStyle CreateDataStyle(IWorkbook wb)
        {
            var s = wb.CreateCellStyle();
            var f = wb.CreateFont();
            f.FontName = "Times New Roman";
            f.FontHeightInPoints = 13;
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
            f.FontName = "Times New Roman";
            f.FontHeightInPoints = 13;
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
            f.FontName = "Times New Roman";
            s.SetFont(f);
            s.Alignment = HorizontalAlignment.Left;
            s.VerticalAlignment = VerticalAlignment.Center;
            return s;
        }

        private ICellStyle CreateMetadataStyle(IWorkbook wb)
        {
            var s = wb.CreateCellStyle();
            var f = wb.CreateFont();
            f.IsItalic = true;
            f.FontHeightInPoints = 13;
            f.FontName = "Times New Roman";
            s.SetFont(f);
            s.Alignment = HorizontalAlignment.Left;
            s.VerticalAlignment = VerticalAlignment.Center;
            return s;
        }

        private ICellStyle CreateColoredSectionStyle(IWorkbook wb, short colorIndex)
        {
            var s = wb.CreateCellStyle();
            var f = wb.CreateFont();
            f.IsBold = true;
            f.Color = NPOI.HSSF.Util.HSSFColor.White.Index;
            f.FontName = "Times New Roman";
            f.FontHeightInPoints = 13;
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
