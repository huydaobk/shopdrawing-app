# PRD - Xuất PDF (ExportPdf) v1.1

**Plugin:** Shop Drawing Plugin (AutoCAD .NET)  
**Command:** `SD_EXPORT`  
**Trạng thái:** Confirmed - Sẵn sàng code

---

## 1. Mục tiêu

Xuất toàn bộ layout tab của bản vẽ sang một file PDF nhiều trang trực tiếp từ palette.

Yêu cầu của v1:
- Mỗi layout là một trang PDF.
- Giữ khổ giấy theo từng layout.
- Cho phép chọn plotter, plot style, thư mục lưu và tên file.
- Hỏi xử lý khi file đích đã tồn tại.
- Có thể mở PDF sau khi xuất xong.

---

## 2. Phạm vi v1

| Hạng mục | v1 |
|---|---|
| Xuất tất cả layouts ra 1 PDF | ✅ |
| Mỗi layout = 1 trang | ✅ |
| Nhiều khổ giấy khác nhau theo layout | ✅ |
| Chọn thư mục + tên file | ✅ |
| Xử lý trùng file | ✅ |
| Bỏ qua layout trống | ✅ |
| Giữ thứ tự theo tab layout | ✅ |
| Chọn máy in (PC3) | ✅ |
| Chọn plot style (CTB) | ✅ |
| Mở PDF sau khi xong | ✅ |
| Tạo `SD_Black.ctb` | ✅ |
| Watermark | ❌ v2 |
| Chọn từng layout riêng lẻ | ❌ v2 |

---

## 3. Luồng người dùng

```text
[Ribbon] Xuất PDF
    ↓
[Palette SD_EXPORT]
    |- Máy in:   [DWG To PDF.pc3]
    |- PlotStyle:[SD_Black.ctb]
    |- Thư mục:  [C:\...\output] [...]
    |- Tên file: [W1-ShopDrg.pdf]
    |- [x] Mở PDF sau khi xong
    `- [Xuất PDF]
            ↓
    Kiểm tra file đích
    |- Chưa tồn tại -> xuất luôn
    `- Đã tồn tại -> [Ghi đè] [Tự thêm _v2] [Huỷ]
            ↓
    Duyệt từng layout theo tab order
    |- Layout trống -> bỏ qua
    `- Layout hợp lệ -> plot vào PDF
            ↓
    Xong -> thông báo thành công và mở file nếu người dùng bật tuỳ chọn
