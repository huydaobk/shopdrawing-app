using System.Collections.Generic;
using System.Linq;
using ShopDrawing.Plugin.Models;

namespace ShopDrawing.Plugin.Core
{
    public static class AccessoryDataManager
    {
        public const string DefaultCategoryScope = "Vách";
        public const string DefaultApplication = "Ngoài nhà";

        private const string ScopeAll = "Tất cả";
        private const string AppExterior = "Ngoài nhà";
        private const string AppCleanroom = "Phòng sạch";
        private const string AppColdStorage = "Kho lạnh";

        /// <summary>Tên gốc vít TEK ngoài nhà — dùng để detect auto-sizing</summary>
        public const string ExteriorTekScrewBaseName = "B2S-TEK 12x14";
        public const string ColdStorageBottomUChannelBaseName = "U 40x153x40";
        public const string CleanroomBottomUChannelBaseName = "U 40x50x40";

        /// <summary>
        /// Ánh xạ chiều dày panel (mm) → chiều dài vít TEK (mm).
        /// Nguồn: Greenpan B2S-TEK screw specification.
        /// </summary>
        private static readonly SortedDictionary<int, int> ScrewLengthMap = new()
        {
            { 50, 65 },
            { 60, 75 },
            { 75, 90 },
            { 80, 95 },
            { 100, 115 },
            { 125, 145 },
            { 150, 170 },
            { 175, 195 },
            { 180, 200 },
            { 200, 220 }
        };

        /// <summary>
        /// Auto-size tên vít TEK theo chiều dày panel.
        /// VD: thickness=100 → "B2S-TEK 12x14-115HGS"
        /// Trả về baseName nếu thickness=0 hoặc không khớp.
        /// </summary>
        public static string GetAutoSizedScrewName(string baseName, int panelThicknessMm)
        {
            if (panelThicknessMm <= 0)
                return baseName;

            if (ScrewLengthMap.TryGetValue(panelThicknessMm, out int screwLength))
                return $"{baseName}-{screwLength}HGS";

            foreach (var kv in ScrewLengthMap)
            {
                if (kv.Key >= panelThicknessMm)
                    return $"{baseName}-{kv.Value}HGS";
            }

            var last = ScrewLengthMap.Last();
            return $"{baseName}-{last.Value}HGS";
        }

        public static string GetColdStorageBottomUChannelName(int panelThicknessMm)
        {
            if (panelThicknessMm <= 0)
                return ColdStorageBottomUChannelBaseName;

            return $"U 40x{panelThicknessMm + 3}x40";
        }

        public static string GetColdStorageBottomUChannelMaterial(int panelThicknessMm)
        {
            return panelThicknessMm == 50 || panelThicknessMm == 75
                ? "Nhôm"
                : "Thép mạ kẽm";
        }

        public static string GetCleanroomBottomUChannelName(int panelThicknessMm)
        {
            if (panelThicknessMm <= 0)
                return CleanroomBottomUChannelBaseName;

            return $"U 40x{panelThicknessMm}x40";
        }

        public static string GetCleanroomBottomUChannelMaterial(int panelThicknessMm)
        {
            return panelThicknessMm == 50 || panelThicknessMm == 75
                ? "Nhôm"
                : "Thép mạ kẽm";
        }

        // ═══════════════════════════════════════════════════
        // POSITION CONSTANTS
        // ═══════════════════════════════════════════════════
        private const string PosMoiNoi = "Mối nối";
        private const string PosKheDung = "Khe đứng";
        private const string PosCanhTren = "Đỉnh vách";
        private const string PosCanhDuoi = "Chân vách";
        private const string PosDauCuoi = "Đầu/cuối vách";
        private const string PosGocNgoai = "Góc ngoài";
        private const string PosGocTrong = "Góc trong";
        private const string PosCuaDi = "Cửa đi";
        private const string PosCuaSoLKT = "Cửa sổ/lỗ KT";
        private const string PosLoMo = "Lỗ mở";
        private const string PosLoMo2Mat = "Lỗ mở 2 mặt";
        private const string PosCanhDungLM = "Cạnh đứng LM";
        private const string PosCanhDauLM = "Cạnh đầu LM";
        private const string PosCanhSillLM = "Cạnh sill LM";
        private const string PosDinhVach = "Đỉnh vách";
        private const string PosChanVach = "Chân vách";
        private const string PosCanhHo = "Mép đứng tự do";
        private const string PosPanelThep = "Panel→thép";
        private const string PosVach = "Vách";
        private const string PosTreoTNhôm = "Treo T nhôm";
        private const string PosTreoBulongNam = "Treo bulong nấm";

        // ═══════════════════════════════════════════════════
        // PRODUCT NAME CONSTANTS
        // ═══════════════════════════════════════════════════
        private const string SheetTrim01 = "Diềm 01";
        private const string SealantMS617 = "Sealant MS-617";
        private const string SiliconeSN505 = "Silicone SN-505";
        private const string SealantMC202 = "Sealant MC-202";
        private const string VBoR40Name = "V bo R40";
        private const string CleanroomPerimeterAngleName = "V 40x80";
        private const string BoltSetM10Name = "Bu lông + tán + long đen M10";
        private const string CleanroomMushroomBoltName = "Bulong nấm nhựa M10x100L";
        private const string ColdStorageMushroomBoltName = "Bulong nấm nhựa M12x200L";
        private const double SiliconeSn505JointBottleFactor = 1.0 / 15.0; // quy doi 15 md/chai cho keo moi noi
        private const double SealantMc202BottleFactor = 1.0 / 6.0; // quy doi 6 md/chai cho sealant moi noi
        private const double RiveAt300Factor = 1000.0 / 300.0; // quy doi rive @300 theo md tuyen phu kien
        private const double RiveAt500Factor = 1000.0 / 500.0; // quy doi rive @500 theo md tuyen phu kien

        // Legacy name mappings (for backward compat with saved JSON)
        private static readonly Dictionary<string, string> LegacyNameMap = new()
        {
            { "Sealant mối nối", SealantMS617 },
            { "Sealant lỗ mở", SealantMS617 },
            { "Sealant hoàn thiện ngoài", SealantMS617 },
            { "Silicone vệ sinh", SiliconeSN505 },
            { "Sealant chống thấm", SealantMC202 },
            { "Silicone SN-505 kháng mốc", SiliconeSN505 },
            { "Silicone SN-505 mối nối phòng sạch", SiliconeSN505 },
            { "Nẹp nhôm phẳng", SiliconeSN505 },
            { "Nẹp chân vệ sinh", VBoR40Name },
            { "Nẹp bo cong R40", VBoR40Name },
            { "R40 P.C. Alu. Internal cove", VBoR40Name },
            { "Sealant MC-202 giữa ngàm panel kho lạnh", SealantMC202 },
            { "Vít TEK ngoài nhà", ExteriorTekScrewBaseName },
            { "Vít TEK inox", "B2S-TEK inox" },
            // v2.x → v3.x renames
            { "B2S-TEK", ExteriorTekScrewBaseName },               // vis panel: B2S-TEK → B2S-TEK 12x14
            { "Mastic tape", "Băng keo butyl" },
            { "Alu. Omega profile", "Nẹp Omega" },
            { "Mastic tape (Omega)", "Băng keo butyl Omega" },
            { "Foam PU on site", "Foam PU bơm tại chỗ" },
            { "Panel support bracket", "Bát thép chân panel" },
            { "Rivet inox Ø4.2×12", "Rive Ø4.2×12" },
            { "Rive inox Ø4.2×12", "Rive Ø4.2×12" },
            { "T-profile", "Thanh T" },
            { "T-profile 68x75", "Thanh T 68x75" },
            { "Steel plate 50x325x3", "Bản mã 50x325x3" },
            { "Turnbuckle M10", "Tăng đơ M10" },
            { "Turnbuckle M12", "Tăng đơ M12" },
            { "Wire rope Ø10", "Cáp treo Ø10" },
            { "Wire rope Ø12", "Cáp treo Ø12" },
            { "Wire rope clip Ø10", "Cùm xiết cáp Ø10" },
            { "Wire rope clip Ø12", "Cùm xiết cáp Ø12" },
            { "Cùm cáp Ø10", "Cùm xiết cáp Ø10" },
            { "Cùm cáp Ø12", "Cùm xiết cáp Ø12" },
            { "C-channel 100x50", "Thanh C 100x50" },
            { "Bu lông + tán + long đen", BoltSetM10Name },
            { "Ống PVC Ø114", "Ống Ø114" },
            { "Ống phi 114", "Ống Ø114" },
            // Legacy exterior flashing names → Diềm 01-05
            { "Úp nóc",       "Diềm 01" },
            { "Úp chân",      "Diềm 02" },
            { "Úp cạnh hở",   "Diềm 03" },
            { "Nẹp cạnh hở",  "Nẹp mép đứng" },
            { "Úp góc ngoài", "Diềm 04" },
            { "Úp nối dọc",   "Diềm 05" },
            // English Flashing format (intermediate naming convention)
            { "Flashing đỉnh (top)",      "Diềm 01" },
            { "Flashing chân (base)",      "Diềm 02" },
            { "Flashing nối dọc (joint)",  "Diềm 05" },
        };

        // Legacy long names → Position mapping (for expansion backward compat)
        private static readonly Dictionary<string, string> LegacyPositionMap = new()
        {
            { "Sealant MS-617 mối nối", PosMoiNoi },
            { "Sealant MS-617 viền lỗ mở", PosLoMo },
            { "Sealant MS-617 hoàn thiện lộ thiên ngoài nhà", PosVach },
            { "Sealant MS-617 cạnh đứng lỗ mở ngoài nhà", PosCanhDungLM },
            { "Sealant MS-617 cạnh đầu lỗ mở ngoài nhà", PosCanhDauLM },
            { "Sealant MS-617 cạnh sill lỗ mở ngoài nhà", PosCanhSillLM },
            { "Sealant MS-617 đỉnh vách ngoài nhà", PosDinhVach },
            { "Sealant MS-617 chân vách ngoài nhà", PosChanVach },
            { "Sealant MS-617 cạnh hở ngoài nhà", PosCanhHo },
            { "Silicone SN-505 viền lỗ mở 2 mặt phòng sạch", PosLoMo2Mat },
            { "Silicone SN-505 cạnh đứng lỗ mở 2 mặt phòng sạch", PosCanhDungLM },
            { "Silicone SN-505 cạnh đầu lỗ mở 2 mặt phòng sạch", PosCanhDauLM },
            { "Silicone SN-505 cạnh sill lỗ mở 2 mặt phòng sạch", PosCanhSillLM },
            { "Silicone SN-505 hoàn thiện vệ sinh phòng sạch", PosVach },
            { "Silicone SN-505 đỉnh vách phòng sạch", PosDinhVach },
            { "Silicone SN-505 chân vách phòng sạch", PosChanVach },
            { "Silicone SN-505 cạnh hở phòng sạch", PosCanhHo },
            { "Nẹp nhôm phẳng", PosMoiNoi },
            { "Nẹp chân vệ sinh", PosCanhDuoi },
            { "Silicone SN-505 viền lỗ mở kho lạnh", PosLoMo },
            { "Silicone SN-505 cạnh đứng lỗ mở kho lạnh", PosCanhDungLM },
            { "Silicone SN-505 cạnh đầu lỗ mở kho lạnh", PosCanhDauLM },
            { "Silicone SN-505 cạnh sill lỗ mở kho lạnh", PosCanhSillLM },
            { "Silicone SN-505 hoàn thiện lộ thiên kho lạnh", PosVach },
            { "Silicone SN-505 đỉnh vách kho lạnh", PosDinhVach },
            { "Silicone SN-505 chân vách kho lạnh", PosChanVach },
            { "Silicone SN-505 cạnh hở kho lạnh", PosCanhHo },
        };

