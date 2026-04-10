# Lớp 8 - Bảng Chuẩn Cuối Cùng Cho Vách + Ngoài Nhà

Tài liệu này là bảng chuẩn cuối cùng cho nhánh `Vách + Ngoài nhà`.

Mục đích:

- khóa tên phụ kiện đang dùng
- khóa vị trí lắp
- khóa input điều khiển
- khóa công thức tính
- khóa note thi công để đọc BOM không bị mơ hồ

Phạm vi áp dụng:

- `Category = Vách`
- `Application = Ngoài nhà`

## 1. Nguyên tắc đọc bảng

- `Tên phụ kiện`: tên chuẩn để hiển thị trong Tender / Excel
- `Vị trí lắp`: nơi vật tư thực sự ăn vào
- `Input điều khiển`: ô hoặc nhóm dữ liệu người dùng tác động
- `Công thức chuẩn`: cách hiểu nghiệp vụ
- `Note thi công`: để đọc đúng bản chất tuyến vật tư

## 2. Bảng chuẩn cuối cùng

| Tên phụ kiện | Vật liệu | Vị trí lắp | Input điều khiển | Công thức chuẩn | Đơn vị | Note thi công |
|---|---|---|---|---|---|---|
| Diềm 01 | Tole | Lộ trên | `Lộ trên`, `Chiều dài vách` | `TopEdgeLength` | md | Diềm mép trên của vách ngoài nhà |
| Diềm 02 | Tole | Lộ dưới / chân vách | `Lộ dưới`, `Chiều dài vách` | `BottomEdgeLength` | md | Diềm hoàn thiện mép chân vách |
| Diềm 03 | Tole | Đầu lộ / cuối lộ | `Đầu lộ`, `Cuối lộ`, `Chiều cao vách` | `ExposedEndLength` | md | Diềm bo các cạnh biên hở |
| Diềm 04 | Tole | Góc ngoài | `Góc ngoài`, `Chiều cao vách` | `OutsideCornerHeight` | md | Diềm bo góc ngoài theo chiều cao |
| Diềm 05 | Tole | Khe nối giữa các tấm panel | `Khổ rộng panel`, `Chiều dài vách`, `Chiều cao vách`, `Hướng chia tấm` | `max(0, EstimatedPanelCount - 1) x PanelSpan` | md | Chỉ chạy theo tuyến khe nối panel thực tế |
| Diềm 06 | Tole | Viền cửa đi | dữ liệu `Cửa đi` | `TotalDoorOpeningPerimeter` | md | Diềm bo chu vi cửa đi |
| Diềm 07 | Tole | Viền cửa sổ / lỗ kỹ thuật | dữ liệu `Cửa sổ / lỗ kỹ thuật` | `TotalNonDoorOpeningPerimeter` | md | Diềm bo chu vi lỗ mở không phải cửa đi |
| Sealant MS-617 | Sealant | Lộ trên | `Lộ trên`, `Chiều dài vách` | `TopEdgeLength` | md hoặc chai | Trám kín mép trên lộ thiên |
| Sealant MS-617 | Sealant | Lộ dưới / chân vách | `Lộ dưới`, `Chiều dài vách` | `BottomEdgeLength` | md hoặc chai | Trám kín chân vách |
| Sealant MS-617 | Sealant | Đầu lộ / cuối lộ | `Đầu lộ`, `Cuối lộ`, `Chiều cao vách` | `ExposedEndLength` | md hoặc chai | Trám kín các cạnh biên hở |
| Sealant MS-617 | Sealant | Khe nối giữa các tấm panel | `Khổ rộng panel`, `Chiều dài vách`, `Chiều cao vách`, `Hướng chia tấm` | `JointLength` | md hoặc chai | Trám theo tuyến khe panel |
| Sealant MS-617 | Sealant | Viền cửa đi | dữ liệu `Cửa đi` | `TotalDoorOpeningPerimeter` | md hoặc chai | Trám kín chu vi cửa đi |
| Sealant MS-617 | Sealant | Viền cửa sổ / lỗ kỹ thuật | dữ liệu `Cửa sổ / lỗ kỹ thuật` | `TotalNonDoorOpeningPerimeter` | md hoặc chai | Trám kín chu vi lỗ mở |
| Băng keo butyl | Butyl | Khe nối giữa các tấm panel | `Khổ rộng panel`, `Chiều dài vách`, `Chiều cao vách`, `Hướng chia tấm` | `JointLength` | md | Dán theo tuyến khe nối panel |
| Nẹp Omega | Nhôm | Khe đứng / Omega | `Khe đứng`, `Chiều cao vách`, `Hướng chia tấm` | `VerticalJointTotalLength` | md | Chỉ dùng cho `Vách + Ngoài nhà`, chỉ có tác dụng khi `Xếp ngang` |
| Băng keo butyl Omega | Butyl | Hai mép nẹp Omega | `Khe đứng`, `Chiều cao vách`, `Hướng chia tấm` | `VerticalJointTotalLength x 2 mép` | md | Dán 2 mép trước khi ép nẹp Omega |
| Foam PU bơm tại chỗ | Foam PU | Khe đứng / Omega | `Khe đứng`, `Chiều cao vách`, `Hướng chia tấm` | quy đổi theo `VerticalJointTotalLength` | chai | Bơm kín phần rỗng tuyến Omega |
| Gioăng xốp làm kín | Gioăng | Hai mép khe đứng / Omega | `Khe đứng`, `Chiều cao vách`, `Hướng chia tấm` | `VerticalJointTotalLength x 2 mép` | md | Tạo lớp kín nước, kín gió tại khe Omega |
| Bát thép chân panel | Thép | Chân vách | `Chiều dài vách` | `ceil(Chiều dài vách / 1500)` | cái | Bát đỡ chân panel theo nhịp xà gồ giả định 1500 mm |
| B2S-TEK 15-15×20 HWFS | Thép | Bắt diềm mép trên | `Lộ trên`, `Chiều dài vách` | `TopEdgeLength x 6` | cái | Vít tự khoan bắt tuyến diềm trên |
| B2S-TEK 15-15×20 HWFS | Thép | Bắt diềm chân vách | `Lộ dưới`, `Chiều dài vách` | `BottomEdgeLength x 6` | cái | Vít tự khoan bắt tuyến diềm dưới |
| B2S-TEK 15-15×20 HWFS | Thép | Bắt diềm đầu/cuối lộ | `Đầu lộ`, `Cuối lộ`, `Chiều cao vách` | `ExposedEndLength x 6` | cái | Vít tự khoan bắt tuyến biên hở |
| B2S-TEK 15-15×20 HWFS | Thép | Bắt diềm góc ngoài | `Góc ngoài`, `Chiều cao vách` | `OutsideCornerHeight x 6` | cái | Vít tự khoan bắt tuyến góc ngoài |
| B2S-TEK 15-15×20 HWFS | Thép | Bắt diềm khe nối panel | `Khổ rộng panel`, `Chiều dài vách`, `Chiều cao vách`, `Hướng chia tấm` | `JointLength x 6` | cái | Vít tự khoan cho tuyến khe nối panel |
| B2S-TEK 15-15×20 HWFS | Thép | Bắt nẹp Omega | `Khe đứng`, `Chiều cao vách`, `Hướng chia tấm` | `VerticalJointTotalLength x 6` | cái | Vít tự khoan cho tuyến Omega |
| B2S-TEK 15-15×20 HWFS | Thép | Bắt viền cửa đi | dữ liệu `Cửa đi` | `TotalDoorOpeningPerimeter x 6` | cái | Vít tự khoan theo chu vi cửa đi |
| B2S-TEK 15-15×20 HWFS | Thép | Bắt viền cửa sổ / lỗ kỹ thuật | dữ liệu `Cửa sổ / lỗ kỹ thuật` | `TotalNonDoorOpeningPerimeter x 6` | cái | Vít tự khoan theo chu vi lỗ mở |
| B2S-TEK 15-15×20 HWFS | Thép | Đi kèm bát thép chân panel | `Chiều dài vách` | `Bát thép chân panel x 2` | cái | 2 vít cho 1 bát đỡ chân panel |

