# Lớp 7 - Input Tender Ảnh Hưởng Gì Đến Phụ Kiện (Vách + Ngoài Nhà)

Tài liệu này trả lời đúng câu hỏi khi test Tender:

`Tôi sửa ô nào thì những phụ kiện nào phải đổi theo?`

Phạm vi tài liệu:

- chỉ cho `Category = Vách`
- chỉ cho `Application = Ngoài nhà`

## 1. Cách đọc nhanh

Mỗi dòng dưới đây được hiểu theo format:

- `Ô nhập`
- `Điều kiện để ô có tác dụng`
- `Phụ kiện bị ảnh hưởng`
- `Lưu ý để không hiểu sai`

## 2. Bảng tác động theo từng ô

| Ô nhập / thao tác | Khi nào có tác dụng | Phụ kiện bị ảnh hưởng trực tiếp | Lưu ý kỹ thuật |
|---|---|---|---|
| `Hạng mục` chuyển sang `Vách` | luôn | bật toàn bộ logic vách | nếu không phải `Vách` thì không nên dùng bộ mapping này |
| `Ứng dụng` chuyển sang `Ngoài nhà` | luôn | bật nhóm diềm ngoài nhà, Omega, vít `B2S-TEK 15-15×20 HWFS`, `Bát thép chân panel` | nếu không phải `Ngoài nhà` thì `Khe đứng` phải bị khóa |
| `Chiều dài vách` | luôn | `Diềm 01`, `Diềm 02`, `Bát thép chân panel`, các vật tư chân panel đi kèm, và toàn bộ nhóm `khe nối` khi `xếp ngang` | với `xếp dọc`, chiều dài còn ảnh hưởng số tấm; với `xếp ngang`, chiều dài là `span` của tấm |
| `Chiều cao vách` | luôn | `Diềm 03`, `Diềm 04`, nhóm `Omega / Khe đứng`, và toàn bộ nhóm `khe nối` khi `xếp dọc` | với `xếp ngang`, chiều cao còn ảnh hưởng số tấm và số khe đứng |
| `Khổ rộng panel` | khi > 0 | `Diềm 05`, `Sealant MS-617` tại khe nối, `Băng keo butyl`, `B2S-TEK 15-15×20 HWFS` tại khe nối | vì khổ panel đổi thì `EstimatedPanelCount` đổi, kéo theo `JointLength` đổi |
| `Hướng chia tấm` (`Dọc/Ngang`) | luôn | toàn bộ nhóm phụ kiện theo `khe nối`, và nhóm `Omega / Khe đứng` | đây là ô cực nhạy vì nó đổi `DivisionSpan`, `PanelSpan`, `EstimatedPanelCount`, `JointLength` |
| `Lộ trên` | khi bật | `Diềm 01`, `Sealant MS-617` vùng trên, `B2S-TEK 15-15×20 HWFS` vùng trên | tắt `Lộ trên` thì nhóm này phải về 0 |
| `Lộ dưới` | khi bật | `Diềm 02`, `Sealant MS-617` vùng chân | lưu ý: `Bát thép chân panel` hiện đang tính theo `chiều dài vách`, chưa bị gate bởi ô `Lộ dưới` |
| `Đầu lộ` | khi bật | nhóm `Diềm 03`, `Sealant MS-617`, `B2S-TEK 15-15×20 HWFS` phần đầu/cuối lộ | cộng dồn với `Cuối lộ` để ra `ExposedEndLength` |
| `Cuối lộ` | khi bật | nhóm `Diềm 03`, `Sealant MS-617`, `B2S-TEK 15-15×20 HWFS` phần đầu/cuối lộ | cộng dồn với `Đầu lộ` để ra `ExposedEndLength` |
| `Góc ngoài` | khi > 0 | `Diềm 04`, `B2S-TEK 15-15×20 HWFS` vùng góc ngoài | không nên vừa bật `Đầu lộ/Cuối lộ` vừa tính `Góc ngoài` cho cùng một cạnh, nếu không sẽ trùng tuyến |
| `Khe đứng` | chỉ có tác dụng khi `Vách + Ngoài nhà + Xếp ngang` | `Nẹp Omega`, `Băng keo butyl Omega`, `Foam PU bơm tại chỗ`, `Gioăng xốp làm kín`, `B2S-TEK 15-15×20 HWFS` vùng Omega | nếu `Xếp dọc` thì code trả về 0 cho nhóm này |
| `Thêm / sửa cửa đi` | khi opening là `IsDoor = true` | `Diềm 06`, `Sealant MS-617`, `B2S-TEK 15-15×20 HWFS` theo chu vi cửa đi | cơ sở tính là `TotalDoorOpeningPerimeter` |
| `Thêm / sửa cửa sổ / lỗ kỹ thuật` | khi opening là `IsNonDoor = true` | `Diềm 07`, `Sealant MS-617`, `B2S-TEK 15-15×20 HWFS` theo chu vi lỗ mở không phải cửa đi | cơ sở tính là `TotalNonDoorOpeningPerimeter` |