        public static List<TenderAccessory> GetDefaults()
        {
            var list = new List<TenderAccessory>();
            list.AddRange(GetExteriorDefaults());
            list.AddRange(GetCleanroomDefaults());
            list.AddRange(GetCleanroomCeilingDefaults());
            list.AddRange(GetColdStorageDefaults());
            return DeduplicateAndSort(ExpandLineBasedRiveAccessories(list));
        }

        public static List<TenderAccessory> NormalizeConfiguredAccessories(IEnumerable<TenderAccessory>? accessories)
        {
            var source = accessories?.ToList() ?? new List<TenderAccessory>();
            if (source.Count == 0)
                return GetDefaults();

            var canonicalDefaults = GetDefaults();
            var normalized = new List<TenderAccessory>();

            foreach (var accessory in source)
            {
                if (accessory == null)
                    continue;

                // Migrate legacy names
                MigrateLegacyAccessory(accessory);

                if (IsObsoleteConfiguredAccessory(accessory))
                    continue;

                string category = TenderAccessoryRules.NormalizeScope(accessory.CategoryScope);
                string application = TenderAccessoryRules.NormalizeScope(accessory.Application);
                bool allCategory = TenderAccessoryRules.IsAllScope(category);
                bool allApplication = TenderAccessoryRules.IsAllScope(application);

                if (allCategory && allApplication)
                {
                    foreach (var app in TenderWall.ApplicationOptions)
                        normalized.AddRange(ExpandForScope(accessory, DefaultCategoryScope, app));
                    continue;
                }

                if (allCategory)
                    category = DefaultCategoryScope;

                if (allApplication)
                {
                    foreach (var app in TenderWall.ApplicationOptions)
                        normalized.AddRange(ExpandForScope(accessory, category, app));
                    continue;
                }

                normalized.AddRange(ExpandForScope(accessory, category, application));
            }

            normalized = ExpandLineBasedRiveAccessories(normalized);
            normalized = normalized
                .Where(accessory => !ShouldPruneConfiguredAccessory(accessory, canonicalDefaults))
                .Where(accessory => ShouldKeepConfiguredAccessory(accessory, canonicalDefaults))
                .ToList();
            SynchronizeConfiguredAccessories(normalized, canonicalDefaults);
            MergeMissingDefaults(normalized, canonicalDefaults);
            return DeduplicateAndSort(normalized);
        }

#pragma warning disable CS8602
        private static void MigrateLegacyAccessory(TenderAccessory accessory)
        {
            string name = (accessory.Name ?? string.Empty).Trim();
            string application = TenderAccessoryRules.NormalizeScope(accessory.Application);
            string position = (accessory.Position ?? string.Empty).Trim();

            if (string.Equals(position, "Cạnh hở", StringComparison.OrdinalIgnoreCase)
                || string.Equals(position, "Mép hở", StringComparison.OrdinalIgnoreCase))
            {
                accessory.Position = PosCanhHo;
            }
            else if (string.Equals(position, "Đầu/cuối", StringComparison.OrdinalIgnoreCase))
            {
                accessory.Position = PosDauCuoi;
            }
            else if (string.Equals(position, "Cạnh trên", StringComparison.OrdinalIgnoreCase))
            {
                accessory.Position = PosCanhTren;
            }
            else if (string.Equals(position, "Cạnh dưới", StringComparison.OrdinalIgnoreCase))
            {
                accessory.Position = PosCanhDuoi;
            }

            // Check if long legacy name → extract position and remap name
            if (LegacyPositionMap.TryGetValue(name, out string? legacyPos))
            {
                if (string.IsNullOrWhiteSpace(accessory.Position))
                    accessory.Position = legacyPos;

                // Extract product name from legacy long name
                if (name.StartsWith("Sealant MS-617"))
                    accessory.Name = SealantMS617;
                else if (name.StartsWith("Silicone SN-505"))
                    accessory.Name = SiliconeSN505;
                else if (name.StartsWith("Sealant MC-202"))
                    accessory.Name = SealantMC202;
            }

            // Check simple legacy name → remap (use trimmed name for reliable match)
            if (LegacyNameMap.TryGetValue(name, out string? newName))
                accessory.Name = newName;

            if (string.Equals(application, AppExterior, StringComparison.OrdinalIgnoreCase))
            {
                if (string.Equals(name, "Rivet Ø4.2×12", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(name, "Rivets", StringComparison.OrdinalIgnoreCase))
                {
                    accessory.Name = "B2S-TEK 15-15×20 HWFS";
                }
            }

            if (string.Equals(application, AppCleanroom, StringComparison.OrdinalIgnoreCase))
            {
                if (string.Equals(name, "Rivet inox Ø4.2×12", StringComparison.OrdinalIgnoreCase))
                {
                    accessory.Name = "Rive Ø4.2×12";
                    accessory.Material = "Nhôm";
                }

                if (string.Equals(name, "Rive inox Ø4.2×12", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(name, "Rive Ø4.2×12", StringComparison.OrdinalIgnoreCase))
                {
                    accessory.Name = "Rive Ø4.2×12";
                    accessory.Material = "Nhôm";
                }

                if (string.Equals(accessory.Position, PosTreoTNhôm, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(accessory.Name, "Bu lông + tán + long đen", StringComparison.OrdinalIgnoreCase))
                {
                    accessory.Name = BoltSetM10Name;
                }

                if (string.Equals(accessory.Position, PosTreoBulongNam, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(accessory.Name, "Bulong nấm nhựa", StringComparison.OrdinalIgnoreCase))
                {
                    accessory.Name = CleanroomMushroomBoltName;
                }
            }

            if (string.Equals(application, AppColdStorage, StringComparison.OrdinalIgnoreCase))
            {
                if (string.Equals(name, "Rivet Ø4.2×12", StringComparison.OrdinalIgnoreCase))
                    accessory.Name = "Rive Ø4.2×12";

                if (string.Equals(accessory.Position, PosTreoBulongNam, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(accessory.Name, "Ty ren M10", StringComparison.OrdinalIgnoreCase))
                {
                    accessory.Name = "Ty ren M12";
                }

                if (IsColdStorageBottomUChannelName(accessory.Name))
                    accessory.Name = ColdStorageBottomUChannelBaseName;

                if (string.Equals(accessory.Name, "Bản mã 50x325x3", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(accessory.Name, "Ty ren M12", StringComparison.OrdinalIgnoreCase))
                {
                    if (string.IsNullOrWhiteSpace(accessory.Material)
                        || string.Equals(accessory.Material, "Thép", StringComparison.OrdinalIgnoreCase))
                    {
                        accessory.Material = "Thép mạ kẽm";
                    }
                }

                if (string.Equals(accessory.Position, PosTreoBulongNam, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(accessory.Name, "Bulong nấm nhựa", StringComparison.OrdinalIgnoreCase))
                {
                    accessory.Name = ColdStorageMushroomBoltName;
                }
            }

            // Migrate CalcRule: B2S-TEK panel screw PER_PANEL_QTY → PER_TEK_SCREW_QTY
            if (string.Equals(accessory.Name, ExteriorTekScrewBaseName, StringComparison.OrdinalIgnoreCase)
                && accessory.CalcRule == AccessoryCalcRule.PER_PANEL_QTY)
            {
                accessory.CalcRule = AccessoryCalcRule.PER_TEK_SCREW_QTY;
                accessory.Factor = 1.0;
            }

            // Cold storage convention: keep trim name in Name and move material to Material.
            if (string.Equals(application, AppColdStorage, StringComparison.OrdinalIgnoreCase))
            {
                if (string.Equals(name, "Viền lỗ mở", StringComparison.OrdinalIgnoreCase))
                    accessory.Name = SheetTrim01;

                if (string.Equals(name, "Viền lỗ mở 2 mặt", StringComparison.OrdinalIgnoreCase))
                    accessory.Name = SheetTrim01;

                if (string.Equals(accessory.Name, "B2S-TEK inox", StringComparison.OrdinalIgnoreCase))
                {
                    accessory.Name = ExteriorTekScrewBaseName;
                    if (string.IsNullOrWhiteSpace(accessory.Material))
                        accessory.Material = "Inox";
                }
            }

            if (string.Equals(application, AppExterior, StringComparison.OrdinalIgnoreCase)
                && accessory.CalcRule == AccessoryCalcRule.PER_VERTICAL_JOINT_HEIGHT)
            {
                accessory.Position = PosKheDung;
            }

            // Auto-set Material for all Diềm items that haven't been set yet
            if (accessory.Name.StartsWith("Diềm ", StringComparison.OrdinalIgnoreCase)
                && string.IsNullOrWhiteSpace(accessory.Material))
            {
                accessory.Material = "Tole";
            }

            if (string.IsNullOrWhiteSpace(accessory.Material))
            {
                if (string.Equals(accessory.Name, VBoR40Name, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(accessory.Name, "Nẹp bo cong R40", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(accessory.Name, "V 80x80", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(accessory.Name, CleanroomPerimeterAngleName, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(accessory.Name, "Nẹp Omega", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(accessory.Name, "Thanh T", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(accessory.Name, "Thanh T 68x75", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(accessory.Name, "Rive Ø4.2×12", StringComparison.OrdinalIgnoreCase))
                {
                    accessory.Material = "Nhôm";
                }
            }

            if (string.Equals(application, AppCleanroom, StringComparison.OrdinalIgnoreCase))
            {
                if (string.Equals(name, "Viền lỗ mở", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(name, "Viền lỗ mở 2 mặt", StringComparison.OrdinalIgnoreCase))
                {
                    accessory.Name = CleanroomBottomUChannelBaseName;
                }

                if (string.Equals(accessory.Position, PosTreoTNhôm, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(accessory.Name, "Thanh T", StringComparison.OrdinalIgnoreCase))
                {
                    accessory.Name = "Thanh T 60x80";
                }

                if (string.Equals(accessory.Position, PosTreoTNhôm, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(accessory.Name, "Bu lông + tán + long đen", StringComparison.OrdinalIgnoreCase))
                {
                    accessory.Name = BoltSetM10Name;
                }

                if (string.Equals(accessory.Position, PosTreoBulongNam, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(accessory.Name, "Bulong nấm nhựa", StringComparison.OrdinalIgnoreCase))
                {
                    accessory.Name = CleanroomMushroomBoltName;
                }
            }

        }
#pragma warning restore CS8602


        private static List<TenderAccessory> ExpandLineBasedRiveAccessories(IEnumerable<TenderAccessory> items)
        {
            var expanded = new List<TenderAccessory>();
            foreach (var item in items)
                expanded.AddRange(ExpandLineBasedRiveAccessory(item));

            return expanded;
        }

        private static IEnumerable<TenderAccessory> ExpandLineBasedRiveAccessory(TenderAccessory accessory)
        {
            if (!ShouldExpandLineBasedRive(accessory))
            {
                yield return accessory;
                yield break;
            }

            string application = TenderAccessoryRules.NormalizeScope(accessory.Application);
            if (string.Equals(application, AppCleanroom, StringComparison.OrdinalIgnoreCase))
            {
                yield return CloneAccessory(accessory, accessory.Name, PosCanhDuoi, AccessoryCalcRule.PER_BOTTOM_EDGE_LENGTH, RiveAt500Factor * 2.0, "Bắn theo 2 tuyến V bo R40 chân vách @500");
                yield return CloneAccessory(accessory, accessory.Name, PosCanhTren, AccessoryCalcRule.PER_TOP_PANEL_CEILING_CENTER_LENGTH, RiveAt500Factor * 2.0, "Đỉnh vách giao trần giữa: 2 tuyến rive theo 2 V bo R40 @500");
                yield return CloneAccessory(accessory, accessory.Name, PosCanhTren, AccessoryCalcRule.PER_TOP_PANEL_CEILING_PERIMETER_LENGTH, RiveAt500Factor * 2.0, "Đỉnh vách giao biên trần: 2 tuyến rive theo V 40x80 + V bo R40 @500");
                yield return CloneAccessory(accessory, accessory.Name, PosDauCuoi, AccessoryCalcRule.PER_END_PANEL_CENTER_LENGTH, RiveAt500Factor * 2.0, "Đầu/cuối vách giao giữa: 2 tuyến rive theo 2 V bo R40 @500");
                yield return CloneAccessory(accessory, accessory.Name, PosDauCuoi, AccessoryCalcRule.PER_END_PANEL_PERIMETER_LENGTH, RiveAt500Factor * 2.0, "Đầu/cuối vách giao biên: 2 tuyến rive theo V 40x80 + V bo R40 @500");
                yield return CloneAccessory(accessory, accessory.Name, PosLoMo, AccessoryCalcRule.PER_OPENING_PERIMETER, RiveAt500Factor, "Lỗ mở phòng sạch: bắn theo U nhôm 4 cạnh @500");
                yield break;
            }

            if (string.Equals(application, AppColdStorage, StringComparison.OrdinalIgnoreCase))
            {
                yield return CloneAccessory(accessory, accessory.Name, PosCanhTren, AccessoryCalcRule.PER_TOP_PANEL_CEILING_CENTER_LENGTH, 4.0, "Đỉnh vách giao trần giữa: 2 tuyến rive @500");
                yield return CloneAccessory(accessory, accessory.Name, PosCanhTren, AccessoryCalcRule.PER_TOP_PANEL_CEILING_PERIMETER_LENGTH, RiveAt500Factor * 2.0, "Đỉnh vách giao biên trần: 2 tuyến rive @500");
                yield return CloneAccessory(accessory, accessory.Name, PosCanhTren, AccessoryCalcRule.PER_TOP_PANEL_FREE_LENGTH, RiveAt300Factor, "Đỉnh vách mép tự do: 1 tuyến rive theo diềm @300");
                yield return CloneAccessory(accessory, accessory.Name, PosDauCuoi, AccessoryCalcRule.PER_END_PANEL_CENTER_LENGTH, RiveAt500Factor * 2.0, "Đầu/cuối vách giao giữa: 2 tuyến rive @500");
                yield return CloneAccessory(accessory, accessory.Name, PosDauCuoi, AccessoryCalcRule.PER_END_PANEL_PERIMETER_LENGTH, RiveAt500Factor * 2.0, "Đầu/cuối vách giao biên: 2 tuyến rive @500");
                yield return CloneAccessory(accessory, accessory.Name, PosDauCuoi, AccessoryCalcRule.PER_END_PANEL_FREE_LENGTH, RiveAt300Factor, "Đầu/cuối vách mép đứng tự do: 1 tuyến rive @300");
                yield return CloneAccessory(accessory, accessory.Name, PosMoiNoi, AccessoryCalcRule.PER_JOINT_LENGTH, RiveAt300Factor, "Bắn theo diềm mối nối @300");
                yield return CloneAccessory(accessory, accessory.Name, PosLoMo, AccessoryCalcRule.PER_OPENING_PERIMETER_TWO_FACES, RiveAt300Factor, "Bắn theo diềm viền lỗ mở 2 mặt @300");
                yield break;
            }

            yield return accessory;
        }

        private static bool ShouldExpandLineBasedRive(TenderAccessory accessory)
        {
            string name = (accessory.Name ?? string.Empty).Trim();
            if (!string.Equals(name, "Rive Ø4.2×12", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(name, "Rive Ã˜4.2Ã—12", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(name, "Rivet Ø4.2×12", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(name, "Rivet Ã˜4.2Ã—12", StringComparison.OrdinalIgnoreCase))
                return false;

            if (!string.Equals((accessory.Position ?? string.Empty).Trim(), PosVach, StringComparison.OrdinalIgnoreCase))
                return false;

            if (accessory.CalcRule != AccessoryCalcRule.PER_WALL_LENGTH)
                return false;

            string application = TenderAccessoryRules.NormalizeScope(accessory.Application);
            return string.Equals(application, AppCleanroom, StringComparison.OrdinalIgnoreCase)
                || string.Equals(application, AppColdStorage, StringComparison.OrdinalIgnoreCase);
        }

        private static List<TenderAccessory> DeduplicateAndSort(IEnumerable<TenderAccessory> items)
        {
            return items
                .GroupBy(item => new
                {
                    item.CategoryScope,
                    item.Application,
                    item.SpecKey,
                    item.Name,
                    item.Material,
                    item.Position,
                    item.Unit,
                    item.CalcRule
                })
                .Select(group => MergeDuplicateAccessoryGroup(group.ToList()))
                .OrderBy(item => item, Comparer<TenderAccessory>.Create(CompareAccessories))
                .ToList();
        }

        private static TenderAccessory MergeDuplicateAccessoryGroup(List<TenderAccessory> items)
        {
            var seed = items[0];
            var latest = items[^1];
            string note = items
                .Select(item => item.Note?.Trim())
                .LastOrDefault(text => !string.IsNullOrWhiteSpace(text))
                ?? string.Empty;

            return new TenderAccessory
            {
                Name = seed.Name?.Trim() ?? string.Empty,
                Material = seed.Material?.Trim() ?? string.Empty,
                Position = seed.Position?.Trim() ?? string.Empty,
                Unit = seed.Unit,
                CalcRule = seed.CalcRule,
                Factor = seed.Factor,
                CategoryScope = seed.CategoryScope,
                SpecKey = seed.SpecKey,
                Application = seed.Application,
                WasteFactor = latest.WasteFactor,
                Adjustment = latest.Adjustment,
                IsManualOnly = latest.IsManualOnly,
                Note = note
            };
        }

        private static void MergeMissingDefaults(
            List<TenderAccessory> configuredAccessories,
            IReadOnlyList<TenderAccessory> canonicalDefaults)
        {
            foreach (var defaultAccessory in canonicalDefaults)
            {
                if (configuredAccessories.Any(item => HasSameLogicalAccessory(item, defaultAccessory)))
                    continue;

                configuredAccessories.Add(defaultAccessory);
            }
        }

        private static bool IsObsoleteConfiguredAccessory(TenderAccessory accessory)
        {
            string application = TenderAccessoryRules.NormalizeScope(accessory.Application);
            string name = (accessory.Name ?? string.Empty).Trim();
            string position = (accessory.Position ?? string.Empty).Trim();

            if (string.Equals(application, AppExterior, StringComparison.OrdinalIgnoreCase)
                && string.Equals(name, "B2S-TEK 15-15×20 HWFS", StringComparison.OrdinalIgnoreCase)
                && string.Equals(position, PosVach, StringComparison.OrdinalIgnoreCase)
                && accessory.CalcRule == AccessoryCalcRule.PER_WALL_LENGTH)
            {
                return true;
            }

            if (string.Equals(application, AppColdStorage, StringComparison.OrdinalIgnoreCase))
            {
                if (string.Equals(name, "B2S-TEK 15-15×20 HWFS", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(name, "Sealant opening", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(name, "Viền opening (trim)", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(name, "Diềm 02", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(name, "Diềm 05", StringComparison.OrdinalIgnoreCase)
                    || IsColdStorageTekAlias(name))
                {
                    return true;
                }
            }

            if (string.Equals(application, AppCleanroom, StringComparison.OrdinalIgnoreCase)
                && (string.Equals(name, SiliconeSN505, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(name, CleanroomBottomUChannelBaseName, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(name, "Viền lỗ mở 2 mặt", StringComparison.OrdinalIgnoreCase))
                && (string.Equals(position, PosCanhDungLM, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(position, PosCanhDauLM, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(position, PosCanhSillLM, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(position, PosLoMo2Mat, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            return false;
        }

        private static bool ShouldPruneConfiguredAccessory(
            TenderAccessory accessory,
            IReadOnlyList<TenderAccessory> canonicalDefaults)
        {
            if (FindCanonicalDefault(accessory, canonicalDefaults) != null)
                return false;

            if (IsObsoleteConfiguredAccessory(accessory))
                return true;

            return IsManagedCanonicalName(accessory, canonicalDefaults);
        }

        private static bool ShouldKeepConfiguredAccessory(
            TenderAccessory accessory,
            IReadOnlyList<TenderAccessory> canonicalDefaults)
        {
            if (FindCanonicalDefault(accessory, canonicalDefaults) != null)
                return true;

            if (accessory.IsManualOnly)
                return true;

            if (string.IsNullOrWhiteSpace(accessory.Note))
                return false;

            return true;
        }

        private static bool IsManagedCanonicalName(
            TenderAccessory accessory,
            IReadOnlyList<TenderAccessory> canonicalDefaults)
        {
            string category = TenderAccessoryRules.NormalizeScope(accessory.CategoryScope);
            string application = TenderAccessoryRules.NormalizeScope(accessory.Application);
            string specKey = TenderAccessoryRules.NormalizeScope(accessory.SpecKey);
            string name = (accessory.Name ?? string.Empty).Trim();

            return canonicalDefaults.Any(item =>
                string.Equals(TenderAccessoryRules.NormalizeScope(item.CategoryScope), category, System.StringComparison.OrdinalIgnoreCase)
                && string.Equals(TenderAccessoryRules.NormalizeScope(item.Application), application, System.StringComparison.OrdinalIgnoreCase)
                && string.Equals(TenderAccessoryRules.NormalizeScope(item.SpecKey), specKey, System.StringComparison.OrdinalIgnoreCase)
                && string.Equals((item.Name ?? string.Empty).Trim(), name, System.StringComparison.OrdinalIgnoreCase));
        }

        private static void SynchronizeConfiguredAccessories(
            List<TenderAccessory> configuredAccessories,
            IReadOnlyList<TenderAccessory> canonicalDefaults)
        {
            foreach (var accessory in configuredAccessories)
            {
                var canonical = FindCanonicalDefault(accessory, canonicalDefaults);
                if (canonical == null)
                    continue;

                accessory.Name = canonical.Name;
                accessory.Material = canonical.Material;
                accessory.Position = canonical.Position;
                accessory.Unit = canonical.Unit;
                accessory.CalcRule = canonical.CalcRule;
                accessory.Factor = canonical.Factor;
                accessory.CategoryScope = canonical.CategoryScope;
                accessory.SpecKey = canonical.SpecKey;
                accessory.Application = canonical.Application;
            }
        }

        private static TenderAccessory? FindCanonicalDefault(
            TenderAccessory accessory,
            IReadOnlyList<TenderAccessory> canonicalDefaults)
        {
            string category = TenderAccessoryRules.NormalizeScope(accessory.CategoryScope);
            string application = TenderAccessoryRules.NormalizeScope(accessory.Application);
            string specKey = TenderAccessoryRules.NormalizeScope(accessory.SpecKey);
            string name = (accessory.Name ?? string.Empty).Trim();
            string position = (accessory.Position ?? string.Empty).Trim();

            var exactMatch = canonicalDefaults.FirstOrDefault(item =>
                string.Equals(TenderAccessoryRules.NormalizeScope(item.CategoryScope), category, System.StringComparison.OrdinalIgnoreCase)
                && string.Equals(TenderAccessoryRules.NormalizeScope(item.Application), application, System.StringComparison.OrdinalIgnoreCase)
                && string.Equals(TenderAccessoryRules.NormalizeScope(item.SpecKey), specKey, System.StringComparison.OrdinalIgnoreCase)
                && string.Equals((item.Name ?? string.Empty).Trim(), name, System.StringComparison.OrdinalIgnoreCase)
                && string.Equals((item.Position ?? string.Empty).Trim(), position, System.StringComparison.OrdinalIgnoreCase)
                && item.CalcRule == accessory.CalcRule);

            if (exactMatch != null)
                return exactMatch;

            return FindCanonicalTekAccessory(category, application, specKey, name, canonicalDefaults);
        }

        private static TenderAccessory? FindCanonicalTekAccessory(
            string category,
            string application,
            string specKey,
            string name,
            IReadOnlyList<TenderAccessory> canonicalDefaults)
        {
            if (IsColdStorageTekAlias(name))
            {
                return canonicalDefaults.FirstOrDefault(item =>
                    string.Equals(TenderAccessoryRules.NormalizeScope(item.CategoryScope), category, System.StringComparison.OrdinalIgnoreCase)
                    && string.Equals(TenderAccessoryRules.NormalizeScope(item.Application), application, System.StringComparison.OrdinalIgnoreCase)
                    && string.Equals(TenderAccessoryRules.NormalizeScope(item.SpecKey), specKey, System.StringComparison.OrdinalIgnoreCase)
                    && string.Equals(item.Name, "B2S-TEK", System.StringComparison.OrdinalIgnoreCase)
                    && string.Equals(item.Position, PosPanelThep, System.StringComparison.OrdinalIgnoreCase)
                    && string.Equals(item.Material, "Inox", System.StringComparison.OrdinalIgnoreCase));
            }

            if (IsExteriorTekAlias(name))
            {
                return canonicalDefaults.FirstOrDefault(item =>
                    string.Equals(TenderAccessoryRules.NormalizeScope(item.CategoryScope), category, System.StringComparison.OrdinalIgnoreCase)
                    && string.Equals(TenderAccessoryRules.NormalizeScope(item.Application), application, System.StringComparison.OrdinalIgnoreCase)
                    && string.Equals(TenderAccessoryRules.NormalizeScope(item.SpecKey), specKey, System.StringComparison.OrdinalIgnoreCase)
                    && string.Equals(item.Name, ExteriorTekScrewBaseName, System.StringComparison.OrdinalIgnoreCase)
                    && string.Equals(item.Position, PosPanelThep, System.StringComparison.OrdinalIgnoreCase));
            }

            return null;
        }

        private static bool IsColdStorageTekAlias(string name)
        {
            return string.Equals(name, "B2S-TEK", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, ExteriorTekScrewBaseName, StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "B2S-TEK inox", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "Vít TEK", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "Vit TEK", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "Vít TEK inox", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "Vit TEK inox", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsExteriorTekAlias(string name)
        {
            return string.Equals(name, ExteriorTekScrewBaseName, StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "B2S-TEK", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "Vít TEK ngoài nhà", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "Vit TEK ngoai nha", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsColdStorageBottomUChannelName(string? name)
        {
            string normalized = (name ?? string.Empty).Trim();
            if (!normalized.StartsWith("U 40x", StringComparison.OrdinalIgnoreCase)
                || !normalized.EndsWith("x40", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            int middleLength = normalized.Length - "U 40x".Length - "x40".Length;
            if (middleLength <= 0)
                return false;

            return int.TryParse(normalized.Substring("U 40x".Length, middleLength), out _);
        }

        private static bool HasSameLogicalAccessory(TenderAccessory left, TenderAccessory right)
        {
            return string.Equals(TenderAccessoryRules.NormalizeScope(left.CategoryScope), TenderAccessoryRules.NormalizeScope(right.CategoryScope), System.StringComparison.OrdinalIgnoreCase)
                && string.Equals(TenderAccessoryRules.NormalizeScope(left.Application), TenderAccessoryRules.NormalizeScope(right.Application), System.StringComparison.OrdinalIgnoreCase)
                && string.Equals(TenderAccessoryRules.NormalizeScope(left.SpecKey), TenderAccessoryRules.NormalizeScope(right.SpecKey), System.StringComparison.OrdinalIgnoreCase)
                && string.Equals(left.Name?.Trim(), right.Name?.Trim(), System.StringComparison.OrdinalIgnoreCase)
                && string.Equals(left.Material?.Trim(), right.Material?.Trim(), System.StringComparison.OrdinalIgnoreCase)
                && string.Equals(left.Position?.Trim(), right.Position?.Trim(), System.StringComparison.OrdinalIgnoreCase)
                && string.Equals(left.Unit?.Trim(), right.Unit?.Trim(), System.StringComparison.OrdinalIgnoreCase)
                && left.CalcRule == right.CalcRule;
        }

        private static TenderAccessory CloneForScope(TenderAccessory source, string categoryScope, string application)
        {
            return new TenderAccessory
            {
                Name = source.Name?.Trim() ?? string.Empty,
                Material = source.Material?.Trim() ?? string.Empty,
                Position = source.Position?.Trim() ?? string.Empty,
                Unit = source.Unit,
                CalcRule = source.CalcRule,
                Factor = source.Factor,
                CategoryScope = categoryScope,
                SpecKey = TenderAccessoryRules.NormalizeScope(source.SpecKey),
                Application = application,
                WasteFactor = source.WasteFactor,
                Adjustment = source.Adjustment,
                IsManualOnly = source.IsManualOnly,
                Note = source.Note
            };
        }

        private static IEnumerable<TenderAccessory> ExpandForScope(TenderAccessory source, string categoryScope, string application)
        {
            var normalized = CloneForScope(source, categoryScope, application);

            if (ShouldDropDeprecatedAccessory(normalized))
                yield break;

            bool isCleanroom = string.Equals(
                TenderAccessoryRules.NormalizeScope(normalized.Application),
                AppCleanroom,
                StringComparison.OrdinalIgnoreCase);

            if (isCleanroom
                && (normalized.Position == PosLoMo || normalized.Position == PosLoMo2Mat)
                && (string.Equals(normalized.Name, CleanroomBottomUChannelBaseName, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(normalized.Name, SiliconeSN505, StringComparison.OrdinalIgnoreCase)))
            {
                normalized.Position = PosLoMo;
                normalized.CalcRule = AccessoryCalcRule.PER_OPENING_PERIMETER;
                if (string.Equals(normalized.Name, SiliconeSN505, StringComparison.OrdinalIgnoreCase))
                    normalized.Factor = SiliconeSn505JointBottleFactor * 2.0;

                yield return normalized;
                yield break;
            }

            // Sealant/silicone opening expansion: Lỗ mở → cạnh đứng + cạnh đầu + cạnh sill
            if (normalized.Position == PosLoMo || normalized.Position == PosLoMo2Mat)
            {
                bool twoFaces = normalized.Position == PosLoMo2Mat;
                double factor = twoFaces ? normalized.Factor * 2.0 : normalized.Factor;
                string suffix = twoFaces ? " (2 mặt)" : "";

                yield return CloneAccessory(normalized, normalized.Name, PosCanhDungLM,
                    AccessoryCalcRule.PER_OPENING_VERTICAL_EDGES, factor,
                    $"Cạnh đứng lỗ mở{suffix}");
                yield return CloneAccessory(normalized, normalized.Name, PosCanhDauLM,
                    AccessoryCalcRule.PER_OPENING_HORIZONTAL_TOP_LENGTH, factor,
                    $"Cạnh đầu lỗ mở{suffix}");
                yield return CloneAccessory(normalized, normalized.Name, PosCanhSillLM,
                    AccessoryCalcRule.PER_OPENING_SILL_LENGTH, factor,
                    $"Cạnh sill lỗ mở{suffix}");
                yield break;
            }

            // Sealant/silicone vách expansion: Vách biên tự do → đỉnh + chân + mép đứng tự do
            if (normalized.Position == PosVach
                && (normalized.Name == SealantMS617 || normalized.Name == SiliconeSN505))
            {
                yield return CloneAccessory(normalized, normalized.Name, PosDinhVach,
                    AccessoryCalcRule.PER_TOP_EDGE_LENGTH, normalized.Factor,
                    "Hoàn thiện đỉnh vách");
                yield return CloneAccessory(normalized, normalized.Name, PosChanVach,
                    AccessoryCalcRule.PER_BOTTOM_EDGE_LENGTH, normalized.Factor,
                    "Hoàn thiện chân vách");
                yield return CloneAccessory(normalized, normalized.Name, PosCanhHo,
                    AccessoryCalcRule.PER_EXPOSED_END_LENGTH, normalized.Factor,
                    "Hoàn thiện mép đứng tự do");
                yield break;
            }

            yield return normalized;
        }

        private static bool ShouldDropDeprecatedAccessory(TenderAccessory accessory)
        {
            if (!string.Equals(TenderAccessoryRules.NormalizeScope(accessory.Application), AppCleanroom, StringComparison.OrdinalIgnoreCase))
                return false;

            string name = (accessory.Name ?? string.Empty).Trim();
            return string.Equals(name, "VÃ­t TEK", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "Vit TEK", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "VÃ­t TEK inox", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "Vit TEK inox", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "B2S-TEK inox", StringComparison.OrdinalIgnoreCase);
        }

        private static TenderAccessory CloneAccessory(
            TenderAccessory source, string name, string position,
            AccessoryCalcRule calcRule, double factor, string note)
        {
            return new TenderAccessory
            {
                Name = name,
                Material = source.Material?.Trim() ?? string.Empty,
                Position = position,
                Unit = source.Unit,
                CalcRule = calcRule,
                Factor = factor,
                CategoryScope = source.CategoryScope,
                SpecKey = source.SpecKey,
                Application = source.Application,
                WasteFactor = source.WasteFactor,
                Adjustment = source.Adjustment,
                IsManualOnly = source.IsManualOnly,
                Note = note
            };
        }

        private static int CompareAccessories(TenderAccessory left, TenderAccessory right)
        {
            int result = TenderAccessoryRules.CompareApplications(left.Application, right.Application);
            if (result != 0) return result;

            result = TenderAccessoryRules.CompareScopes(left.CategoryScope, right.CategoryScope);
            if (result != 0) return result;

            result = string.Compare(
                TenderAccessoryRules.NormalizeScope(left.SpecKey),
                TenderAccessoryRules.NormalizeScope(right.SpecKey),
                System.StringComparison.OrdinalIgnoreCase);
            if (result != 0) return result;

            result = string.Compare(left.Name, right.Name, System.StringComparison.OrdinalIgnoreCase);
            if (result != 0) return result;

            result = string.Compare(left.Material, right.Material, System.StringComparison.OrdinalIgnoreCase);
            if (result != 0) return result;

            return string.Compare(left.Position, right.Position, System.StringComparison.OrdinalIgnoreCase);
        }

        // ═══════════════════════════════════════════════════
        // EXTERIOR DEFAULTS (Ngoài nhà)
        // ═══════════════════════════════════════════════════
        private static List<TenderAccessory> GetExteriorDefaults() => new()
        {
            new() { Name = "Diềm 01", Material = "Tole", Position = PosCanhTren,  Unit = "md", CalcRule = AccessoryCalcRule.PER_TOP_EDGE_LENGTH,           Factor = 1.0, CategoryScope = DefaultCategoryScope, Application = AppExterior, Note = "Diềm xử lý đỉnh vách, tiếp giáp mái/trần" },
            new() { Name = "Diềm 02", Material = "Tole", Position = PosCanhDuoi,  Unit = "md", CalcRule = AccessoryCalcRule.PER_BOTTOM_EDGE_LENGTH,        Factor = 1.0, CategoryScope = DefaultCategoryScope, Application = AppExterior, Note = "Mép dưới vách, tiếp giáp nền/sàn" },
            new() { Name = "Diềm 03", Material = "Tole", Position = PosDauCuoi,   Unit = "md", CalcRule = AccessoryCalcRule.PER_EXPOSED_END_LENGTH,        Factor = 1.0, CategoryScope = DefaultCategoryScope, Application = AppExterior, Note = "Diềm kết thúc mép đứng đầu/cuối vách (che lõi xốp)" },
            new() { Name = "Diềm 04", Material = "Tole", Position = PosGocNgoai,  Unit = "md", CalcRule = AccessoryCalcRule.PER_OUTSIDE_CORNER_HEIGHT,     Factor = 1.0, CategoryScope = DefaultCategoryScope, Application = AppExterior, Note = "Góc giao 2 vách nhô ra ngoài (90°)" },
            new() { Name = "Diềm 05", Material = "Tole", Position = PosMoiNoi,    Unit = "md", CalcRule = AccessoryCalcRule.PER_JOINT_LENGTH,              Factor = 1.0, CategoryScope = DefaultCategoryScope, Application = AppExterior, Note = "Khe nối T/G giữa 2 tấm panel kề nhau" },
            new() { Name = "Diềm 06", Material = "Tole", Position = PosCuaDi,     Unit = "md", CalcRule = AccessoryCalcRule.PER_DOOR_OPENING_PERIMETER,    Factor = 1.0, CategoryScope = DefaultCategoryScope, Application = AppExterior, Note = "Bao quanh 3 mép lỗ cửa đi (trên + 2 đứng, bỏ nưỡng)" },
            new() { Name = "Diềm 07", Material = "Tole", Position = PosCuaSoLKT,  Unit = "md", CalcRule = AccessoryCalcRule.PER_NON_DOOR_OPENING_PERIMETER, Factor = 1.0, CategoryScope = DefaultCategoryScope, Application = AppExterior, Note = "Bao quanh 4 mép cửa sổ/LKT (trên + 2 đứng + sill)" },
            new() { Name = "Bộ cửa đi",           Position = PosCuaDi,     Unit = "bộ", CalcRule = AccessoryCalcRule.PER_DOOR_OPENING_QTY,            Factor = 1.0, CategoryScope = DefaultCategoryScope, Application = AppExterior, Note = "Tính sơ bộ theo số lượng cửa đi" },
            new() { Name = ExteriorTekScrewBaseName, Position = PosPanelThep, Unit = "cái", CalcRule = AccessoryCalcRule.PER_TEK_SCREW_QTY, Factor = 1.0, CategoryScope = DefaultCategoryScope, Application = AppExterior, Note = "Nhịp xà gồ 1500mm × 2 vít/điểm × số tấm" },
            new() { Name = SealantMS617, Position = PosMoiNoi,     Unit = "md", CalcRule = AccessoryCalcRule.PER_JOINT_LENGTH,                   Factor = 1.0, CategoryScope = DefaultCategoryScope, Application = AppExterior, Note = "Bẹ khe nối mặt ngoài giữa 2 tấm panel" },
            new() { Name = SealantMS617, Position = PosCanhDungLM, Unit = "md", CalcRule = AccessoryCalcRule.PER_OPENING_VERTICAL_EDGES,         Factor = 1.0, CategoryScope = DefaultCategoryScope, Application = AppExterior, Note = "Bẹ 2 cạnh đứng viền lỗ mở" },
            new() { Name = SealantMS617, Position = PosCanhDauLM,  Unit = "md", CalcRule = AccessoryCalcRule.PER_OPENING_HORIZONTAL_TOP_LENGTH,  Factor = 1.0, CategoryScope = DefaultCategoryScope, Application = AppExterior, Note = "Bẹ cạnh ngang trên đầu lỗ mở" },
            new() { Name = SealantMS617, Position = PosCanhSillLM, Unit = "md", CalcRule = AccessoryCalcRule.PER_OPENING_SILL_LENGTH,             Factor = 1.0, CategoryScope = DefaultCategoryScope, Application = AppExterior, Note = "Bẹ ngưỡng dưới cửa sổ/LKT (không tính cửa đi)" },
            new() { Name = SealantMS617, Position = PosDinhVach,   Unit = "md", CalcRule = AccessoryCalcRule.PER_TOP_EDGE_LENGTH,                Factor = 1.0, CategoryScope = DefaultCategoryScope, Application = AppExterior, Note = "Sealant hoàn thiện đỉnh vách ngoài nhà" },
            new() { Name = SealantMS617, Position = PosChanVach,   Unit = "md", CalcRule = AccessoryCalcRule.PER_BOTTOM_EDGE_LENGTH,             Factor = 1.0, CategoryScope = DefaultCategoryScope, Application = AppExterior, Note = "Hoàn thiện chân vách biên tự do" },
            new() { Name = SealantMS617, Position = PosCanhHo,     Unit = "md", CalcRule = AccessoryCalcRule.PER_EXPOSED_END_LENGTH,             Factor = 1.0, CategoryScope = DefaultCategoryScope, Application = AppExterior, Note = "Hoàn thiện mép đứng tự do đầu/cuối vách" },
            new() { Name = "Băng keo butyl",  Position = PosMoiNoi, Unit = "md",  CalcRule = AccessoryCalcRule.PER_JOINT_LENGTH,  Factor = 1.0, CategoryScope = DefaultCategoryScope, Application = AppExterior, Note = "Băng butyl dán trong ngàm T/G trước khi lắp tấm" },
            // ─── Nhóm khe nối đứng (chỉ xếp Ngang, tự động = 0 khi xếp Dọc) ───
            new() { Name = "Nẹp Omega",  Material = "Nhôm", Position = PosKheDung, Unit = "md",   CalcRule = AccessoryCalcRule.PER_VERTICAL_JOINT_HEIGHT, Factor = 1.0,  CategoryScope = DefaultCategoryScope, Application = AppExterior, Note = "Nẹp nhôm che khe đứng Omega (chỉ xếp Ngang)" },
            new() { Name = "Băng keo butyl Omega",               Position = PosKheDung, Unit = "md",   CalcRule = AccessoryCalcRule.PER_VERTICAL_JOINT_HEIGHT, Factor = 2.0,  CategoryScope = DefaultCategoryScope, Application = AppExterior, Note = "Dán 2 mép nẹp Omega trước khi ép vào panel" },
            new() { Name = "Foam PU bơm tại chỗ",                   Position = PosKheDung, Unit = "chai", CalcRule = AccessoryCalcRule.PER_VERTICAL_JOINT_HEIGHT, Factor = 0.33, CategoryScope = DefaultCategoryScope, Application = AppExterior, Note = "Bơm kín tuyến khe đứng Omega, quy đổi theo md khe" },
            new() { Name = "Gioăng xốp làm kín",               Position = PosKheDung, Unit = "md",   CalcRule = AccessoryCalcRule.PER_VERTICAL_JOINT_HEIGHT, Factor = 2.0,  CategoryScope = DefaultCategoryScope, Application = AppExterior, Note = "Dán 2 mép làm kín nước và gió tại khe Omega" },
            new() { Name = "B2S-TEK 15-15×20 HWFS", Position = PosCanhTren, Unit = "cái", CalcRule = AccessoryCalcRule.PER_TOP_EDGE_LENGTH, Factor = 6.0, CategoryScope = DefaultCategoryScope, Application = AppExterior, Note = "Vít tự khoan bắt diềm đỉnh vách, quy đổi ~6 con/md" },
            new() { Name = "B2S-TEK 15-15×20 HWFS", Position = PosCanhDuoi, Unit = "cái", CalcRule = AccessoryCalcRule.PER_BOTTOM_EDGE_LENGTH, Factor = 6.0, CategoryScope = DefaultCategoryScope, Application = AppExterior, Note = "Vít tự khoan bắt diềm chân vách, quy đổi ~6 con/md" },
            new() { Name = "B2S-TEK 15-15×20 HWFS", Position = PosDauCuoi, Unit = "cái", CalcRule = AccessoryCalcRule.PER_EXPOSED_END_LENGTH, Factor = 6.0, CategoryScope = DefaultCategoryScope, Application = AppExterior, Note = "Vít tự khoan bắt diềm đầu/cuối, quy đổi ~6 con/md" },
            new() { Name = "B2S-TEK 15-15×20 HWFS", Position = PosGocNgoai, Unit = "cái", CalcRule = AccessoryCalcRule.PER_OUTSIDE_CORNER_HEIGHT, Factor = 6.0, CategoryScope = DefaultCategoryScope, Application = AppExterior, Note = "Vít tự khoan bắt diềm góc ngoài, quy đổi ~6 con/md" },
            new() { Name = "B2S-TEK 15-15×20 HWFS", Position = PosMoiNoi, Unit = "cái", CalcRule = AccessoryCalcRule.PER_JOINT_LENGTH, Factor = 6.0, CategoryScope = DefaultCategoryScope, Application = AppExterior, Note = "Vít tự khoan bắt diềm/nẹp theo khe nối T/G, quy đổi ~6 con/md" },
            new() { Name = "B2S-TEK 15-15×20 HWFS", Position = PosKheDung, Unit = "cái", CalcRule = AccessoryCalcRule.PER_VERTICAL_JOINT_HEIGHT, Factor = 6.0, CategoryScope = DefaultCategoryScope, Application = AppExterior, Note = "Vít tự khoan bắt nẹp Omega theo khe đứng, quy đổi ~6 con/md" },
            new() { Name = "B2S-TEK 15-15×20 HWFS", Position = PosCuaDi, Unit = "cái", CalcRule = AccessoryCalcRule.PER_DOOR_OPENING_PERIMETER, Factor = 6.0, CategoryScope = DefaultCategoryScope, Application = AppExterior, Note = "Vít tự khoan bắt diềm viền cửa đi, quy đổi ~6 con/md" },
            new() { Name = "B2S-TEK 15-15×20 HWFS", Position = PosCuaSoLKT, Unit = "cái", CalcRule = AccessoryCalcRule.PER_NON_DOOR_OPENING_PERIMETER, Factor = 6.0, CategoryScope = DefaultCategoryScope, Application = AppExterior, Note = "Vít tự khoan bắt diềm viền cửa sổ/lỗ kỹ thuật, quy đổi ~6 con/md" },
            new() { Name = "Bát thép chân panel", Material = "Thép", Position = PosChanVach, Unit = "cái", CalcRule = AccessoryCalcRule.PER_PANEL_SUPPORT_BRACKET_QTY, Factor = 1.0, CategoryScope = DefaultCategoryScope, Application = AppExterior, Note = "Chiều dài vách / nhịp xà gồ 1500mm, làm tròn lên" },
            new() { Name = "B2S-TEK 15-15×20 HWFS", Position = PosChanVach, Unit = "cái", CalcRule = AccessoryCalcRule.PER_PANEL_SUPPORT_BRACKET_QTY, Factor = 2.0, CategoryScope = DefaultCategoryScope, Application = AppExterior, Note = "2 vít / 1 bát thép chân panel" }
        };

        // ═══════════════════════════════════════════════════
        // CLEANROOM DEFAULTS (Phòng sạch)
        // ═══════════════════════════════════════════════════
        private static List<TenderAccessory> GetCleanroomDefaults() => new()
        {
            new() { Name = SiliconeSN505, Position = PosMoiNoi, Unit = "chai", CalcRule = AccessoryCalcRule.PER_JOINT_LENGTH, Factor = SiliconeSn505JointBottleFactor, CategoryScope = DefaultCategoryScope, Application = AppCleanroom, Note = "Bơm kín mối nối phòng sạch, quy đổi 15 md/chai" },
            new() { Name = VBoR40Name, Material = "Nhôm", Position = PosCanhDuoi, Unit = "md", CalcRule = AccessoryCalcRule.PER_BOTTOM_EDGE_LENGTH, Factor = 2.0, CategoryScope = DefaultCategoryScope, Application = AppCleanroom, Note = "Chân vách phòng sạch: 2 tuyến V bo R40 hai bên vách" },
            new() { Name = CleanroomBottomUChannelBaseName, Material = "Nhôm / Thép mạ kẽm", Position = PosCanhDuoi, Unit = "md", CalcRule = AccessoryCalcRule.PER_BOTTOM_EDGE_LENGTH, Factor = 1.0, CategoryScope = DefaultCategoryScope, Application = AppCleanroom, Note = "Chân vách phòng sạch: 1 tuyến U chân vách, bụng U = chiều dày panel; panel 50/75 dùng nhôm, còn lại dùng thép mạ kẽm" },
            new() { Name = "Vít No.5 + tắc kê nhựa", Material = "Thép mạ kẽm + Nhựa", Position = PosCanhDuoi, Unit = "cái", CalcRule = AccessoryCalcRule.PER_BOTTOM_EDGE_LENGTH, Factor = RiveAt500Factor, CategoryScope = DefaultCategoryScope, Application = AppCleanroom, Note = "Cố định U chân vách phòng sạch xuống sàn: vít + tắc kê nhựa @500" },
            new() { Name = VBoR40Name, Material = "Nhôm", Position = PosCanhTren, Unit = "md", CalcRule = AccessoryCalcRule.PER_TOP_PANEL_CEILING_CENTER_LENGTH, Factor = 2.0, CategoryScope = DefaultCategoryScope, Application = AppCleanroom, Note = "Đỉnh vách phòng sạch giao trần giữa: 2 tuyến V bo R40 hai bên vách" },
            new() { Name = VBoR40Name, Material = "Nhôm", Position = PosCanhTren, Unit = "md", CalcRule = AccessoryCalcRule.PER_TOP_PANEL_CEILING_PERIMETER_LENGTH, Factor = 1.0, CategoryScope = DefaultCategoryScope, Application = AppCleanroom, Note = "Đỉnh vách phòng sạch giao biên trần: 1 tuyến V bo R40 phía trong" },
            new() { Name = CleanroomPerimeterAngleName, Material = "Nhôm", Position = PosCanhTren, Unit = "md", CalcRule = AccessoryCalcRule.PER_TOP_PANEL_CEILING_PERIMETER_LENGTH, Factor = 1.0, CategoryScope = DefaultCategoryScope, Application = AppCleanroom, Note = "Đỉnh vách phòng sạch giao biên trần: 1 tuyến V 40x80 nhôm sơn tĩnh điện phía ngoài" },
            new() { Name = VBoR40Name, Material = "Nhôm", Position = PosDauCuoi, Unit = "md", CalcRule = AccessoryCalcRule.PER_END_PANEL_CENTER_LENGTH, Factor = 2.0, CategoryScope = DefaultCategoryScope, Application = AppCleanroom, Note = "Đầu/cuối vách phòng sạch giao giữa: 2 tuyến V bo R40 hai bên vách" },
            new() { Name = VBoR40Name, Material = "Nhôm", Position = PosDauCuoi, Unit = "md", CalcRule = AccessoryCalcRule.PER_END_PANEL_PERIMETER_LENGTH, Factor = 1.0, CategoryScope = DefaultCategoryScope, Application = AppCleanroom, Note = "Đầu/cuối vách phòng sạch giao biên: 1 tuyến V bo R40 phía trong" },
            new() { Name = CleanroomPerimeterAngleName, Material = "Nhôm", Position = PosDauCuoi, Unit = "md", CalcRule = AccessoryCalcRule.PER_END_PANEL_PERIMETER_LENGTH, Factor = 1.0, CategoryScope = DefaultCategoryScope, Application = AppCleanroom, Note = "Đầu/cuối vách phòng sạch giao biên: 1 tuyến V 40x80 nhôm sơn tĩnh điện phía ngoài" },
            new() { Name = CleanroomBottomUChannelBaseName, Material = "Nhôm / Thép mạ kẽm", Position = PosLoMo, Unit = "md", CalcRule = AccessoryCalcRule.PER_OPENING_PERIMETER, Factor = 1.0, CategoryScope = DefaultCategoryScope, Application = AppCleanroom, Note = "Lỗ mở phòng sạch: U 4 cạnh cùng loại chân vách; panel 50/75 dùng nhôm, còn lại dùng thép mạ kẽm" },
            new() { Name = "Bộ cửa đi phòng sạch", Position = PosCuaDi, Unit = "bộ", CalcRule = AccessoryCalcRule.PER_DOOR_OPENING_QTY, Factor = 1.0, CategoryScope = DefaultCategoryScope, Application = AppCleanroom, Note = "Sơ bộ theo số lượng cửa đi phòng sạch" },
            new() { Name = SiliconeSN505, Position = PosLoMo, Unit = "chai", CalcRule = AccessoryCalcRule.PER_OPENING_PERIMETER, Factor = SiliconeSn505JointBottleFactor * 2.0, CategoryScope = DefaultCategoryScope, Application = AppCleanroom, Note = "Lỗ mở phòng sạch: 2 tuyến silicone hoàn thiện theo 4 cạnh U nhôm, quy đổi 15 md/chai" },
            new() { Name = SiliconeSN505, Position = PosDinhVach, Unit = "chai", CalcRule = AccessoryCalcRule.PER_TOP_PANEL_CEILING_CENTER_LENGTH, Factor = SiliconeSn505JointBottleFactor * 2.0, CategoryScope = DefaultCategoryScope, Application = AppCleanroom, Note = "Đỉnh vách phòng sạch giao trần giữa: 2 tuyến silicone hoàn thiện theo 2 V bo R40, quy đổi 15 md/chai" },
            new() { Name = SiliconeSN505, Position = PosDinhVach, Unit = "chai", CalcRule = AccessoryCalcRule.PER_TOP_PANEL_CEILING_PERIMETER_LENGTH, Factor = SiliconeSn505JointBottleFactor * 2.0, CategoryScope = DefaultCategoryScope, Application = AppCleanroom, Note = "Đỉnh vách phòng sạch giao biên trần: 2 tuyến silicone hoàn thiện, quy đổi 15 md/chai" },
            new() { Name = SiliconeSN505, Position = PosChanVach, Unit = "chai", CalcRule = AccessoryCalcRule.PER_BOTTOM_EDGE_LENGTH, Factor = SiliconeSn505JointBottleFactor * 2.0, CategoryScope = DefaultCategoryScope, Application = AppCleanroom, Note = "Chân vách phòng sạch: 2 tuyến silicone hoàn thiện theo 2 V bo R40, quy đổi 15 md/chai" },
            new() { Name = SiliconeSN505, Position = PosCanhHo, Unit = "chai", CalcRule = AccessoryCalcRule.PER_END_PANEL_CENTER_LENGTH, Factor = SiliconeSn505JointBottleFactor * 2.0, CategoryScope = DefaultCategoryScope, Application = AppCleanroom, Note = "Đầu/cuối vách phòng sạch giao giữa: 2 tuyến silicone hoàn thiện theo 2 V bo R40, quy đổi 15 md/chai" },
            new() { Name = SiliconeSN505, Position = PosCanhHo, Unit = "chai", CalcRule = AccessoryCalcRule.PER_END_PANEL_PERIMETER_LENGTH, Factor = SiliconeSn505JointBottleFactor * 2.0, CategoryScope = DefaultCategoryScope, Application = AppCleanroom, Note = "Đầu/cuối vách phòng sạch giao biên: 2 tuyến silicone hoàn thiện, quy đổi 15 md/chai" },
            new() { Name = "Rive Ø4.2×12", Material = "Nhôm", Position = PosVach, Unit = "cái", CalcRule = AccessoryCalcRule.PER_WALL_LENGTH, Factor = 6.0, CategoryScope = DefaultCategoryScope, Application = AppCleanroom, Note = "Rive nhôm line-based theo từng tuyến phụ kiện phòng sạch" }
        };

        // ═══════════════════════════════════════════════════
        // COLD STORAGE DEFAULTS (Kho lạnh)
        // ═══════════════════════════════════════════════════
        private static List<TenderAccessory> GetCleanroomCeilingDefaults() => new()
        {
            new() { Name = "Thanh T 60x80", Material = "Nhôm", Position = PosTreoTNhôm, Unit = "cây", CalcRule = AccessoryCalcRule.PER_COLD_STORAGE_T_SUSPENSION_LENGTH, Factor = 1.0 / 6.0, CategoryScope = "Trần", Application = AppCleanroom, Note = "DT-06 tổng chiều dài tuyến treo thanh T 60x80, quy đổi 1 cây = 6m" },
            new() { Name = BoltSetM10Name, Material = "Thép mạ kẽm", Position = PosTreoTNhôm, Unit = "bộ", CalcRule = AccessoryCalcRule.PER_COLD_STORAGE_T_SUSPENSION_POINT_QTY, Factor = 1.0, CategoryScope = "Trần", Application = AppCleanroom, Note = "1 bộ cho mỗi điểm treo thanh T" },
            new() { Name = "Tăng đơ M10", Material = "Thép mạ kẽm", Position = PosTreoTNhôm, Unit = "cái", CalcRule = AccessoryCalcRule.PER_COLD_STORAGE_T_SUSPENSION_POINT_QTY, Factor = 1.0, CategoryScope = "Trần", Application = AppCleanroom, Note = "1 tăng đơ cho mỗi điểm treo thanh T" },
            new() { Name = "Cáp treo Ø10", Material = "Thép bọc nhựa", Position = PosTreoTNhôm, Unit = "md", CalcRule = AccessoryCalcRule.PER_COLD_STORAGE_T_WIRE_ROPE_LENGTH, Factor = 1.0, CategoryScope = "Trần", Application = AppCleanroom, Note = "DT-06 tổng chiều dài cáp treo thanh T, nhân theo Thả cáp (mm)" },
            new() { Name = "Cùm xiết cáp Ø10", Material = "Thép mạ kẽm", Position = PosTreoTNhôm, Unit = "cái", CalcRule = AccessoryCalcRule.PER_COLD_STORAGE_T_SUSPENSION_POINT_QTY, Factor = 3.0, CategoryScope = "Trần", Application = AppCleanroom, Note = "3 cùm xiết cáp cho mỗi điểm treo thanh T" },
            new() { Name = "Thanh C 100x50", Material = "Thép mạ kẽm", Position = PosTreoBulongNam, Unit = "cây", CalcRule = AccessoryCalcRule.PER_COLD_STORAGE_MUSHROOM_SUSPENSION_LENGTH, Factor = 1.0 / 6.0, CategoryScope = "Trần", Application = AppCleanroom, Note = "DT-08 Type 2 tổng chiều dài tuyến treo bulong nấm/thanh C, quy đổi 1 cây = 6m" },
            new() { Name = "Ty ren M10", Material = "Thép", Position = PosTreoBulongNam, Unit = "cái", CalcRule = AccessoryCalcRule.PER_COLD_STORAGE_MUSHROOM_SUSPENSION_POINT_QTY, Factor = 1.0, CategoryScope = "Trần", Application = AppCleanroom, Note = "1 ty ren cho mỗi điểm treo bulong nấm" },
            new() { Name = "Tăng đơ M10", Material = "Thép mạ kẽm", Position = PosTreoBulongNam, Unit = "cái", CalcRule = AccessoryCalcRule.PER_COLD_STORAGE_MUSHROOM_SUSPENSION_POINT_QTY, Factor = 1.0, CategoryScope = "Trần", Application = AppCleanroom, Note = "1 tăng đơ cho mỗi điểm treo bulong nấm" },
            new() { Name = "Cáp treo Ø10", Material = "Thép bọc nhựa", Position = PosTreoBulongNam, Unit = "md", CalcRule = AccessoryCalcRule.PER_COLD_STORAGE_MUSHROOM_WIRE_ROPE_LENGTH, Factor = 1.0, CategoryScope = "Trần", Application = AppCleanroom, Note = "DT-08 Type 2 tổng chiều dài cáp treo bulong nấm, nhân theo Thả cáp (mm)" },
            new() { Name = "Cùm xiết cáp Ø10", Material = "Thép mạ kẽm", Position = PosTreoBulongNam, Unit = "cái", CalcRule = AccessoryCalcRule.PER_COLD_STORAGE_MUSHROOM_SUSPENSION_POINT_QTY, Factor = 3.0, CategoryScope = "Trần", Application = AppCleanroom, Note = "3 cùm xiết cáp cho mỗi điểm treo bulong nấm" },
            new() { Name = CleanroomMushroomBoltName, Material = "Nhựa", Position = PosTreoBulongNam, Unit = "cái", CalcRule = AccessoryCalcRule.PER_COLD_STORAGE_MUSHROOM_BOLT_QTY, Factor = 1.0, CategoryScope = "Trần", Application = AppCleanroom, Note = "1 bulong nấm cho mỗi điểm liên kết panel với thanh C" }
        };

        private static List<TenderAccessory> GetColdStorageDefaults() => new()
        {
            new() { Name = VBoR40Name, Material = "Nhôm", Position = PosCanhTren, Unit = "md", CalcRule = AccessoryCalcRule.PER_TOP_PANEL_CEILING_CENTER_LENGTH, Factor = 2.0, CategoryScope = DefaultCategoryScope, Application = AppColdStorage, Note = "Đỉnh vách giao trần giữa kho lạnh: 2 tuyến V bo R40 hai bên vách, áp dụng panel 100/125/150/180/200 khi T<15°C" },
            new() { Name = "Foam PU bơm tại chỗ", Position = PosCanhTren, Unit = "chai", CalcRule = AccessoryCalcRule.PER_TOP_PANEL_CEILING_CENTER_LENGTH, Factor = 0.33, CategoryScope = DefaultCategoryScope, Application = AppColdStorage, Note = "Đỉnh vách giao trần giữa kho lạnh: 1 tuyến foam bơm tại chỗ, quy đổi 3 md/chai, áp dụng panel 100/125/150/180/200 khi T<15°C" },
            new() { Name = VBoR40Name, Material = "Nhôm", Position = PosCanhTren, Unit = "md", CalcRule = AccessoryCalcRule.PER_TOP_PANEL_CEILING_PERIMETER_LENGTH, Factor = 1.0, CategoryScope = DefaultCategoryScope, Application = AppColdStorage, Note = "Đỉnh vách giao biên trần kho lạnh: 1 tuyến V bo R40 phía trong" },
            new() { Name = "V 80x80", Material = "Nhôm", Position = PosCanhTren, Unit = "md", CalcRule = AccessoryCalcRule.PER_TOP_PANEL_CEILING_PERIMETER_LENGTH, Factor = 1.0, CategoryScope = DefaultCategoryScope, Application = AppColdStorage, Note = "Đỉnh vách giao biên trần kho lạnh: 1 tuyến V 80x80 anodized phía ngoài" },
            new() { Name = "Foam PU bơm tại chỗ", Position = PosCanhTren, Unit = "chai", CalcRule = AccessoryCalcRule.PER_TOP_PANEL_CEILING_PERIMETER_LENGTH, Factor = 0.33, CategoryScope = DefaultCategoryScope, Application = AppColdStorage, Note = "Đỉnh vách giao biên trần kho lạnh: 1 tuyến foam bơm tại chỗ, quy đổi 3 md/chai" },
            new() { Name = SheetTrim01, Material = "Tole", Position = PosCanhTren, Unit = "md", CalcRule = AccessoryCalcRule.PER_TOP_PANEL_FREE_LENGTH, Factor = 1.0, CategoryScope = DefaultCategoryScope, Application = AppColdStorage, Note = "Đỉnh vách mép tự do kho lạnh: 1 tuyến diềm tôn" },
            new() { Name = "V bo R40", Material = "Nhôm", Position = PosCanhDuoi, Unit = "md", CalcRule = AccessoryCalcRule.PER_BOTTOM_EDGE_LENGTH, Factor = 2.0, CategoryScope = DefaultCategoryScope, Application = AppColdStorage, Note = "Chân vách kho lạnh trên bệ chân (curb): 2 tuyến V bo R40 hai bên panel" },
            new() { Name = ColdStorageBottomUChannelBaseName, Material = "Nhôm / Thép mạ kẽm", Position = PosCanhDuoi, Unit = "md", CalcRule = AccessoryCalcRule.PER_BOTTOM_EDGE_LENGTH, Factor = 1.0, CategoryScope = DefaultCategoryScope, Application = AppColdStorage, Note = "Chân vách kho lạnh trên bệ chân (curb): 1 tuyến U chân vách, bụng U = chiều dày panel + 3mm; panel 50/75 dùng nhôm, còn lại dùng thép mạ kẽm" },
            new() { Name = "Vít No.5 + tắc kê nhựa", Material = "Thép mạ kẽm + Nhựa", Position = PosCanhDuoi, Unit = "cái", CalcRule = AccessoryCalcRule.PER_BOTTOM_EDGE_LENGTH, Factor = RiveAt500Factor, CategoryScope = DefaultCategoryScope, Application = AppColdStorage, Note = "Cố định U chân vách kho lạnh xuống bệ chân (curb): vít + tắc kê nhựa @500" },
            new() { Name = "Foam PU bơm tại chỗ", Position = PosCanhDuoi, Unit = "chai", CalcRule = AccessoryCalcRule.PER_BOTTOM_EDGE_LENGTH, Factor = 0.33, CategoryScope = DefaultCategoryScope, Application = AppColdStorage, Note = "Chân vách kho lạnh trên bệ chân (curb): foam PU bơm tại chỗ, quy đổi 3 md/chai" },
            new() { Name = VBoR40Name, Material = "Nhôm", Position = PosDauCuoi, Unit = "md", CalcRule = AccessoryCalcRule.PER_END_PANEL_CENTER_LENGTH, Factor = 2.0, CategoryScope = DefaultCategoryScope, Application = AppColdStorage, Note = "Đầu/cuối vách giao giữa kho lạnh: 2 tuyến V bo R40 hai bên panel" },
            new() { Name = "Foam PU bơm tại chỗ", Position = PosDauCuoi, Unit = "chai", CalcRule = AccessoryCalcRule.PER_END_PANEL_CENTER_LENGTH, Factor = 0.33, CategoryScope = DefaultCategoryScope, Application = AppColdStorage, Note = "Đầu/cuối vách giao giữa kho lạnh: 1 tuyến foam bơm tại chỗ, quy đổi 3 md/chai" },
            new() { Name = VBoR40Name, Material = "Nhôm", Position = PosDauCuoi, Unit = "md", CalcRule = AccessoryCalcRule.PER_END_PANEL_PERIMETER_LENGTH, Factor = 1.0, CategoryScope = DefaultCategoryScope, Application = AppColdStorage, Note = "Đầu/cuối vách giao biên kho lạnh: 1 tuyến V bo R40 phía trong" },
            new() { Name = "V 80x80", Material = "Nhôm", Position = PosDauCuoi, Unit = "md", CalcRule = AccessoryCalcRule.PER_END_PANEL_PERIMETER_LENGTH, Factor = 1.0, CategoryScope = DefaultCategoryScope, Application = AppColdStorage, Note = "Đầu/cuối vách giao biên kho lạnh: 1 tuyến V 80x80 phía ngoài" },
            new() { Name = "Foam PU bơm tại chỗ", Position = PosDauCuoi, Unit = "chai", CalcRule = AccessoryCalcRule.PER_END_PANEL_PERIMETER_LENGTH, Factor = 0.33, CategoryScope = DefaultCategoryScope, Application = AppColdStorage, Note = "Đầu/cuối vách giao biên kho lạnh: 1 tuyến foam bơm tại chỗ, quy đổi 3 md/chai" },
            new() { Name = SheetTrim01, Material = "Tole", Position = PosMoiNoi, Unit = "md", CalcRule = AccessoryCalcRule.PER_JOINT_LENGTH, Factor = 1.0, CategoryScope = DefaultCategoryScope, Application = AppColdStorage, Note = "Diềm tôn che khe nối giữa 2 tấm panel" },
            new() { Name = SheetTrim01, Material = "Tole", Position = PosLoMo, Unit = "md", CalcRule = AccessoryCalcRule.PER_OPENING_PERIMETER_TWO_FACES, Factor = 1.0, CategoryScope = DefaultCategoryScope, Application = AppColdStorage, Note = "Diềm tôn viền lỗ mở, tính theo chu vi 2 mặt" },
            new() { Name = "Bộ cửa đi kho lạnh", Position = PosCuaDi, Unit = "bộ", CalcRule = AccessoryCalcRule.PER_DOOR_OPENING_QTY, Factor = 1.0, CategoryScope = DefaultCategoryScope, Application = AppColdStorage, Note = "Sơ bộ theo số lượng cửa đi kho lạnh" },
            new() { Name = SealantMC202, Position = PosMoiNoi, Unit = "chai", CalcRule = AccessoryCalcRule.PER_JOINT_LENGTH, Factor = SealantMc202BottleFactor, CategoryScope = DefaultCategoryScope, Application = AppColdStorage, Note = "Bơm sealant giữa ngàm panel, quy đổi 6 md/chai" },
            new() { Name = SiliconeSN505, Position = PosCanhDungLM, Unit = "chai", CalcRule = AccessoryCalcRule.PER_OPENING_VERTICAL_EDGES, Factor = SiliconeSn505JointBottleFactor, CategoryScope = DefaultCategoryScope, Application = AppColdStorage, Note = "Bơm kín cạnh đứng lỗ mở kho lạnh, quy đổi 15 md/chai" },
            new() { Name = SiliconeSN505, Position = PosCanhDauLM, Unit = "chai", CalcRule = AccessoryCalcRule.PER_OPENING_HORIZONTAL_TOP_LENGTH, Factor = SiliconeSn505JointBottleFactor, CategoryScope = DefaultCategoryScope, Application = AppColdStorage, Note = "Bơm kín cạnh đầu lỗ mở kho lạnh, quy đổi 15 md/chai" },
            new() { Name = SiliconeSN505, Position = PosCanhSillLM, Unit = "chai", CalcRule = AccessoryCalcRule.PER_OPENING_SILL_LENGTH, Factor = SiliconeSn505JointBottleFactor, CategoryScope = DefaultCategoryScope, Application = AppColdStorage, Note = "Bơm kín cạnh sill lỗ mở kho lạnh, quy đổi 15 md/chai" },
            new() { Name = SiliconeSN505, Position = PosDinhVach, Unit = "chai", CalcRule = AccessoryCalcRule.PER_TOP_PANEL_CEILING_CENTER_LENGTH, Factor = SiliconeSn505JointBottleFactor * 2.0, CategoryScope = DefaultCategoryScope, Application = AppColdStorage, Note = "Đỉnh vách giao trần giữa kho lạnh: 2 tuyến silicone, quy đổi 15 md/chai, áp dụng panel 100/125/150/180/200 khi T<15°C" },
            new() { Name = SiliconeSN505, Position = PosDinhVach, Unit = "chai", CalcRule = AccessoryCalcRule.PER_TOP_PANEL_CEILING_PERIMETER_LENGTH, Factor = SiliconeSn505JointBottleFactor * 2.0, CategoryScope = DefaultCategoryScope, Application = AppColdStorage, Note = "Đỉnh vách giao biên trần kho lạnh: 2 tuyến silicone hoàn thiện, quy đổi 15 md/chai" },
            new() { Name = SiliconeSN505, Position = PosDinhVach, Unit = "chai", CalcRule = AccessoryCalcRule.PER_TOP_PANEL_FREE_LENGTH, Factor = SiliconeSn505JointBottleFactor, CategoryScope = DefaultCategoryScope, Application = AppColdStorage, Note = "Đỉnh vách mép tự do kho lạnh: 1 tuyến silicone hoàn thiện, quy đổi 15 md/chai" },
            new() { Name = SiliconeSN505, Position = PosChanVach, Unit = "chai", CalcRule = AccessoryCalcRule.PER_BOTTOM_EDGE_LENGTH, Factor = SiliconeSn505JointBottleFactor * 2.0, CategoryScope = DefaultCategoryScope, Application = AppColdStorage, Note = "Chân vách kho lạnh trên bệ chân (curb): bắn theo 2 tuyến V bo R40, quy đổi 15 md/chai" },
            new() { Name = SiliconeSN505, Position = PosCanhHo, Unit = "chai", CalcRule = AccessoryCalcRule.PER_END_PANEL_CENTER_LENGTH, Factor = SiliconeSn505JointBottleFactor * 2.0, CategoryScope = DefaultCategoryScope, Application = AppColdStorage, Note = "Đầu/cuối vách giao giữa kho lạnh: 2 tuyến silicone hoàn thiện, quy đổi 15 md/chai" },
            new() { Name = SiliconeSN505, Position = PosCanhHo, Unit = "chai", CalcRule = AccessoryCalcRule.PER_END_PANEL_PERIMETER_LENGTH, Factor = SiliconeSn505JointBottleFactor * 2.0, CategoryScope = DefaultCategoryScope, Application = AppColdStorage, Note = "Đầu/cuối vách giao biên kho lạnh: 2 tuyến silicone hoàn thiện, quy đổi 15 md/chai" },
            new() { Name = "Rive Ø4.2×12", Material = "Nhôm", Position = PosVach, Unit = "cái", CalcRule = AccessoryCalcRule.PER_WALL_LENGTH, Factor = 6.0, CategoryScope = DefaultCategoryScope, Application = AppColdStorage, Note = "Rive nhôm cố định phụ kiện, quy đổi sơ bộ theo chiều dài vách" },
            new() { Name = "Thanh T 68x75", Material = "Nhôm", Position = PosTreoTNhôm, Unit = "cây", CalcRule = AccessoryCalcRule.PER_COLD_STORAGE_T_SUSPENSION_LENGTH, Factor = 1.0 / 6.0, CategoryScope = "Trần", Application = AppColdStorage, Note = "Tổng chiều dài tuyến treo thanh T, quy đổi 1 cây = 6m" },
            new() { Name = "Bản mã 50x325x3", Material = "Thép mạ kẽm", Position = PosTreoTNhôm, Unit = "cái", CalcRule = AccessoryCalcRule.PER_COLD_STORAGE_T_SUSPENSION_POINT_QTY, Factor = 1.0, CategoryScope = "Trần", Application = AppColdStorage, Note = "1 bản mã cho mỗi điểm treo thanh T" },
            new() { Name = "Tăng đơ M12", Material = "Thép mạ kẽm", Position = PosTreoTNhôm, Unit = "cái", CalcRule = AccessoryCalcRule.PER_COLD_STORAGE_T_SUSPENSION_POINT_QTY, Factor = 1.0, CategoryScope = "Trần", Application = AppColdStorage, Note = "1 tăng đơ cho mỗi điểm treo thanh T" },
            new() { Name = "Cáp treo Ø12", Material = "Thép mạ kẽm", Position = PosTreoTNhôm, Unit = "md", CalcRule = AccessoryCalcRule.PER_COLD_STORAGE_T_WIRE_ROPE_LENGTH, Factor = 1.0, CategoryScope = "Trần", Application = AppColdStorage, Note = "Tổng chiều dài cáp treo thanh T, nhân theo Thả cáp (mm)" },
            new() { Name = "Cùm xiết cáp Ø12", Material = "Thép mạ kẽm", Position = PosTreoTNhôm, Unit = "cái", CalcRule = AccessoryCalcRule.PER_COLD_STORAGE_T_SUSPENSION_POINT_QTY, Factor = 3.0, CategoryScope = "Trần", Application = AppColdStorage, Note = "3 cùm xiết cáp cho mỗi điểm treo thanh T" },
            new() { Name = "Ống Ø114", Material = "PVC", Position = PosTreoTNhôm, Unit = "cây", CalcRule = AccessoryCalcRule.PER_COLD_STORAGE_T_SUSPENSION_POINT_QTY, Factor = 0.125, CategoryScope = "Trần", Application = AppColdStorage, Note = "Ống PVC chụp điểm treo T, quy đổi 500mm/điểm, 1 cây = 4m theo DT-15" },
            new() { Name = "Thanh C 100x50", Material = "Thép mạ kẽm", Position = PosTreoBulongNam, Unit = "cây", CalcRule = AccessoryCalcRule.PER_COLD_STORAGE_MUSHROOM_SUSPENSION_LENGTH, Factor = 1.0 / 6.0, CategoryScope = "Trần", Application = AppColdStorage, Note = "Tổng chiều dài tuyến treo bulong nấm/thanh C, quy đổi 1 cây = 6m" },
            new() { Name = "Ty ren M12", Material = "Thép mạ kẽm", Position = PosTreoBulongNam, Unit = "cây", CalcRule = AccessoryCalcRule.PER_COLD_STORAGE_MUSHROOM_SUSPENSION_POINT_QTY, Factor = 0.35 / 3.0, CategoryScope = "Trần", Application = AppColdStorage, Note = "Ty ren M12 quy đổi 350mm/điểm, 1 cây = 3m" },
            new() { Name = "Tăng đơ M12", Material = "Thép mạ kẽm", Position = PosTreoBulongNam, Unit = "cái", CalcRule = AccessoryCalcRule.PER_COLD_STORAGE_MUSHROOM_SUSPENSION_POINT_QTY, Factor = 1.0, CategoryScope = "Trần", Application = AppColdStorage, Note = "1 tăng đơ cho mỗi điểm treo bulong nấm" },
            new() { Name = "Cáp treo Ø12", Material = "Thép mạ kẽm", Position = PosTreoBulongNam, Unit = "md", CalcRule = AccessoryCalcRule.PER_COLD_STORAGE_MUSHROOM_WIRE_ROPE_LENGTH, Factor = 1.0, CategoryScope = "Trần", Application = AppColdStorage, Note = "Tổng chiều dài cáp treo bulong nấm, nhân theo Thả cáp (mm)" },
            new() { Name = "Cùm xiết cáp Ø12", Material = "Thép mạ kẽm", Position = PosTreoBulongNam, Unit = "cái", CalcRule = AccessoryCalcRule.PER_COLD_STORAGE_MUSHROOM_SUSPENSION_POINT_QTY, Factor = 3.0, CategoryScope = "Trần", Application = AppColdStorage, Note = "3 cùm xiết cáp cho mỗi điểm treo bulong nấm" },
            new() { Name = "Ống Ø114", Material = "PVC", Position = PosTreoBulongNam, Unit = "cây", CalcRule = AccessoryCalcRule.PER_COLD_STORAGE_MUSHROOM_SUSPENSION_POINT_QTY, Factor = 0.05, CategoryScope = "Trần", Application = AppColdStorage, Note = "Ống PVC chụp bulong nấm, quy đổi 200mm/điểm, 1 cây = 4m theo DT-16" },
            new() { Name = ColdStorageMushroomBoltName, Material = "Nhựa", Position = PosTreoBulongNam, Unit = "cái", CalcRule = AccessoryCalcRule.PER_COLD_STORAGE_MUSHROOM_BOLT_QTY, Factor = 1.0, CategoryScope = "Trần", Application = AppColdStorage, Note = "1 bulong nấm cho mỗi điểm liên kết panel với thanh C" }
        };
    }
}
