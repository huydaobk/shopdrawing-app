# Changelog - ShopDrawing AutoCAD Plugin

## [2026-04-20] - v0.2.31 Rollback Release ⏪
### Fixed
- **System Stability**: Tự động rollback hệ thống về trạng thái ổn định nhất (tương đương với mã nguồn phiên bản v0.2.27). Tính năng Manual CUI đang trong quá trình hoàn thiện sẽ được tiếp tục phát triển ở một nhánh riêng biệt, đảm bảo không ảnh hưởng đến trải nghiệm người dùng hiện tại trên phiên bản chính thức. Phiên bản này được phát hành để máy của người dùng tự động hạ cấp từ các phiên bản lỗi.


## [2026-04-20] - v0.2.27 Tender Polygon Waste & Preview UI 🚀
### Changed
- **Tender Waste Logic**: Tối ưu logic tính toán khối lượng hao hụt cho vách "Pick Vùng" (Polygon) đồng nhất với Pick vách. Bắt chính xác lượng "Hao hụt (Lỗ mở)" (Grazing Waste) do lỗ cắt lẹm 1 phần, và sửa lỗi tính mẩu vụn tấm cuối không chính xác khi dải tấm bị chia cắt.
- **Tender CAD Preview**: Tối ưu đường ghép tấm trên lưới CAD/WPF, tự động loại bỏ các đoạn thẳng đi xuyên qua không gian lỗ mở, giúp bản vẽ mô phỏng trực quan hơn.

## [2026-04-20] - v0.2.26 Tender Excel Export Synchronization 📊
### Fixed
- **Excel Reference Bug**: Cập nhật lại logic xuất báo cáo Excel cho chức năng Tender, đảm bảo tham chiếu chỉ số chính xác ở phần tổng (Tổng diện tích dự kiến cấp và Khối lượng hao hụt) đến đúng các cột dữ liệu thay vì cột số lượng tấm.


## [2026-04-20] - v0.2.25 Tender Opening CAD Projection Fix 🔧
### Fixed
- **CAD Projection**: Sửa lỗi sai tọa độ lỗ mở khi bấm "Vẽ CAD" trong chế độ "Pick Dài" bằng cách bắt tuân thủ tọa độ chuẩn Unroll thay vì tọa độ chuột tuyệt đối.

## [2026-04-20] - v0.2.24 Tender Project Folder Structure 📁
### Changed
- Cập nhật cấu trúc thư mục dữ liệu dự án từ `ShopDrawingData` sang `Project Data` để bao quát hơn (chứa Tender, Shopdrawing, Production).
- Tự động gom các file Excel xuất BOM vào thư mục `BOQ` / `Tender` tương ứng.
- Khắc phục lỗi test case và đảm bảo file `Project Data` marker được tạo thành công trong môi trường runtime.

## [2026-04-20] - v0.2.23 Tender Net Area Relabeling 🏷️
### Changed
- **UI Label Update**: Đổi tên cột tiêu đề `DT net (m²)` thành `DT nghiệm thu (m²)` trong form xuất báo cáo Excel BOM để trở nên trực quan và dễ hiểu hơn đối với nghiệp vụ nghiệm thu công trường, giữ nguyên tính đúng đắn của logic tính toán.

## [2026-04-18] - v0.2.22 Panel Splitting Optimization ✂️
### Added
- **Tính năng mới**: Tối ưu tự động chia tấm (splitting panel) khi đi qua lỗ mở "nguyên khổ". `ScanLineAnalyzer` hiện tại đã tính toán chính xác để phân tách đoạn trên và dưới, và nhảy nhịp (skip) ở vùng đi qua lỗ mở.
- **Cải thiện UI**: Đường chia nhịp trên giao diện xem trước (CAD preview) tự động không đi xuyên qua không gian lỗ mở.

## [2026-04-18] - v0.2.21 Tender Excel Geometric Area Export 📊
### Changed
- **Tender UI Optimization** - Đồng bộ hóa giá trị diện tích hình học thực tế (từ Pick Vùng/Pick Dài) hiển thị trực tiếp vào file Excel xuất BOM thay vì áp dụng công thức Dài x Rộng cũ, giúp đồng nhất dữ liệu hiển thị (source of truth) giữa AutoCAD, App và Excel. Ghi chú tại Excel cũng cập nhật thông báo rõ gốc trích xuất từ mô hình CAD.
- **Tender Opening Logic** - Cột diện tích lỗ mở trong Excel được trả lại form công thức gốc `Rộng * Cao * SL / 1000000` để người dùng có thể linh hoạt nhập tay hoặc tinh chỉnh các tham số nếu cần thiết mà không bị ảnh hưởng.

## [2026-04-18] - v0.2.20 Tender UI Dimension Locking 🔒
### Changed
- **Tender UI Optimization**: Các cột kích thước trong bảng Quản lý khối lượng chào giá bao gồm: Dài, Cao (của vách) và Rộng, Cao, Cao độ đáy (của lỗ mở) đã được khóa lại (read-only). Dữ liệu này được lấy trực tiếp từ việc bắt điểm/dựng hình trích xuất từ CAD. Việc khóa lại để đảm bảo tính đồng nhất dữ liệu và bảo toàn "nguồn sự thật" từ mô hình (tránh việc sửa tay nhầm trên DataGrid).

## [2026-04-18] - v0.2.19 Tender Data Persistence Fix 💾
### Fixed
- **Tender Data Loss**: Khắc phục triệt để lỗi thỉnh thoảng mất dữ liệu Tender khi tắt bật Autocad. Đã hook trực tiếp vào sự kiện `Database.SaveComplete` nguyên thủy của hạ tầng CAD. Mỗi khi người dùng bấm Save hoặc Save As, tệp tin dữ liệu dự án (`.json`) sẽ tự động được đồng bộ lưu ngay lập tức bên cạnh thư mục chứa tệp `dwg` mới nhất, đảm bảo tính bền vững (persistence) của dữ liệu.

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
