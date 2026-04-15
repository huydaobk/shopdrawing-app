using System;
using System.IO;
using System.Linq;
using NPOI.SS.UserModel;
using NPOI.SS.Util;
using NPOI.XSSF.UserModel;
using ShopDrawing.Plugin.Core;
using ShopDrawing.Plugin.Models;

namespace ShopDrawing.Plugin.UI
{
    /// <summary>
    /// Xuất bảng khối lượng đấu thầu ra Excel.
    /// </summary>
    public class TenderExcelExporter
    {
        private const int TenderSheetMaxColumnIndex = 17;
        private const int BasisSheetMaxColumnIndex = 15;
        private const int SpecSheetMaxColumnIndex = 18;

        public void Export(TenderProject project, string filePath)
        {
            var workbook = new XSSFWorkbook();

            var tenderSheet = workbook.CreateSheet("Khối lượng đấu thầu");
            var basisSheet = workbook.CreateSheet("Cơ sở tính phụ kiện riêng");
            var specSheet = workbook.CreateSheet("Quản lý Spec");

            var titleStyle = CreateTitleStyle(workbook);
            var infoStyle = CreateInfoStyle(workbook);
            var headerStyle = CreateHeaderStyle(workbook);
            var dataStyle = CreateDataStyle(workbook);
            var dataWrapStyle = CreateWrappedDataStyle(workbook, dataStyle);
            var computedStyle = CreateComputedStyle(workbook, dataStyle);
            var totalStyle = CreateTotalStyle(workbook, dataStyle);

            var panelSectionStyle = CreateColoredSectionStyle(workbook, IndexedColors.DarkBlue.Index);
            var accessorySummarySectionStyle = CreateColoredSectionStyle(workbook, IndexedColors.DarkGreen.Index);
            var accessoryBasisSectionStyle = CreateColoredSectionStyle(workbook, IndexedColors.Brown.Index);
            var specSectionStyle = CreateColoredSectionStyle(workbook, IndexedColors.Grey50Percent.Index);

            int tenderRowIdx = WriteSheetHeader(
                tenderSheet,
                $"BẢNG KHỐI LƯỢNG ĐẤU THẦU - {project.ProjectName}",
                project.CustomerName,
                titleStyle,
                infoStyle,
                TenderSheetMaxColumnIndex);

            tenderRowIdx = WritePanelSummarySection(
                project,
                tenderSheet,
                tenderRowIdx,
                headerStyle,
                dataStyle,
                dataWrapStyle,
                computedStyle,
                totalStyle,
                panelSectionStyle);

            tenderRowIdx += 2;
            WriteAccessorySummarySection(
                project,
                tenderSheet,
                tenderRowIdx,
                headerStyle,
                dataStyle,
                dataWrapStyle,
                computedStyle,
                totalStyle,
                accessorySummarySectionStyle);

            int basisRowIdx = WriteSheetHeader(
                basisSheet,
                $"CƠ SỞ TÍNH PHỤ KIỆN - {project.ProjectName}",
                project.CustomerName,
                titleStyle,
                infoStyle,
                BasisSheetMaxColumnIndex);

            WriteAccessoryBasisSection(
                project,
                basisSheet,
                basisRowIdx,
                headerStyle,
                dataStyle,
                dataWrapStyle,
                computedStyle,
                accessoryBasisSectionStyle);

            WriteSpecSheet(
                project,
                specSheet,
                titleStyle,
                infoStyle,
                headerStyle,
                dataStyle,
                computedStyle,
                totalStyle,
                specSectionStyle,
                workbook);

            AutoSizeSheet(tenderSheet, TenderSheetMaxColumnIndex);
            AutoSizeSheet(basisSheet, BasisSheetMaxColumnIndex);
            AutoSizeSheet(specSheet, SpecSheetMaxColumnIndex);

            ApplyTenderSheetColumnWidths(tenderSheet);
            ApplyAccessoryBasisSheetColumnWidths(basisSheet);
            ApplySpecSheetColumnWidths(specSheet);

            tenderSheet.CreateFreezePane(0, 3);
            basisSheet.CreateFreezePane(0, 5);
            specSheet.CreateFreezePane(0, 7);

            tenderSheet.SetZoom(90);
            basisSheet.SetZoom(90);
            specSheet.SetZoom(90);

            using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write);
            workbook.Write(fs);
        }

