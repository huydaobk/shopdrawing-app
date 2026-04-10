using System.IO;
using NPOI.XSSF.UserModel;
using ShopDrawing.Plugin.Core;
using ShopDrawing.Plugin.Models;
using ShopDrawing.Plugin.UI;

namespace ShopDrawing.Tests
{
    public class TenderBomCalculatorTests
    {
        [Fact]
        public void CalculateAccessoryReport_ShouldReturnBasisAndFinalQuantityInVietnameseStructure()
        {
            var walls = new List<TenderWall>
            {
                new()
                {
                    Category = "Vách",
                    Floor = "T1",
                    Name = "W1",
                    Length = 10000,
                    Height = 5000,
                    SpecKey = "SP1",
                    PanelWidth = 1000,
                    LayoutDirection = "Dọc",
                    Application = "Ngoài nhà"
                }
            };

            var accessories = new List<TenderAccessory>
            {
                new()
                {
                    CategoryScope = "Vách",
                    Application = "Ngoài nhà",
                    SpecKey = "SP1",
                    Name = "Úp nóc",
                    Unit = "md",
                    CalcRule = AccessoryCalcRule.PER_TOP_EDGE_LENGTH,
                    Factor = 1.1,
                    WasteFactor = 5,
                    Adjustment = 2
                }
            };

            var calculator = new TenderBomCalculator();
            var report = calculator.CalculateAccessoryReport(walls, accessories);

            var basis = Assert.Single(report.BasisRows);
            Assert.Equal("Cạnh trên lộ", basis.BasisLabel);
            Assert.Equal(10, basis.BasisValue);
            Assert.Equal(11, basis.AutoQuantity);

            var summary = Assert.Single(report.SummaryRows);
            Assert.Equal("Theo cạnh trên lộ", summary.RuleLabel);
            Assert.Equal("Vách", summary.CategoryScope);
            Assert.Equal(10, summary.BasisValue);
            Assert.Equal(11, summary.AutoQuantity);
            Assert.Equal(13.55, summary.FinalQuantity);
        }

        [Fact]
        public void GetBasisValue_ShouldRespectExposedEdgesAndOutsideCorners()
        {
            var wall = new TenderWall
            {
                Category = "Vách",
                Length = 8000,
                Height = 4500,
                TopEdgeExposed = false,
                BottomEdgeExposed = true,
                StartEdgeExposed = true,
                EndEdgeExposed = false,
                OutsideCornerCount = 2
            };

            Assert.Equal(0, TenderBomCalculator.GetBasisValue(AccessoryCalcRule.PER_TOP_EDGE_LENGTH, wall));
            Assert.Equal(8, TenderBomCalculator.GetBasisValue(AccessoryCalcRule.PER_BOTTOM_EDGE_LENGTH, wall));
            Assert.Equal(4.5, TenderBomCalculator.GetBasisValue(AccessoryCalcRule.PER_EXPOSED_END_LENGTH, wall));
            Assert.Equal(9, TenderBomCalculator.GetBasisValue(AccessoryCalcRule.PER_OUTSIDE_CORNER_HEIGHT, wall));
        }