## 3. Các chỗ phải hiểu thật đúng

### 3.1. Một tên vật tư có thể xuất hiện nhiều dòng

Điều này là đúng.

Ví dụ `B2S-TEK 15-15×20 HWFS` xuất hiện nhiều dòng vì:

- cùng tên
- nhưng khác vị trí lắp
- khác cơ sở tính

Khi lên bảng tổng hợp cuối:

- phải cộng dồn theo `Tên phụ kiện + Đơn vị`

Khi đọc bảng cơ sở:

- phải giữ tách theo vị trí để kiểm logic

### 3.2. `Sealant MS-617`

Ở lớp chuẩn này tôi giữ nó theo logic tuyến thi công.

Có nghĩa:

- vùng nào cần trám kín thì có một dòng riêng cho vùng đó
- còn khi tổng hợp BOM thì có thể gộp lại theo `Tên + Đơn vị`

### 3.3. `Diềm 05` và `Nẹp Omega` không được nhập nhằng

- `Diềm 05` là tuyến khe nối panel thông thường
- `Nẹp Omega` là tuyến khe đứng đặc thù của `Vách + Ngoài nhà`

Hai nhánh này độc lập.

## 4. Chuẩn dùng để đối chiếu code

Nếu sau này rà code, nên đối chiếu bảng này với:

- `ShopDrawing.Plugin/Core/AccessoryDataManager.cs`
- `ShopDrawing.Plugin/Core/TenderBomCalculator.cs`
- `ShopDrawing.Plugin/Core/TenderAccessoryRules.cs`

## 5. Bộ tài liệu hoàn chỉnh cho Vách + Ngoài Nhà

- sơ đồ vùng: `C:\my_project\shopdrawing-app\artifacts\facade_wall_layer6.png`
- mapping theo vùng: `C:\my_project\shopdrawing-app\docs\tender\Exterior_Wall_Accessory_Layer6.md`
- mapping theo input: `C:\my_project\shopdrawing-app\docs\tender\Exterior_Wall_Input_Impact_Map.md`
- bảng chuẩn cuối: `C:\my_project\shopdrawing-app\docs\tender\Exterior_Wall_Accessory_Master_Table.md`
