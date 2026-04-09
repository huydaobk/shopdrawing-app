namespace ShopDrawing.Plugin.Models
{
    /// <summary>
    /// Quy tắc tính khối lượng phụ kiện.
    /// Mỗi rule ánh xạ sang 1 công thức tính cụ thể trong TenderBomCalculator.
    /// </summary>
    public enum AccessoryCalcRule
    {
        /// <summary>Chiều dài vách x hệ số</summary>
        PER_WALL_LENGTH,

        /// <summary>Chiều cao vách x hệ số</summary>
        PER_WALL_HEIGHT,

        /// <summary>Chiều dài đỉnh vách xử lý x hệ số</summary>
        PER_TOP_EDGE_LENGTH,

        /// <summary>Chiều dài đỉnh vách cho chi tiết giao trần giữa x hệ số</summary>
        PER_TOP_PANEL_CEILING_CENTER_LENGTH,

        /// <summary>Chiều dài đỉnh vách cho chi tiết giao biên trần x hệ số</summary>
        PER_TOP_PANEL_CEILING_PERIMETER_LENGTH,

        /// <summary>Chiều dài đỉnh vách cho mép tự do x hệ số</summary>
        PER_TOP_PANEL_FREE_LENGTH,

        /// <summary>Chiều dài chân vách xử lý x hệ số</summary>
        PER_BOTTOM_EDGE_LENGTH,

        /// <summary>Tổng chiều dài đầu/cuối cho chi tiết giao giữa x hệ số</summary>
        PER_END_PANEL_CENTER_LENGTH,

        /// <summary>Tổng chiều dài đầu/cuối cho chi tiết giao biên x hệ số</summary>
        PER_END_PANEL_PERIMETER_LENGTH,

        /// <summary>Tổng chiều dài đầu/cuối cho mép tự do x hệ số</summary>
        PER_END_PANEL_FREE_LENGTH,

        /// <summary>Tổng chiều dài hai mép đứng xử lý ở đầu/cuối vách x hệ số</summary>
        PER_EXPOSED_END_LENGTH,

        /// <summary>Tổng chiều dài các biên vách xử lý (đỉnh + chân + đầu/cuối) x hệ số</summary>
        PER_TOTAL_EXPOSED_EDGE_LENGTH,

        /// <summary>Tổng chiều cao các góc ngoài x hệ số</summary>
        PER_OUTSIDE_CORNER_HEIGHT,

        /// <summary>Tổng chiều cao các góc trong x hệ số</summary>
        PER_INSIDE_CORNER_HEIGHT,

        /// <summary>Số tấm x hệ số</summary>
        PER_PANEL_QTY,

        /// <summary>(Số tấm - 1) x chiều span tấm x hệ số</summary>
        PER_JOINT_LENGTH,

        /// <summary>Chu vi opening x hệ số</summary>
        PER_OPENING_PERIMETER,

        /// <summary>Chu vi opening hai mặt x hệ số</summary>
        PER_OPENING_PERIMETER_TWO_FACES,

        /// <summary>Chu vi cửa đi x hệ số</summary>
        PER_DOOR_OPENING_PERIMETER,

        /// <summary>Chu vi cửa sổ/lỗ kỹ thuật x hệ số</summary>
        PER_NON_DOOR_OPENING_PERIMETER,

        /// <summary>Số opening x hệ số</summary>
        PER_OPENING_QTY,

        /// <summary>Số cửa đi x hệ số</summary>
        PER_DOOR_OPENING_QTY,

        /// <summary>Số cửa sổ/lỗ kỹ thuật x hệ số</summary>
        PER_NON_DOOR_OPENING_QTY,

        /// <summary>Tổng chiều dài hai cạnh đứng của opening x hệ số</summary>
        PER_OPENING_VERTICAL_EDGES,

        /// <summary>Tổng chiều dài cạnh đầu opening x hệ số</summary>
        PER_OPENING_HORIZONTAL_TOP_LENGTH,

        /// <summary>Tổng chiều dài cạnh sill/ngưỡng dưới opening x hệ số</summary>
        PER_OPENING_SILL_LENGTH,

        /// <summary>Diện tích net x hệ số</summary>
        PER_NET_AREA,

        /// <summary>Tổng chiều dài tuyến treo T nhôm trần kho lạnh x hệ số</summary>
        PER_COLD_STORAGE_T_SUSPENSION_LENGTH,

        /// <summary>Số điểm treo theo hệ T nhôm trần kho lạnh x hệ số</summary>
        PER_COLD_STORAGE_T_SUSPENSION_POINT_QTY,

        /// <summary>Tổng chiều dài cáp treo theo hệ T nhôm trần kho lạnh x hệ số</summary>
        PER_COLD_STORAGE_T_WIRE_ROPE_LENGTH,

        /// <summary>Tổng chiều dài tuyến treo bulong nấm/C-channel trần kho lạnh x hệ số</summary>
        PER_COLD_STORAGE_MUSHROOM_SUSPENSION_LENGTH,

        /// <summary>Số điểm treo theo hệ bulong nấm trần kho lạnh x hệ số</summary>
        PER_COLD_STORAGE_MUSHROOM_SUSPENSION_POINT_QTY,

        /// <summary>Tổng chiều dài cáp treo theo hệ bulong nấm trần kho lạnh x hệ số</summary>
        PER_COLD_STORAGE_MUSHROOM_WIRE_ROPE_LENGTH,

        /// <summary>Số lượng bulong nấm nhựa trần kho lạnh x hệ số</summary>
        PER_COLD_STORAGE_MUSHROOM_BOLT_QTY,

        /// <summary>Phụ kiện cố định theo từng vách</summary>
        FIXED_PER_WALL,

        /// <summary>
        /// Số khe nối đứng (VerticalJointCount) × chiều cao vách × hệ số.
        /// CHỈ có tác dụng khi LayoutDirection = "Ngang". Trả về 0 khi xếp Dọc.
        /// Dùng cho: Alu. Omega, Foam PU, Mastic tape Omega, Gioăng xốp làm kín.
        /// </summary>
        PER_VERTICAL_JOINT_HEIGHT,

        /// <summary>
        /// Số vít TEK = EstimatedPanelCount × ceil(PanelSpan / 1500mm) × 2 vít/điểm.
        /// Tự động điều chỉnh theo chiều xếp tấm (Dọc/Ngang) và chiều cao/dài vách.
        /// </summary>
        PER_TEK_SCREW_QTY,

        /// <summary>
        /// Số bát thép chân panel = ceil(Chiều dài vách / 1500mm).
        /// Dùng cho vách ngoài nhà theo nhịp xà gồ/kết cấu đỡ.
        /// </summary>
        PER_PANEL_SUPPORT_BRACKET_QTY
    }

    /// <summary>
    /// Template phụ kiện, khai báo cách tính khối lượng.
    /// </summary>
    public class TenderAccessory
    {
        /// <summary>Tên phụ kiện</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Đơn vị</summary>
        public string Unit { get; set; } = "md";

        /// <summary>Quy tắc tính</summary>
        public AccessoryCalcRule CalcRule { get; set; } = AccessoryCalcRule.PER_WALL_LENGTH;

        /// <summary>Hệ số nhân</summary>
        public double Factor { get; set; } = 1.0;

        /// <summary>Hạng mục áp dụng: Vách / Trần / Nền / Ốp cột / Tất cả</summary>
        public string CategoryScope { get; set; } = "Tất cả";

        /// <summary>Mã spec áp dụng (Tất cả = áp dụng mọi spec)</summary>
        public string SpecKey { get; set; } = "Tất cả";

        /// <summary>Ứng dụng áp dụng (Tất cả = mọi ứng dụng)</summary>
        public string Application { get; set; } = "Tất cả";

        /// <summary>Hao hụt hoặc dự phòng bổ sung theo phần trăm</summary>
        public double WasteFactor { get; set; }

        /// <summary>Điều chỉnh cộng/trừ thủ công cho giai đoạn đấu thầu</summary>
        public double Adjustment { get; set; }

        /// <summary>Đánh dấu phụ kiện chỉ nhập tay, không tự tính theo rule</summary>
        public bool IsManualOnly { get; set; }

        /// <summary>Ghi chú phục vụ chào giá</summary>
        public string Note { get; set; } = string.Empty;

        /// <summary>Chất liệu phụ kiện (VD: Tole, Nhôm, Inox)</summary>
        public string Material { get; set; } = string.Empty;

        /// <summary>Vị trí lắp đặt (VD: Mối nối, Cạnh trên, Cửa đi...)</summary>
        public string Position { get; set; } = string.Empty;

        /// <summary>Danh sách đơn vị cho dropdown</summary>
        public static readonly string[] UnitOptions = { "md", "m²", "cái", "bộ", "cây", "chai", "kg" };

        /// <summary>Danh sách hạng mục áp dụng cho dropdown</summary>
        public static readonly string[] CategoryScopeOptions =
        {
            "Tất cả", "Vách", "Trần", "Nền", "Ốp cột"
        };

        /// <summary>Danh sách phạm vi dùng chung cho dropdown</summary>
        public static readonly string[] ScopeOptions = { "Tất cả" };
    }
}
