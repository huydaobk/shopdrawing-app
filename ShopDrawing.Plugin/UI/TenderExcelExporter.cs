using System;
using System.IO;
using System.Linq;
using NPOI.SS.UserModel;
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
        public void Export(TenderProject project, string filePath)
        {
            var workbook = new XSSFWorkbook();
            var sheet = workbook.CreateSheet("Khối lượng đấu thầu");

            var headerStyle = CreateHeaderStyle(workbook);
            var dataStyle = CreateDataStyle(workbook);
            var computedStyle = CreateComputedStyle(workbook);
            var totalStyle = CreateTotalStyle(workbook);
            var sectionStyle = CreateSectionStyle(workbook);

            int rowIdx = 0;

            var titleRow = sheet.CreateRow(rowIdx++);
            var titleCell = titleRow.CreateCell(0);
            titleCell.SetCellValue($"BẢNG KHỐI LƯỢNG ĐẤU THẦU - {project.ProjectName}");
            titleCell.CellStyle = CreateTitleStyle(workbook);

            var infoRow = sheet.CreateRow(rowIdx++);
            infoRow.CreateCell(0).SetCellValue($"Khách hàng: {project.CustomerName}");
            infoRow.CreateCell(5).SetCellValue($"Ngày xuất: {DateTime.Now:dd/MM/yyyy}");
            rowIdx++;

            rowIdx = WriteWallSection(project, sheet, rowIdx, headerStyle, dataStyle, computedStyle, totalStyle, sectionStyle);
            rowIdx += 2;
            rowIdx = WriteAccessoryBasisSection(project, sheet, rowIdx, headerStyle, dataStyle, computedStyle, sectionStyle);
            rowIdx += 2;
            rowIdx = WriteAccessorySummarySection(project, sheet, rowIdx, headerStyle, dataStyle, computedStyle, totalStyle, sectionStyle);

            for (int i = 0; i <= 22; i++)
            {
                try { sheet.AutoSizeColumn(i); }
                catch { sheet.SetColumnWidth(i, 14 * 256); }
            }

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

            string[] headers =
            {
                "STT", "Hạng mục", "Tầng", "Ký hiệu vách", "Dài (mm)", "Cao (mm)",
                "Mã spec", "Ứng dụng", "Lộ trên", "Lộ dưới", "Đầu lộ", "Cuối lộ",
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
                    SetCell(row, col++, wall.TopEdgeExposed ? "Có" : "Không", dataStyle);
                    SetCell(row, col++, wall.BottomEdgeExposed ? "Có" : "Không", dataStyle);
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

            string[] headers =
            {
                "STT", "Tầng", "Hạng mục", "Ký hiệu vách", "Ứng dụng", "Mã spec",
                "Phụ kiện", "Vật liệu", "Vị trí", "Quy tắc tính", "Cơ sở tính", "Giá trị cơ sở", "Hệ số", "Khối lượng tự động", "Ghi chú"
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
                SetCell(excelRow, 14, row.Note, dataStyle);
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

            string[] headers =
            {
                "STT", "Phạm vi hạng mục", "Ứng dụng", "Mã spec", "Phụ kiện", "Vật liệu", "Vị trí", "Đơn vị",
                "Quy tắc tính", "Cơ sở tính", "Giá trị cơ sở", "Hệ số", "Hao hụt (%)",
                "Khối lượng tự động", "Điều chỉnh", "Khối lượng chốt", "Ghi chú"
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
                SetCell(excelRow, 16, row.Note, dataStyle);
            }

            var totalRow = sheet.CreateRow(rowIdx++);
            SetCell(totalRow, 3, "TỔNG CHỐT:", totalStyle);
            SetCell(totalRow, 15, summary.Sum(item => item.FinalQuantity), totalStyle);
            return rowIdx;
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
            font.FontHeightInPoints = 14;
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

        private static ICellStyle CreateSectionStyle(IWorkbook workbook)
        {
            var style = workbook.CreateCellStyle();
            var font = workbook.CreateFont();
            font.IsBold = true;
            font.FontHeightInPoints = 12;
            style.SetFont(font);
            return style;
        }
    }
}
