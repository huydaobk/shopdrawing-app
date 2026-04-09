namespace ShopDrawing.Plugin.Models
{
    public class PanelSpec
    {
        // --- Identity ---
        public string Key { get; set; } = string.Empty;          // "ISOFRIGO-TT"
        public string WallCodePrefix { get; set; } = "W";        // "W" (tường) / "C" (trần)
        public string Description { get; set; } = string.Empty;  // "Tôn/Tôn" (hidden from UI)
        public int PanelWidth { get; set; } = 1100;              // Khổ tấm (mm)

        // --- Panel Info ---
        public string PanelType { get; set; } = string.Empty;    // ISOFRIGO, ISODACH, ISOCOOL...
        public string Density { get; set; } = string.Empty;      // "44±2" (kg/m³)
        public string FireRating { get; set; } = "-";
        public bool FmApproved { get; set; } = false;
        public int Thickness { get; set; } = 50;             // Chiều dày panel (mm)
        public string FacingColor { get; set; } = "Trắng";

        // --- Mặt Trên ---
        public string TopFacing { get; set; } = "Tole";          // Vật liệu
        public string TopCoating { get; set; } = "AZ150";        // Độ mạ
        public double TopSteelThickness { get; set; } = 0.6;     // Chiều dày tôn (mm)
        public string TopProfile { get; set; } = "Vuông";        // Vuông / Phẳng / Kim Cương

        // --- Mặt Dưới ---
        public string BottomFacingColor { get; set; } = "Trắng";  // Màu sắc mặt dưới
        public string BottomFacing { get; set; } = "Tole";
        public string BottomCoating { get; set; } = "AZ150";
        public double BottomSteelThickness { get; set; } = 0.6;
        public string BottomProfile { get; set; } = "Vuông";

        // --- Computed ---
        public string DisplayName => $"{Key} — {Description}";

        /// <summary>Danh sách profile hợp lệ cho dropdown.</summary>
        public static readonly string[] ProfileOptions = { "Vuông", "Phẳng", "Kim Cương" };

        /// <summary>Danh sách loại panel hợp lệ cho dropdown.</summary>
        public static readonly string[] PanelTypeOptions = { "ISOFRIGO", "ISOPAR", "GP3", "GP5" };

        /// <summary>Danh sách khổ tấm hợp lệ cho dropdown.</summary>
        public static readonly int[] PanelWidthOptions = { 1000, 1060, 1070, 1100, 1120 };

        /// <summary>Danh sách chiều dày panel hợp lệ cho dropdown (mm).</summary>
        public static readonly int[] ThicknessOptions = { 50, 60, 75, 80, 100, 125, 150, 175, 180, 200 };
    }
}
