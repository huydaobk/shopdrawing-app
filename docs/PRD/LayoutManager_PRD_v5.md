# Layout Manager PRD v5

## Mục tiêu

`LayoutManager` tạo nhanh layout shopdrawing cho mặt đứng vách từ vùng model user chọn, đồng thời chuẩn hóa:

- Khổ giấy A3/A2/A1 landscape
- Khung bản vẽ TCVN với title block cao 55mm
- Viewport nằm gọn trong khung bản vẽ
- View title, scale, revision, số bản vẽ, DMBD
- Page setup sẵn sàng cho bước export PDF hàng loạt về sau

## Quyết định đã chốt

| # | Nội dung | Quyết định |
|---|---|---|
| Q1 | Khổ giấy | User chọn `A3` / `A2` / `A1` |
| Q2 | Nhiều khổ | Có |
| Q3 | Hướng giấy | Luôn `Landscape` |
| Q4 | Margin | Default TCVN: trái `25mm`, còn lại `5mm`, user chỉnh được |
| Q5 | Title block | Built-in + external DWG |
| Q6 | Overlap phân trang | `1000mm` trong model |
| Q7 | Tên layout tab | `MẶT ĐỨNG VÁCH - <title>` và phải sanitize hợp lệ với AutoCAD |
| Q8 | View title | User nhập tay, cho phép nhiều vách trên 1 layout |
| Q9 | Scale | Lấy từ `CANNOSCALE`, lưu metadata dạng `1:n` |
| Q10 | Metadata title block | Tên dự án, tên bản vẽ, số BV, tỷ lệ, revision, ngày, nội dung revision |
| Q11 | Drawing List | AutoCAD `Table` ở layout `00-DMBD` |
| Q12 | Revision | Auto `A -> B -> C`, popup nhập nội dung |
| Q13 | Số bản vẽ | `SD-001`, tăng trong file hiện hành |
| Q14 | External block | Mapping attribute qua UI, lưu NOD |
| Q15 | Auto-sync DMBD | Có, khi thêm/sửa/xóa layout |
| Q16 | Page setup | Phải tạo và lưu cùng layout để phục vụ export |
| Q17 | Viewport border | Nằm trên layer riêng `SD_VIEWPORT`, không plot |
| Q18 | Khung bản vẽ | Phải có outer frame đầy đủ trong paper space |

## Paper Settings

| Khổ | Kích thước giấy (mm) | Usable width | Usable height cho viewport |
|---|---:|---:|---:|
| A3 | 420 x 297 | 390 | 232 |
| A2 | 594 x 420 | 564 | 355 |
| A1 | 841 x 594 | 811 | 529 |

Ghi chú:

- `usable width = paper width - margin trái - margin phải`
- `usable height = paper height - margin trên - margin dưới - title block 55mm`
- Toàn bộ layout tạo theo paper space đơn vị `mm`

## Bố cục layout

### 1. Outer frame

- Vẽ outer frame trong paper space từ:
  - `x = margin trái`
  - `y = margin dưới`
  - `x = paper width - margin phải`
  - `y = paper height - margin trên`
- Outer frame là biên chuẩn của tờ bản vẽ.

### 2. Title block

- Title block đặt ngang phía dưới, cao cố định `55mm`
- Bám full chiều rộng hữu dụng của khung bản vẽ
- Built-in title block tuân theo TCVN option A
- Nếu dùng external title block:
  - insert block vào paper space
  - fill attribute theo mapping đã lưu
  - nếu file hoặc mapping lỗi thì fallback về built-in

### 3. Viewport area

- Viewport chỉ được nằm trong vùng phía trên title block
- Viewport rectangle:
  - trái = margin trái
  - phải = paper width - margin phải
  - dưới = margin dưới + 55
  - trên = paper height - margin trên
- Không để viewport tràn ra ngoài outer frame
- Border viewport đặt trên layer `SD_VIEWPORT`
- Layer `SD_VIEWPORT` phải `no-plot`

### 4. View title

- Vẽ 2 dòng text trong paper space, căn trái:
  - dòng 1: tên bản vẽ
  - dòng 2: `TỶ LỆ 1:n`
- Vị trí: ngay dưới viewport thực tế (`vpBottom - 7mm`)
- Chạy theo viewport, không cố định vị trí
- Font:
  - title: `3.5mm`
  - scale: `2.5mm`