        private static int WriteSheetHeader(
            ISheet sheet,
            string title,
            string customerName,
            ICellStyle titleStyle,
            ICellStyle infoStyle,
            int maxColumnIndex)
        {
            int rowIdx = 0;

            var titleRow = sheet.CreateRow(rowIdx++);
            var titleCell = titleRow.CreateCell(0);
            titleCell.SetCellValue(title);
            titleCell.CellStyle = titleStyle;
            MergeRowAcross(sheet, titleRow.RowNum, maxColumnIndex);

            var infoRow = sheet.CreateRow(rowIdx++);
            SetCell(infoRow, 0, $"Khách hàng: {customerName}", infoStyle);
            SetCell(infoRow, Math.Min(5, maxColumnIndex), $"Ngày xuất: {DateTime.Now:dd/MM/yyyy}", infoStyle);

            rowIdx++;
            return rowIdx;
        }

        private static int WritePanelSummarySection(
            TenderProject project,
            ISheet sheet,
            int rowIdx,
            ICellStyle headerStyle,
            ICellStyle dataStyle,
            ICellStyle dataWrapStyle,
            ICellStyle computedStyle,
            ICellStyle totalStyle,
            ICellStyle sectionStyle)
        {
            var calculator = new TenderBomCalculator();
            var rows = calculator.CalculatePanelSummary(project.Walls);

            var sectionRow = sheet.CreateRow(rowIdx++);
            SetCell(sectionRow, 0, "TỔNG HỢP KHỐI LƯỢNG TẤM THEO TẦNG + SPEC", sectionStyle);
            MergeRowAcross(sheet, sectionRow.RowNum, TenderSheetMaxColumnIndex);

            string[] headers =
            {
                "STT", "Tầng", "Hạng mục", "Mã spec", "Số vùng", "Tổng dài (m)", "Cao TB (mm)",
                "DT vách (m²)", "DT lỗ mở (m²)", "DT net (m²)",
                "DT dự kiến cấp (m²)", "Khối lượng hao hụt tổng (m²)", "Hao hụt (%)"
            };

            var headerRow = sheet.CreateRow(rowIdx++);
            for (int i = 0; i < headers.Length; i++)
            {
                SetCell(headerRow, i, headers[i], headerStyle);
            }

            int stt = 1;
            foreach (var row in rows)
            {
                var excelRow = sheet.CreateRow(rowIdx++);
                int col = 0;
                SetCell(excelRow, col++, stt++, dataStyle);
                SetCell(excelRow, col++, row.Floor, dataStyle);
                SetCell(excelRow, col++, row.Category, dataStyle);
                SetCell(excelRow, col++, row.SpecKey, dataStyle);
                SetCell(excelRow, col++, row.WallCount, dataStyle);
                SetCell(excelRow, col++, row.TotalLengthM, computedStyle);
                SetCell(excelRow, col++, row.HeightMm, computedStyle);
                SetCell(excelRow, col++, row.WallAreaM2, computedStyle);
                SetCell(excelRow, col++, row.OpeningAreaM2, computedStyle);
                SetCell(excelRow, col++, row.NetAreaM2, computedStyle);
                SetCell(excelRow, col++, row.OrderedAreaM2, computedStyle);
                SetCell(excelRow, col++, row.WasteAreaM2, computedStyle);
                SetCell(excelRow, col++, row.WastePercent, computedStyle);
            }

            var totalRow = sheet.CreateRow(rowIdx++);
            SetCell(totalRow, 3, "TỔNG CỘNG:", totalStyle);
            SetCell(totalRow, 4, rows.Sum(x => x.WallCount), totalStyle);
            SetCell(totalRow, 5, rows.Sum(x => x.TotalLengthM), totalStyle);
            SetCell(totalRow, 7, rows.Sum(x => x.WallAreaM2), totalStyle);
            SetCell(totalRow, 8, rows.Sum(x => x.OpeningAreaM2), totalStyle);
            SetCell(totalRow, 9, rows.Sum(x => x.NetAreaM2), totalStyle);
            SetCell(totalRow, 10, rows.Sum(x => x.OrderedAreaM2), totalStyle);
            SetCell(totalRow, 11, rows.Sum(x => x.WasteAreaM2), totalStyle);

            double totalOrderedArea = rows.Sum(x => x.OrderedAreaM2);
            double totalWasteArea = rows.Sum(x => x.WasteAreaM2);
            double totalWastePercent = totalOrderedArea > 0 ? totalWasteArea / totalOrderedArea * 100.0 : 0.0;
            SetCell(totalRow, 12, totalWastePercent, totalStyle);

            rowIdx++;

            var supplyTitleRow = sheet.CreateRow(rowIdx++);
            SetCell(supplyTitleRow, 0, "TỔNG KHỐI LƯỢNG PANEL CẤP DỰ KIẾN", sectionStyle);
            sheet.AddMergedRegion(new CellRangeAddress(supplyTitleRow.RowNum, supplyTitleRow.RowNum, 0, 8));

            var supplyHeaderRow = sheet.CreateRow(rowIdx++);
            SetCell(supplyHeaderRow, 0, "STT", headerStyle);
            SetCell(supplyHeaderRow, 1, "Chỉ tiêu", headerStyle);
            SetCell(supplyHeaderRow, 2, "Giá trị", headerStyle);
            SetMergedTextCell(sheet, supplyHeaderRow, 3, 8, "Ghi chú", headerStyle);

            var supplyRows = new (string Label, double Value, string Note)[]
            {
                ("Tổng diện tích dự kiến phải cấp (m²)", totalOrderedArea, "Diện tích panel quy đổi theo tổng số tấm nguyên cần cấp."),
                ("Khối lượng hao hụt tổng (m²)", totalWasteArea, "Bao gồm phần cắt bỏ tấm cuối và phần diện tích panel bị vướng vào lỗ mở."),
                ("Tỷ lệ hao hụt tổng (%)", totalWastePercent, "Tỷ lệ hao hụt = Khối lượng hao hụt tổng / Tổng diện tích dự kiến phải cấp.")
            };

            for (int i = 0; i < supplyRows.Length; i++)
            {
                var item = supplyRows[i];
                var supplyRow = sheet.CreateRow(rowIdx++);
                SetCell(supplyRow, 0, i + 1, dataStyle);
                SetCell(supplyRow, 1, item.Label, dataStyle);
                SetCell(supplyRow, 2, item.Value, computedStyle);
                SetMergedTextCell(sheet, supplyRow, 3, 8, item.Note, dataWrapStyle);
                ApplyWrapRowHeight(supplyRow, item.Note, 90);
            }

            return rowIdx;
        }

