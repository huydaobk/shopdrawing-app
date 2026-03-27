namespace ShopDrawing.Plugin.Models
{
    public class WasteStats
    {
        public double TotalUsefulAreaMm2 { get; set; }      // Tổng m² panel trên bản vẽ
        public double TotalWasteAreaMm2 { get; set; }       // Tổng m² tấm "available" trong kho
        public double TotalDiscardedAreaMm2 { get; set; }   // Tổng m² tấm "discarded" (STEP/OPEN/TRIM)
        
        /// <summary>
        /// % Hao hụt = Tổng m² "Đã bỏ" / Tổng m² panel trên bản vẽ
        /// </summary>
        public double WastePercentage 
        {
            get
            {
                if (TotalUsefulAreaMm2 == 0) return 0;
                return (TotalDiscardedAreaMm2 / TotalUsefulAreaMm2) * 100.0;
            }
        }
    }
}
