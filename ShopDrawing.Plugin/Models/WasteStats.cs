namespace ShopDrawing.Plugin.Models
{
    public class WasteStats
    {
        /// <summary>
        /// Tổng diện tích (mm²) các panel đang hiển thị trên bản vẽ (layer SD_PANEL).
        /// Tính từ GeometricExtents — cùng đơn vị CAD (mm nếu bản vẽ dùng mm).
        /// </summary>
        public double TotalUsefulAreaMm2 { get; set; }

        /// <summary>
        /// Tổng diện tích (mm²) tấm lẻ có status='available' trong kho.
        /// Đây là vật liệu đã mua/sản xuất nhưng chưa được tái sử dụng.
        /// </summary>
        public double TotalWasteAreaMm2 { get; set; }

        /// <summary>Số tấm lẻ available trong kho.</summary>
        public int AvailableCount { get; set; }

        /// <summary>
        /// Tổng diện tích (mm²) tấm có status='discarded' (STEP/OPEN/TRIM/REM bỏ).
        /// Đây là phần vật liệu thực sự mất đi, không tái dụng được.
        /// </summary>
        public double TotalDiscardedAreaMm2 { get; set; }

        /// <summary>
        /// % Hao hụt = Tổng vật liệu bỏ / Tổng vật liệu đầu vào × 100
        ///
        /// Tổng vật liệu đầu vào = Panel sử dụng + Tấm lẻ available (chưa dùng) + Tấm đã bỏ
        /// Công thức này phản ánh đúng tỉ lệ hao hụt so với tổng nguyên liệu đã mua/sản xuất.
        ///
        /// Ví dụ: 10m² hữu ích + 2m² lẻ kho + 1m² bỏ → % hao hụt = 1/13 ≈ 7.7%
        /// (Trước đây tính sai: 1/10 = 10%)
        /// </summary>
        public double WastePercentage
        {
            get
            {
                // Tổng nguyên liệu đầu vào = useful + available còn kho + đã bỏ
                double totalInput = TotalUsefulAreaMm2 + TotalWasteAreaMm2 + TotalDiscardedAreaMm2;
                if (totalInput <= 0) return 0;
                double generatedWaste = TotalWasteAreaMm2 + TotalDiscardedAreaMm2;
                return (generatedWaste / totalInput) * 100.0;
            }
        }

        /// <summary>Tổng diện tích (m²) tấm lẻ available, dùng để hiển thị UI.</summary>
        public double TotalWasteAreaM2 => TotalWasteAreaMm2 / 1_000_000.0;

        /// <summary>Tổng diện tích (m²) tấm đã bỏ, dùng để hiển thị UI.</summary>
        public double TotalDiscardedAreaM2 => TotalDiscardedAreaMm2 / 1_000_000.0;
    }
}