        private static int WriteAccessoryBasisSection(
            TenderProject project,
            ISheet sheet,
            int rowIdx,
            ICellStyle headerStyle,
            ICellStyle dataStyle,
            ICellStyle dataWrapStyle,
            ICellStyle computedStyle,
            ICellStyle sectionStyle)
        {
            var calculator = new TenderBomCalculator();
            var report = calculator.CalculateAccessoryReport(project.Walls, project.Accessories);

            var titleRow = sheet.CreateRow(rowIdx++);
            SetCell(titleRow, 0, "CƠ SỞ TÍNH PHỤ KIỆN", sectionStyle);
            MergeRowAcross(sheet, titleRow.RowNum, BasisSheetMaxColumnIndex);

            string[] headers =
            {
                "STT", "Tầng", "Hạng mục", "Ký hiệu vách", "Ứng dụng", "Mã spec",
                "Phụ kiện", "Vật liệu", "Vị trí", "Quy tắc tính", "Cơ sở tính", "Giá trị cơ sở",
                "Hệ số", "Khối lượng tự động", "Vị trí / Phạm vi", "Thông số chính"
            };

            var headerRow = sheet.CreateRow(rowIdx++);
            for (int i = 0; i < headers.Length; i++)
            {
                SetCell(headerRow, i, headers[i], headerStyle);
            }

            int stt = 1;
            foreach (var row in report.BasisRows)
            {
                var noteParts = SplitDisplayNote(row.Note);
                var excelRow = sheet.CreateRow(rowIdx++);
                SetCell(excelRow, 0, stt++, dataStyle);
                SetCell(excelRow, 1, row.Floor, dataStyle);
                SetCell(excelRow, 2, row.Category, dataStyle);
                SetCell(excelRow, 3, row.WallName, dataStyle);
                SetCell(excelRow, 4, row.Application, dataStyle);
                SetCell(excelRow, 5, row.SpecKey, dataStyle);
                SetCell(excelRow, 6, row.AccessoryName, dataStyle);
                SetCell(excelRow, 7, row.Material, dataStyle);
                SetCell(excelRow, 8, row.Position, dataStyle);
                SetCell(excelRow, 9, row.RuleLabel, dataStyle);
                SetCell(excelRow, 10, row.BasisLabel, dataStyle);
                SetCell(excelRow, 11, row.BasisValue, computedStyle);
                SetCell(excelRow, 12, row.Factor, computedStyle);
                SetCell(excelRow, 13, row.AutoQuantity, computedStyle);
                SetCell(excelRow, 14, noteParts.Scope, dataWrapStyle);
                SetCell(excelRow, 15, noteParts.Detail, dataWrapStyle);
                ApplyWrapRowHeight(excelRow, $"{noteParts.Scope} {noteParts.Detail}", 75);
            }

            return rowIdx;
        }

