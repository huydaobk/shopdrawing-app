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

        // ═══════════════════════════════════════════════════
        // POSITION CONSTANTS
        // ═══════════════════════════════════════════════════
        private const string PosMoiNoi = "Mối nối";
        private const string PosCanhTren = "Cạnh trên";
        private const string PosCanhDuoi = "Cạnh dưới";
        private const string PosDauCuoi = "Đầu/cuối";
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
        private const string PosCanhHo = "Cạnh hở";
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
            { "Sealant MC-202 giữa ngàm panel kho lạnh", SealantMC202 },
            { "Vít TEK ngoài nhà", ExteriorTekScrewBaseName },
            { "Vít TEK inox", "B2S-TEK inox" },
            // v2.x → v3.x renames
            { "B2S-TEK", ExteriorTekScrewBaseName },               // vis panel: B2S-TEK → B2S-TEK 12x14
            { "Rivet Ø4.2×12", "B2S-TEK 15-15×20 HWFS" },        // vis diềm ngoài nhà: Rivet → TEK screw
            { "Rivets", "B2S-TEK 15-15×20 HWFS" },                // variant tên cũ không có ký tự đặc biệt
            // Legacy exterior flashing names → Diềm 01-05
            { "Úp nóc",       "Diềm 01" },
            { "Úp chân",      "Diềm 02" },
            { "Úp cạnh hở",   "Diềm 03" },
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
            list.AddRange(GetColdStorageDefaults());
            return DeduplicateAndSort(list);
        }

        public static List<TenderAccessory> NormalizeConfiguredAccessories(IEnumerable<TenderAccessory>? accessories)
        {
            var source = accessories?.ToList() ?? new List<TenderAccessory>();
            if (source.Count == 0)
                return GetDefaults();

            var normalized = new List<TenderAccessory>();

            foreach (var accessory in source)
            {
                if (accessory == null)
                    continue;

                // Migrate legacy names
                MigrateLegacyAccessory(accessory);

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

            MergeMissingDefaults(normalized);
            return DeduplicateAndSort(normalized);
        }

        private static void MigrateLegacyAccessory(TenderAccessory accessory)
        {
            string name = (accessory.Name ?? string.Empty).Trim();
            string application = TenderAccessoryRules.NormalizeScope(accessory.Application);

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

            // Auto-set Material for all Diềm items that haven't been set yet
            if (accessory.Name.StartsWith("Diềm ", StringComparison.OrdinalIgnoreCase)
                && string.IsNullOrWhiteSpace(accessory.Material))
            {
                accessory.Material = "Tole";
            }

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
                    item.CalcRule,
                    item.Factor,
                    item.WasteFactor,
                    item.Adjustment,
                    item.IsManualOnly,
                    item.Note
                })
                .Select(group => group.First())
                .OrderBy(item => item, Comparer<TenderAccessory>.Create(CompareAccessories))
                .ToList();
        }

        private static void MergeMissingDefaults(List<TenderAccessory> configuredAccessories)
        {
            foreach (var defaultAccessory in GetDefaults())
            {
                if (configuredAccessories.Any(item => HasSameLogicalAccessory(item, defaultAccessory)))
                    continue;

                configuredAccessories.Add(defaultAccessory);
            }
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

            // Sealant/silicone vách expansion: Vách lộ thiên → đỉnh + chân + cạnh hở
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
                    "Hoàn thiện cạnh hở");
                yield break;
            }

            yield return normalized;
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
            new() { Name = "Diềm 01", Material = "Tole", Position = PosCanhTren,  Unit = "md", CalcRule = AccessoryCalcRule.PER_TOP_EDGE_LENGTH,           Factor = 1.0, CategoryScope = DefaultCategoryScope, Application = AppExterior, Note = "Mép trên vách, tiếp giáp mái/trần" },
            new() { Name = "Diềm 02", Material = "Tole", Position = PosCanhDuoi,  Unit = "md", CalcRule = AccessoryCalcRule.PER_BOTTOM_EDGE_LENGTH,        Factor = 1.0, CategoryScope = DefaultCategoryScope, Application = AppExterior, Note = "Mép dưới vách, tiếp giáp nền/sàn" },
            new() { Name = "Diềm 03", Material = "Tole", Position = PosDauCuoi,   Unit = "md", CalcRule = AccessoryCalcRule.PER_EXPOSED_END_LENGTH,        Factor = 1.0, CategoryScope = DefaultCategoryScope, Application = AppExterior, Note = "Mép đứng hở ở đầu/cuối vách (che lõi xốp)" },
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
            new() { Name = SealantMS617, Position = PosDinhVach,   Unit = "md", CalcRule = AccessoryCalcRule.PER_TOP_EDGE_LENGTH,                Factor = 1.0, CategoryScope = DefaultCategoryScope, Application = AppExterior, Note = "Hoàn thiện mép trên vách lộ thiên" },
            new() { Name = SealantMS617, Position = PosChanVach,   Unit = "md", CalcRule = AccessoryCalcRule.PER_BOTTOM_EDGE_LENGTH,             Factor = 1.0, CategoryScope = DefaultCategoryScope, Application = AppExterior, Note = "Hoàn thiện mép chân vách lộ thiên" },
            new() { Name = SealantMS617, Position = PosCanhHo,     Unit = "md", CalcRule = AccessoryCalcRule.PER_EXPOSED_END_LENGTH,             Factor = 1.0, CategoryScope = DefaultCategoryScope, Application = AppExterior, Note = "Hoàn thiện mép hở đầu/cuối vách" },
            new() { Name = "Mastic tape",  Position = PosMoiNoi, Unit = "md",  CalcRule = AccessoryCalcRule.PER_JOINT_LENGTH,  Factor = 1.0, CategoryScope = DefaultCategoryScope, Application = AppExterior, Note = "Băng butyl dán trong ngàm T/G trước khi lắp tấm" },
            // ─── Nhóm khe nối đứng (chỉ xếp Ngang, tự động = 0 khi xếp Dọc) ───
            new() { Name = "Alu. Omega profile",  Material = "Nhôm", Position = PosMoiNoi, Unit = "md",   CalcRule = AccessoryCalcRule.PER_VERTICAL_JOINT_HEIGHT, Factor = 1.0,  CategoryScope = DefaultCategoryScope, Application = AppExterior, Note = "Nẹp nhôm che khe nối đứng (chỉ xếp Ngang)" },
            new() { Name = "Mastic tape (Omega)",               Position = PosMoiNoi, Unit = "md",   CalcRule = AccessoryCalcRule.PER_VERTICAL_JOINT_HEIGHT, Factor = 2.0,  CategoryScope = DefaultCategoryScope, Application = AppExterior, Note = "Dán 2 mép cánh Omega trước khi ép vào panel" },
            new() { Name = "Foam PU on site",                   Position = PosMoiNoi, Unit = "chai", CalcRule = AccessoryCalcRule.PER_VERTICAL_JOINT_HEIGHT, Factor = 0.33, CategoryScope = DefaultCategoryScope, Application = AppExterior, Note = "Bơm khe 50×100mm, foam nở ×20, chai 750ml" },
            new() { Name = "Gioăng xốp làm kín",               Position = PosMoiNoi, Unit = "md",   CalcRule = AccessoryCalcRule.PER_VERTICAL_JOINT_HEIGHT, Factor = 2.0,  CategoryScope = DefaultCategoryScope, Application = AppExterior, Note = "Dán 2 mép panel vào thép cột đứng, chống nước/gió" },
            new() { Name = "B2S-TEK 15-15×20 HWFS", Position = PosVach, Unit = "cái", CalcRule = AccessoryCalcRule.PER_WALL_LENGTH, Factor = 6.0, CategoryScope = DefaultCategoryScope, Application = AppExterior, Note = "Vít tự khoan gắn diềm tole vào panel (thép dày < 2.5mm), ~6 con/md vách" }
        };

        // ═══════════════════════════════════════════════════
        // CLEANROOM DEFAULTS (Phòng sạch)
        // ═══════════════════════════════════════════════════
        private static List<TenderAccessory> GetCleanroomDefaults() => new()
        {
            new() { Name = "Nẹp nhôm phẳng", Position = PosMoiNoi, Unit = "md", CalcRule = AccessoryCalcRule.PER_JOINT_LENGTH, Factor = 1.0, CategoryScope = DefaultCategoryScope, Application = AppCleanroom },
            new() { Name = "Nẹp chân vệ sinh", Position = PosCanhDuoi, Unit = "md", CalcRule = AccessoryCalcRule.PER_BOTTOM_EDGE_LENGTH, Factor = 1.0, CategoryScope = DefaultCategoryScope, Application = AppCleanroom },
            new() { Name = "Nẹp bo cong R40", Position = PosCanhTren, Unit = "md", CalcRule = AccessoryCalcRule.PER_TOP_EDGE_LENGTH, Factor = 1.0, CategoryScope = DefaultCategoryScope, Application = AppCleanroom },
            new() { Name = "Nẹp cạnh hở", Position = PosDauCuoi, Unit = "md", CalcRule = AccessoryCalcRule.PER_EXPOSED_END_LENGTH, Factor = 1.0, CategoryScope = DefaultCategoryScope, Application = AppCleanroom },
            new() { Name = "Viền lỗ mở 2 mặt", Position = PosLoMo, Unit = "md", CalcRule = AccessoryCalcRule.PER_OPENING_PERIMETER_TWO_FACES, Factor = 1.0, CategoryScope = DefaultCategoryScope, Application = AppCleanroom },
            new() { Name = "Bộ cửa đi phòng sạch", Position = PosCuaDi, Unit = "bộ", CalcRule = AccessoryCalcRule.PER_DOOR_OPENING_QTY, Factor = 1.0, CategoryScope = DefaultCategoryScope, Application = AppCleanroom, Note = "Tính sơ bộ theo SL cửa đi" },
            new() { Name = SiliconeSN505, Position = PosMoiNoi, Unit = "md", CalcRule = AccessoryCalcRule.PER_JOINT_LENGTH, Factor = 1.0, CategoryScope = DefaultCategoryScope, Application = AppCleanroom, Note = "Kháng mốc" },
            new() { Name = SiliconeSN505, Position = PosCanhDungLM, Unit = "md", CalcRule = AccessoryCalcRule.PER_OPENING_VERTICAL_EDGES, Factor = 2.0, CategoryScope = DefaultCategoryScope, Application = AppCleanroom, Note = "Tính 2 mặt" },
            new() { Name = SiliconeSN505, Position = PosCanhDauLM, Unit = "md", CalcRule = AccessoryCalcRule.PER_OPENING_HORIZONTAL_TOP_LENGTH, Factor = 2.0, CategoryScope = DefaultCategoryScope, Application = AppCleanroom, Note = "Tính 2 mặt" },
            new() { Name = SiliconeSN505, Position = PosCanhSillLM, Unit = "md", CalcRule = AccessoryCalcRule.PER_OPENING_SILL_LENGTH, Factor = 2.0, CategoryScope = DefaultCategoryScope, Application = AppCleanroom, Note = "Tính 2 mặt" },
            new() { Name = SiliconeSN505, Position = PosDinhVach, Unit = "md", CalcRule = AccessoryCalcRule.PER_TOP_EDGE_LENGTH, Factor = 1.0, CategoryScope = DefaultCategoryScope, Application = AppCleanroom, Note = "Hoàn thiện vệ sinh" },
            new() { Name = SiliconeSN505, Position = PosChanVach, Unit = "md", CalcRule = AccessoryCalcRule.PER_BOTTOM_EDGE_LENGTH, Factor = 1.0, CategoryScope = DefaultCategoryScope, Application = AppCleanroom, Note = "Hoàn thiện vệ sinh" },
            new() { Name = SiliconeSN505, Position = PosCanhHo, Unit = "md", CalcRule = AccessoryCalcRule.PER_EXPOSED_END_LENGTH, Factor = 1.0, CategoryScope = DefaultCategoryScope, Application = AppCleanroom, Note = "Hoàn thiện vệ sinh" },
            new() { Name = "B2S-TEK inox", Position = PosPanelThep, Unit = "cái", CalcRule = AccessoryCalcRule.PER_PANEL_QTY, Factor = 12.0, CategoryScope = DefaultCategoryScope, Application = AppCleanroom },
            new() { Name = "Rivet inox Ø4.2×12", Position = PosVach, Unit = "cái", CalcRule = AccessoryCalcRule.PER_WALL_LENGTH, Factor = 6.0, CategoryScope = DefaultCategoryScope, Application = AppCleanroom }
        };

        // ═══════════════════════════════════════════════════
        // COLD STORAGE DEFAULTS (Kho lạnh)
        // ═══════════════════════════════════════════════════
        private static List<TenderAccessory> GetColdStorageDefaults() => new()
        {
            new() { Name = SheetTrim01, Material = "Tole", Position = PosCanhTren, Unit = "md", CalcRule = AccessoryCalcRule.PER_TOP_EDGE_LENGTH, Factor = 1.0, CategoryScope = DefaultCategoryScope, Application = AppColdStorage },
            new() { Name = SheetTrim01, Material = "Tole", Position = PosCanhDuoi, Unit = "md", CalcRule = AccessoryCalcRule.PER_BOTTOM_EDGE_LENGTH, Factor = 1.0, CategoryScope = DefaultCategoryScope, Application = AppColdStorage },
            new() { Name = SheetTrim01, Material = "Tole", Position = PosDauCuoi, Unit = "md", CalcRule = AccessoryCalcRule.PER_EXPOSED_END_LENGTH, Factor = 1.0, CategoryScope = DefaultCategoryScope, Application = AppColdStorage },
            new() { Name = SheetTrim01, Material = "Tole", Position = PosMoiNoi, Unit = "md", CalcRule = AccessoryCalcRule.PER_JOINT_LENGTH, Factor = 1.0, CategoryScope = DefaultCategoryScope, Application = AppColdStorage },
            new() { Name = SheetTrim01, Material = "Tole", Position = PosLoMo, Unit = "md", CalcRule = AccessoryCalcRule.PER_OPENING_PERIMETER_TWO_FACES, Factor = 1.0, CategoryScope = DefaultCategoryScope, Application = AppColdStorage },
            new() { Name = "Bộ cửa đi kho lạnh", Position = PosCuaDi, Unit = "bộ", CalcRule = AccessoryCalcRule.PER_DOOR_OPENING_QTY, Factor = 1.0, CategoryScope = DefaultCategoryScope, Application = AppColdStorage, Note = "Tính sơ bộ theo SL cửa đi" },
            new() { Name = "B2S-TEK", Material = "Inox", Position = PosPanelThep, Unit = "cái", CalcRule = AccessoryCalcRule.PER_PANEL_QTY, Factor = 12.0, CategoryScope = DefaultCategoryScope, Application = AppColdStorage },
            new() { Name = SealantMC202, Position = PosMoiNoi, Unit = "md", CalcRule = AccessoryCalcRule.PER_JOINT_LENGTH, Factor = 1.0, CategoryScope = DefaultCategoryScope, Application = AppColdStorage, Note = "Giữa ngàm panel" },
            new() { Name = SiliconeSN505, Position = PosCanhDungLM, Unit = "md", CalcRule = AccessoryCalcRule.PER_OPENING_VERTICAL_EDGES, Factor = 1.0, CategoryScope = DefaultCategoryScope, Application = AppColdStorage },
            new() { Name = SiliconeSN505, Position = PosCanhDauLM, Unit = "md", CalcRule = AccessoryCalcRule.PER_OPENING_HORIZONTAL_TOP_LENGTH, Factor = 1.0, CategoryScope = DefaultCategoryScope, Application = AppColdStorage },
            new() { Name = SiliconeSN505, Position = PosCanhSillLM, Unit = "md", CalcRule = AccessoryCalcRule.PER_OPENING_SILL_LENGTH, Factor = 1.0, CategoryScope = DefaultCategoryScope, Application = AppColdStorage },
            new() { Name = SiliconeSN505, Position = PosDinhVach, Unit = "md", CalcRule = AccessoryCalcRule.PER_TOP_EDGE_LENGTH, Factor = 1.0, CategoryScope = DefaultCategoryScope, Application = AppColdStorage, Note = "Hoàn thiện lộ thiên" },
            new() { Name = SiliconeSN505, Position = PosChanVach, Unit = "md", CalcRule = AccessoryCalcRule.PER_BOTTOM_EDGE_LENGTH, Factor = 1.0, CategoryScope = DefaultCategoryScope, Application = AppColdStorage, Note = "Hoàn thiện lộ thiên" },
            new() { Name = SiliconeSN505, Position = PosCanhHo, Unit = "md", CalcRule = AccessoryCalcRule.PER_EXPOSED_END_LENGTH, Factor = 1.0, CategoryScope = DefaultCategoryScope, Application = AppColdStorage, Note = "Hoàn thiện lộ thiên" },
            new() { Name = "Rivet Ø4.2×12", Material = "Nhôm", Position = PosVach, Unit = "cái", CalcRule = AccessoryCalcRule.PER_WALL_LENGTH, Factor = 6.0, CategoryScope = DefaultCategoryScope, Application = AppColdStorage },
            new() { Name = "T-profile 68x75", Material = "Nhôm", Position = PosTreoTNhôm, Unit = "md", CalcRule = AccessoryCalcRule.PER_COLD_STORAGE_T_SUSPENSION_LENGTH, Factor = 1.0, CategoryScope = "Trần", Application = AppColdStorage, Note = "Tổng chiều dài tuyến treo T nhôm" },
            new() { Name = "Steel plate 50x325x3", Material = "Thép", Position = PosTreoTNhôm, Unit = "cái", CalcRule = AccessoryCalcRule.PER_COLD_STORAGE_T_SUSPENSION_POINT_QTY, Factor = 1.0, CategoryScope = "Trần", Application = AppColdStorage },
            new() { Name = "Turnbuckle M12", Material = "Thép mạ kẽm", Position = PosTreoTNhôm, Unit = "cái", CalcRule = AccessoryCalcRule.PER_COLD_STORAGE_T_SUSPENSION_POINT_QTY, Factor = 1.0, CategoryScope = "Trần", Application = AppColdStorage },
            new() { Name = "Wire rope Ø12", Material = "Thép mạ kẽm", Position = PosTreoTNhôm, Unit = "md", CalcRule = AccessoryCalcRule.PER_COLD_STORAGE_T_WIRE_ROPE_LENGTH, Factor = 1.0, CategoryScope = "Trần", Application = AppColdStorage, Note = "Nhân theo Thả cáp (mm) nhập tay" },
            new() { Name = "Wire rope clip Ø12", Material = "Thép mạ kẽm", Position = PosTreoTNhôm, Unit = "cái", CalcRule = AccessoryCalcRule.PER_COLD_STORAGE_T_SUSPENSION_POINT_QTY, Factor = 2.0, CategoryScope = "Trần", Application = AppColdStorage },
            new() { Name = "Ống Ø114", Material = "PVC", Position = PosTreoTNhôm, Unit = "cây", CalcRule = AccessoryCalcRule.PER_COLD_STORAGE_T_SUSPENSION_POINT_QTY, Factor = 0.125, CategoryScope = "Trần", Application = AppColdStorage, Note = "Quy đổi 500mm/điểm, 1 cây = 4m theo DT-15" },
            new() { Name = "C-channel 100x50", Material = "Thép mạ kẽm", Position = PosTreoBulongNam, Unit = "md", CalcRule = AccessoryCalcRule.PER_COLD_STORAGE_MUSHROOM_SUSPENSION_LENGTH, Factor = 1.0, CategoryScope = "Trần", Application = AppColdStorage, Note = "Tổng chiều dài tuyến treo bulong nấm" },
            new() { Name = "Ty ren M10", Material = "Thép", Position = PosTreoBulongNam, Unit = "cái", CalcRule = AccessoryCalcRule.PER_COLD_STORAGE_MUSHROOM_SUSPENSION_POINT_QTY, Factor = 1.0, CategoryScope = "Trần", Application = AppColdStorage },
            new() { Name = "Turnbuckle M12", Material = "Thép mạ kẽm", Position = PosTreoBulongNam, Unit = "cái", CalcRule = AccessoryCalcRule.PER_COLD_STORAGE_MUSHROOM_SUSPENSION_POINT_QTY, Factor = 1.0, CategoryScope = "Trần", Application = AppColdStorage },
            new() { Name = "Wire rope Ø12", Material = "Thép mạ kẽm", Position = PosTreoBulongNam, Unit = "md", CalcRule = AccessoryCalcRule.PER_COLD_STORAGE_MUSHROOM_WIRE_ROPE_LENGTH, Factor = 1.0, CategoryScope = "Trần", Application = AppColdStorage, Note = "Nhân theo Thả cáp (mm) nhập tay" },
            new() { Name = "Wire rope clip Ø12", Material = "Thép mạ kẽm", Position = PosTreoBulongNam, Unit = "cái", CalcRule = AccessoryCalcRule.PER_COLD_STORAGE_MUSHROOM_SUSPENSION_POINT_QTY, Factor = 2.0, CategoryScope = "Trần", Application = AppColdStorage },
            new() { Name = "Ống Ø114", Material = "PVC", Position = PosTreoBulongNam, Unit = "cây", CalcRule = AccessoryCalcRule.PER_COLD_STORAGE_MUSHROOM_SUSPENSION_POINT_QTY, Factor = 0.05, CategoryScope = "Trần", Application = AppColdStorage, Note = "Quy đổi 200mm/điểm, 1 cây = 4m theo DT-16" },
            new() { Name = "Bulong nấm nhựa", Material = "Nhựa", Position = PosTreoBulongNam, Unit = "cái", CalcRule = AccessoryCalcRule.PER_COLD_STORAGE_MUSHROOM_BOLT_QTY, Factor = 1.0, CategoryScope = "Trần", Application = AppColdStorage }
        };
    }
}