        [Fact]
        public void GetBasisValue_ShouldSplitOpeningPerimeterAndDoorQuantity()
        {
            var wall = new TenderWall
            {
                Category = "Vách",
                Length = 9000,
                Height = 5000,
                Application = "Ngoài nhà",
                Openings = new List<TenderOpening>
                {
                    new() { Type = "Cửa đi", Width = 1800, Height = 2400, Quantity = 2 },
                    new() { Type = "Cửa sổ", Width = 1200, Height = 1500, Quantity = 1 },
                    new() { Type = "Lỗ kỹ thuật", Width = 800, Height = 800, Quantity = 1 }
                }
            };

            Assert.Equal(13.2, TenderBomCalculator.GetBasisValue(AccessoryCalcRule.PER_DOOR_OPENING_PERIMETER, wall));
            Assert.Equal(8.6, TenderBomCalculator.GetBasisValue(AccessoryCalcRule.PER_NON_DOOR_OPENING_PERIMETER, wall));
            Assert.Equal(21.8, TenderBomCalculator.GetBasisValue(AccessoryCalcRule.PER_OPENING_PERIMETER, wall));
            Assert.Equal(43.6, TenderBomCalculator.GetBasisValue(AccessoryCalcRule.PER_OPENING_PERIMETER_TWO_FACES, wall));
            Assert.Equal(2, TenderBomCalculator.GetBasisValue(AccessoryCalcRule.PER_DOOR_OPENING_QTY, wall));
            Assert.Equal(2, TenderBomCalculator.GetBasisValue(AccessoryCalcRule.PER_NON_DOOR_OPENING_QTY, wall));
            Assert.Equal(14.2, TenderBomCalculator.GetBasisValue(AccessoryCalcRule.PER_OPENING_VERTICAL_EDGES, wall));
            Assert.Equal(5.6, TenderBomCalculator.GetBasisValue(AccessoryCalcRule.PER_OPENING_HORIZONTAL_TOP_LENGTH, wall));
            Assert.Equal(2.0, TenderBomCalculator.GetBasisValue(AccessoryCalcRule.PER_OPENING_SILL_LENGTH, wall));
        }

        [Fact]
        public void CalculateAccessoryReport_ShouldSupportDoorKitAndTwoFaceOpeningTrim()
        {
            var walls = new List<TenderWall>
            {
                new()
                {
                    Category = "Vách",
                    Floor = "T1",
                    Name = "W4",
                    Length = 10000,
                    Height = 5000,
                    SpecKey = "SP1",
                    PanelWidth = 1000,
                    LayoutDirection = "Dọc",
                    Application = "Kho lạnh",
                    Openings = new List<TenderOpening>
                    {
                        new() { Type = "Cửa đi", Width = 1762, Height = 2615, Quantity = 1 }
                    }
                }
            };

            var accessories = new List<TenderAccessory>
            {
                new()
                {
                    CategoryScope = "Vách",
                    Application = "Kho lạnh",
                    SpecKey = "SP1",
                    Name = "Viền lỗ mở 2 mặt",
                    Unit = "md",
                    CalcRule = AccessoryCalcRule.PER_OPENING_PERIMETER_TWO_FACES,
                    Factor = 1
                },
                new()
                {
                    CategoryScope = "Vách",
                    Application = "Kho lạnh",
                    SpecKey = "SP1",
                    Name = "Bộ cửa đi kho lạnh",
                    Unit = "bộ",
                    CalcRule = AccessoryCalcRule.PER_DOOR_OPENING_QTY,
                    Factor = 1
                }
            };

            var calculator = new TenderBomCalculator();
            var report = calculator.CalculateAccessoryReport(walls, accessories);

            Assert.Contains(report.SummaryRows, row => row.Name == "Viền lỗ mở 2 mặt" && row.BasisValue == 13.98);
            Assert.Contains(report.SummaryRows, row => row.Name == "Bộ cửa đi kho lạnh" && row.BasisValue == 1 && row.FinalQuantity == 1);
        }