        private static int WriteAccessorySummarySection(
            TenderProject project,
            ISheet sheet,
            int rowIdx,
            ICellStyle headerStyle,
            ICellStyle dataStyle,
            ICellStyle dataWrapStyle,
            ICellStyle computedStyle,
            ICellStyle totalStyle,
            ICellStyle sectionStyle)
        {
            var calculator = new TenderBomCalculator();
            var summary = calculator.CalculateAccessorySummary(project.Walls, project.Accessories);

            var titleRow = sheet.CreateRow(rowIdx++);
            SetCell(titleRow, 0, "TỔNG HỢP PHỤ KIỆN ĐẤU THẦU", sectionStyle);
            MergeRowAcross(sheet, titleRow.RowNum, TenderSheetMaxColumnIndex);

            string[] headers =
            {
                "STT", "Phạm vi hạng mục", "Ứng dụng", "Mã spec", "Phụ kiện", "Vật liệu", "Vị trí", "Đơn vị",
                "Quy tắc tính", "Cơ sở tính", "Giá trị cơ sở", "Hệ số", "Hao hụt (%)",
                "Khối lượng tự động", "Điều chỉnh", "Khối lượng chốt", "Vị trí / Phạm vi", "Thông số chính"
            };

            var headerRow = sheet.CreateRow(rowIdx++);
            for (int i = 0; i < headers.Length; i++)
            {
                SetCell(headerRow, i, headers[i], headerStyle);
            }

            int stt = 1;
            foreach (var row in summary)
            {
                var noteParts = SplitDisplayNote(row.Note);
                var excelRow = sheet.CreateRow(rowIdx++);
                SetCell(excelRow, 0, stt++, dataStyle);
                SetCell(excelRow, 1, row.CategoryScope, dataStyle);
                SetCell(excelRow, 2, row.Application, dataStyle);
                SetCell(excelRow, 3, row.SpecKey, dataStyle);
                SetCell(excelRow, 4, row.Name, dataStyle);
                SetCell(excelRow, 5, row.Material, dataStyle);
                SetCell(excelRow, 6, row.Position, dataStyle);
                SetCell(excelRow, 7, row.Unit, dataStyle);
                SetCell(excelRow, 8, row.RuleLabel, dataStyle);
                SetCell(excelRow, 9, row.BasisLabel, dataStyle);
                SetCell(excelRow, 10, row.BasisValue, computedStyle);
                SetCell(excelRow, 11, row.Factor, computedStyle);
                SetCell(excelRow, 12, row.WastePercent, computedStyle);
                SetCell(excelRow, 13, row.AutoQuantity, computedStyle);
                SetCell(excelRow, 14, row.Adjustment, computedStyle);
                SetCell(excelRow, 15, row.FinalQuantity, computedStyle);
                SetCell(excelRow, 16, noteParts.Scope, dataWrapStyle);
                SetCell(excelRow, 17, noteParts.Detail, dataWrapStyle);
                ApplyWrapRowHeight(excelRow, $"{noteParts.Scope} {noteParts.Detail}", 90);
            }

            var totalRow = sheet.CreateRow(rowIdx++);
            SetCell(totalRow, 3, "TỔNG CHỐT:", totalStyle);
            SetCell(totalRow, 15, summary.Sum(item => item.FinalQuantity), totalStyle);

            return rowIdx;
        }

