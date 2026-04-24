# Changelog - ShopDrawing AutoCAD Plugin

## [2026-04-24] - v0.2.49 Shopdrawing Excel BOM Synchronization 📊
### Changed
- **Excel Export**: Nâng cấp module xuất file báo cáo Excel (Lệnh Sản Xuất, Quản lý Spec, Đặt hàng Phụ kiện). Đồng bộ hoàn toàn logic tính toán hao hụt `WasteFactor` từ giao diện UI vào công thức tính khối lượng phụ kiện.
- **Accessory Calculation**: Bổ sung hàm làm tròn lên (`Math.Ceiling`) cho mọi đơn vị phụ kiện (cây, hộp, con) để xuất dữ liệu nguyên trên BOM, chống sai số khi đặt hàng.
- **Excel Formatting**: Khôi phục chuẩn định dạng Quản lý Spec (19 cột). Bổ sung thiết lập tự động chèn công thức Excel (`=SUM`, `=`...) cho các dòng TỔNG để tính tổng số lượng/diện tích. Thiết lập in ấn A4 chuẩn cho tất cả các Sheet.
- **BOM Priority Integration**: Liên kết cột Ưu tiên trên Sheet Lệnh Sản Xuất thẳng vào `BatchNo` (Đợt giao hàng) được gán trên CAD, tự động đồng bộ khi xuất Excel.

## [2026-04-23] - v0.2.48 Real-time BOM Auto-Refresh on CAD Edit 🔄
### Fixed
- **BOM Realtime Sync**: Hook vào sự kiện `Document.CommandEnded` của AutoCAD để tự động gọi `NotifyWasteUpdated()` ngay sau khi người dùng thực thi các lệnh CAD thay đổi đối tượng (ERASE, UNDO, REDO, MOVE, COPY, ROTATE, SCALE, STRETCH, GRIP_STRETCH). Bảng "Phụ Kiện" trong WasteManagerDialog sẽ tự cập nhật realtime mà không cần thao tác thủ công.

## [2026-04-23] - v0.2.47 Ceiling Hanger Interactive Selection ☂️
### Changed
- **Ceiling Hanger UX**: Nâng cấp lệnh "Pick điểm treo thanh T" và "Pick điểm treo bulong nấm". Bổ sung Popup chọn Hạng mục ứng dụng (Ngoài nhà, Phòng sạch, Kho lạnh) trước khi pick điểm, tương tự như lệnh Pick góc.
- **BOM Logic**: Tách biệt logic bóc tách phụ kiện điểm treo trần theo từng Hạng mục ứng dụng được người dùng chỉ định.
- **BOM UI**: Thêm logic cập nhật bảng thống kê BOM realtime (tự động load lại danh sách) ngay sau khi người dùng Pick xong điểm treo.

## [2026-04-23] - v0.2.46 Auto-refresh BOM upon Deletion ♻️
### Fixed
- **BOM Logic**: Sửa lỗi `eWasErased` gây crash ngầm khi xóa marker (góc/tường) trên CAD và tính toán lại BOM.
- **BOM UI**: Thêm logic cập nhật bảng thống kê BOM realtime (tự động load lại danh sách) ngay sau khi người dùng xóa (ERASE) marker ngoài giao diện CAD.

## [2026-04-23] - v0.2.45 Corner Marker Interactive Selection 🏗️
### Changed
- **Corner Marker UX**: Nâng cấp lệnh "Pick góc" trên giao diện Palette. Thay vì ngầm định chèn góc theo hạng mục mặc định của dự án, hệ thống sẽ hiển thị một Popup nhỏ gọn cho phép người dùng chủ động chọn Hạng mục ứng dụng (Ngoài nhà, Phòng sạch, Kho lạnh) ngay trước khi pick điểm.
- Khối lượng phụ kiện đi kèm (V, Rive, Silicone, Foam) sẽ tự động được gán và chiết tính chuẩn xác theo loại hạng mục được chọn.

