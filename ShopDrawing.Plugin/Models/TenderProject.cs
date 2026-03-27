using System;
using System.Collections.Generic;

namespace ShopDrawing.Plugin.Models
{
    /// <summary>
    /// Wrapper dự án chào giá — chứa toàn bộ data cho 1 dự án Tender.
    /// Serialize/Deserialize thành JSON file.
    /// </summary>
    public class TenderProject
    {
        /// <summary>Tên dự án (VD: "Kho lạnh ABC")</summary>
        public string ProjectName { get; set; } = string.Empty;

        /// <summary>Khách hàng (VD: "Cty TNHH XYZ")</summary>
        public string CustomerName { get; set; } = string.Empty;

        /// <summary>Ngày tạo</summary>
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        /// <summary>Ngày cập nhật cuối</summary>
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        /// <summary>Danh sách vách + opening</summary>
        public List<TenderWall> Walls { get; set; } = new();

        /// <summary>Danh sách Spec (fork từ ShopDrawing panel_specs.json)</summary>
        public List<PanelSpec> Specs { get; set; } = new();

        /// <summary>Danh sách phụ kiện template</summary>
        public List<TenderAccessory> Accessories { get; set; } = new();

        /// <summary>File path khi đã save (nullable — chưa save thì null)</summary>
        public string? FilePath { get; set; }
    }
}