        [Fact]
        public void CalculateAccessoryReport_ShouldSortByApplicationFirst()
        {
            var walls = new List<TenderWall>
            {
                new() { Category = "Vách", Floor = "T1", Name = "W1", Length = 6000, Height = 4000, SpecKey = "SP1", Application = "Kho lạnh" },
                new() { Category = "Vách", Floor = "T1", Name = "W2", Length = 6000, Height = 4000, SpecKey = "SP1", Application = "Ngoài nhà" },
                new() { Category = "Vách", Floor = "T1", Name = "W3", Length = 6000, Height = 4000, SpecKey = "SP1", Application = "Phòng sạch" }
            };

            var accessories = new List<TenderAccessory>
            {
                new() { CategoryScope = "Vách", Application = "Kho lạnh", SpecKey = "SP1", Name = "A-Kho", Unit = "md", CalcRule = AccessoryCalcRule.PER_WALL_LENGTH, Factor = 1 },
                new() { CategoryScope = "Vách", Application = "Phòng sạch", SpecKey = "SP1", Name = "A-Sach", Unit = "md", CalcRule = AccessoryCalcRule.PER_WALL_LENGTH, Factor = 1 },
                new() { CategoryScope = "Vách", Application = "Ngoài nhà", SpecKey = "SP1", Name = "A-Ngoai", Unit = "md", CalcRule = AccessoryCalcRule.PER_WALL_LENGTH, Factor = 1 }
            };

            var calculator = new TenderBomCalculator();
            var report = calculator.CalculateAccessoryReport(walls, accessories);

            Assert.Equal("Ngoài nhà", report.SummaryRows[0].Application);
            Assert.Equal("Phòng sạch", report.SummaryRows[1].Application);
            Assert.Equal("Kho lạnh", report.SummaryRows[2].Application);

            Assert.Equal("Ngoài nhà", report.BasisRows[0].Application);
            Assert.Equal("Phòng sạch", report.BasisRows[1].Application);
            Assert.Equal("Kho lạnh", report.BasisRows[2].Application);
        }

        [Fact]
        public void NormalizeConfiguredAccessories_ShouldExpandLegacyAllScopeRows()
        {
            var legacy = new List<TenderAccessory>
            {
                new()
                {
                    CategoryScope = "Tất cả",
                    Application = "Tất cả",
                    SpecKey = "Tất cả",
                    Name = "Viền legacy",
                    Unit = "md",
                    CalcRule = AccessoryCalcRule.PER_WALL_LENGTH,
                    Factor = 1
                }
            };

            var normalized = AccessoryDataManager.NormalizeConfiguredAccessories(legacy);

            var expandedLegacyRows = normalized
                .Where(item => (item.Name ?? string.Empty).Contains("legacy"))
                .ToList();

            Assert.Equal(3, expandedLegacyRows.Count);
            Assert.Equal(3, expandedLegacyRows.Select(item => item.Application).Distinct().Count());
        }

        [Fact]
        public void NormalizeConfiguredAccessories_ShouldKeepProductNameAndUsePositionAsSeparateField()
        {
            var legacy = new List<TenderAccessory>
            {
                new()
                {
                    CategoryScope = "Vách",
                    Application = "Ngoài nhà",
                    SpecKey = "Tất cả",
                    Name = "Sealant mối nối",
                    Unit = "md",
                    CalcRule = AccessoryCalcRule.PER_JOINT_LENGTH,
                    Factor = 1
                },
                new()
                {
                    CategoryScope = "Vách",
                    Application = "Phòng sạch",
                    SpecKey = "Tất cả",
                    Name = "Silicone vệ sinh",
                    Unit = "md",
                    CalcRule = AccessoryCalcRule.PER_JOINT_LENGTH,
                    Factor = 1
                },
                new()
                {
                    CategoryScope = "Vách",
                    Application = "Kho lạnh",
                    SpecKey = "Tất cả",
                    Name = "Sealant lỗ mở",
                    Unit = "md",
                    CalcRule = AccessoryCalcRule.PER_OPENING_PERIMETER_TWO_FACES,
                    Factor = 1
                }
            };

            var normalized = AccessoryDataManager.NormalizeConfiguredAccessories(legacy);

            Assert.Contains(normalized, item =>
                item.Name == "Sealant MS-617" &&
                item.Application == "Ngoài nhà" &&
                item.CalcRule == AccessoryCalcRule.PER_JOINT_LENGTH);

            Assert.Contains(normalized, item =>
                item.Name == "Silicone SN-505" &&
                item.Application == "Phòng sạch" &&
                item.CalcRule == AccessoryCalcRule.PER_JOINT_LENGTH);

            Assert.Contains(normalized, item =>
                item.Name == "Sealant MS-617" &&
                item.Application == "Kho lạnh" &&
                item.CalcRule == AccessoryCalcRule.PER_OPENING_PERIMETER_TWO_FACES);
        }