        private static void WriteSpecSheet(
            TenderProject project,
            ISheet sheet,
            ICellStyle titleStyle,
            ICellStyle infoStyle,
            ICellStyle headerStyle,
            ICellStyle dataStyle,
            ICellStyle computedStyle,
            ICellStyle totalStyle,
            ICellStyle sectionStyle,
            IWorkbook workbook)
        {
            int rowIdx = WriteSheetHeader(
                sheet,
                $"BẢNG QUẢN LÝ SPEC - {project.ProjectName}",
                project.CustomerName,
                titleStyle,
                infoStyle,
                SpecSheetMaxColumnIndex);

            var specGroupStyle = CreateGroupHeaderStyle(workbook, IndexedColors.Grey50Percent.Index);
            var topGroupStyle = CreateGroupHeaderStyle(workbook, IndexedColors.Blue.Index);
            var bottomGroupStyle = CreateGroupHeaderStyle(workbook, IndexedColors.Green.Index);
            var topHeaderStyle = CreateSubHeaderStyle(workbook, IndexedColors.PaleBlue.Index);
            var bottomHeaderStyle = CreateSubHeaderStyle(workbook, IndexedColors.LightGreen.Index);

            var sectionRow = sheet.CreateRow(rowIdx++);
            SetCell(sectionRow, 0, "DANH SÁCH SPEC DỰ ÁN", sectionStyle);
            MergeRowAcross(sheet, sectionRow.RowNum, SpecSheetMaxColumnIndex);

            var groupRow = sheet.CreateRow(rowIdx++);
            SetCell(groupRow, 0, "THÔNG TIN CHUNG", specGroupStyle);
            SetCell(groupRow, 9, "MẶT TRÊN", topGroupStyle);
            SetCell(groupRow, 14, "MẶT DƯỚI", bottomGroupStyle);
            sheet.AddMergedRegion(new CellRangeAddress(groupRow.RowNum, groupRow.RowNum, 0, 8));
            sheet.AddMergedRegion(new CellRangeAddress(groupRow.RowNum, groupRow.RowNum, 9, 13));
            sheet.AddMergedRegion(new CellRangeAddress(groupRow.RowNum, groupRow.RowNum, 14, 18));

            string[] headers =
            {
                "STT", "Mã spec", "Mã ký hiệu", "Khổ tấm (mm)", "Loại panel", "Tỷ trọng", "Chiều dày (mm)", "Chống cháy", "FM",
                "Màu mặt trên", "Vật liệu mặt trên", "Độ mạ mặt trên", "Dày tôn mặt trên (mm)", "Profile mặt trên",
                "Màu mặt dưới", "Vật liệu mặt dưới", "Độ mạ mặt dưới", "Dày tôn mặt dưới (mm)", "Profile mặt dưới"
            };

            var headerRow = sheet.CreateRow(rowIdx++);
            for (int i = 0; i < headers.Length; i++)
            {
                var style = i switch
                {
                    >= 9 and <= 13 => topHeaderStyle,
                    >= 14 and <= 18 => bottomHeaderStyle,
                    _ => headerStyle
                };

                SetCell(headerRow, i, headers[i], style);
            }

            int stt = 1;
            foreach (var spec in project.Specs.OrderBy(s => s.Key))
            {
                var row = sheet.CreateRow(rowIdx++);
                int col = 0;
                SetCell(row, col++, stt++, dataStyle);
                SetCell(row, col++, spec.Key, dataStyle);
                SetCell(row, col++, spec.WallCodePrefix, dataStyle);
                SetCell(row, col++, spec.PanelWidth, dataStyle);
                SetCell(row, col++, spec.PanelType, dataStyle);
                SetCell(row, col++, spec.Density, dataStyle);
                SetCell(row, col++, spec.Thickness, dataStyle);
                SetCell(row, col++, spec.FireRating, dataStyle);
                SetCell(row, col++, spec.FmApproved ? "Có" : "Không", dataStyle);
                SetCell(row, col++, spec.FacingColor, dataStyle);
                SetCell(row, col++, spec.TopFacing, dataStyle);
                SetCell(row, col++, spec.TopCoating, dataStyle);
                SetCell(row, col++, spec.TopSteelThickness, computedStyle);
                SetCell(row, col++, spec.TopProfile, dataStyle);
                SetCell(row, col++, spec.BottomFacingColor, dataStyle);
                SetCell(row, col++, spec.BottomFacing, dataStyle);
                SetCell(row, col++, spec.BottomCoating, dataStyle);
                SetCell(row, col++, spec.BottomSteelThickness, computedStyle);
                SetCell(row, col++, spec.BottomProfile, dataStyle);
            }

            var totalRow = sheet.CreateRow(rowIdx++);
            SetCell(totalRow, 1, "TỔNG SỐ SPEC:", totalStyle);
            SetCell(totalRow, 2, project.Specs.Count, totalStyle);
        }