## 3. Công thức lõi đang chi phối

### 3.1. Nhóm cạnh biên

- `TopEdgeLength = Length` nếu bật `Lộ trên`, ngược lại bằng `0`
- `BottomEdgeLength = Length` nếu bật `Lộ dưới`, ngược lại bằng `0`
- `ExposedEndLength = Height` cho mỗi cạnh có `Đầu lộ` hoặc `Cuối lộ`
- `OutsideCornerHeight = OutsideCornerCount x Height`

### 3.2. Nhóm chia tấm

- nếu `Xếp dọc`
  - `DivisionSpan = Chiều dài vách`
  - `PanelSpan = Chiều cao vách`
- nếu `Xếp ngang`
  - `DivisionSpan = Chiều cao vách`
  - `PanelSpan = Chiều dài vách`

- `EstimatedPanelCount = ceil(DivisionSpan / Khổ rộng panel)`
- `JointLength = max(0, EstimatedPanelCount - 1) x PanelSpan`

Hệ quả:

- đổi `Khổ rộng panel` là đổi số tấm
- đổi `Hướng chia tấm` là đổi cả chiều đang chia lẫn chiều span
- vì vậy nhóm `Diềm 05 / Butyl / Sealant / vít tại khe nối` sẽ nhảy mạnh

### 3.3. Nhóm khe đứng Omega

- `VerticalJointTotalLength = Khe đứng x Chiều cao vách`
- nhưng chỉ tính khi `Hướng chia tấm = Ngang`

Hệ quả:

- nhập `Khe đứng` mà đang `Xếp dọc` thì nhìn thấy số nhưng khối lượng Omega vẫn phải bằng `0`

### 3.4. Bát thép chân panel

- `Bát thép chân panel = ceil(Chiều dài vách / 1500)`
- `B2S-TEK 15-15×20 HWFS` đi kèm = `2 cái / 1 bát`

Hệ quả:

- chỉ cần sửa `Chiều dài vách` là số lượng bát và vít đi kèm đổi ngay
- hiện tại logic này không phụ thuộc trực tiếp vào `Lộ dưới`

## 4. Mapping ngược: từng phụ kiện đang bị ô nào điều khiển

