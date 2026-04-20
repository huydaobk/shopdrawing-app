# Changelog - ShopDrawing AutoCAD Plugin

## [2026-04-20] - v0.2.28 Manual CUI Integration 📖
### Added
- **Manual CUI Integration**: Thêm nút "Manual" vào System Ribbon để mở bảng hướng dẫn sử dụng.
- **Tender Workflow Guide**: Xây dựng UI WPF "ManualWindow" hướng dẫn tuần tự các bước cấu hình (Input -> Spec -> Pick vách -> Khoét lỗ -> Export Excel).
- **Quick Command Launcher**: Tích hợp các hyperlink "👉 Mở bảng ngay" xử lý qua hàm `SendStringToExecute` để gọi nhanh lệnh mà không cần gõ (SD_INPUT, SD_TAB, SD_TENDER_BOM).

## [2026-04-17] - v0.2.13 Tender Persistence Fix 🔧
### Fixed
- **Critical: Tender Wall AutoLoad bị chặn bởi marker file gate** — Sau khi cập nhật plugin và restart CAD, vách đã vẽ mất khỏi Bảng quản lý khối lượng. Nguyên nhân: `EnsureProject()` yêu cầu file `.shopdrawing-project.json` tồn tại trước khi AutoLoad, nhưng AutoSave không tạo file này. Đã bỏ gate condition thừa vì `TryAutoLoad()` đã có guard `File.Exists` riêng.
- **CS8620 nullability warnings** — Sửa 2 warning `IEnumerable<string?>` trong `TenderBomDialog.CadOps.cs` khi gom CAD handle để group entities.

## [2026-04-17] - v0.2.5 Tender Grouping & Drawing Refinement 🚀
### Added
- **AutoCAD CAD Grouping**: Tự động gom nhóm toàn bộ cấu kiện được sinh ra từ lệnh vẽ Tender (đường bao, panel, lỗ mở, ghi chú, và đường nối) vào một đối tượng Group duy nhất (`Tender Elevation Group`), giúp thao tác di chuyển/xóa cả cụm bản vẽ trên CAD chỉ qua 1 click.
- **TraceBoundary Picking (Pick Vùng)**: Giải pháp linh hoạt cho việc nhận diện vùng kín, cho phép click vào điểm bất kỳ để tự scan lấy boundary hoặc click trực tiếp lên Polyline. Giải quyết vấn đề chọn các vùng dị dạng vuông hoặc khuyết.
- **Link Line Rendering**: Tự động vẽ các dải Line tham chiếu màu xám nhạt (`SD_LINK`) để kết nối từ vùng Floorplan ban đầu với bản vẽ Mặt Đứng Panel để kiểm soát nguồn gốc chiết tính rõ ràng.
- **Multi-select Opening/Lỗ Mở**: Tính năng cho phép giữ phím trỏ hàng loạt lỗ mở khi thêm vào hệ thống và bổ sung khai báo Cao độ đáy (Bottom Offset) thay vì cố định = 0.
- **Tender UI Improvements**: Cập nhật logic làm mới UI mượt mà, render ngay lập tức thông số lỗ mở vào Footer của "Pick Nhịp" sau mỗi lần chỉnh sửa. Thêm phím tắt Shift+Click cho mở nhanh.

### Fixed
- **BOM Deletion Sync Bug**: Khắc phục triệt để lỗi khi người dùng xóa dòng vách Tender mà Canvas lưới không tự động xóa CAD block. Tính năng Cleanup CAD Artifacts đã dọn sạch các handle cũ và clear Canvas chính xác.
- **Pick Dài Dimension Reset**: Sửa lỗi Panel width/height tính sai khi dựng "Pick dài" bằng cách chuẩn hoá Unit Coordinate và áp dụng Vector quay (Rotate) đúng ma trận điểm góc.

## [2026-02-27] - MVP Milestone 🚀
### Added
- **Phase 05: AutoCAD Drawing**: `BlockManager` for drawing panels, hatches, and tags.
- **Phase 06: Waste Match**: `WasteMatcher` for finding remnants in SQLite DB.
- **Phase 07: UI Dialogs**: Professional WPF dialogs (`WallCreateDialog`, `WasteSuggestionDialog`, `SpecManagerDialog`) implemented in pure C# for max compatibility.
- **Phase 08: BOM & Commands**: 
    - `BomManager`: Live AutoCAD Table for panel statistics.
    - `ShopDrawingCommands`: `SD_WALL_CREATE`, `SD_SPEC`, `SD_BOM`, `SD_WASTE`.
- **Database**: Initialized SQLite schema for waste panels.

### Changed
- **UI Architecture**: Moved from XAML to Programmatic C# to bypass build issues in restricted environments.
- **Target Framework**: Verified .NET 8.0 support for AutoCAD 2026.

### Fixed
- Resolved ambiguity errors between `ShopDrawing.Models.Panel` and `System.Windows.Controls.Panel`.
- Fixed `AttributeReference` initialization bugs in `BlockManager`.
- Modernized `Table` API usage in `BomManager` (Cells vs SetTextString).

### Refactored
- Added robust error handling (try-catch) to all commands and static reactors.
- Improved null-safety and modernized coding patterns across the plugin.
