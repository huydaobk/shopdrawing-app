using System;
using System.Collections.Generic;
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
        private const int TenderPanelSummaryMaxColumnIndex = 12;
        private const int BasisSheetMaxColumnIndex = 16;
        private const int SpecSheetMaxColumnIndex = 18;
        private const int PanelExplainSheetMaxColumnIndex = 13;

        public void Export(TenderProject project, string filePath)
        {
            var workbook = new XSSFWorkbook();

            var tenderSheet = workbook.CreateSheet("Khối lượng đấu thầu");
            var basisSheet = workbook.CreateSheet("Cơ sở tính phụ kiện riêng");
            var specSheet = workbook.CreateSheet("Quản lý Spec");
            var panelExplainSheet = workbook.CreateSheet("Diễn giải panel & lỗ mở");

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

            WritePanelOpeningExplanationSheet(
                project,
                panelExplainSheet,
                titleStyle,
                infoStyle,
                headerStyle,
                dataStyle,
                dataWrapStyle,
                computedStyle,
                totalStyle,
                panelSectionStyle);

            AutoSizeSheet(tenderSheet, TenderSheetMaxColumnIndex);
            AutoSizeSheet(basisSheet, BasisSheetMaxColumnIndex);
            AutoSizeSheet(specSheet, SpecSheetMaxColumnIndex);
            AutoSizeSheet(panelExplainSheet, PanelExplainSheetMaxColumnIndex);

            ApplyTenderSheetColumnWidths(tenderSheet);
            ApplyAccessoryBasisSheetColumnWidths(basisSheet);
            ApplySpecSheetColumnWidths(specSheet);
            ApplyPanelExplainSheetColumnWidths(panelExplainSheet);

            tenderSheet.CreateFreezePane(0, 3);
            basisSheet.CreateFreezePane(0, 5);
            specSheet.CreateFreezePane(0, 7);
            panelExplainSheet.CreateFreezePane(0, 5);

            tenderSheet.SetZoom(90);
            basisSheet.SetZoom(90);
            specSheet.SetZoom(90);
            panelExplainSheet.SetZoom(90);

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
            SetMergedTextCell(sheet, titleRow, 0, maxColumnIndex, title, titleStyle);

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
            SetMergedTextCell(
                sheet,
                sectionRow,
                0,
                TenderPanelSummaryMaxColumnIndex,
                sectionRow.GetCell(0)?.StringCellValue ?? string.Empty,
                sectionStyle);

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

            int dataStartRowIndex = rowIdx;
            int stt = 1;
            const string panelExplainSheetRef = "'Diễn giải panel & lỗ mở'";
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
                string floorRef = CellRef(excelRow.RowNum, 1);
                string categoryRef = CellRef(excelRow.RowNum, 2);
                string specRef = CellRef(excelRow.RowNum, 3);
                SetFormulaCell(
                    excelRow,
                    col++,
                    $"SUMIFS({panelExplainSheetRef}!$M:$M,{panelExplainSheetRef}!$B:$B,{floorRef},{panelExplainSheetRef}!$D:$D,{categoryRef},{panelExplainSheetRef}!$F:$F,{specRef},{panelExplainSheetRef}!$G:$G,\"Cửa đi\")+SUMIFS({panelExplainSheetRef}!$M:$M,{panelExplainSheetRef}!$B:$B,{floorRef},{panelExplainSheetRef}!$D:$D,{categoryRef},{panelExplainSheetRef}!$F:$F,{specRef},{panelExplainSheetRef}!$G:$G,\"Cửa sổ\")+SUMIFS({panelExplainSheetRef}!$M:$M,{panelExplainSheetRef}!$B:$B,{floorRef},{panelExplainSheetRef}!$D:$D,{categoryRef},{panelExplainSheetRef}!$F:$F,{specRef},{panelExplainSheetRef}!$G:$G,\"Lỗ kỹ thuật\")",
                    computedStyle);
                SetFormulaCell(
                    excelRow,
                    col++,
                    $"MAX(0,{CellRef(excelRow.RowNum, 7)}-{CellRef(excelRow.RowNum, 8)})",
                    computedStyle);
                SetFormulaCell(
                    excelRow,
                    col++,
                    $"SUMIFS({panelExplainSheetRef}!$L:$L,{panelExplainSheetRef}!$B:$B,{floorRef},{panelExplainSheetRef}!$D:$D,{categoryRef},{panelExplainSheetRef}!$F:$F,{specRef},{panelExplainSheetRef}!$G:$G,\"Nguyên\")+SUMIFS({panelExplainSheetRef}!$L:$L,{panelExplainSheetRef}!$B:$B,{floorRef},{panelExplainSheetRef}!$D:$D,{categoryRef},{panelExplainSheetRef}!$F:$F,{specRef},{panelExplainSheetRef}!$G:$G,\"Giảm*\")",
                    computedStyle);
                SetFormulaCell(
                    excelRow,
                    col++,
                    $"SUMIFS({panelExplainSheetRef}!$M:$M,{panelExplainSheetRef}!$B:$B,{floorRef},{panelExplainSheetRef}!$D:$D,{categoryRef},{panelExplainSheetRef}!$F:$F,{specRef},{panelExplainSheetRef}!$G:$G,\"Hao hụt*\")",
                    computedStyle);
                SetFormulaCell(
                    excelRow,
                    col++,
                    $"IF({CellRef(excelRow.RowNum, 10)}>0,{CellRef(excelRow.RowNum, 11)}/{CellRef(excelRow.RowNum, 10)}*100,0)",
                    computedStyle);
            }

            int dataEndRowIndex = rowIdx - 1;
            var totalRow = sheet.CreateRow(rowIdx++);
            SetCell(totalRow, 3, "TỔNG CỘNG:", totalStyle);
            if (rows.Count > 0)
            {
                SetFormulaCell(totalRow, 4, $"SUM({CellRef(dataStartRowIndex, 4)}:{CellRef(dataEndRowIndex, 4)})", totalStyle);
                SetFormulaCell(totalRow, 5, $"SUM({CellRef(dataStartRowIndex, 5)}:{CellRef(dataEndRowIndex, 5)})", totalStyle);
                SetFormulaCell(totalRow, 7, $"SUM({CellRef(dataStartRowIndex, 7)}:{CellRef(dataEndRowIndex, 7)})", totalStyle);
                SetFormulaCell(totalRow, 8, $"SUM({CellRef(dataStartRowIndex, 8)}:{CellRef(dataEndRowIndex, 8)})", totalStyle);
                SetFormulaCell(totalRow, 9, $"SUM({CellRef(dataStartRowIndex, 9)}:{CellRef(dataEndRowIndex, 9)})", totalStyle);
                SetFormulaCell(totalRow, 10, $"SUM({CellRef(dataStartRowIndex, 10)}:{CellRef(dataEndRowIndex, 10)})", totalStyle);
                SetFormulaCell(totalRow, 11, $"SUM({CellRef(dataStartRowIndex, 11)}:{CellRef(dataEndRowIndex, 11)})", totalStyle);
                SetFormulaCell(totalRow, 12, $"IF({CellRef(totalRow.RowNum, 10)}>0,{CellRef(totalRow.RowNum, 11)}/{CellRef(totalRow.RowNum, 10)}*100,0)", totalStyle);
            }
            else
            {
                SetCell(totalRow, 4, 0, totalStyle);
                SetCell(totalRow, 5, 0, totalStyle);
                SetCell(totalRow, 7, 0, totalStyle);
                SetCell(totalRow, 8, 0, totalStyle);
                SetCell(totalRow, 9, 0, totalStyle);
                SetCell(totalRow, 10, 0, totalStyle);
                SetCell(totalRow, 11, 0, totalStyle);
                SetCell(totalRow, 12, 0, totalStyle);
            }

            rowIdx++;

            var supplyTitleRow = sheet.CreateRow(rowIdx++);
            SetCell(supplyTitleRow, 0, "TỔNG KHỐI LƯỢNG PANEL CẤP DỰ KIẾN", sectionStyle);
            SetMergedTextCell(
                sheet,
                supplyTitleRow,
                0,
                8,
                supplyTitleRow.GetCell(0)?.StringCellValue ?? string.Empty,
                sectionStyle);

            var supplyHeaderRow = sheet.CreateRow(rowIdx++);
            SetCell(supplyHeaderRow, 0, "STT", headerStyle);
            SetCell(supplyHeaderRow, 1, "Chỉ tiêu", headerStyle);
            SetCell(supplyHeaderRow, 2, "Giá trị", headerStyle);
            SetMergedTextCell(sheet, supplyHeaderRow, 3, 8, "Ghi chú", headerStyle);

            string totalOrderedRef = CellRef(totalRow.RowNum, 10);
            string totalWasteRef = CellRef(totalRow.RowNum, 11);
            var supplyRows = new (string Label, string Formula, string Note)[]
            {
                ("Tổng diện tích dự kiến phải cấp (m²)", totalOrderedRef, "Diện tích panel quy đổi theo tổng số tấm nguyên cần cấp."),
                ("Khối lượng hao hụt tổng (m²)", totalWasteRef, "Bao gồm phần cắt bỏ tấm cuối và phần diện tích panel bị vướng vào lỗ mở."),
                ("Tỷ lệ hao hụt tổng (%)", $"IF({totalOrderedRef}>0,{totalWasteRef}/{totalOrderedRef}*100,0)", "Tỷ lệ hao hụt = Khối lượng hao hụt tổng / Tổng diện tích dự kiến phải cấp.")
            };

            for (int i = 0; i < supplyRows.Length; i++)
            {
                var item = supplyRows[i];
                var supplyRow = sheet.CreateRow(rowIdx++);
                SetCell(supplyRow, 0, i + 1, dataStyle);
                SetCell(supplyRow, 1, item.Label, dataStyle);
                SetFormulaCell(supplyRow, 2, item.Formula, computedStyle);
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
            SetMergedTextCell(
                sheet,
                titleRow,
                0,
                BasisSheetMaxColumnIndex,
                titleRow.GetCell(0)?.StringCellValue ?? string.Empty,
                sectionStyle);

            string[] headers =
            {
                "STT", "Tầng", "Hạng mục", "Ký hiệu vách", "Ứng dụng", "Mã spec",
                "Phụ kiện", "Vật liệu", "Đơn vị", "Vị trí", "Quy tắc tính", "Cơ sở tính", "Giá trị cơ sở",
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
                SetCell(excelRow, 8, row.Unit, dataStyle);
                SetCell(excelRow, 9, row.Position, dataStyle);
                SetCell(excelRow, 10, row.RuleLabel, dataStyle);
                SetCell(excelRow, 11, row.BasisLabel, dataStyle);
                SetCell(excelRow, 12, row.BasisValue, computedStyle);
                SetCell(excelRow, 13, row.Factor, computedStyle);
                SetFormulaCell(
                    excelRow,
                    14,
                    $"{CellRef(excelRow.RowNum, 12)}*{CellRef(excelRow.RowNum, 13)}",
                    computedStyle);
                SetCell(excelRow, 15, noteParts.Scope, dataWrapStyle);
                SetCell(excelRow, 16, noteParts.Detail, dataWrapStyle);
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
            SetMergedTextCell(
                sheet,
                titleRow,
                0,
                TenderSheetMaxColumnIndex,
                titleRow.GetCell(0)?.StringCellValue ?? string.Empty,
                sectionStyle);

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
            const string basisSheetRef = "'Cơ sở tính phụ kiện riêng'";
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
                string basisCriteria =
                    $"{basisSheetRef}!$G:$G,{CellRef(excelRow.RowNum, 4)}," +
                    $"{basisSheetRef}!$H:$H,{CellRef(excelRow.RowNum, 5)}," +
                    $"{basisSheetRef}!$I:$I,{CellRef(excelRow.RowNum, 7)}";
                SetFormulaCell(
                    excelRow,
                    10,
                    $"SUMIFS({basisSheetRef}!$M:$M,{basisCriteria})",
                    computedStyle);
                SetFormulaCell(
                    excelRow,
                    11,
                    $"IF({CellRef(excelRow.RowNum, 10)}>0,SUMIFS({basisSheetRef}!$O:$O,{basisCriteria})/{CellRef(excelRow.RowNum, 10)},0)",
                    computedStyle);
                SetCell(excelRow, 12, row.WastePercent, computedStyle);
                SetFormulaCell(
                    excelRow,
                    13,
                    $"SUMIFS({basisSheetRef}!$O:$O,{basisCriteria})*(1+{CellRef(excelRow.RowNum, 12)}/100)",
                    computedStyle);
                SetCell(excelRow, 14, row.Adjustment, computedStyle);
                SetFormulaCell(
                    excelRow,
                    15,
                    $"{CellRef(excelRow.RowNum, 13)}+{CellRef(excelRow.RowNum, 14)}",
                    computedStyle);
                SetCell(excelRow, 16, noteParts.Scope, dataWrapStyle);
                SetCell(excelRow, 17, noteParts.Detail, dataWrapStyle);
                ApplyWrapRowHeight(excelRow, $"{noteParts.Scope} {noteParts.Detail}", 90);
            }

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
            SetMergedTextCell(
                sheet,
                sectionRow,
                0,
                SpecSheetMaxColumnIndex,
                sectionRow.GetCell(0)?.StringCellValue ?? string.Empty,
                sectionStyle);

            var groupRow = sheet.CreateRow(rowIdx++);
            SetCell(groupRow, 0, "THÔNG TIN CHUNG", specGroupStyle);
            SetCell(groupRow, 9, "MẶT TRÊN", topGroupStyle);
            SetCell(groupRow, 14, "MẶT DƯỚI", bottomGroupStyle);
            SetMergedTextCell(sheet, groupRow, 0, 8, groupRow.GetCell(0)?.StringCellValue ?? string.Empty, specGroupStyle);
            SetMergedTextCell(sheet, groupRow, 9, 13, groupRow.GetCell(9)?.StringCellValue ?? string.Empty, topGroupStyle);
            SetMergedTextCell(sheet, groupRow, 14, 18, groupRow.GetCell(14)?.StringCellValue ?? string.Empty, bottomGroupStyle);

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

        private static void WritePanelOpeningExplanationSheet(
            TenderProject project,
            ISheet sheet,
            ICellStyle titleStyle,
            ICellStyle infoStyle,
            ICellStyle headerStyle,
            ICellStyle dataStyle,
            ICellStyle dataWrapStyle,
            ICellStyle computedStyle,
            ICellStyle totalStyle,
            ICellStyle sectionStyle)
        {
            int rowIdx = WriteSheetHeader(
                sheet,
                $"DIỄN GIẢI KHỐI LƯỢNG PANEL & LỖ MỞ - {project.ProjectName}",
                project.CustomerName,
                titleStyle,
                infoStyle,
                PanelExplainSheetMaxColumnIndex);

            var orderedWalls = project.Walls
                .OrderBy(w => w.Floor)
                .ThenBy(w => w.Name)
                .ToList();

            var openingTitleRow = sheet.CreateRow(rowIdx++);
            SetCell(openingTitleRow, 0, "DIỄN GIẢI LỖ MỞ THEO VÁCH", sectionStyle);
            SetMergedTextCell(
                sheet,
                openingTitleRow,
                0,
                PanelExplainSheetMaxColumnIndex,
                openingTitleRow.GetCell(0)?.StringCellValue ?? string.Empty,
                sectionStyle);

            string[] openingHeaders =
            {
                "STT", "Tầng", "Ký Hiệu Vách", "Hạng Mục", "Ứng Dụng", "Mã Spec", "Loại Lỗ Mở",
                "Lý Trình LT (mm)", "Rộng (mm)", "Cao (mm)", "Cao Độ Đáy (mm)", "SL", "DT Lỗ Mở (m²)", "Ghi Chú"
            };

            var openingHeaderRow = sheet.CreateRow(rowIdx++);
            for (int i = 0; i < openingHeaders.Length; i++)
            {
                SetCell(openingHeaderRow, i, openingHeaders[i], headerStyle);
            }

            int openingStt = 1;
            int openingDataStart = rowIdx;
            foreach (var wall in orderedWalls)
            {
                var openings = wall.Openings ?? new List<TenderOpening>();
                foreach (var opening in openings)
                {
                    if (opening == null || opening.Width <= 0 || opening.Height <= 0 || opening.Quantity <= 0)
                        continue;

                    var row = sheet.CreateRow(rowIdx++);
                    SetCell(row, 0, openingStt++, dataStyle);
                    SetCell(row, 1, wall.Floor, dataStyle);
                    SetCell(row, 2, wall.Name, dataStyle);
                    SetCell(row, 3, wall.Category, dataStyle);
                    SetCell(row, 4, wall.Application, dataStyle);
                    SetCell(row, 5, wall.SpecKey, dataStyle);
                    SetCell(row, 6, opening.Type, dataStyle);
                    if (opening.CenterStationMm >= 0)
                        SetCell(row, 7, Math.Round(opening.CenterStationMm), computedStyle);
                    else
                        SetCell(row, 7, "Chưa xác định", dataStyle);

                    SetCell(row, 8, Math.Round(opening.Width), computedStyle);
                    SetCell(row, 9, Math.Round(opening.Height), computedStyle);
                    SetCell(row, 10, Math.Round(opening.BottomElevationMm), computedStyle);
                    SetCell(row, 11, Math.Max(1, opening.Quantity), computedStyle);
                    SetFormulaCell(
                        row,
                        12,
                        $"{CellRef(row.RowNum, 8)}*{CellRef(row.RowNum, 9)}*{CellRef(row.RowNum, 11)}/1000000",
                        computedStyle);

                    string openingNote = opening.CenterStationMm >= 0
                        ? $"Vách {wall.Name}: LT {Math.Round(opening.CenterStationMm)} mm"
                        : $"Vách {wall.Name}: chưa có LT";
                    SetCell(row, 13, openingNote, dataWrapStyle);
                    ApplyWrapRowHeight(row, openingNote, 80);
                }
            }

            var openingTotalRow = sheet.CreateRow(rowIdx++);
            SetMergedTextCell(sheet, openingTotalRow, 0, 10, "TỔNG LỖ MỞ", totalStyle);
            if (rowIdx > openingDataStart)
            {
                SetFormulaCell(
                    openingTotalRow,
                    11,
                    $"SUM({CellRef(openingDataStart, 11)}:{CellRef(rowIdx - 2, 11)})",
                    totalStyle);
                SetFormulaCell(
                    openingTotalRow,
                    12,
                    $"SUM({CellRef(openingDataStart, 12)}:{CellRef(rowIdx - 2, 12)})",
                    totalStyle);
            }
            else
            {
                SetCell(openingTotalRow, 11, 0, totalStyle);
                SetCell(openingTotalRow, 12, 0, totalStyle);
            }

            SetCell(openingTotalRow, 13, string.Empty, totalStyle);

            rowIdx += 2;

            var panelTitleRow = sheet.CreateRow(rowIdx++);
            SetCell(panelTitleRow, 0, "DIỄN GIẢI DÒNG TẤM PANEL", sectionStyle);
            SetMergedTextCell(
                sheet,
                panelTitleRow,
                0,
                PanelExplainSheetMaxColumnIndex,
                panelTitleRow.GetCell(0)?.StringCellValue ?? string.Empty,
                sectionStyle);

            string[] panelHeaders =
            {
                "STT", "Tầng", "Ký Hiệu Vách", "Hạng Mục", "Ứng Dụng", "Mã Spec", "Nhóm Dòng",
                "Khổ Tấm (mm)", "Dài Tấm (mm)", "SL", "DT Dòng (m²)", "DT Cấp (m²)", "DT Hao Hụt (m²)", "Ghi Chú"
            };

            var panelHeaderRow = sheet.CreateRow(rowIdx++);
            for (int i = 0; i < panelHeaders.Length; i++)
            {
                SetCell(panelHeaderRow, i, panelHeaders[i], headerStyle);
            }

            int panelStt = 1;
            int panelDataStart = rowIdx;
            foreach (var wall in orderedWalls)
            {
                var breakdown = wall.GetPanelBreakdown();
                foreach (var entry in breakdown)
                {
                    if (entry == null || entry.WidthMm <= 0 || entry.LengthMm <= 0 || entry.Count <= 0)
                        continue;

                    var row = sheet.CreateRow(rowIdx++);
                    SetCell(row, 0, panelStt++, dataStyle);
                    SetCell(row, 1, wall.Floor, dataStyle);
                    SetCell(row, 2, wall.Name, dataStyle);
                    SetCell(row, 3, wall.Category, dataStyle);
                    SetCell(row, 4, wall.Application, dataStyle);
                    SetCell(row, 5, wall.SpecKey, dataStyle);
                    SetCell(row, 6, entry.Label, dataStyle);
                    SetCell(row, 7, entry.WidthMm, computedStyle);
                    SetCell(row, 8, entry.LengthMm, computedStyle);
                    SetCell(row, 9, entry.Count, computedStyle);
                    SetFormulaCell(
                        row,
                        10,
                        $"{CellRef(row.RowNum, 7)}*{CellRef(row.RowNum, 8)}*{CellRef(row.RowNum, 9)}/1000000",
                        computedStyle);
                    SetFormulaCell(
                        row,
                        11,
                        $"IF(LEFT({CellRef(row.RowNum, 6)},6)=\"Hao hụt\",0,{CellRef(row.RowNum, 10)})",
                        computedStyle);
                    SetFormulaCell(
                        row,
                        12,
                        $"IF(LEFT({CellRef(row.RowNum, 6)},6)=\"Hao hụt\",{CellRef(row.RowNum, 10)},0)",
                        computedStyle);

                    string panelNote = $"Khổ chuẩn {wall.PanelWidth} mm; Lỗ mở: {wall.TotalOpeningCount}";
                    SetCell(row, 13, panelNote, dataWrapStyle);
                    ApplyWrapRowHeight(row, panelNote, 80);
                }
            }

            var panelTotalRow = sheet.CreateRow(rowIdx++);
            SetMergedTextCell(sheet, panelTotalRow, 0, 9, "TỔNG PANEL", totalStyle);
            if (rowIdx > panelDataStart)
            {
                SetFormulaCell(
                    panelTotalRow,
                    10,
                    $"SUM({CellRef(panelDataStart, 10)}:{CellRef(rowIdx - 2, 10)})",
                    totalStyle);
                SetFormulaCell(
                    panelTotalRow,
                    11,
                    $"SUM({CellRef(panelDataStart, 11)}:{CellRef(rowIdx - 2, 11)})",
                    totalStyle);
                SetFormulaCell(
                    panelTotalRow,
                    12,
                    $"SUM({CellRef(panelDataStart, 12)}:{CellRef(rowIdx - 2, 12)})",
                    totalStyle);
            }
            else
            {
                SetCell(panelTotalRow, 10, 0, totalStyle);
                SetCell(panelTotalRow, 11, 0, totalStyle);
                SetCell(panelTotalRow, 12, 0, totalStyle);
            }

            SetCell(panelTotalRow, 13, string.Empty, totalStyle);
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
                6, 10, 12, 14, 12, 13, 28, 16, 9, 24, 20, 20, 14, 10, 15, 34, 52
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

        private static void ApplyPanelExplainSheetColumnWidths(ISheet sheet)
        {
            int[] widths =
            {
                6, 12, 14, 12, 12, 12, 18, 14, 11, 11, 14, 8, 14, 40
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

        private static void SetFormulaCell(IRow row, int col, string formula, ICellStyle style)
        {
            var cell = row.CreateCell(col);
            cell.CellFormula = formula;
            cell.CellStyle = style;
        }

        private static string CellRef(int zeroBasedRowIndex, int zeroBasedColumnIndex)
        {
            return $"{ToExcelColumnName(zeroBasedColumnIndex)}{zeroBasedRowIndex + 1}";
        }

        private static string ToExcelColumnName(int columnIndex)
        {
            if (columnIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(columnIndex));

            int current = columnIndex;
            string name = string.Empty;
            while (current >= 0)
            {
                int remainder = current % 26;
                name = (char)('A' + remainder) + name;
                current = current / 26 - 1;
            }

            return name;
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