| Phụ kiện | Ô nhập chi phối |
|---|---|
| `Diềm 01` | `Lộ trên`, `Chiều dài vách` |
| `Diềm 02` | `Lộ dưới`, `Chiều dài vách` |
| `Diềm 03` | `Đầu lộ`, `Cuối lộ`, `Chiều cao vách` |
| `Diềm 04` | `Góc ngoài`, `Chiều cao vách` |
| `Diềm 05` | `Khổ rộng panel`, `Chiều dài vách`, `Chiều cao vách`, `Hướng chia tấm` |
| `Diềm 06` | dữ liệu `Cửa đi` |
| `Diềm 07` | dữ liệu `Cửa sổ / lỗ kỹ thuật` |
| `Sealant MS-617` cạnh trên | `Lộ trên`, `Chiều dài vách` |
| `Sealant MS-617` cạnh dưới | `Lộ dưới`, `Chiều dài vách` |
| `Sealant MS-617` cạnh biên hở | `Đầu lộ`, `Cuối lộ`, `Chiều cao vách` |
| `Sealant MS-617` khe nối | `Khổ rộng panel`, `Chiều dài vách`, `Chiều cao vách`, `Hướng chia tấm` |
| `Sealant MS-617` viền cửa đi | dữ liệu `Cửa đi` |
| `Sealant MS-617` viền cửa sổ / lỗ KT | dữ liệu `Cửa sổ / lỗ kỹ thuật` |
| `Băng keo butyl` | `Khổ rộng panel`, `Chiều dài vách`, `Chiều cao vách`, `Hướng chia tấm` |
| `Nẹp Omega` | `Khe đứng`, `Chiều cao vách`, `Hướng chia tấm` |
| `Băng keo butyl Omega` | `Khe đứng`, `Chiều cao vách`, `Hướng chia tấm` |
| `Foam PU bơm tại chỗ` | `Khe đứng`, `Chiều cao vách`, `Hướng chia tấm` |
| `Gioăng xốp làm kín` | `Khe đứng`, `Chiều cao vách`, `Hướng chia tấm` |
| `Bát thép chân panel` | `Chiều dài vách` |
| `B2S-TEK 15-15×20 HWFS` vùng trên | `Lộ trên`, `Chiều dài vách` |
| `B2S-TEK 15-15×20 HWFS` vùng dưới | `Lộ dưới`, `Chiều dài vách` |
| `B2S-TEK 15-15×20 HWFS` đầu/cuối lộ | `Đầu lộ`, `Cuối lộ`, `Chiều cao vách` |
| `B2S-TEK 15-15×20 HWFS` góc ngoài | `Góc ngoài`, `Chiều cao vách` |
| `B2S-TEK 15-15×20 HWFS` khe nối | `Khổ rộng panel`, `Chiều dài vách`, `Chiều cao vách`, `Hướng chia tấm` |
| `B2S-TEK 15-15×20 HWFS` khe đứng Omega | `Khe đứng`, `Chiều cao vách`, `Hướng chia tấm` |
| `B2S-TEK 15-15×20 HWFS` viền cửa đi | dữ liệu `Cửa đi` |
| `B2S-TEK 15-15×20 HWFS` viền cửa sổ / lỗ KT | dữ liệu `Cửa sổ / lỗ kỹ thuật` |
| `B2S-TEK 15-15×20 HWFS` đi kèm bát thép | `Chiều dài vách` |

## 5. Những chỗ dễ test sai

### 5.1. Tắt `Lộ dưới` nhưng vẫn còn `Bát thép chân panel`

Đây không hẳn là bug.

Logic hiện tại của `Bát thép chân panel` đang là:

- áp cho `Vách + Ngoài nhà`
- tính theo `chiều dài vách`

Nó chưa bị ràng buộc bởi cờ `Lộ dưới`.

Nếu sau này mình muốn:

- chỉ khi chân thật sự lộ mới tính

thì đó là một quyết định logic mới, không phải sửa lỗi nhỏ.

### 5.2. Nhập `Khe đứng` nhưng không thấy ra phụ kiện Omega

Nguyên nhân thường là:

- đang để `Xếp dọc`
- hoặc không phải `Ngoài nhà`
- hoặc không phải `Vách`

### 5.3. Bị trùng giữa `Góc ngoài` và `Đầu lộ/Cuối lộ`

Nếu cùng một cạnh đã là góc ngoài, thì không nên đồng thời coi nó là cạnh biên hở.

Nếu nhập cả hai:

- phần mềm sẽ tính cả 2 tuyến
- khối lượng sẽ bị cộng chồng

## 6. Flow test nhanh đề xuất

Để test không rối, nên làm theo trình tự:

1. Tạo một dòng `Vách + Ngoài nhà`.
2. Chỉ bật `Lộ trên`, xem `Diềm 01 + sealant + vít` có nhảy không.
3. Chỉ bật `Lộ dưới`, xem `Diềm 02` có nhảy không.
4. Tăng `Chiều dài vách`, kiểm `Bát thép chân panel` và `2 vít / bát`.
5. Đổi `Hướng chia tấm` giữa `Dọc/Ngang`, kiểm `Diềm 05` và `Khe đứng`.
6. Nhập `Khe đứng`, chỉ kiểm khi `Xếp ngang`.
7. Thêm một `Cửa đi`, rồi thêm một `Cửa sổ`, kiểm 2 nhóm viền tách riêng.

## 7. Tài liệu đi kèm

- ảnh vùng phụ kiện: `C:\my_project\shopdrawing-app\artifacts\facade_wall_layer6.png`
- mapping theo vùng: `C:\my_project\shopdrawing-app\docs\tender\Exterior_Wall_Accessory_Layer6.md`
