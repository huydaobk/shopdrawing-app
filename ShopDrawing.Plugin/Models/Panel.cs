namespace ShopDrawing.Plugin.Models
{
    public enum JointType { Male, Female, Cut }

    public class Panel
    {
        public string PanelId { get; set; } = string.Empty;     // W1-01
        public string WallCode { get; set; } = string.Empty;    // A3
        public double X { get; set; }
        public double Y { get; set; }
        public double WidthMm { get; set; }
        public double LengthMm { get; set; }
        public int ThickMm { get; set; }
        public string Spec { get; set; } = string.Empty;        // Spec1
        public JointType JointLeft { get; set; }
        public JointType JointRight { get; set; }
        public bool IsReused { get; set; }
        public string? SourceId { get; set; }    // null nếu mới
        public bool IsCutPanel { get; set; }
        public bool IsHorizontal { get; set; }   // true = tấm nằm ngang (Length theo X)
        public string? ParentPanelId { get; set; }
        public double StepWasteWidth { get; set; }   // Chiều rộng waste bậc thang (partial)
        public double StepWasteHeight { get; set; }  // Chiều dài waste bậc thang (chênh lệch)
    }
}
