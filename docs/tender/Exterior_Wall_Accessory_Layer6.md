# Lớp 6 - Mapping Phụ Kiện Cho Vách + Ngoài Nhà

Tài liệu này chốt riêng nhánh `Vách + Ngoài nhà` theo đúng logic Tender hiện tại.

Ảnh minh họa đi kèm:

`C:\my_project\shopdrawing-app\artifacts\facade_wall_layer6.png`

## 1. Mục tiêu

- Tách rõ từng vùng trên mặt đứng vách.
- Chỉ ra phụ kiện nào ăn vào từng vùng.
- Chỉ ra công thức tính đang dùng trong Tender.
- Chỉ ra dữ liệu đầu vào nào điều khiển khối lượng.

## 2. Điều kiện áp dụng

Một dòng phụ kiện chỉ được tính trong lớp này khi đồng thời đúng:

- `Category = Vách`
- `Application = Ngoài nhà`

Riêng ô `Khe đứng` chỉ sáng và cho nhập khi đúng 2 điều kiện trên.

## 3. Nhóm dữ liệu đầu vào cần hiểu

Các giá trị chính đang điều khiển lớp này:

- `Chiều dài vách`
- `Chiều cao vách`
- `Khổ rộng panel`
- `Lộ trên`
- `Lộ dưới`
- `Đầu lộ`
- `Cuối lộ`
- `Góc ngoài`
- `Khe đứng`
- `Cửa đi`
- `Cửa sổ / lỗ kỹ thuật`

Các giá trị này được quy đổi thành các cơ sở tính như:

- `TopEdgeLength`
- `BottomEdgeLength`
- `ExposedEndLength`
- `OutsideCornerHeight`
- `JointLength`
- `VerticalJointTotalLength`
- `DoorOpeningPerimeter`
- `NonDoorOpeningPerimeter`

## 4. Mapping theo vùng

