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
    /// Cấu trúc: dữ liệu vách, cơ sở tính phụ kiện, tổng hợp phụ kiện chốt.
    /// </summary>
    public class TenderExcelExporter
    {
        private const int TenderSheetMaxColumnIndex = 22;
        private const int SpecSheetMaxColumnIndex = 18;

        public void Export(TenderProject project, string filePath)
        {
            var workbook = new XSSFWorkbook();
            var sheet = workbook.CreateSheet("Khối lượng đấu thầu");
            var specSheet = workbook.CreateSheet("Quản lý Spec");

            var headerStyle = CreateHeaderStyle(workbook);
            var dataStyle = CreateDataStyle(workbook);
            var computedStyle = CreateComputedStyle(workbook);
            var totalStyle = CreateTotalStyle(workbook);
            var panelSectionStyle = CreateColoredSectionStyle(workbook, IndexedColors.DarkBlue.Index);
            var accessorySummarySectionStyle = CreateColoredSectionStyle(workbook, IndexedColors.DarkGreen.Index);
            var wallSectionStyle = CreateColoredSectionStyle(workbook, IndexedColors.Teal.Index);
            var accessoryBasisSectionStyle = CreateColoredSectionStyle(workbook, IndexedColors.Brown.Index);
            var specSectionStyle = CreateColoredSectionStyle(workbook, IndexedColors.Grey50Percent.Index);

            int rowIdx = 0;

            var titleRow = sheet.CreateRow(rowIdx++);
            var titleCell = titleRow.CreateCell(0);
            titleCell.SetCellValue($"BẢNG KHỐI LƯỢNG ĐẤU THẦU - {project.ProjectName}");
            titleCell.CellStyle = CreateTitleStyle(workbook);
            MergeRowAcross(sheet, titleRow.RowNum, TenderSheetMaxColumnIndex);

            var infoRow = sheet.CreateRow(rowIdx++);
            infoRow.CreateCell(0).SetCellValue($"Khách hàng: {project.CustomerName}");
            infoRow.CreateCell(5).SetCellValue($"Ngày xuất: {DateTime.Now:dd/MM/yyyy}");
            rowIdx++;

            rowIdx = WritePanelSummarySection(project, sheet, rowIdx, headerStyle, dataStyle, computedStyle, totalStyle, panelSectionStyle);
            rowIdx += 2;
            rowIdx = WriteAccessorySummarySection(project, sheet, rowIdx, headerStyle, dataStyle, computedStyle, totalStyle, accessorySummarySectionStyle);
            rowIdx += 2;
            rowIdx = WriteWallSection(project, sheet, rowIdx, headerStyle, dataStyle, computedStyle, totalStyle, wallSectionStyle);
            rowIdx += 2;
            rowIdx = WriteAccessoryBasisSection(project, sheet, rowIdx, headerStyle, dataStyle, computedStyle, accessoryBasisSectionStyle);
            WriteSpecSheet(project, specSheet, headerStyle, dataStyle, computedStyle, totalStyle, specSectionStyle, workbook);

            AutoSizeSheet(sheet, TenderSheetMaxColumnIndex);
            AutoSizeSheet(specSheet, SpecSheetMaxColumnIndex);
            ApplyTenderSheetColumnWidths(sheet);
            ApplySpecSheetColumnWidths(specSheet);
            sheet.CreateFreezePane(0, 3);
            sheet.SetZoom(90);
            specSheet.SetZoom(90);
            for (int i = 0; i <= 23; i++)
            {
                try
                {
                    sheet.AutoSizeColumn(i);
                }
                catch (System.Exception ex) { ShopDrawing.Plugin.Core.PluginLogger.Warn("Suppressed exception: " + ex.Message); 
                    sheet.SetColumnWidth(i, 14 * 256);
                }
            }

            ApplyTenderSheetColumnWidths(sheet);
            ApplySpecSheetColumnWidths(specSheet);

            using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write);
            workbook.Write(fs);
        }

        private static int WriteWallSection(
            TenderProject project,
            ISheet sheet,
            int rowIdx,
            ICellStyle headerStyle,
            ICellStyle dataStyle,
            ICellStyle computedStyle,
            ICellStyle totalStyle,
            ICellStyle sectionStyle)
        {
            var sectionRow = sheet.CreateRow(rowIdx++);
            SetCell(sectionRow, 0, "DỮ LIỆU VÁCH VÀ LỖ MỞ", sectionStyle);
            MergeRowAcross(sheet, sectionRow.RowNum, TenderSheetMaxColumnIndex);

            string[] headers =
            {
                "STT", "Hạng mục", "Tầng", "Ký hiệu vách", "Dài (mm)", "Cao (mm)",
                "Mã spec", "Ứng dụng", "Chi tiết đỉnh vách", "Chi tiết đầu/cuối vách", "Chi tiết chân vách", "Xử lý mép trái", "Xử lý mép phải",
                "Góc ngoài", "Góc trong", "Khổ tấm (mm)", "DT vách (m²)",
                "Rộng lỗ mở (mm)", "Cao lỗ mở (mm)", "SL lỗ mở", "DT lỗ mở (m²)",
                "DT net (m²)", "Số tấm"
            };

            var headerRow = sheet.CreateRow(rowIdx++);
            for (int i = 0; i < headers.Length; i++)
            {
                var cell = headerRow.CreateCell(i);
                cell.SetCellValue(headers[i]);
                cell.CellStyle = headerStyle;
            }

            var wallsByFloor = project.Walls.GroupBy(w => w.Floor).OrderBy(g => g.Key);
            int stt = 1;

            foreach (var floorGroup in wallsByFloor)
            {
                double floorWallArea = 0;
                double floorOpeningArea = 0;
                double floorNetArea = 0;
                int floorPanels = 0;

                foreach (var wall in floorGroup)
                {
                    var row = sheet.CreateRow(rowIdx++);
                    int col = 0;
                    SetCell(row, col++, stt++, dataStyle);
                    SetCell(row, col++, wall.Category, dataStyle);
                    SetCell(row, col++, wall.Floor, dataStyle);
                    SetCell(row, col++, wall.Name, dataStyle);
                    SetCell(row, col++, wall.Length, dataStyle);
                    SetCell(row, col++, wall.Height, dataStyle);
                    SetCell(row, col++, wall.SpecKey, dataStyle);
                    SetCell(row, col++, wall.Application, dataStyle);
                    SetCell(row, col++, wall.ResolvedTopPanelTreatment, dataStyle);
                    SetCell(row, col++, wall.ResolvedEndPanelTreatment, dataStyle);
                    SetCell(row, col++, wall.IsColdStorageWall ? wall.ResolvedBottomPanelTreatment : (wall.BottomEdgeExposed ? "Có" : "Không"), dataStyle);
                    SetCell(row, col++, wall.StartEdgeExposed ? "Có" : "Không", dataStyle);
                    SetCell(row, col++, wall.EndEdgeExposed ? "Có" : "Không", dataStyle);
                    SetCell(row, col++, wall.OutsideCornerCount, dataStyle);
                    SetCell(row, col++, wall.InsideCornerCount, dataStyle);
                    SetCell(row, col++, wall.PanelWidth, dataStyle);
                    SetCell(row, col++, wall.WallAreaM2, computedStyle);

                    if (wall.Openings.Count > 0)
                    {
                        var firstOpening = wall.Openings[0];
                        SetCell(row, col++, firstOpening.Width, dataStyle);
                        SetCell(row, col++, firstOpening.Height, dataStyle);
                        SetCell(row, col++, firstOpening.Quantity, dataStyle);
                        SetCell(row, col++, firstOpening.TotalAreaM2, computedStyle);

                        for (int i = 1; i < wall.Openings.Count; i++)
                        {
                            var opening = wall.Openings[i];
                            var opRow = sheet.CreateRow(rowIdx++);
                            SetCell(opRow, 3, wall.Name, dataStyle);
                            SetCell(opRow, 16, opening.Width, dataStyle);
                            SetCell(opRow, 17, opening.Height, dataStyle);
                            SetCell(opRow, 18, opening.Quantity, dataStyle);
                            SetCell(opRow, 19, opening.TotalAreaM2, computedStyle);
                        }

                        var sumRow = sheet.CreateRow(rowIdx++);
                        SetCell(sumRow, 19, wall.OpeningAreaM2, totalStyle);
                        SetCell(sumRow, 20, wall.NetAreaM2, computedStyle);
                        SetCell(sumRow, 21, wall.EstimatedPanelCount, computedStyle);
                    }
                    else
                    {
                        col += 3;
                        SetCell(row, col++, 0.0, computedStyle);
                        SetCell(row, col++, wall.NetAreaM2, computedStyle);
                        SetCell(row, col++, wall.EstimatedPanelCount, computedStyle);
                    }

                    floorWallArea += wall.WallAreaM2;
                    floorOpeningArea += wall.OpeningAreaM2;
                    floorNetArea += wall.NetAreaM2;
                    floorPanels += wall.EstimatedPanelCount;
                }

                var floorTotalRow = sheet.CreateRow(rowIdx++);
                SetCell(floorTotalRow, 2, $"TỔNG {floorGroup.Key}:", totalStyle);
                SetCell(floorTotalRow, 15, floorWallArea, totalStyle);
                SetCell(floorTotalRow, 19, floorOpeningArea, totalStyle);
                SetCell(floorTotalRow, 20, floorNetArea, totalStyle);
                SetCell(floorTotalRow, 21, floorPanels, totalStyle);
                rowIdx++;
            }

            var grandRow = sheet.CreateRow(rowIdx++);
            SetCell(grandRow, 2, "TỔNG CỘNG:", totalStyle);
            SetCell(grandRow, 15, project.Walls.Sum(w => w.WallAreaM2), totalStyle);
            SetCell(grandRow, 19, project.Walls.Sum(w => w.OpeningAreaM2), totalStyle);
            SetCell(grandRow, 20, project.Walls.Sum(w => w.NetAreaM2), totalStyle);
            SetCell(grandRow, 21, project.Walls.Sum(w => w.EstimatedPanelCount), totalStyle);

            return rowIdx;
        }

        private static int WritePanelSummarySection(

            TenderProject project,

            ISheet sheet,

            int rowIdx,

            ICellStyle headerStyle,

            ICellStyle dataStyle,

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
                "DT vách (m²)", "DT lỗ mở (m²)", "DT net (m²)", "Khối lượng thực tế cần cấp (tấm)",
                "DT dự kiến cấp (m²)", "Khối lượng hao hụt tổng (m²)", "Hao hụt (%)"
            };

            var headerRow = sheet.CreateRow(rowIdx++);
            for (int i = 0; i < headers.Length; i++)
            {
                var cell = headerRow.CreateCell(i);
                cell.SetCellValue(headers[i]);
                cell.CellStyle = headerStyle;
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
                SetCell(excelRow, col++, row.EstimatedPanels, computedStyle);
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
            SetCell(totalRow, 10, rows.Sum(x => x.EstimatedPanels), totalStyle);
            SetCell(totalRow, 11, rows.Sum(x => x.OrderedAreaM2), totalStyle);
            SetCell(totalRow, 12, rows.Sum(x => x.WasteAreaM2), totalStyle);

            double totalOrderedArea = rows.Sum(x => x.OrderedAreaM2);
            double totalWasteArea = rows.Sum(x => x.WasteAreaM2);
            double totalWastePercent = totalOrderedArea > 0 ? totalWasteArea / totalOrderedArea * 100.0 : 0.0;
            SetCell(totalRow, 13, totalWastePercent, totalStyle);

            rowIdx++;

            var supplyTitleRow = sheet.CreateRow(rowIdx++);
            SetCell(supplyTitleRow, 0, "TỔNG CẤP DỰ KIẾN", sectionStyle);
            MergeRowAcross(sheet, supplyTitleRow.RowNum, TenderSheetMaxColumnIndex);

            var supplyHeaderRow = sheet.CreateRow(rowIdx++);
            SetCell(supplyHeaderRow, 0, "Chỉ tiêu", headerStyle);
            SetCell(supplyHeaderRow, 1, "Giá trị", headerStyle);
            SetCell(supplyHeaderRow, 2, "Ghi chú", headerStyle);

            var supplyRows = new (string Label, double Value, string Note)[]
            {
                ("Khối lượng thực tế cần cấp (tấm)", rows.Sum(x => x.EstimatedPanels), "Tổng số tấm nguyên cần cấp để triển khai sản xuất/cắt lắp."),
                ("Khối lượng hao hụt tổng (m²)", totalWasteArea, "Bao gồm phần cắt bỏ tấm cuối và phần diện tích panel bị vướng vào lỗ mở."),
                ("Tổng diện tích dự kiến phải cấp (m²)", totalOrderedArea, "Diện tích panel quy đổi theo tổng số tấm nguyên cần cấp."),
                ("Tỷ lệ hao hụt tổng (%)", totalWastePercent, "Tỷ lệ hao hụt = Khối lượng hao hụt tổng / Tổng diện tích dự kiến phải cấp.")
            };

            foreach (var item in supplyRows)
            {
                var supplyRow = sheet.CreateRow(rowIdx++);
                SetCell(supplyRow, 0, item.Label, dataStyle);
                SetCell(supplyRow, 1, item.Value, computedStyle);
                SetCell(supplyRow, 2, item.Note, dataStyle);
            }

            return rowIdx;

        }

        private static int WriteAccessoryBasisSection(
            TenderProject project,
            ISheet sheet,
            int rowIdx,
            ICellStyle headerStyle,
            ICellStyle dataStyle,
            ICellStyle computedStyle,
            ICellStyle sectionStyle)
        {
            var calculator = new TenderBomCalculator();
            var report = calculator.CalculateAccessoryReport(project.Walls, project.Accessories);

            var titleRow = sheet.CreateRow(rowIdx++);
            SetCell(titleRow, 0, "CƠ SỞ TÍNH PHỤ KIỆN", sectionStyle);
            MergeRowAcross(sheet, titleRow.RowNum, TenderSheetMaxColumnIndex);

            string[] headers =
            {
                "STT", "Tầng", "Hạng mục", "Ký hiệu vách", "Ứng dụng", "Mã spec",
                "Phụ kiện", "Vật liệu", "Vị trí", "Quy tắc tính", "Cơ sở tính", "Giá trị cơ sở", "Hệ số", "Khối lượng tự động", "Vị trí / Phạm vi", "Thông số chính"
            };

            var headerRow = sheet.CreateRow(rowIdx++);
            for (int i = 0; i < headers.Length; i++)
            {
                var cell = headerRow.CreateCell(i);
                cell.SetCellValue(headers[i]);
                cell.CellStyle = headerStyle;
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
                SetCell(excelRow, 14, noteParts.Scope, dataStyle);
                SetCell(excelRow, 15, noteParts.Detail, dataStyle);
            }

            return rowIdx;
        }

        private static int WriteAccessorySummarySection(
            TenderProject project,
            ISheet sheet,
            int rowIdx,
            ICellStyle headerStyle,
            ICellStyle dataStyle,
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
                var cell = headerRow.CreateCell(i);
                cell.SetCellValue(headers[i]);
                cell.CellStyle = headerStyle;
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
                SetCell(excelRow, 16, noteParts.Scope, dataStyle);
                SetCell(excelRow, 17, noteParts.Detail, dataStyle);
            }

            var totalRow = sheet.CreateRow(rowIdx++);
            SetCell(totalRow, 3, "TỔNG CHỐT:", totalStyle);
            SetCell(totalRow, 15, summary.Sum(item => item.FinalQuantity), totalStyle);
            return rowIdx;
        }

        private static void WriteSpecSheet(
            TenderProject project,
            ISheet sheet,
            ICellStyle headerStyle,
            ICellStyle dataStyle,
            ICellStyle computedStyle,
            ICellStyle totalStyle,
            ICellStyle sectionStyle,
            IWorkbook workbook)

        {

            int rowIdx = 0;
            var specGroupStyle = CreateGroupHeaderStyle(workbook, IndexedColors.Grey50Percent.Index);
            var topGroupStyle = CreateGroupHeaderStyle(workbook, IndexedColors.Blue.Index);
            var bottomGroupStyle = CreateGroupHeaderStyle(workbook, IndexedColors.Green.Index);
            var topHeaderStyle = CreateSubHeaderStyle(workbook, IndexedColors.PaleBlue.Index);
            var bottomHeaderStyle = CreateSubHeaderStyle(workbook, IndexedColors.LightGreen.Index);

            var titleRow = sheet.CreateRow(rowIdx++);
            var titleCell = titleRow.CreateCell(0);
            titleCell.SetCellValue($"BẢNG QUẢN LÝ SPEC - {project.ProjectName}");
            titleCell.CellStyle = CreateTitleStyle(workbook);
            MergeRowAcross(sheet, titleRow.RowNum, SpecSheetMaxColumnIndex);

            var infoRow = sheet.CreateRow(rowIdx++);
            infoRow.CreateCell(0).SetCellValue($"Khách hàng: {project.CustomerName}");
            infoRow.CreateCell(5).SetCellValue($"Ngày xuất: {DateTime.Now:dd/MM/yyyy}");
            rowIdx++;

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
                var cell = headerRow.CreateCell(i);
                cell.SetCellValue(headers[i]);
                cell.CellStyle = i switch
                {
                    >= 9 and <= 13 => topHeaderStyle,
                    >= 14 and <= 18 => bottomHeaderStyle,
                    _ => headerStyle
                };
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
            SetCell(totalRow, 1, "TỔNG SỐ SPEC:", headerStyle);
            SetCell(totalRow, 2, project.Specs.Count, totalStyle);
            sheet.CreateFreezePane(0, rowIdx - project.Specs.Count - 1);

        }

        private static void AutoSizeSheet(ISheet sheet, int maxColumnIndex)

        {

            for (int i = 0; i <= maxColumnIndex; i++)
            {
                try
                {
                    sheet.AutoSizeColumn(i);
                }
                catch (System.Exception ex)
                {
                    ShopDrawing.Plugin.Core.PluginLogger.Warn("Suppressed exception: " + ex.Message);
                    sheet.SetColumnWidth(i, 14 * 256);
                }
            }

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

        private static void ApplySpecSheetColumnWidths(ISheet sheet)

        {

            int[] widths =
            {
                6,   // STT
                18,  // Mã spec
                12,  // Mã ký hiệu
                12,  // Khổ tấm
                14,  // Loại panel
                12,  // Tỷ trọng
                14,  // Chiều dày
                12,  // Chống cháy
                8,   // FM
                14,  // Màu mặt trên
                16,  // Vật liệu mặt trên
                14,  // Độ mạ mặt trên
                18,  // Dày tôn mặt trên
                16,  // Profile mặt trên
                14,  // Màu mặt dưới
                16,  // Vật liệu mặt dưới
                14,  // Độ mạ mặt dưới
                18,  // Dày tôn mặt dưới
                16   // Profile mặt dưới
            };

            for (int i = 0; i < widths.Length; i++)
            {
                int targetWidth = widths[i] * 256;
                if (sheet.GetColumnWidth(i) < targetWidth)
                    sheet.SetColumnWidth(i, targetWidth);
            }

            sheet.SetColumnWidth(14, Math.Max(sheet.GetColumnWidth(14), 24 * 256));
            sheet.SetColumnWidth(15, Math.Max(sheet.GetColumnWidth(15), 34 * 256));
            sheet.SetColumnWidth(16, Math.Max(sheet.GetColumnWidth(16), 24 * 256));
            sheet.SetColumnWidth(17, Math.Max(sheet.GetColumnWidth(17), 34 * 256));

        }

        private static void ApplyTenderSheetColumnWidths(ISheet sheet)

        {

            int[] widths =
            {
                6, 12, 14, 16, 12, 12, 18, 14, 20, 20, 18, 14, 14, 10, 34, 14, 40, 14, 14, 12, 16, 16, 14
            };

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
            style.BorderBottom = BorderStyle.Thin;
            style.Alignment = HorizontalAlignment.Center;
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
            style.BorderTop = BorderStyle.Thin;
            style.BorderBottom = BorderStyle.Thin;
            style.BorderLeft = BorderStyle.Thin;
            style.BorderRight = BorderStyle.Thin;
            return style;

        }

        private static ICellStyle CreateDataStyle(IWorkbook workbook)
        {
            var style = workbook.CreateCellStyle();
            style.BorderBottom = BorderStyle.Thin;
            style.BorderLeft = BorderStyle.Thin;
            style.BorderRight = BorderStyle.Thin;
            return style;
        }

        private static ICellStyle CreateComputedStyle(IWorkbook workbook)
        {
            var style = workbook.CreateCellStyle();
            var font = workbook.CreateFont();
            font.Color = NPOI.HSSF.Util.HSSFColor.Blue.Index;
            style.SetFont(font);
            style.BorderBottom = BorderStyle.Thin;
            style.DataFormat = workbook.CreateDataFormat().GetFormat("#,##0.00");
            return style;
        }

        private static ICellStyle CreateTotalStyle(IWorkbook workbook)
        {
            var style = workbook.CreateCellStyle();
            var font = workbook.CreateFont();
            font.IsBold = true;
            font.FontHeightInPoints = 10;
            style.SetFont(font);
            style.BorderTop = BorderStyle.Medium;
            style.BorderBottom = BorderStyle.Medium;
            style.DataFormat = workbook.CreateDataFormat().GetFormat("#,##0.00");
            return style;
        }

        private static ICellStyle CreateSectionStyle(IWorkbook workbook, short fillColor)
        {
            var style = workbook.CreateCellStyle();
            var font = workbook.CreateFont();
            font.IsBold = true;
            font.FontHeightInPoints = 12;
            font.Color = IndexedColors.White.Index;
            style.SetFont(font);
            return style;
        }
    }
}