```

---

## 4. Plot Style - `SD_Black.ctb`

### 4.1 Lý do tạo mới

`monochrome.ctb` mặc định của AutoCAD không phản ánh tốt phân cấp nét của shop drawing.

`SD_Black.ctb` dùng hệ phân cấp nét theo ISO/DIN để:
- Giữ toàn bộ output màu đen.
- Tách rõ nét tường, panel, dim, text và hatch.
- Đồng bộ với mapping layer/màu của plugin.

### 4.2 Hệ phân cấp nét

| Mức nét | Lineweight | Mục đích |
|---|---:|---|
| Ultra thin | 0.09mm | Hatch nền |
| Very thin | 0.13mm | Chi tiết nhỏ |
| Thin | 0.18mm | Dim, text, annotation |
| Medium thin | 0.25mm | Đường phụ thông thường |
| Medium | 0.35mm | Outline panel, opening |
| Heavy | 0.50mm | Outline tường chính |
| Extra heavy | 0.70mm | Dự phòng cho section cut |

### 4.3 Mapping màu chính

| ACI | Vai trò | Lineweight |
|---|---|---:|
| 1 | Detail nhỏ | 0.13mm |
| 2 | General lines | 0.18mm |
| 3 | `Defpoints` | Không plot |
| 4 | `SD_DIM_PANEL`, elevation | 0.18mm |
| 6 | `SD_DIM_OPENING` | 0.18mm |
| 7 | Title/frame/text | 0.50 / 0.35 / 0.18mm |
| 30 | `SD_OPENING` outline | 0.35mm |
| 251 | `SD_WALL` | 0.50mm |
| 252 | Panel border | 0.35mm |
| 253 | Hatch filling | 0.09mm |
| 254 | Non-plot | 0.00mm |
| Còn lại | Safe default | 0.18mm |

> Tất cả màu đều in đen.

### 4.4 Cài đặt

Nguồn trong repo:

```text
resources/plotstyles/SD_Black.ctb
```

Đường dẫn cài vào AutoCAD:

```text
%APPDATA%\Autodesk\AutoCAD 2026\R25.1\enu\Plotters\Plot Styles\SD_Black.ctb
```

`PlotStyleInstaller.cs` chịu trách nhiệm copy file vào thư mục Plot Styles khi plugin load.

---

## 5. Logic kỹ thuật

### 5.1 Plotter list

Quét danh sách PC3 có sẵn và hiển thị trong palette. Giá trị mặc định ưu tiên `DWG To PDF.pc3`.

### 5.2 Plot style list

Quét danh sách CTB, ưu tiên `SD_Black.ctb`, fallback về style mặc định nếu cần.

### 5.3 Giữ media theo layout

Khi export, engine giữ `CanonicalMediaName` hiện có trên layout và chỉ thay plotter + plot style.

Nếu media hiện tại không hợp lệ với plotter đã chọn thì fallback sang `SetPlotConfigurationName(..., null)`.

### 5.4 Multi-sheet PDF

Engine dùng publish engine của AutoCAD để plot nhiều sheet vào cùng một file PDF.

### 5.5 Xử lý trùng file

Nếu file đã tồn tại:
- `Yes` = ghi đè.
- `No` = tạo tên mới `_v2`, `_v3`, ...
- `Cancel` = huỷ export.

### 5.6 Thread safety

- Plot chạy trên main CAD thread.
- Palette chỉ gửi command wrapper và nhận status cập nhật lại từ wrapper.

---

## 6. UI palette

Các control chính:
- ComboBox máy in.
- ComboBox plot style.
- TextBox thư mục xuất + nút browse.
- TextBox tên file PDF.
- CheckBox mở PDF sau khi hoàn tất.
- Button `Xuất PDF`.
- TextBlock trạng thái.

Trạng thái cần hiển thị:
- Chuẩn bị xuất.
- Đang xuất layout `i/n`.
- Thành công.
- Lỗi hoặc huỷ.

---

## 7. Xử lý lỗi

| Trường hợp | Xử lý |
|---|---|
| Không có layout hợp lệ | Báo lỗi rõ ràng |
| File PDF đang bị mở | Yêu cầu đóng file trước khi xuất |
| Người dùng huỷ plot | Huỷ export và xoá file dở dang |
| Layout trống | Bỏ qua |
| Plot engine lỗi | Log lỗi, hiện thông báo, không crash plugin |

---

## 8. Files liên quan

| File | Vai trò |
|---|---|
| `ShopDrawing.Plugin/Core/PdfExportEngine.cs` | Engine plot PDF |
| `ShopDrawing.Plugin/UI/ExportPdfPaletteControl.xaml` | UI palette |
| `ShopDrawing.Plugin/UI/ExportPdfPaletteControl.xaml.cs` | Validate input, gửi request, nhận progress |
| `ShopDrawing.Plugin/Modules/Export/ExportModuleFacade.cs` | Wrapper command và orchestration |
| `ShopDrawing.Plugin/Commands/ExportPdfCommandGroup.cs` | Command entry points |
| `ShopDrawing.Plugin/Core/PlotStyleInstaller.cs` | Cài `SD_Black.ctb` |

---

## 9. Kiểm thử

| Test | Expected |
|---|---|
| DWG có nhiều layout với khổ giấy khác nhau | PDF nhiều trang, đúng thứ tự, đúng khổ |
| Chỉ có Model | Báo không có layout hợp lệ |
| File PDF đã tồn tại | Hiện dialog xử lý trùng file |
| Chọn auto-version | Tạo `_v2`, `_v3`, ... |
| Huỷ giữa chừng | Không để lại file PDF dở dang |
| Có layout trống xen giữa | Layout trống bị bỏ qua, layout còn lại vẫn xuất |