### 5. Multi-viewport (v5.2 — Planned)

Cho phép đặt nhiều viewport trên cùng 1 layout.

**Flow:**

1. User chọn vùng model → Engine tính viewport size theo scale
2. Scan tất cả layout SD đã có → Tìm layout nào còn đủ chỗ
3. Nếu tìm thấy:
   - Hiện dialog: *"Layout 'W1' còn chỗ, ghép vào đây?"*
   - `[Đồng ý]` → Đặt viewport vào layout đó
   - `[Tạo mới]` → Tạo layout mới (mặc định)
4. Nếu không tìm thấy → Tạo layout mới

**Quy tắc xếp viewport:**

- Ưu tiên sang **phải** trước (cùng hàng)
- Nếu không đủ chiều rộng → xếp **xuống dưới** (hàng mới)
- Mỗi viewport có view title riêng
- Cho phép viewport có **tỷ lệ khác nhau** trên cùng 1 layout
- Khoảng cách giữa các viewport: `10mm`

**Auto-naming (khi nhiều mã tường):**

- Tên bản vẽ tự ghép mã tường: `MẶT ĐỨNG VÁCH - W1, W2, W3`
- Layout tab name đổi theo: `SD-W1` → `SD-W1-W2` → `SD-W1-W2-W3`
- Cùng mã tường nhiều phần (paging) chỉ ghi **1 lần** (không lặp)
- Khi **xóa viewport**, tên tự cập nhật lại (bỏ mã đã xóa)
- DMBD (danh mục bản vẽ) **auto-sync** theo tên mới

## Paging + Auto-arrange (v5.2 — Planned)

Khi viewport quá dài/rộng cho usable area, tự động ngắt thành nhiều phần:

- User pick 2 góc vùng model
- Engine chia trang theo chiều ưu tiên:
  - nếu vùng rộng hơn cao thì phân theo `X`
  - ngược lại phân theo `Y`
- Mỗi page giữ overlap `1000mm` (model space)

### Auto-arrange flow

1. Tính tất cả viewport parts sau khi split
2. Xếp vào layout hiện tại theo quy tắc Multi-viewport (phải → dưới)
3. Nếu hết chỗ trên layout hiện tại:
   - Hiện dialog: *"Cần thêm N layout. Tạo tự động?"*
   - `[OK]` → Tạo thêm layout mới + tiếp tục xếp
   - `[Hủy]` → Dừng, giữ nguyên các phần đã xếp

### View title cho paging

- Phần duy nhất: `MẶT ĐỨNG VÁCH - W1`
- Nhiều phần: `MẶT ĐỨNG VÁCH - W1 (PHẦN 1/3)`, `... (PHẦN 2/3)`
- Layout tab name phải sanitize bỏ ký tự cấm như `/`, `:`, `?`, `*`, `|`

## Metadata và Drawing List

### Layout metadata

Mỗi layout lưu metadata trong NOD:

- `SoBanVe`
- `ProjectName`
- `TyLe`
- `Ngay`
- `Revision`
- `GhiChu`

### Drawing List

- Tạo tại layout `00-DMBD`
- Là `AutoCAD Table`
- **Tab order:** DMBD luôn nằm đầu tiên (`TabOrder = 1`, ngay sau Model Space)
- **Page setup:** Áp dụng page setup A3 Landscape giống các layout shopdrawing (chỉ khi tạo mới)
- Mỗi layout manager tạo ra tương ứng 1 dòng
- Cột:
  - `STT`
  - `Tên bản vẽ`
  - `Số BV`
  - `Tỷ lệ`
  - `Ngày`
  - `REV`
  - `Ghi chú`
- Khi xóa layout:
  - xóa metadata
  - sync lại DMBD
  - renumber `STT`

## External Title Block

### Lần đầu cấu hình

1. User chọn file `.dwg`
2. Plugin đọc block/attributes khả dụng
3. Hiện mapping UI:
   - tag khách hàng -> field plugin
4. User xác nhận
5. Lưu config + mapping vào NOD của bản vẽ hiện tại

### Các lần sau

- Plugin đọc config trong NOD
- Insert đúng block source đã chọn
- Fill attribute theo metadata layout hiện tại

## Page Setup

Đây là phần bắt buộc để phục vụ export về sau.

Mỗi layout tạo mới phải tự gắn page setup tối thiểu:

- Plot device mặc định: `DWG To PDF.pc3`
- Paper size đúng với `A3` / `A2` / `A1`
- Paper units: `Millimeters`
- Plot type: `Layout`
- Plot rotation: `Landscape` (`Degrees090`)
- Standard scale: `Scale to Fit`
- Plot centered: `true`

### API Implementation (v5.1)

```csharp
// Dùng FindMediaName để iterate GetCanonicalMediaNameList
// → Tìm media name chứa "A3" + ưu tiên "full_bleed"
// → Không dùng SetClosestMediaName (buggy trong AutoCAD .NET API)
string? media = FindMediaName(plotSettings, validator, paperSize);
validator.SetPlotConfigurationName(ps, device, media);
validator.SetPlotRotation(ps, PlotRotation.Degrees090); // Luôn Landscape
```

### Viewport Positioning (v5.1)

- Viewport căn **góc trên-trái** vùng usable (không căn giữa)
- Viewport bị giới hạn không vượt quá diện tích usable
- View title (tên + tỷ lệ) nằm ngay **dưới viewport thực tế**, không cố định

Yêu cầu bổ sung:

- Page setup phải được lưu vào chính `Layout` object, không chỉ set tạm bằng system variables
- Export batch PDF về sau phải có thể dùng trực tiếp page setup này mà không cần hỏi lại user
- Nếu PC không có device/media tương ứng, plugin phải fallback an toàn và log warning cho user

## Export readiness

`LayoutManager` chưa cần triển khai command export trong PRD này, nhưng layout tạo ra phải sẵn sàng cho export:

- paper size đúng
- orientation đúng
- viewport nằm trong frame
- viewport border không plot
- title block và view title nằm đúng vị trí
- metadata đủ để tạo tên file PDF sau này

Đề xuất naming cho bước export sau:

- `SD-001_MAT-DUNG-VACH-W1.pdf`
- `SD-002_MAT-DUNG-VACH-W1_PHAN-1-3.pdf`

## UX Palette

Palette cần có:

- chọn khổ giấy
- nhập margin
- hiển thị scale theo `CANNOSCALE`
- nhập tên dự án
- chọn built-in hoặc external title block
- chọn `.dwg` external
- mở mapping dialog
- nhập title
- nút `Chọn vùng & Tạo Layout`
- list layout đã tạo
- xóa layout
- sync DMBD

## Kiến trúc code

### `LayoutManagerEngine.cs`

Chịu trách nhiệm:

- đọc scale
- pick region
- compute pages
- dựng geometry paper/frame/viewport
- tạo layout
- apply page setup
- tạo viewport
- dựng view title
- dựng built-in title block hoặc external title block

### `DrawingListManager.cs`

Chịu trách nhiệm:

- lưu metadata layout
- cấp số bản vẽ
- tạo/sync DMBD
- đọc project name từ layout metadata

### `LayoutTitleBlockConfigStore.cs`

Chịu trách nhiệm:

- lưu config external title block
- khám phá block source/attribute tags
- import block definition
- insert và fill attributes

## Acceptance Criteria

### AC1. Tạo layout 1 trang

- User chọn vùng vừa 1 trang
- Plugin tạo 1 layout
- Có outer frame đầy đủ
- Có title block
- Có viewport đúng vị trí
- Viewport hiển thị đúng vùng model đã chọn

### AC2. Tạo layout nhiều trang

- User chọn vùng dài
- Plugin tạo nhiều layout
- View title có suffix `(PHẦN x/n)`
- Layout tab name hợp lệ, không lỗi `eInvalidInput`

### AC3. Page setup

- Layout sau khi tạo đã có page setup tương ứng với khổ giấy
- Có thể plot/export mà không phải set lại khổ giấy thủ công

### AC4. DMBD

- Tạo layout mới thì DMBD có dòng mới
- Xóa layout thì DMBD mất dòng tương ứng
- `STT` và `Số BV` nhất quán

### AC5. External title block

- Mapping đúng thì attribute được fill
- Mapping lỗi hoặc DWG lỗi thì fallback built-in

## Ghi chú triển khai

- Không dùng view title raw làm layout tab name
- Không để viewport geometry phụ thuộc vào page setup mặc định của AutoCAD
- Mọi layout mới phải tự normalize environment paper space để user kiểm tra trực quan dễ hơn