        [Fact]
        public void NormalizeConfiguredAccessories_ShouldMergeMissingColdStorageCeilingDefaultsIntoExistingProjectConfig()
        {
            var existingProjectAccessories = new List<TenderAccessory>
            {
                new()
                {
                    CategoryScope = "VÃ¡ch",
                    Application = "Kho láº¡nh",
                    SpecKey = "Táº¥t cáº£",
                    Name = "Diá»m 01",
                    Material = "Tole",
                    Position = "Cáº¡nh trÃªn",
                    Unit = "md",
                    CalcRule = AccessoryCalcRule.PER_TOP_EDGE_LENGTH,
                    Factor = 1.25
                }
            };

            var normalized = AccessoryDataManager.NormalizeConfiguredAccessories(existingProjectAccessories);

            Assert.Contains(normalized, item =>
                item.CalcRule == AccessoryCalcRule.PER_COLD_STORAGE_T_SUSPENSION_LENGTH);

            Assert.Contains(normalized, item =>
                item.CalcRule == AccessoryCalcRule.PER_COLD_STORAGE_MUSHROOM_BOLT_QTY);

            Assert.Single(normalized.Where(item =>
                item.CalcRule == AccessoryCalcRule.PER_TOP_EDGE_LENGTH &&
                item.Factor == 1.25));
        }

        [Fact]
        public void GetDefaults_ShouldSplitColdStorageSealantIntoMc202AndSn505()
        {
            var defaults = AccessoryDataManager.GetDefaults()
                .Where(item => item.Application == "Kho lạnh")
                .ToList();

            Assert.Contains(defaults, item =>
                item.Name == "Sealant MC-202" &&
                item.Position == "Mối nối" &&
                item.CalcRule == AccessoryCalcRule.PER_JOINT_LENGTH);

            Assert.Contains(defaults, item =>
                item.Name == "Silicone SN-505" &&
                item.Position == "Cạnh đứng LM" &&
                item.CalcRule == AccessoryCalcRule.PER_OPENING_VERTICAL_EDGES);

            Assert.Contains(defaults, item =>
                item.Name == "Silicone SN-505" &&
                item.Position == "Cạnh đầu LM" &&
                item.CalcRule == AccessoryCalcRule.PER_OPENING_HORIZONTAL_TOP_LENGTH);

            Assert.Contains(defaults, item =>
                item.Name == "Silicone SN-505" &&
                item.Position == "Cạnh sill LM" &&
                item.CalcRule == AccessoryCalcRule.PER_OPENING_SILL_LENGTH);

            Assert.Contains(defaults, item =>
                item.Name == "Silicone SN-505" &&
                item.Position == "Đỉnh vách" &&
                item.CalcRule == AccessoryCalcRule.PER_TOP_EDGE_LENGTH);

            Assert.Contains(defaults, item =>
                item.Name == "Silicone SN-505" &&
                item.Position == "Chân vách" &&
                item.CalcRule == AccessoryCalcRule.PER_BOTTOM_EDGE_LENGTH);

            Assert.Contains(defaults, item =>
                item.Name == "Silicone SN-505" &&
                item.Position == "Cạnh hở" &&
                item.CalcRule == AccessoryCalcRule.PER_EXPOSED_END_LENGTH);
        }

        [Fact]
        public void GetBasisValue_ShouldReturnTotalExposedEdgeLength()
        {
            var wall = new TenderWall
            {
                Category = "Vách",
                Length = 8000,
                Height = 4500,
                TopEdgeExposed = true,
                BottomEdgeExposed = true,
                StartEdgeExposed = true,
                EndEdgeExposed = false
            };

            Assert.Equal(20.5, TenderBomCalculator.GetBasisValue(AccessoryCalcRule.PER_TOTAL_EXPOSED_EDGE_LENGTH, wall));
        }