        private static void AutoSizeSheet(ISheet sheet, int maxColumnIndex)
        {
            for (int i = 0; i <= maxColumnIndex; i++)
            {
                try
                {
                    sheet.AutoSizeColumn(i);
                }
                catch (Exception ex)
                {
                    PluginLogger.Warn("Suppressed exception: " + ex.Message);
                    sheet.SetColumnWidth(i, 14 * 256);
                }
            }
        }

        private static void ApplyTenderSheetColumnWidths(ISheet sheet)
        {
            int[] widths =
            {
                6, 16, 14, 13, 28, 16, 30, 9, 20, 20, 14, 10, 11, 15, 11, 14, 34, 52
            };

            ApplyColumnWidths(sheet, widths);
        }

        private static void ApplyAccessoryBasisSheetColumnWidths(ISheet sheet)
        {
            int[] widths =
            {
                6, 10, 12, 14, 12, 13, 28, 16, 24, 20, 20, 14, 10, 15, 34, 52
            };

            ApplyColumnWidths(sheet, widths);
        }

        private static void ApplySpecSheetColumnWidths(ISheet sheet)
        {
            int[] widths =
            {
                6, 18, 12, 12, 14, 12, 14, 12, 8, 14, 18, 14, 18, 16, 14, 18, 14, 18, 16
            };

            ApplyColumnWidths(sheet, widths);
        }

        private static void ApplyColumnWidths(ISheet sheet, int[] widths)
        {
            for (int i = 0; i < widths.Length; i++)
            {
                int targetWidth = widths[i] * 256;
                if (sheet.GetColumnWidth(i) < targetWidth)
                    sheet.SetColumnWidth(i, targetWidth);
            }
        }

        private static void MergeRowAcross(ISheet sheet, int rowIndex, int lastColumnIndex)
        {
            sheet.AddMergedRegion(new CellRangeAddress(rowIndex, rowIndex, 0, lastColumnIndex));
        }

