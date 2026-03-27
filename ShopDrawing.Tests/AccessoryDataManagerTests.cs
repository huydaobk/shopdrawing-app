using System;
using System.Linq;
using ShopDrawing.Plugin.Core;
using ShopDrawing.Plugin.Models;

namespace ShopDrawing.Tests
{
    public class AccessoryDataManagerTests
    {
        [Fact]
        public void GetDefaults_ShouldIncludeDetailedExteriorAndCleanroomSealantRows()
        {
            var defaults = AccessoryDataManager.GetDefaults();

            Assert.Contains(defaults, item =>
                item.Name == "Sealant MS-617" &&
                item.Position == "C\u1ea1nh \u0111\u1ee9ng LM" &&
                item.Application == "Ngo\u00e0i nh\u00e0" &&
                item.CalcRule == AccessoryCalcRule.PER_OPENING_VERTICAL_EDGES);

            Assert.Contains(defaults, item =>
                item.Name == "Sealant MS-617" &&
                item.Position == "\u0110\u1ec9nh v\u00e1ch" &&
                item.Application == "Ngo\u00e0i nh\u00e0" &&
                item.CalcRule == AccessoryCalcRule.PER_TOP_EDGE_LENGTH);

            Assert.Contains(defaults, item =>
                item.Name == "Silicone SN-505" &&
                item.Position == "C\u1ea1nh \u0111\u1ee9ng LM" &&
                item.Application == "Ph\u00f2ng s\u1ea1ch" &&
                item.CalcRule == AccessoryCalcRule.PER_OPENING_VERTICAL_EDGES &&
                item.Factor == 2.0);

            Assert.Contains(defaults, item =>
                item.Name == "Silicone SN-505" &&
                item.Position == "\u0110\u1ec9nh v\u00e1ch" &&
                item.Application == "Ph\u00f2ng s\u1ea1ch" &&
                item.CalcRule == AccessoryCalcRule.PER_TOP_EDGE_LENGTH);
        }

        [Fact]
        public void NormalizeConfiguredAccessories_ShouldMigrateLegacySealantNamesWithoutPositionExpansion()
        {
            var legacy = new[]
            {
                new TenderAccessory
                {
                    CategoryScope = "V\u00e1ch",
                    Application = "Ngo\u00e0i nh\u00e0",
                    SpecKey = "T\u1ea5t c\u1ea3",
                    Name = "Sealant l\u1ed7 m\u1edf",
                    Unit = "md",
                    CalcRule = AccessoryCalcRule.PER_OPENING_PERIMETER,
                    Factor = 1
                },
                new TenderAccessory
                {
                    CategoryScope = "V\u00e1ch",
                    Application = "Ph\u00f2ng s\u1ea1ch",
                    SpecKey = "T\u1ea5t c\u1ea3",
                    Name = "Sealant l\u1ed7 m\u1edf",
                    Unit = "md",
                    CalcRule = AccessoryCalcRule.PER_OPENING_PERIMETER_TWO_FACES,
                    Factor = 1
                },
                new TenderAccessory
                {
                    CategoryScope = "V\u00e1ch",
                    Application = "Ph\u00f2ng s\u1ea1ch",
                    SpecKey = "T\u1ea5t c\u1ea3",
                    Name = "Sealant m\u1ed1i n\u1ed1i",
                    Unit = "md",
                    CalcRule = AccessoryCalcRule.PER_JOINT_LENGTH,
                    Factor = 1
                }
            };

            var normalized = AccessoryDataManager.NormalizeConfiguredAccessories(legacy).ToList();

            Assert.Contains(normalized, item =>
                item.Name == "Sealant MS-617" &&
                item.Application == "Ngo\u00e0i nh\u00e0" &&
                item.CalcRule == AccessoryCalcRule.PER_OPENING_PERIMETER);

            Assert.Contains(normalized, item =>
                item.Name == "Sealant MS-617" &&
                item.Application == "Ph\u00f2ng s\u1ea1ch" &&
                item.CalcRule == AccessoryCalcRule.PER_OPENING_PERIMETER_TWO_FACES &&
                item.Factor == 1.0);

            Assert.Contains(normalized, item =>
                item.Name == "Sealant MS-617" &&
                item.Application == "Ph\u00f2ng s\u1ea1ch" &&
                item.CalcRule == AccessoryCalcRule.PER_JOINT_LENGTH);
        }