        [Fact]
        public void GetBasisValue_ShouldCalculateColdStorageCeilingSuspensionAndWireRope()
        {
            var wall = new TenderWall
            {
                Category = "Tr\u1ea7n",
                Floor = "T1",
                Name = "CL-01",
                Length = 12000,
                Height = 5000,
                SpecKey = "SP1",
                PanelWidth = 1000,
                PanelThickness = 100,
                LayoutDirection = "Ngang",
                Application = "Kho l\u1ea1nh",
                CableDropLengthMm = 1800
            };

            Assert.Equal(10.0, TenderBomCalculator.GetBasisValue(AccessoryCalcRule.PER_COLD_STORAGE_T_SUSPENSION_LENGTH, wall), 3);
            Assert.Equal(7.0, TenderBomCalculator.GetBasisValue(AccessoryCalcRule.PER_COLD_STORAGE_T_SUSPENSION_POINT_QTY, wall), 3);
            Assert.Equal(12.6, TenderBomCalculator.GetBasisValue(AccessoryCalcRule.PER_COLD_STORAGE_T_WIRE_ROPE_LENGTH, wall), 3);
            Assert.Equal(20.0, TenderBomCalculator.GetBasisValue(AccessoryCalcRule.PER_COLD_STORAGE_MUSHROOM_SUSPENSION_LENGTH, wall), 3);
            Assert.Equal(14.0, TenderBomCalculator.GetBasisValue(AccessoryCalcRule.PER_COLD_STORAGE_MUSHROOM_SUSPENSION_POINT_QTY, wall), 3);
            Assert.Equal(25.2, TenderBomCalculator.GetBasisValue(AccessoryCalcRule.PER_COLD_STORAGE_MUSHROOM_WIRE_ROPE_LENGTH, wall), 3);
            Assert.Equal(20.0, TenderBomCalculator.GetBasisValue(AccessoryCalcRule.PER_COLD_STORAGE_MUSHROOM_BOLT_QTY, wall), 3);
        }

        [Fact]
        public void Export_ShouldWriteVietnameseAccessorySections()
        {
            string tempFile = Path.Combine(Path.GetTempPath(), $"sd-tender-{Guid.NewGuid():N}.xlsx");

            try
            {
                var project = new TenderProject
                {
                    ProjectName = "Du an thu",
                    CustomerName = "Khach hang",
                    Walls = new List<TenderWall>
                    {
                        new()
                        {
                            Category = "Vách",
                            Floor = "T1",
                            Name = "W1",
                            Length = 6000,
                            Height = 4000,
                            SpecKey = "SP1",
                            PanelWidth = 1000,
                            LayoutDirection = "Dọc",
                            Application = "Ngoài nhà"
                        }
                    },
                    Accessories = new List<TenderAccessory>
                    {
                        new()
                        {
                            CategoryScope = "Vách",
                            Application = "Ngoài nhà",
                            SpecKey = "SP1",
                            Name = "Úp nóc",
                            Unit = "md",
                            CalcRule = AccessoryCalcRule.PER_TOP_EDGE_LENGTH,
                            Factor = 1
                        }
                    }
                };

                var exporter = new TenderExcelExporter();
                exporter.Export(project, tempFile);

                using var fs = new FileStream(tempFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                var workbook = new XSSFWorkbook(fs);
                var sheet = workbook.GetSheetAt(0);

                var values = new List<string>();
                for (int rowIndex = 0; rowIndex <= sheet.LastRowNum; rowIndex++)
                {
                    var row = sheet.GetRow(rowIndex);
                    if (row == null)
                        continue;

                    for (int col = 0; col < row.LastCellNum; col++)
                    {
                        string text = row.GetCell(col)?.ToString() ?? "";
                        if (!string.IsNullOrWhiteSpace(text))
                            values.Add(text);
                    }
                }

                Assert.Contains("CƠ SỞ TÍNH PHỤ KIỆN", values);
                Assert.Contains("TỔNG HỢP PHỤ KIỆN ĐẤU THẦU", values);
                Assert.Contains("Phạm vi hạng mục", values);
                Assert.Contains("Khối lượng chốt", values);
            }
            finally
            {
                if (File.Exists(tempFile))
                    File.Delete(tempFile);
            }
        }
    }
}
