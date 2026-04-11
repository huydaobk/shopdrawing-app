namespace ShopDrawing.Plugin.Models
{
    public class TenderOpening
    {
        /// <summary>Loai opening: Cua di / Cua so / Lo ky thuat</summary>
        public string Type { get; set; } = "Cửa đi";

        /// <summary>Chieu rong opening (mm)</summary>
        public double Width { get; set; }

        /// <summary>Chieu cao opening (mm)</summary>
        public double Height { get; set; }

        /// <summary>Cao do day opening (mm, tinh tu cot 0 cua vach)</summary>
        public double BottomElevationMm { get; set; }

        /// <summary>Lý trình tâm opening theo tuyến vách (mm, từ đầu tuyến). -1 = chưa xác định.</summary>
        public double CenterStationMm { get; set; } = -1;

        /// <summary>So luong opening cung kich thuoc</summary>
        public int Quantity { get; set; } = 1;

        public bool IsDoor => Type == "Cửa đi";
        public bool IsNonDoor => !IsDoor;

        /// <summary>Dien tich 1 opening (m2)</summary>
        public double AreaM2 => Width * Height / 1_000_000.0;

        /// <summary>Tong dien tich (m2) = DT x SL</summary>
        public double TotalAreaM2 => AreaM2 * Quantity;

        /// <summary>
        /// Chu vi can lap vien (mm):
        /// - Cua di: Width + Height x 2  (3 canh - khong co nguong duoi)
        /// - Cua so / lo ky thuat: (Width + Height) x 2 (4 canh)
        /// </summary>
        public double Perimeter => IsDoor
            ? Width + Height * 2
            : (Width + Height) * 2;

        /// <summary>Chu vi 2 mat cho opening can xu ly ca hai phia (mm)</summary>
        public double PerimeterTwoFaces => Perimeter * 2;

        /// <summary>Tong chu vi (mm) = Chu vi x SL</summary>
        public double TotalPerimeter => Perimeter * Quantity;

        /// <summary>Tong chu vi 2 mat (mm) = Chu vi 2 mat x SL</summary>
        public double TotalPerimeterTwoFaces => PerimeterTwoFaces * Quantity;

        /// <summary>Tong chieu rong (mm) = Rong x SL - dung de tru khi tinh so tam</summary>
        public double TotalWidth => Width * Quantity;

        /// <summary>Tổng chiều dài 2 cạnh đứng của opening (mm)</summary>
        public double TotalVerticalEdges => Height * 2 * Quantity;

        /// <summary>Tổng chiều dài cạnh đầu opening (mm)</summary>
        public double TotalHorizontalTopLength => Width * Quantity;

        /// <summary>
        /// Tổng chiều dài cạnh sill/ngưỡng dưới (mm).
        /// - Cửa đi: mặc định 0 vì không tính ngưỡng dưới
        /// - Cửa sổ/lỗ kỹ thuật: tính cạnh dưới
        /// </summary>
        public double TotalSillLength => IsDoor ? 0 : Width * Quantity;

        /// <summary>Danh sach loai opening cho dropdown</summary>
        public static readonly string[] TypeOptions = { "Cửa đi", "Cửa sổ", "Lỗ kỹ thuật" };
    }
}