| Vùng trên mặt đứng | Phụ kiện | Vật liệu | Đơn vị | CalcRule | Công thức hiểu nhanh | Dữ liệu điều khiển |
|---|---|---|---|---|---|---|
| Lộ trên | Diềm 01 | Tole | md | `PER_TOP_EDGE_LENGTH` | lấy tổng chiều dài cạnh trên lộ thiên | `Lộ trên` |
| Lộ trên | Sealant MS-617 | Sealant | chai | cấu hình theo md/chai nếu quy đổi, hiện dữ liệu gốc bám tuyến | trám kín mép trên lộ thiên | `Lộ trên` |
| Lộ trên | B2S-TEK 15-15×20 HWFS | Thép | cái | `PER_TOP_EDGE_LENGTH` | `cạnh trên (md) x 6 cái/md` | `Lộ trên` |
| Lộ dưới / chân vách | Diềm 02 | Tole | md | `PER_BOTTOM_EDGE_LENGTH` | lấy tổng chiều dài chân vách lộ thiên | `Lộ dưới` |
| Lộ dưới / chân vách | Sealant MS-617 | Sealant | chai | cấu hình theo md/chai nếu quy đổi, hiện dữ liệu gốc bám tuyến | trám kín chân vách | `Lộ dưới` |
| Lộ dưới / chân vách | Bát thép chân panel | Thép | cái | `PER_PANEL_SUPPORT_BRACKET_QTY` | `ceil(chiều dài vách / 1500)` | `Chiều dài vách` |
| Lộ dưới / chân vách | B2S-TEK 15-15×20 HWFS | Thép | cái | `PER_PANEL_SUPPORT_BRACKET_QTY` | `2 vít / 1 bát thép` | `Chiều dài vách` |
| Đầu lộ / cuối lộ | Diềm 03 | Tole | md | `PER_EXPOSED_END_LENGTH` | lấy tổng 2 cạnh biên hở | `Đầu lộ`, `Cuối lộ` |
| Đầu lộ / cuối lộ | Sealant MS-617 | Sealant | chai | cấu hình theo md/chai nếu quy đổi, hiện dữ liệu gốc bám tuyến | trám kín các cạnh biên hở | `Đầu lộ`, `Cuối lộ` |
| Đầu lộ / cuối lộ | B2S-TEK 15-15×20 HWFS | Thép | cái | `PER_EXPOSED_END_LENGTH` | `tổng cạnh biên hở (md) x 6 cái/md` | `Đầu lộ`, `Cuối lộ` |
| Góc ngoài | Diềm 04 | Tole | md | `PER_OUTSIDE_CORNER_HEIGHT` | `số góc ngoài x chiều cao vách` | `Góc ngoài`, `Chiều cao vách` |
| Góc ngoài | B2S-TEK 15-15×20 HWFS | Thép | cái | `PER_OUTSIDE_CORNER_HEIGHT` | `chiều dài góc ngoài (md) x 6 cái/md` | `Góc ngoài`, `Chiều cao vách` |
| Khe nối giữa các tấm | Diềm 05 | Tole | md | `PER_JOINT_LENGTH` | `(số tấm - 1) x span` | `Khổ rộng panel`, `Chiều dài vách`, `Chiều cao vách`, hướng chia tấm |
| Khe nối giữa các tấm | Sealant MS-617 | Sealant | chai | cấu hình theo md/chai nếu quy đổi, hiện dữ liệu gốc bám tuyến | trám kín tuyến khe nối panel | `Khổ rộng panel`, `Chiều dài vách`, `Chiều cao vách`, hướng chia tấm |
| Khe nối giữa các tấm | Băng keo butyl | Butyl | md | `PER_JOINT_LENGTH` | theo tổng chiều dài khe nối panel | `Khổ rộng panel`, `Chiều dài vách`, `Chiều cao vách`, hướng chia tấm |
| Khe nối giữa các tấm | B2S-TEK 15-15×20 HWFS | Thép | cái | `PER_JOINT_LENGTH` | `tổng khe nối (md) x 6 cái/md` | `Khổ rộng panel`, `Chiều dài vách`, `Chiều cao vách`, hướng chia tấm |
| Khe đứng / Omega | Nẹp Omega | Nhôm | md | `PER_VERTICAL_JOINT_HEIGHT` | `số khe đứng x chiều cao vách` | `Khe đứng`, `Chiều cao vách` |
| Khe đứng / Omega | Băng keo butyl Omega | Butyl | md | `PER_VERTICAL_JOINT_HEIGHT` | `2 mép x chiều cao khe đứng` | `Khe đứng`, `Chiều cao vách` |
| Khe đứng / Omega | Foam PU bơm tại chỗ | Foam PU | chai | `PER_VERTICAL_JOINT_HEIGHT` | quy đổi theo md khe đứng, hiện đang dùng hệ số catalog | `Khe đứng`, `Chiều cao vách` |
| Khe đứng / Omega | Gioăng xốp làm kín | Gioăng | md | `PER_VERTICAL_JOINT_HEIGHT` | `2 mép x chiều cao khe đứng` | `Khe đứng`, `Chiều cao vách` |
| Khe đứng / Omega | B2S-TEK 15-15×20 HWFS | Thép | cái | `PER_VERTICAL_JOINT_HEIGHT` | `tổng khe đứng (md) x 6 cái/md` | `Khe đứng`, `Chiều cao vách` |
| Viền cửa đi | Diềm 06 / theo type mở | Tole | md | `PER_DOOR_OPENING_PERIMETER` | lấy chu vi toàn bộ cửa đi | dữ liệu cửa đi |
| Viền cửa đi | Sealant MS-617 | Sealant | chai | cấu hình theo md/chai nếu quy đổi, hiện dữ liệu gốc bám tuyến | trám viền cửa đi | dữ liệu cửa đi |
| Viền cửa đi | B2S-TEK 15-15×20 HWFS | Thép | cái | `PER_DOOR_OPENING_PERIMETER` | `chu vi cửa đi (md) x 6 cái/md` | dữ liệu cửa đi |
| Viền cửa sổ / lỗ kỹ thuật | Diềm 07 / theo type mở | Tole | md | `PER_NON_DOOR_OPENING_PERIMETER` | lấy chu vi toàn bộ lỗ kỹ thuật hoặc cửa sổ | dữ liệu lỗ mở không phải cửa đi |
| Viền cửa sổ / lỗ kỹ thuật | Sealant MS-617 | Sealant | chai | cấu hình theo md/chai nếu quy đổi, hiện dữ liệu gốc bám tuyến | trám viền lỗ mở | dữ liệu lỗ mở không phải cửa đi |
| Viền cửa sổ / lỗ kỹ thuật | B2S-TEK 15-15×20 HWFS | Thép | cái | `PER_NON_DOOR_OPENING_PERIMETER` | `chu vi lỗ mở (md) x 6 cái/md` | dữ liệu lỗ mở không phải cửa đi |