## [2026-04-22] - v0.2.44 Elevation Openings UX Polish 🛠️
### Fixed
- **Elevation Opening Keywords**: Đồng bộ từ khóa chọn loại lỗ mở mặt đứng `[cửa Đi(D)/cửa Sổ(S)]` giống với mặt bằng, tránh lỗi nhận nhầm thành Cửa đi do sai cú pháp.
### Changed
- **Elevation UX**: Bổ sung hiển thị `(Rộng xxx)` vào các câu nhắc nhập liệu chiều cao và Sill để người dùng dễ theo dõi.
- **Elevation Base Point**: Đổi gốc tọa độ đo khoảng cách (Base Point) sang góc bên phải (`Điểm 2`), hỗ trợ người dùng thao tác vuốt chuột chữ U (Trái -> Phải -> Lên -> Xuống) mượt mà hơn.
## [2026-04-22] - v0.2.43 Elevation Openings & Grouping 🚀
### Changed
- **Elevation Opening UX**: Nâng cấp tính năng vẽ lỗ mở trên mặt đứng. Thay thế việc chọn polyline vẽ sẵn bằng thao tác pick 2 điểm trực tiếp để xác định chiều rộng, đồng thời nhập khoảng cách chiều cao và cao đáy (Sill height) tương tự như mặt bằng. Tự động set Sill = 0 cho Cửa Đi.
- **Palette UI Cleanup**: Loại bỏ hoàn toàn checkbox "Cắt lỗ (Openings)?" trên UI Palette để giảm bớt sự rườm rà. Lệnh `SD_WALL_ELEVATION` giờ đây luôn hỏi cắt lỗ, nhưng người dùng có thể dễ dàng nhấn `Enter` hoặc chuột phải để bỏ qua.
### Added
- **Elevation CAD Grouping**: Toàn bộ kết quả vẽ của một mặt đứng (tấm vách, lỗ mở, hardware trần) giờ đây được tự động nhóm lại thành một AutoCAD `Group` duy nhất sau khi lệnh `SD_WALL_ELEVATION` hoàn tất. Việc này giúp thao tác chọn và di chuyển toàn bộ mảng vách trên mặt đứng dễ dàng hơn rất nhiều.

## [2026-04-22] - v0.2.42 Automated CAD Grouping 📦
### Added
- **AutoCAD CAD Grouping**: Tự động gộp tất cả các đối tượng sinh ra từ lệnh vẽ "Tạo tường mặt bằng" (`SD_WALL_PLAN_QUICK`) bao gồm các đoạn line trên mặt bằng, text Ký hiệu/Spec của tường và toàn bộ mặt dựng 3D (panel, hatch, đường nối, tag) vào một AutoCAD `Group` duy nhất.
- Group được đặt tên ẩn tự động để tránh trùng lặp. Tính năng này giúp thao tác chọn, di chuyển toàn bộ mảng tường trên CAD trở nên dễ dàng và đồng bộ thông qua `PICKSTYLE` (Ctrl+Shift+A).

## [2026-04-22] - v0.2.41 Dynamic Openings Separation 🚪
### Changed
- **Opening Type UX**: Bỏ hoàn toàn combobox "Loại lỗ" chung trên UI Palette để tách triệt để cơ chế gán Cửa đi/Cửa sổ.
- **SD_WALL_PLAN_QUICK**: Tích hợp keyword prompt trực tiếp chọn loại lỗ mở `[cửa Đi(D)/cửa Sổ(S)]` vào trong luồng bắt điểm. Tự động bỏ qua câu hỏi Sill khi Cửa đi được lựa chọn (cho Sill = 0).
- **SD_PANEL_LAYOUT**: Tách làm 2 bước: Quét cửa đi (bước 1) và Quét cửa sổ (bước 2) để gán type tùy biến cho hàng loạt đối tượng hiệu quả hơn.

## [2026-04-22] - v0.2.40 Quick Plan Wall Upgrade & Visual Fixes 🚀
### Changed
- **Quick Plan Wall**: Nâng cấp tính năng `SD_WALL_PLAN_QUICK` hỗ trợ chế độ pick dải nhiều đoạn liên tục. Cho phép nhập chiều cao riêng cho từng đoạn. Lỗ mở được ánh xạ chuẩn xác xuống mặt bằng polyline bằng thuật toán hình học đường cong (`GetClosestPointTo` / `GetDistAtPoint`).
- **Highlight Visual**: Loại bỏ `ConstantWidth` của viền highlight trong "Kho lẻ", khắc phục lỗi nét quá to che khuất tầm nhìn khi zoom.

