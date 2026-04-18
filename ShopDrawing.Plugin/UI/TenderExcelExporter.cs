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
    /// Xu\u1EA5t b\u1EA3ng kh\u1ED1i l\u01B0\u1EE3ng \u0111\u1EA5u th\u1EA7u ra Excel.
    /// </summary>
    public class TenderExcelExporter
    {
        private const int TenderSheetMaxColumnIndex = 17;
        private const int TenderPanelSummaryMaxColumnIndex = 10;
        private const int BasisSheetMaxColumnIndex = 16;
        private const int SpecSheetMaxColumnIndex = 18;
        private const int PanelExplainSheetMaxColumnIndex = 13;
        // Keep sheet names ASCII + short to avoid NPOI validation issues on some encodings/locales.
        private const string TenderSheetName = "\u004B\u0068\u1ED1\u0069 \u006C\u01B0\u1EE3\u006E\u0067 \u0111\u1EA5\u0075 \u0074\u0068\u1EA7\u0075";
        private const string PanelExplainSheetName = "\u0044\u0069\u1EC5\u006E \u0067\u0069\u1EA3\u0069 \u0070\u0061\u006E\u0065\u006C";
        private const string BasisSheetName = "\u0043\u01A1 \u0073\u1EDF \u0070\u0068\u1EE5 \u006B\u0069\u1EC7\u006E";
        private const string SpecSheetName = "\u0051\u0075\u1EA3\u006E \u006C\u00FD \u0073\u0070\u0065\u0063";

        public void Export(TenderProject project, string filePath)
        {
            var workbook = new XSSFWorkbook();

            var tenderSheet = workbook.CreateSheet(TenderSheetName);
            var panelExplainSheet = workbook.CreateSheet(PanelExplainSheetName);
            var basisSheet = workbook.CreateSheet(BasisSheetName);
            var specSheet = workbook.CreateSheet(SpecSheetName);

            // Ch\u1ED1t th\u1EE9 t\u1EF1 sheet: Kh\u1ED1i l\u01B0\u1EE3ng \u0111\u1EA5u th\u1EA7u -> Di\u1EC5n gi\u1EA3i -> C\u01A1 s\u1EDF -> Spec.
            workbook.SetSheetOrder(TenderSheetName, 0);
            workbook.SetSheetOrder(PanelExplainSheetName, 1);
            workbook.SetSheetOrder(BasisSheetName, 2);
            workbook.SetSheetOrder(SpecSheetName, 3);

            var titleStyle = CreateTitleStyle(workbook);
            var infoStyle = CreateInfoStyle(workbook);
            var headerStyle = CreateHeaderStyle(workbook);
            var dataStyle = CreateDataStyle(workbook);
            var dataWrapStyle = CreateWrappedDataStyle(workbook, dataStyle);
            var computedStyle = CreateComputedStyle(workbook, dataStyle);
            var computedIntegerStyle = CreateComputedIntegerStyle(workbook, dataStyle);
            var totalStyle = CreateTotalStyle(workbook, dataStyle);
            var totalIntegerStyle = CreateTotalIntegerStyle(workbook, dataStyle);

            var panelSectionStyle = CreateColoredSectionStyle(workbook, IndexedColors.DarkBlue.Index);
            var accessorySummarySectionStyle = CreateColoredSectionStyle(workbook, IndexedColors.DarkGreen.Index);
            var accessoryBasisSectionStyle = CreateColoredSectionStyle(workbook, IndexedColors.Brown.Index);
            var specSectionStyle = CreateColoredSectionStyle(workbook, IndexedColors.Grey50Percent.Index);

            int tenderRowIdx = WriteSheetHeader(
                tenderSheet,
                $"B\u1EA2NG KH\u1ED0I L\u01AF\u1EE2NG \u0110\u1EA4U TH\u1EA6U - {project.ProjectName}",
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
                $"C\u01A0 S\u1EDE T\u00CDNH PH\u1EE4 KI\u1EC6N - {project.ProjectName}",
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
                computedIntegerStyle,
                totalStyle,
                totalIntegerStyle,
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
            SetCell(infoRow, 0, $"Kh\u00E1ch h\u00E0ng: {customerName}", infoStyle);
            SetCell(infoRow, Math.Min(5, maxColumnIndex), $"Ng\u00E0y xu\u1EA5t: {DateTime.Now:dd/MM/yyyy}", infoStyle);

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
            SetCell(sectionRow, 0, "T\u1ED4NG H\u1EE2P KH\u1ED0I L\u01AF\u1EE2NG T\u1EA4M THEO T\u1EA6NG + SPEC", sectionStyle);
            SetMergedTextCell(
                sheet,
                sectionRow,
                0,
                TenderPanelSummaryMaxColumnIndex,
                sectionRow.GetCell(0)?.StringCellValue ?? string.Empty,
                sectionStyle);

            string[] headers =
            {
                "STT", "T\u1EA7ng", "H\u1EA1ng m\u1EE5c", "M\u00E3 spec", "S\u1ED1 v\u00F9ng",
                "DT h\u00ECnh h\u1ECDc (m\u00B2)", "DT l\u1ED7 m\u1EDF (m\u00B2)", "DT net (m\u00B2)",
                "DT d\u1EF1 ki\u1EBFn c\u1EA5p (m\u00B2)", "Kh\u1ED1i l\u01B0\u1EE3ng hao h\u1EE5t t\u1ED5ng (m\u00B2)", "Hao h\u1EE5t (%)"
            };

            var headerRow = sheet.CreateRow(rowIdx++);
            for (int i = 0; i < headers.Length; i++)
            {
                SetCell(headerRow, i, headers[i], headerStyle);
            }

            int dataStartRowIndex = rowIdx;
            int stt = 1;
            string panelExplainSheetRef = $"'{PanelExplainSheetName}'";
            foreach (var row in rows)
            {
                var excelRow = sheet.CreateRow(rowIdx++);
                int col = 0;
                SetCell(excelRow, col++, stt++, dataStyle);
                SetCell(excelRow, col++, row.Floor, dataStyle);
                SetCell(excelRow, col++, row.Category, dataStyle);
                SetCell(excelRow, col++, row.SpecKey, dataStyle);
                SetCell(excelRow, col++, row.WallCount, dataStyle);
                string floorRef = CellRef(excelRow.RowNum, 1);
                string categoryRef = CellRef(excelRow.RowNum, 2);
                string specRef = CellRef(excelRow.RowNum, 3);
                SetFormulaCell(
                    excelRow,
                    col++,
                    $"SUMIFS({panelExplainSheetRef}!$J:$J,{panelExplainSheetRef}!$B:$B,{floorRef},{panelExplainSheetRef}!$D:$D,{categoryRef},{panelExplainSheetRef}!$F:$F,{specRef},{panelExplainSheetRef}!$G:$G,\"Th\u00F4ng s\u1ED1\")",
                    computedStyle);
                SetFormulaCell(
                    excelRow,
                    col++,
                    $"SUMIFS({panelExplainSheetRef}!$M:$M,{panelExplainSheetRef}!$B:$B,{floorRef},{panelExplainSheetRef}!$D:$D,{categoryRef},{panelExplainSheetRef}!$F:$F,{specRef},{panelExplainSheetRef}!$G:$G,\"C\u1EEDa \u0111i\")+SUMIFS({panelExplainSheetRef}!$M:$M,{panelExplainSheetRef}!$B:$B,{floorRef},{panelExplainSheetRef}!$D:$D,{categoryRef},{panelExplainSheetRef}!$F:$F,{specRef},{panelExplainSheetRef}!$G:$G,\"C\u1EEDa s\u1ED5\")+SUMIFS({panelExplainSheetRef}!$M:$M,{panelExplainSheetRef}!$B:$B,{floorRef},{panelExplainSheetRef}!$D:$D,{categoryRef},{panelExplainSheetRef}!$F:$F,{specRef},{panelExplainSheetRef}!$G:$G,\"L\u1ED7 k\u1EF9 thu\u1EADt\")",
                    computedStyle);
                SetFormulaCell(
                    excelRow,
                    col++,
                    $"MAX(0,{CellRef(excelRow.RowNum, 5)}-{CellRef(excelRow.RowNum, 6)})",
                    computedStyle);
                SetFormulaCell(
                    excelRow,
                    col++,
                    $"SUMIFS({panelExplainSheetRef}!$L:$L,{panelExplainSheetRef}!$B:$B,{floorRef},{panelExplainSheetRef}!$D:$D,{categoryRef},{panelExplainSheetRef}!$F:$F,{specRef},{panelExplainSheetRef}!$G:$G,\"Nguy\u00EAn\")+SUMIFS({panelExplainSheetRef}!$L:$L,{panelExplainSheetRef}!$B:$B,{floorRef},{panelExplainSheetRef}!$D:$D,{categoryRef},{panelExplainSheetRef}!$F:$F,{specRef},{panelExplainSheetRef}!$G:$G,\"Gi\u1EA3m*\")",
                    computedStyle);
                SetFormulaCell(
                    excelRow,
                    col++,
                    $"SUMIFS({panelExplainSheetRef}!$M:$M,{panelExplainSheetRef}!$B:$B,{floorRef},{panelExplainSheetRef}!$D:$D,{categoryRef},{panelExplainSheetRef}!$F:$F,{specRef},{panelExplainSheetRef}!$G:$G,\"Hao h\u1EE5t*\")",
                    computedStyle);
                SetFormulaCell(
                    excelRow,
                    col++,
                    $"IF({CellRef(excelRow.RowNum, 8)}>0,{CellRef(excelRow.RowNum, 9)}/{CellRef(excelRow.RowNum, 8)}*100,0)",
                    computedStyle);
            }

            int dataEndRowIndex = rowIdx - 1;
            var totalRow = sheet.CreateRow(rowIdx++);
            SetCell(totalRow, 3, "T\u1ED4NG C\u1ED8NG:", totalStyle);
            if (rows.Count > 0)
            {
                SetFormulaCell(totalRow, 4, $"SUM({CellRef(dataStartRowIndex, 4)}:{CellRef(dataEndRowIndex, 4)})", totalStyle);
                SetFormulaCell(totalRow, 5, $"SUM({CellRef(dataStartRowIndex, 5)}:{CellRef(dataEndRowIndex, 5)})", totalStyle);
                SetFormulaCell(totalRow, 6, $"SUM({CellRef(dataStartRowIndex, 6)}:{CellRef(dataEndRowIndex, 6)})", totalStyle);
                SetFormulaCell(totalRow, 7, $"SUM({CellRef(dataStartRowIndex, 7)}:{CellRef(dataEndRowIndex, 7)})", totalStyle);
                SetFormulaCell(totalRow, 8, $"SUM({CellRef(dataStartRowIndex, 8)}:{CellRef(dataEndRowIndex, 8)})", totalStyle);
                SetFormulaCell(totalRow, 9, $"SUM({CellRef(dataStartRowIndex, 9)}:{CellRef(dataEndRowIndex, 9)})", totalStyle);
                SetFormulaCell(totalRow, 10, $"IF({CellRef(totalRow.RowNum, 8)}>0,{CellRef(totalRow.RowNum, 9)}/{CellRef(totalRow.RowNum, 8)}*100,0)", totalStyle);
            }
            else
            {
                SetCell(totalRow, 4, 0, totalStyle);
                SetCell(totalRow, 5, 0, totalStyle);
                SetCell(totalRow, 6, 0, totalStyle);
                SetCell(totalRow, 7, 0, totalStyle);
                SetCell(totalRow, 8, 0, totalStyle);
                SetCell(totalRow, 9, 0, totalStyle);
                SetCell(totalRow, 10, 0, totalStyle);
            }

            rowIdx++;

            var supplyTitleRow = sheet.CreateRow(rowIdx++);
            SetCell(supplyTitleRow, 0, "T\u1ED4NG KH\u1ED0I L\u01AF\u1EE2NG PANEL C\u1EA4P D\u1EF0 KI\u1EBEN", sectionStyle);
            SetMergedTextCell(
                sheet,
                supplyTitleRow,
                0,
                8,
                supplyTitleRow.GetCell(0)?.StringCellValue ?? string.Empty,
                sectionStyle);

            var supplyHeaderRow = sheet.CreateRow(rowIdx++);
            SetCell(supplyHeaderRow, 0, "STT", headerStyle);
            SetCell(supplyHeaderRow, 1, "Ch\u1EC9 ti\u00EAu", headerStyle);
            SetCell(supplyHeaderRow, 2, "Gi\u00E1 tr\u1ECB", headerStyle);
            SetMergedTextCell(sheet, supplyHeaderRow, 3, 8, "Ghi ch\u00FA", headerStyle);

            string totalOrderedRef = CellRef(totalRow.RowNum, 8);
            string totalWasteRef = CellRef(totalRow.RowNum, 9);
            var supplyRows = new (string Label, string Formula, string Note)[]
            {
                ("T\u1ED5ng di\u1EC7n t\u00EDch d\u1EF1 ki\u1EBFn ph\u1EA3i c\u1EA5p (m\u00B2)", totalOrderedRef, "Di\u1EC7n t\u00EDch panel quy \u0111\u1ED5i theo t\u1ED5ng s\u1ED1 t\u1EA5m nguy\u00EAn c\u1EA7n c\u1EA5p."),
                ("Kh\u1ED1i l\u01B0\u1EE3ng hao h\u1EE5t t\u1ED5ng (m\u00B2)", totalWasteRef, "Bao g\u1ED3m ph\u1EA7n c\u1EAFt b\u1ECF t\u1EA5m cu\u1ED1i v\u00E0 ph\u1EA7n di\u1EC7n t\u00EDch panel b\u1ECB v\u01B0\u1EDBng v\u00E0o l\u1ED7 m\u1EDF."),
                ("T\u1EF7 l\u1EC7 hao h\u1EE5t t\u1ED5ng (%)", $"IF({totalOrderedRef}>0,{totalWasteRef}/{totalOrderedRef}*100,0)", "T\u1EF7 l\u1EC7 hao h\u1EE5t = Kh\u1ED1i l\u01B0\u1EE3ng hao h\u1EE5t t\u1ED5ng / T\u1ED5ng di\u1EC7n t\u00EDch d\u1EF1 ki\u1EBFn ph\u1EA3i c\u1EA5p.")
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
            SetCell(titleRow, 0, "C\u01A0 S\u1EDE T\u00CDNH PH\u1EE4 KI\u1EC6N", sectionStyle);
            SetMergedTextCell(
                sheet,
                titleRow,
                0,
                BasisSheetMaxColumnIndex,
                titleRow.GetCell(0)?.StringCellValue ?? string.Empty,
                sectionStyle);

            string[] headers =
            {
                "STT", "T\u1EA7ng", "H\u1EA1ng m\u1EE5c", "K\u00FD hi\u1EC7u v\u00E1ch", "\u1EE8ng d\u1EE5ng", "M\u00E3 spec",
                "Ph\u1EE5 ki\u1EC7n", "V\u1EADt li\u1EC7u", "\u0110\u01A1n v\u1ECB", "V\u1ECB tr\u00ED", "Quy t\u1EAFc t\u00EDnh", "C\u01A1 s\u1EDF t\u00EDnh", "Gi\u00E1 tr\u1ECB c\u01A1 s\u1EDF",
                "H\u1EC7 s\u1ED1", "Kh\u1ED1i l\u01B0\u1EE3ng t\u1EF1 \u0111\u1ED9ng", "V\u1ECB tr\u00ED / Ph\u1EA1m vi", "Th\u00F4ng s\u1ED1 ch\u00EDnh"
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
            SetCell(titleRow, 0, "T\u1ED4NG H\u1EE2P PH\u1EE4 KI\u1EC6N \u0110\u1EA4U TH\u1EA6U", sectionStyle);
            SetMergedTextCell(
                sheet,
                titleRow,
                0,
                TenderSheetMaxColumnIndex,
                titleRow.GetCell(0)?.StringCellValue ?? string.Empty,
                sectionStyle);

            string[] headers =
            {
                "STT", "Ph\u1EA1m vi h\u1EA1ng m\u1EE5c", "\u1EE8ng d\u1EE5ng", "M\u00E3 spec", "Ph\u1EE5 ki\u1EC7n", "V\u1EADt li\u1EC7u", "V\u1ECB tr\u00ED", "\u0110\u01A1n v\u1ECB",
                "Quy t\u1EAFc t\u00EDnh", "C\u01A1 s\u1EDF t\u00EDnh", "Gi\u00E1 tr\u1ECB c\u01A1 s\u1EDF", "H\u1EC7 s\u1ED1", "Hao h\u1EE5t (%)",
                "Kh\u1ED1i l\u01B0\u1EE3ng t\u1EF1 \u0111\u1ED9ng", "\u0110i\u1EC1u ch\u1EC9nh", "Kh\u1ED1i l\u01B0\u1EE3ng ch\u1ED1t", "V\u1ECB tr\u00ED / Ph\u1EA1m vi", "Th\u00F4ng s\u1ED1 ch\u00EDnh"
            };

            var headerRow = sheet.CreateRow(rowIdx++);
            for (int i = 0; i < headers.Length; i++)
            {
                SetCell(headerRow, i, headers[i], headerStyle);
            }

            int stt = 1;
            string basisSheetRef = $"'{BasisSheetName}'";
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
                $"B\u1EA2NG QU\u1EA2N L\u00DD SPEC - {project.ProjectName}",
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
            SetCell(sectionRow, 0, "DANH S\u00C1CH SPEC D\u1EF0 \u00C1N", sectionStyle);
            SetMergedTextCell(
                sheet,
                sectionRow,
                0,
                SpecSheetMaxColumnIndex,
                sectionRow.GetCell(0)?.StringCellValue ?? string.Empty,
                sectionStyle);

            var groupRow = sheet.CreateRow(rowIdx++);
            SetCell(groupRow, 0, "TH\u00D4NG TIN CHUNG", specGroupStyle);
            SetCell(groupRow, 9, "M\u1EB6T TR\u00CAN", topGroupStyle);
            SetCell(groupRow, 14, "M\u1EB6T D\u01AF\u1EDAI", bottomGroupStyle);
            SetMergedTextCell(sheet, groupRow, 0, 8, groupRow.GetCell(0)?.StringCellValue ?? string.Empty, specGroupStyle);
            SetMergedTextCell(sheet, groupRow, 9, 13, groupRow.GetCell(9)?.StringCellValue ?? string.Empty, topGroupStyle);
            SetMergedTextCell(sheet, groupRow, 14, 18, groupRow.GetCell(14)?.StringCellValue ?? string.Empty, bottomGroupStyle);

            string[] headers =
            {
                "STT", "M\u00E3 spec", "M\u00E3 k\u00FD hi\u1EC7u", "Kh\u1ED5 t\u1EA5m (mm)", "Lo\u1EA1i panel", "T\u1EF7 tr\u1ECDng", "Chi\u1EC1u d\u00E0y (mm)", "Ch\u1ED1ng ch\u00E1y", "FM",
                "M\u00E0u m\u1EB7t tr\u00EAn", "V\u1EADt li\u1EC7u m\u1EB7t tr\u00EAn", "\u0110\u1ED9 m\u1EA1 m\u1EB7t tr\u00EAn", "D\u00E0y t\u00F4n m\u1EB7t tr\u00EAn (mm)", "Profile m\u1EB7t tr\u00EAn",
                "M\u00E0u m\u1EB7t d\u01B0\u1EDBi", "V\u1EADt li\u1EC7u m\u1EB7t d\u01B0\u1EDBi", "\u0110\u1ED9 m\u1EA1 m\u1EB7t d\u01B0\u1EDBi", "D\u00E0y t\u00F4n m\u1EB7t d\u01B0\u1EDBi (mm)", "Profile m\u1EB7t d\u01B0\u1EDBi"
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
                SetCell(row, col++, spec.FmApproved ? "C\u00F3" : "Kh\u00F4ng", dataStyle);
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
            SetCell(totalRow, 1, "T\u1ED4NG S\u1ED0 SPEC:", totalStyle);
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
            ICellStyle computedIntegerStyle,
            ICellStyle totalStyle,
            ICellStyle totalIntegerStyle,
            ICellStyle sectionStyle)
        {
            int rowIdx = WriteSheetHeader(
                sheet,
                $"DI\u1EC4N GI\u1EA2I KH\u1ED0I L\u01AF\u1EE2NG PANEL & L\u1ED6 M\u1EDE - {project.ProjectName}",
                project.CustomerName,
                titleStyle,
                infoStyle,
                PanelExplainSheetMaxColumnIndex);

            var orderedWalls = project.Walls
                .OrderBy(w => w.Floor)
                .ThenBy(w => w.Name)
                .ToList();

            var openingTitleRow = sheet.CreateRow(rowIdx++);
            SetCell(openingTitleRow, 0, "DI\u1EC4N GI\u1EA2I L\u1ED6 M\u1EDE THEO V\u00C1CH", sectionStyle);
            SetMergedTextCell(
                sheet,
                openingTitleRow,
                0,
                PanelExplainSheetMaxColumnIndex,
                openingTitleRow.GetCell(0)?.StringCellValue ?? string.Empty,
                sectionStyle);

            string[] openingHeaders =
            {
                "STT", "T\u1EA7ng", "K\u00FD Hi\u1EC7u V\u00E1ch", "H\u1EA1ng M\u1EE5c", "\u1EE8ng D\u1EE5ng", "M\u00E3 Spec", "Lo\u1EA1i L\u1ED7 M\u1EDF",
                "L\u00FD Tr\u00ECnh LT (mm)", "R\u1ED9ng (mm)", "Cao (mm)", "Cao \u0110\u1ED9 \u0110\u00E1y (mm)", "SL", "DT L\u1ED7 M\u1EDF (m\u00B2)", "Ghi Ch\u00FA"
            };

            var openingHeaderRow = sheet.CreateRow(rowIdx++);
            openingHeaderRow.HeightInPoints = 24f;
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
                        SetCell(row, 7, Math.Round(opening.CenterStationMm), computedIntegerStyle);
                    else
                        SetCell(row, 7, "Ch\u01B0a x\u00E1c \u0111\u1ECBnh", dataStyle);

                    SetCell(row, 8, Math.Round(opening.Width), computedIntegerStyle);
                    SetCell(row, 9, Math.Round(opening.Height), computedIntegerStyle);
                    SetCell(row, 10, Math.Round(opening.BottomElevationMm), computedIntegerStyle);
                    SetCell(row, 11, Math.Max(1, opening.Quantity), computedIntegerStyle);
                    SetFormulaCell(
                        row,
                        12,
                        $"{CellRef(row.RowNum, 8)}*{CellRef(row.RowNum, 9)}*{CellRef(row.RowNum, 11)}/1000000",
                        computedStyle);

                    string openingNote = opening.CenterStationMm >= 0
                        ? $"V\u00E1ch {wall.Name}: LT {Math.Round(opening.CenterStationMm)} mm"
                        : $"V\u00E1ch {wall.Name}: ch\u01B0a c\u00F3 LT";
                    SetCell(row, 13, openingNote, dataWrapStyle);
                    ApplyWrapRowHeight(row, openingNote, 80);
                }
            }

            var openingTotalRow = sheet.CreateRow(rowIdx++);
            SetMergedTextCell(sheet, openingTotalRow, 0, 10, "T\u1ED4NG L\u1ED6 M\u1EDE", totalStyle);
            if (rowIdx > openingDataStart)
            {
                SetFormulaCell(
                    openingTotalRow,
                    11,
                    $"SUM({CellRef(openingDataStart, 11)}:{CellRef(rowIdx - 2, 11)})",
                    totalIntegerStyle);
                SetFormulaCell(
                    openingTotalRow,
                    12,
                    $"SUM({CellRef(openingDataStart, 12)}:{CellRef(rowIdx - 2, 12)})",
                    totalStyle);
            }
            else
            {
                SetCell(openingTotalRow, 11, 0, totalIntegerStyle);
                SetCell(openingTotalRow, 12, 0, totalStyle);
            }

            SetCell(openingTotalRow, 13, string.Empty, totalStyle);

            rowIdx += 2;

            var geometryTitleRow = sheet.CreateRow(rowIdx++);
            SetCell(geometryTitleRow, 0, "TH\u00D4NG S\u1ED0 H\u00CCNH H\u1ECCC V\u00C1CH / M\u1EA2NG TR\u1EA6N", sectionStyle);
            SetMergedTextCell(
                sheet,
                geometryTitleRow,
                0,
                10,
                geometryTitleRow.GetCell(0)?.StringCellValue ?? string.Empty,
                sectionStyle);

            string[] geometryHeaders =
            {
                "STT", "T\u1EA7ng", "K\u00FD Hi\u1EC7u V\u00E1ch", "H\u1EA1ng M\u1EE5c", "\u1EE8ng D\u1EE5ng", "M\u00E3 Spec", "Nh\u00F3m D\u00F2ng",
                "D\u00E0i M\u1EA3ng (mm)", "Cao TB (mm)", "DT H\u00ECnh H\u1ECDc (m\u00B2)", "Ghi Ch\u00FA"
            };

            var geometryHeaderRow = sheet.CreateRow(rowIdx++);
            geometryHeaderRow.HeightInPoints = 24f;
            for (int i = 0; i < geometryHeaders.Length; i++)
            {
                SetCell(geometryHeaderRow, i, geometryHeaders[i], headerStyle);
            }

            int geometryStt = 1;
            foreach (var wall in orderedWalls)
            {
                var row = sheet.CreateRow(rowIdx++);
                SetCell(row, 0, geometryStt++, dataStyle);
                SetCell(row, 1, wall.Floor, dataStyle);
                SetCell(row, 2, wall.Name, dataStyle);
                SetCell(row, 3, wall.Category, dataStyle);
                SetCell(row, 4, wall.Application, dataStyle);
                SetCell(row, 5, wall.SpecKey, dataStyle);
                SetCell(row, 6, "Th\u00F4ng s\u1ED1", dataStyle);
                SetCell(row, 7, Math.Round(wall.Length), computedIntegerStyle);
                SetCell(row, 8, Math.Round(wall.RepresentativeHeightMm), computedIntegerStyle);
                SetCell(row, 9, wall.WallAreaM2, computedStyle);
                string geometryNote = $"K\u00EDch th\u01B0\u1EDBc h\u00ECnh h\u1ECDc xu\u1EA5t t\u1EEB AutoCAD: L={Math.Round(wall.Length)} mm; Htb={Math.Round(wall.RepresentativeHeightMm)} mm";
                SetCell(row, 10, geometryNote, dataWrapStyle);
                ApplyWrapRowHeight(row, geometryNote, 88);
            }

            rowIdx += 2;

            var panelTitleRow = sheet.CreateRow(rowIdx++);
            SetCell(panelTitleRow, 0, "DI\u1EC4N GI\u1EA2I D\u00D2NG T\u1EA4M PANEL", sectionStyle);
            SetMergedTextCell(
                sheet,
                panelTitleRow,
                0,
                PanelExplainSheetMaxColumnIndex,
                panelTitleRow.GetCell(0)?.StringCellValue ?? string.Empty,
                sectionStyle);

            string[] panelHeaders =
            {
                "STT", "T\u1EA7ng", "K\u00FD Hi\u1EC7u V\u00E1ch", "H\u1EA1ng M\u1EE5c", "\u1EE8ng D\u1EE5ng", "M\u00E3 Spec", "Nh\u00F3m D\u00F2ng",
                "Kh\u1ED5 T\u1EA5m (mm)", "D\u00E0i T\u1EA5m (mm)", "SL", "DT D\u00F2ng (m\u00B2)", "DT C\u1EA5p (m\u00B2)", "DT Hao H\u1EE5t (m\u00B2)", "Ghi Ch\u00FA"
            };

            var panelHeaderRow = sheet.CreateRow(rowIdx++);
            panelHeaderRow.HeightInPoints = 24f;
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
                    SetCell(row, 7, entry.WidthMm, computedIntegerStyle);
                    SetCell(row, 8, entry.LengthMm, computedIntegerStyle);
                    SetCell(row, 9, entry.Count, computedIntegerStyle);
                    SetFormulaCell(
                        row,
                        10,
                        $"{CellRef(row.RowNum, 7)}*{CellRef(row.RowNum, 8)}*{CellRef(row.RowNum, 9)}/1000000",
                        computedStyle);
                    SetFormulaCell(
                        row,
                        11,
                        $"IF(LEFT({CellRef(row.RowNum, 6)},3)=\"Hao\",0,{CellRef(row.RowNum, 10)})",
                        computedStyle);
                    SetFormulaCell(
                        row,
                        12,
                        $"IF(LEFT({CellRef(row.RowNum, 6)},3)=\"Hao\",{CellRef(row.RowNum, 10)},0)",
                        computedStyle);
                    string panelNote = $"Kh\u1ED5 chu\u1EA9n {wall.PanelWidth} mm; L\u1ED7 m\u1EDF: {wall.TotalOpeningCount}";
                    SetCell(row, 13, panelNote, dataWrapStyle);
                    ApplyWrapRowHeight(row, panelNote, 80);
                }
            }

            var panelTotalRow = sheet.CreateRow(rowIdx++);
            SetMergedTextCell(sheet, panelTotalRow, 0, 9, "T\u1ED4NG PANEL", totalStyle);
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
                6, 10, 13, 11, 10, 12, 16, 15, 14, 11, 13, 7, 12, 42
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

        private static ICellStyle CreateComputedIntegerStyle(IWorkbook workbook, ICellStyle baseStyle)
        {
            var style = workbook.CreateCellStyle();
            style.CloneStyleFrom(baseStyle);

            var font = workbook.CreateFont();
            font.Color = NPOI.HSSF.Util.HSSFColor.Blue.Index;
            style.SetFont(font);

            style.Alignment = HorizontalAlignment.Right;
            style.DataFormat = workbook.CreateDataFormat().GetFormat("#,##0");
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

        private static ICellStyle CreateTotalIntegerStyle(IWorkbook workbook, ICellStyle baseStyle)
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
            style.DataFormat = workbook.CreateDataFormat().GetFormat("#,##0");
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