## 5. Vật tư không được hiểu sai

### 5.1. Nhóm vít `B2S-TEK 15-15×20 HWFS`

Nhóm này trong lớp `Vách + Ngoài nhà` không còn chạy kiểu "ăn cả chiều dài vách".
Nó đã được tách đúng theo từng tuyến:

- cạnh trên
- cạnh dưới
- đầu/cuối lộ
- góc ngoài
- khe nối
- khe đứng Omega
- viền cửa đi
- viền cửa sổ / lỗ kỹ thuật
- cộng thêm `2 vít / 1 bát thép chân panel`

### 5.2. `Khe nối` khác `Khe đứng`

- `Khe nối`: khe giữa các tấm panel theo hướng chia tấm.
- `Khe đứng`: khe đặc biệt của `Omega`, chỉ dùng cho `Vách + Ngoài nhà`.

Nếu nhập sai 2 ô này thì khối lượng sẽ sai hoàn toàn.

### 5.3. `Bát thép chân panel`

Đây là một nhánh riêng, không phải diềm, không phải omega.

Hiện đang chốt:

- tên vật tư: `Bát thép chân panel`
- công thức: `ceil(chiều dài vách / 1500)`
- vít đi kèm: `2 cái B2S-TEK 15-15×20 HWFS / 1 bát`

## 6. Flow đọc nhanh trên Tender

Khi nhìn một dòng `Vách + Ngoài nhà`, nên đọc theo thứ tự:

1. Xác định vùng nào đang lộ thật.
2. Xác định có góc ngoài hay không.
3. Xác định hướng chia tấm để ra `Khe nối`.
4. Xác định có `Khe đứng / Omega` hay không.
5. Xác định có cửa đi hoặc cửa sổ / lỗ kỹ thuật hay không.
6. Từ đó mới suy ra các tuyến diềm, sealant, butyl, omega, vít và bát thép.

## 7. Phần đang nên giữ tách riêng

Các vật tư sau không nên gộp logic với lớp diềm ngoài nhà:

- vít panel vào kết cấu phụ loại khác
- các vật tư của kho lạnh
- các vật tư của phòng sạch
- các vật tư treo trần

Lý do: cùng là vít hoặc sealant nhưng vị trí lắp và cơ sở tính khác nhau.

## 8. Kết luận thao tác

Nếu cần đọc trực quan:

- xem ảnh `facade_wall_layer6.png`

Nếu cần đọc công thức:

- xem bảng mapping ở mục 4

Nếu cần đối chiếu logic phần mềm:

- `ShopDrawing.Plugin/Core/AccessoryDataManager.cs`
- `ShopDrawing.Plugin/Core/TenderBomCalculator.cs`
- `ShopDrawing.Plugin/Core/TenderAccessoryRules.cs`
