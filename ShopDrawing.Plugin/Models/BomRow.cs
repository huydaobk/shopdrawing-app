namespace ShopDrawing.Plugin.Models
{
    public class BomItem 
    { 
        public string Id { get; set; } = string.Empty; 
        public string Spec { get; set; } = string.Empty; 
        public string Dims { get; set; } = string.Empty; 
        public string Status { get; set; } = string.Empty;
        public string WallCode { get; set; } = string.Empty;
        public string JointLeft { get; set; } = string.Empty;
        public string JointRight { get; set; } = string.Empty;
        public double WidthMm { get; set; }
        public double LengthMm { get; set; }
        public int ThickMm { get; set; }
    }

    public class BomRow 
    { 
        public string Id { get; set; } = string.Empty; 
        public string Spec { get; set; } = string.Empty; 
        public string Dims { get; set; } = string.Empty; 
        public string Status { get; set; } = string.Empty; 
        public int Qty { get; set; }
        public string WallCode { get; set; } = string.Empty;
        public string JointLeft { get; set; } = string.Empty;
        public string JointRight { get; set; } = string.Empty;
        public double WidthMm { get; set; }
        public double LengthMm { get; set; }
        public int ThickMm { get; set; }
        public double AreaM2 => (WidthMm * LengthMm) / 1_000_000.0;
    }
}
