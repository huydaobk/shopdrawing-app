namespace ShopDrawing.Plugin.Models
{
    public class WastePanel
    {
        public int Id { get; set; }
        public string PanelCode { get; set; } = string.Empty;
        public double WidthMm { get; set; }
        public double LengthMm { get; set; }
        public int ThickMm { get; set; }
        public string PanelSpec { get; set; } = string.Empty;
        public JointType JointLeft { get; set; }
        public JointType JointRight { get; set; }
        public string SourceWall { get; set; } = string.Empty;
        public string Project { get; set; } = string.Empty;
        public string Status { get; set; } = "available";      // available | used | discarded
        public string SourceType { get; set; } = "REM";         // REM | STEP | OPEN | TRIM
        public double? SourcePanelX { get; set; }
        public double? SourcePanelY { get; set; }

        public double DienTich => Math.Round(WidthMm * LengthMm / 1_000_000.0, 3);

        // Tấm sandwich: ~11 kg/m²/cm dày (EPS/rock-wool). Công thức: DienTich × (ThickMm/10) × 11
        public double KhoiLuong => Math.Round(DienTich * (ThickMm / 10.0) * 11.0, 1);

        public string StatusDisplay => Status switch
        {
            "available" => "Sẵn sàng",
            "used" => "Đã dùng",
            "discarded" => "Đã bỏ",
            _ => Status
        };

        public string SourceTypeDisplay => SourceType switch
        {
            "REM" => "Tấm lẻ",
            "STEP" => "Bậc thang",
            "OPEN" => "Lỗ mở",
            "TRIM" => "Cắt tận dụng",
            _ => SourceType
        };

        public string JointLeftDisplay => JointLeft switch
        {
            JointType.Male => "+",
            JointType.Female => "-",
            _ => "0"
        };

        public string JointRightDisplay => JointRight switch
        {
            JointType.Male => "+",
            JointType.Female => "-",
            _ => "0"
        };
    }
}