        private static void SetMergedTextCell(
            ISheet sheet,
            IRow row,
            int firstCol,
            int lastCol,
            string value,
            ICellStyle style)
        {
            for (int col = firstCol; col <= lastCol; col++)
            {
                var cell = row.GetCell(col) ?? row.CreateCell(col);
                cell.CellStyle = style;
                if (col == firstCol)
                    cell.SetCellValue(value);
            }

            sheet.AddMergedRegion(new CellRangeAddress(row.RowNum, row.RowNum, firstCol, lastCol));
        }

        private static (string Scope, string Detail) SplitDisplayNote(string? note)
        {
            string normalized = (note ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(normalized))
                return (string.Empty, string.Empty);

            var parts = normalized
                .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(part => part.Trim())
                .Where(part => !string.IsNullOrWhiteSpace(part))
                .ToList();

            if (parts.Count == 0)
                return (string.Empty, string.Empty);

            if (parts.Count == 1)
                return (parts[0], string.Empty);

            string scope = string.Join(" | ", parts.Take(Math.Min(2, parts.Count)));
            string detail = string.Join(" | ", parts.Skip(Math.Min(2, parts.Count)));
            return (scope, detail);
        }

        private static void ApplyWrapRowHeight(IRow row, string? text, int charsPerLine)
        {
            string normalized = (text ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(normalized))
                return;

            int lines = Math.Max(1, (int)Math.Ceiling(normalized.Length / Math.Max(1.0, charsPerLine)));
            row.HeightInPoints = Math.Max(18f, 16f * lines);
        }

        private static void SetCell(IRow row, int col, string value, ICellStyle style)
        {
            var cell = row.CreateCell(col);
            cell.SetCellValue(value);
            cell.CellStyle = style;
        }

        private static void SetCell(IRow row, int col, double value, ICellStyle style)
        {
            var cell = row.CreateCell(col);
            cell.SetCellValue(value);
            cell.CellStyle = style;
        }

        private static void SetCell(IRow row, int col, int value, ICellStyle style)
        {
            var cell = row.CreateCell(col);
            cell.SetCellValue(value);
            cell.CellStyle = style;
        }

        private static ICellStyle CreateTitleStyle(IWorkbook workbook)
        {
            var style = workbook.CreateCellStyle();
            var font = workbook.CreateFont();
            font.IsBold = true;
            font.FontHeightInPoints = 15;
            font.Color = IndexedColors.DarkBlue.Index;
            style.SetFont(font);
            style.Alignment = HorizontalAlignment.Left;
            style.VerticalAlignment = VerticalAlignment.Center;
            return style;
        }

        private static ICellStyle CreateInfoStyle(IWorkbook workbook)
        {
            var style = workbook.CreateCellStyle();
            var font = workbook.CreateFont();
            font.FontHeightInPoints = 10;
            style.SetFont(font);
            style.Alignment = HorizontalAlignment.Left;
            style.VerticalAlignment = VerticalAlignment.Center;
            return style;
        }

        private static ICellStyle CreateHeaderStyle(IWorkbook workbook)
        {
            var style = workbook.CreateCellStyle();
            var font = workbook.CreateFont();
            font.IsBold = true;
            font.FontHeightInPoints = 10;
            style.SetFont(font);

            style.FillForegroundColor = NPOI.HSSF.Util.HSSFColor.Grey25Percent.Index;
            style.FillPattern = FillPattern.SolidForeground;
            style.Alignment = HorizontalAlignment.Center;
            style.VerticalAlignment = VerticalAlignment.Center;
            style.WrapText = true;
            style.BorderTop = BorderStyle.Thin;
            style.BorderBottom = BorderStyle.Thin;
            style.BorderLeft = BorderStyle.Thin;
            style.BorderRight = BorderStyle.Thin;
            return style;
        }