        [Fact]
        public void NormalizeConfiguredAccessories_ShouldOnlyRenameColdStorageTrimNames()
        {
            var legacy = new[]
            {
                new TenderAccessory
                {
                    CategoryScope = "V\u00e1ch",
                    Application = "Ngo\u00e0i nh\u00e0",
                    SpecKey = "T\u1ea5t c\u1ea3",
                    Name = "Vi\u1ec1n l\u1ed7 m\u1edf",
                    Position = "C\u1eeda \u0111i",
                    Unit = "md",
                    CalcRule = AccessoryCalcRule.PER_DOOR_OPENING_PERIMETER,
                    Factor = 1
                },
                new TenderAccessory
                {
                    CategoryScope = "V\u00e1ch",
                    Application = "Ph\u00f2ng s\u1ea1ch",
                    SpecKey = "T\u1ea5t c\u1ea3",
                    Name = "Vi\u1ec1n l\u1ed7 m\u1edf 2 m\u1eb7t",
                    Position = "L\u1ed7 m\u1edf",
                    Unit = "md",
                    CalcRule = AccessoryCalcRule.PER_OPENING_PERIMETER_TWO_FACES,
                    Factor = 1
                },
                new TenderAccessory
                {
                    CategoryScope = "V\u00e1ch",
                    Application = "Kho l\u1ea1nh",
                    SpecKey = "T\u1ea5t c\u1ea3",
                    Name = "Vi\u1ec1n l\u1ed7 m\u1edf 2 m\u1eb7t",
                    Position = "L\u1ed7 m\u1edf",
                    Unit = "md",
                    CalcRule = AccessoryCalcRule.PER_OPENING_PERIMETER_TWO_FACES,
                    Factor = 1
                }
            };

            var normalized = AccessoryDataManager.NormalizeConfiguredAccessories(legacy).ToList();

            Assert.Contains(normalized, item =>
                item.Application == "Ngo\u00e0i nh\u00e0" &&
                item.Name == "Vi\u1ec1n l\u1ed7 m\u1edf");

            Assert.Contains(normalized, item =>
                item.Application == "Ph\u00f2ng s\u1ea1ch" &&
                item.Name == "Vi\u1ec1n l\u1ed7 m\u1edf 2 m\u1eb7t");

            Assert.Contains(normalized, item =>
                item.Application == "Kho l\u1ea1nh" &&
                item.Name == "Di\u1ec1m 01");
        }

        [Fact]
        public void GetDefaults_ShouldUseMaterialColumnForColdStorageConvention()
        {
            const string coldStorage = "Kho l\u1ea1nh";
            const string trim01 = "Di\u1ec1m 01";
            const string rivet = "Rivet \u00d84.2\u00d712";
            const string aluminum = "Nh\u00f4m";

            var defaults = AccessoryDataManager.GetDefaults();

            Assert.Contains(defaults, item =>
                item.Application == coldStorage &&
                item.Name == trim01 &&
                item.Material == "Tole");

            Assert.Contains(defaults, item =>
                item.Application == coldStorage &&
                item.Name == "B2S-TEK" &&
                item.Material == "Inox");

            Assert.Contains(defaults, item =>
                item.Application == coldStorage &&
                item.Name == rivet &&
                item.Material == aluminum);

            Assert.Contains(defaults, item =>
                item.Application == coldStorage &&
                item.Name == "Silicone SN-505" &&
                string.IsNullOrWhiteSpace(item.Material));

            Assert.Contains(defaults, item =>
                item.Application == coldStorage &&
                item.Name == "Sealant MC-202" &&
                string.IsNullOrWhiteSpace(item.Material));
        }

        [Fact]
        public void GetDefaults_ShouldIncludeColdStorageCeilingSuspensionAccessories()
        {
            const string coldStorage = "Kho l\u1ea1nh";
            const string ceiling = "Tr\u1ea7n";
            const string aluminum = "Nh\u00f4m";

            var defaults = AccessoryDataManager.GetDefaults();

            Assert.Contains(defaults, item =>
                item.Application == coldStorage &&
                item.CategoryScope == ceiling &&
                item.Name == "T-profile 68x75" &&
                item.Material == aluminum &&
                item.CalcRule == AccessoryCalcRule.PER_COLD_STORAGE_T_SUSPENSION_LENGTH);

            Assert.Contains(defaults, item =>
                item.Application == coldStorage &&
                item.CategoryScope == ceiling &&
                item.Name == "Wire rope \u00d812" &&
                item.Position == "Treo T nh\u00f4m" &&
                item.CalcRule == AccessoryCalcRule.PER_COLD_STORAGE_T_WIRE_ROPE_LENGTH);

            Assert.Contains(defaults, item =>
                item.Application == coldStorage &&
                item.CategoryScope == ceiling &&
                item.Name == "\u1ed0ng \u00d8114" &&
                item.Material == "PVC" &&
                item.Position == "Treo T nh\u00f4m" &&
                item.Unit == "c\u00e2y" &&
                item.CalcRule == AccessoryCalcRule.PER_COLD_STORAGE_T_SUSPENSION_POINT_QTY &&
                item.Factor == 0.125);

            Assert.Contains(defaults, item =>
                item.Application == coldStorage &&
                item.CategoryScope == ceiling &&
                item.Name == "C-channel 100x50" &&
                item.Position == "Treo bulong n\u1ea5m" &&
                item.CalcRule == AccessoryCalcRule.PER_COLD_STORAGE_MUSHROOM_SUSPENSION_LENGTH);

            Assert.Contains(defaults, item =>
                item.Application == coldStorage &&
                item.CategoryScope == ceiling &&
                item.Name == "\u1ed0ng \u00d8114" &&
                item.Material == "PVC" &&
                item.Position == "Treo bulong n\u1ea5m" &&
                item.Unit == "c\u00e2y" &&
                item.CalcRule == AccessoryCalcRule.PER_COLD_STORAGE_MUSHROOM_SUSPENSION_POINT_QTY &&
                item.Factor == 0.05);
        }
    }
}