## [2026-04-22] - v0.2.39 Vietnamese Font Encoding Fix 🔤
### Fixed
- **Font Mojibake – String Literals**: Sửa triệt để lỗi hiển thị ký tự lạ (mojibake) trong toàn bộ string literal tiếng Việt bị double-encode trong mã nguồn. Áp dụng phương pháp double-encode detection để thay thế chính xác tại byte-level, không làm ảnh hưởng các chuỗi đúng sẵn có.
  - `QuickPlanWallCommandService.cs` — 16 chuỗi thông báo AutoCAD command line (`WriteMessage`) cho lệnh `SD_WALL_PLAN_QUICK` (Lỗi, Tấm lẻ, Tìm khớp, Trần/Tường, v.v.)
  - `QuickPanelLayoutCommandService.cs` — 24 chuỗi thông báo cho lệnh `SD_PANEL_LAYOUT`/`SD_CEILING_QUICK` (Chọn vùng cắt, Phải là Polyline, Cửa sổ/LKT, v.v.)
  - `PlaceDetailCommandService.cs` — 4 chuỗi prompt/thông báo cho lệnh `SD_DETAIL`
  - `TenderBomDialog.cs` — ComboBox `"Dọc"/"Ngang"` (L2881) và ký tự nhân `×` trong parse kích thước (L3356)
  - `AccessoryDataManager.cs` — 2 chuỗi tên rivet `"Ø4.2×12"` dùng cho so sánh lọc phụ kiện (L545, L547)

## [2026-04-21] - v0.2.37 Enhanced UX for Openings and UI Labels 🔄
### Changed
- **Opening Selection UX**: Cải tiến hoàn toàn phần bắt lỗ mở cho tính năng "Tạo tường mặt bằng" (`_SD_WALL_PLAN_QUICK`). Thay thế phương pháp quét đối tượng (`PromptSelectionOptions`) trước đây bằng vòng lặp Pick 2 điểm lọt lòng (`PromptPointOptions`). Phương pháp mới không bị phụ thuộc vào chất lượng vẽ kiến trúc và tăng độ linh hoạt/chính xác tối đa.
- **UI Labels**: Đổi tên nút "Tạo tường mới" thành "Tạo tường mặt đứng" trên Palette chính giúp tường minh dễ hiểu hơn khi đặt cạnh "Tạo tường mặt bằng".

## [2026-04-21] - v0.2.36 Plan-Based Wall Layout (Pick Dài) 🚀
### Added
- **Plan-based Layout**: Thêm tính năng "Tạo tường theo mặt bằng" (`_SD_WALL_PLAN_QUICK`) cho phép vẽ tường bằng cách chọn các đường/điểm trên mặt bằng sàn và nhập chiều cao tổng. Tính năng sử dụng `QuickPlanWallCommandService` được thiết kế tương thích hoàn toàn với lõi tính toán hao hụt và LayoutEngine hiện có. Dễ dàng truy cập từ nút "Tạo tường mặt bằng" trên bảng Palette chính.

## [2026-04-21] - v0.2.35 Pick Vùng Remnant Waste Fix 🔧
### Fixed
- **Pick Vùng - Hao hụt tấm cuối**: Loại bỏ hoàn toàn lỗi xuất hiện tấm "Hao hụt 1100mm" giả tạo ở tấm cuối khi dùng chế độ Pick Vùng. Nguyên nhân gốc rễ là công thức `totalScanSpan - (totalStripes-1) * panelWidth` nhạy cảm với sai số float CAD; đã thay bằng cách trích xuất trực tiếp từ biến `stripeW` (đã clamp cứng theo `scanMax`) bên trong vòng lặp quét, đồng bộ 100% với cơ chế Pick Dài.
- **Code Quality**: Sửa toàn bộ 16 cảnh báo nullability (CS8602, CS8604, CS8620, CS8625) trên `ScanLineAnalyzer.cs`, `TenderBomDialog.cs`, `TenderBomDialog.CadOps.cs`. Build đạt 0 Warning, 0 Error.

## [2026-04-21] - v0.2.34 Step Waste Extraction Fix
### Fixed
- **Pick Vùng**: Fix step waste extraction logic cho chế độ Pick vùng.

## [2026-04-21] - v0.2.33 UI Refinements
### Changed
- **Tender Palette**: Cải thiện layout bảng thống kê, wrap text và Auto grid width.
### Fixed
- **Lỗ mở Pick Dài**: Sửa panel line trimming logic quanh lỗ mở cho Pick Dài.

## [2026-04-21] - v0.2.32
### Changed
- Cập nhật Git Ignore để bỏ qua các file test rác.

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