        private static ICellStyle CreateGroupHeaderStyle(IWorkbook workbook, short fillColor)
        {
            var style = workbook.CreateCellStyle();
            var font = workbook.CreateFont();
            font.IsBold = true;
            font.FontHeightInPoints = 11;
            font.Color = IndexedColors.White.Index;
            style.SetFont(font);
            style.FillForegroundColor = fillColor;
            style.FillPattern = FillPattern.SolidForeground;
            style.Alignment = HorizontalAlignment.Center;
            style.VerticalAlignment = VerticalAlignment.Center;
            style.WrapText = true;
            style.BorderTop = BorderStyle.Thin;
            style.BorderBottom = BorderStyle.Thin;
            style.BorderLeft = BorderStyle.Thin;
            style.BorderRight = BorderStyle.Thin;
            return style;
        }

        private static ICellStyle CreateSubHeaderStyle(IWorkbook workbook, short fillColor)
        {
            var style = workbook.CreateCellStyle();
            var font = workbook.CreateFont();
            font.IsBold = true;
            font.FontHeightInPoints = 10;
            style.SetFont(font);
            style.FillForegroundColor = fillColor;
            style.FillPattern = FillPattern.SolidForeground;
            style.Alignment = HorizontalAlignment.Center;
            style.VerticalAlignment = VerticalAlignment.Center;
            style.WrapText = true;
            style.BorderTop = BorderStyle.Thin;
            style.BorderBottom = BorderStyle.Thin;
            style.BorderLeft = BorderStyle.Thin;
            style.BorderRight = BorderStyle.Thin;
            return style;
        }

        private static ICellStyle CreateDataStyle(IWorkbook workbook)
        {
            var style = workbook.CreateCellStyle();
            style.Alignment = HorizontalAlignment.Left;
            style.VerticalAlignment = VerticalAlignment.Center;
            style.BorderTop = BorderStyle.Thin;
            style.BorderBottom = BorderStyle.Thin;
            style.BorderLeft = BorderStyle.Thin;
            style.BorderRight = BorderStyle.Thin;
            return style;
        }

        private static ICellStyle CreateWrappedDataStyle(IWorkbook workbook, ICellStyle baseStyle)
        {
            var style = workbook.CreateCellStyle();
            style.CloneStyleFrom(baseStyle);
            style.WrapText = true;
            style.VerticalAlignment = VerticalAlignment.Top;
            return style;
        }

        private static ICellStyle CreateComputedStyle(IWorkbook workbook, ICellStyle baseStyle)
        {
            var style = workbook.CreateCellStyle();
            style.CloneStyleFrom(baseStyle);

            var font = workbook.CreateFont();
            font.Color = NPOI.HSSF.Util.HSSFColor.Blue.Index;
            style.SetFont(font);

            style.Alignment = HorizontalAlignment.Right;
            style.DataFormat = workbook.CreateDataFormat().GetFormat("#,##0.00");
            return style;
        }

        private static ICellStyle CreateTotalStyle(IWorkbook workbook, ICellStyle baseStyle)
        {
            var style = workbook.CreateCellStyle();
            style.CloneStyleFrom(baseStyle);

            var font = workbook.CreateFont();
            font.IsBold = true;
            font.FontHeightInPoints = 10;
            style.SetFont(font);

            style.Alignment = HorizontalAlignment.Right;
            style.BorderTop = BorderStyle.Medium;
            style.BorderBottom = BorderStyle.Medium;
            style.DataFormat = workbook.CreateDataFormat().GetFormat("#,##0.00");
            return style;
        }

        private static ICellStyle CreateColoredSectionStyle(IWorkbook workbook, short fillColor)
        {
            var style = workbook.CreateCellStyle();
            var font = workbook.CreateFont();
            font.IsBold = true;
            font.FontHeightInPoints = 12;
            font.Color = IndexedColors.White.Index;
            style.SetFont(font);
            style.FillForegroundColor = fillColor;
            style.FillPattern = FillPattern.SolidForeground;
            style.Alignment = HorizontalAlignment.Left;
            style.VerticalAlignment = VerticalAlignment.Center;
            style.BorderTop = BorderStyle.Thin;
            style.BorderBottom = BorderStyle.Thin;
            style.BorderLeft = BorderStyle.Thin;
            style.BorderRight = BorderStyle.Thin;
            return style;
        }
    }
}
