using System;
using System.IO;
using NPOI.XSSF.UserModel;
using ShopDrawing.Plugin.Models;
using ShopDrawing.Plugin.UI;

namespace ShopDrawing.Tests
{
    public class TenderExcelExporterTests
    {
        [Fact]
        public void Export_ShouldIncludeSpecManagementSheetInSameWorkbook()
        {
            string tempFile = Path.Combine(Path.GetTempPath(), $"tender-export-{Guid.NewGuid():N}.xlsx");

            try
            {
                var project = new TenderProject
                {
                    ProjectName = "Du an test",
                    CustomerName = "Khach hang A",
                    Walls =
                    {
                        new TenderWall
                        {
                            Category = "Vách",
                            Floor = "T1",
                            Name = "W-1",
                            Length = 6000,
                            Height = 3000,
                            SpecKey = "SP-01",
                            PanelWidth = 1100,
                            Application = "Kho lạnh"
                        }
                    },
                    Specs =
                    {
                        new PanelSpec
                        {
                            Key = "SP-01",
                            PanelWidth = 1100,
                            PanelType = "ISOFRIGO",
                            Density = "44+-2",
                            Thickness = 100,
                            FireRating = "B2",
                            FmApproved = true,
                            FacingColor = "Trang",
                            TopFacing = "Tole",
                            TopCoating = "AZ150",
                            TopSteelThickness = 0.5,
                            TopProfile = "Vuông",
                            BottomFacingColor = "Trang",
                            BottomFacing = "Tole",
                            BottomCoating = "AZ150",
                            BottomSteelThickness = 0.4,
                            BottomProfile = "Phẳng"
                        }
                    }
                };

                var exporter = new TenderExcelExporter();
                exporter.Export(project, tempFile);

                using var fs = new FileStream(tempFile, FileMode.Open, FileAccess.Read);
                var workbook = new XSSFWorkbook(fs);

                Assert.Equal(2, workbook.NumberOfSheets);
                var bomSheet = workbook.GetSheet("BOM đấu thầu");
                Assert.NotNull(bomSheet);
                Assert.Equal("BẢNG BOM ĐẤU THẦU - Du an test", bomSheet.GetRow(0).GetCell(0).StringCellValue);

                var specSheet = workbook.GetSheet("Bảng quản lý Spec");
                Assert.NotNull(specSheet);
                Assert.Equal("Mã spec", specSheet.GetRow(5).GetCell(0).StringCellValue);
                Assert.Equal("SP-01", specSheet.GetRow(6).GetCell(0).StringCellValue);
                Assert.Equal(1100, specSheet.GetRow(6).GetCell(1).NumericCellValue);
                Assert.Equal("ISOFRIGO", specSheet.GetRow(6).GetCell(2).StringCellValue);
                Assert.Equal("Có", specSheet.GetRow(6).GetCell(6).StringCellValue);
            }
            finally
            {
                if (File.Exists(tempFile))
                    File.Delete(tempFile);
            }
        }
    }
}
