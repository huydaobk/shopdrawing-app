# Changelog - ShopDrawing AutoCAD Plugin

## [2026-04-18] - v0.2.18 Tender Preview Text Overlay Fix 📐
### Changed
- **Tender UI Optimization** - Tăng kích thước nét chữ (text size) trong Preview (Vẽ CAD) cho kích thước panel và lỗ mở (vd: size tăng từ 70 -> 150).
- Căn chỉnh lại `Justify` và `Offset` cho Text trong AutoCAD Preview để số (dimension text) không bị đè dính gạch trực tiếp lên các nét red/green line của viền / mí nối (joints). Text giờ đây tự động dàn sáng ra mép ngoài dựa theo phương pháp gióng phải ngang/trái tương ứng.

## [2026-04-18] - v0.2.17 Immediate Preview on Pick Lỗ Mở ⚡
### Changed
- **Tender UI Optimization** - Khi user thực hiện thao tác (Pick Lỗ mở), khung Preview và bảng khối lượng (BOM) bên dưới sẽ cập nhật giao diện ngay lập tức thay vì phải chờ người dùng bấm Enter để thoát thao tác.

## [2026-04-18] - v0.2.16 Tender CAD "Pick Khoảng Cách Đáy" Revert 🐛
### Fixed
- **Tender Geometry Error for Openings** - Xóa bỏ lỗi dịch chuyển (shift) khoảng cách đáy lần 2 khi lưu `OpeningPolygon`. Khắc phục triệt để lỗ mở Floating khi vẽ CAD sau khi pick. (Các điểm pick p1, p2 bằng chuột trên màn hình đã ngầm định chứa cao độ thực tế rồi, không cần cộng thêm tham số BottomElevationMm vào tọa độ `OpeningPolygon` nữa).

## [2026-04-17] - v0.2.15 Tender CAD "Pick Vùng" Hole Fix 🐛

## [2026-04-17] - v0.2.14 Tender CAD "Pick Vùng" Fix 🐛
### Fixed
- **Tender Geometry Error for "Pick Vùng"** - Khắc phục lỗi khi chọn vách bằng "Pick vùng" (WallPolygon) với các đường bao hình đa giác không vuông góc (ví dụ vách mái dốc). Hệ thống không còn unroll tự động (trải phẳng) biên dạng trên bản vẽ CAD mà giữ nguyên hình dạng nguyên gốc (literal geometry offset) giúp đồng bộ tuyệt đối với hình ảnh trên Preview Canvas.
- **Tender CAD Openings Placement Offset** - Đã sửa lỗi tọa độ lỗ mở khi dựng bản vẽ CAD cho vách "Pick vùng", các lỗ mở bây giờ được offset hoàn toàn chuẩn xác theo biên dạng Polyline chính.

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
