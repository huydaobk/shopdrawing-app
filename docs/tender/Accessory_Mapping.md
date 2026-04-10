# Tender Accessory Mapping

Tài liệu này chốt logic tính khối lượng cho từng loại phụ kiện trong module Tender.
Nguồn sự thật hiện tại là:

- `ShopDrawing.Plugin/Core/AccessoryDataManager.cs`
- `ShopDrawing.Plugin/Core/TenderBomCalculator.cs`
- `ShopDrawing.Plugin/Core/TenderAccessoryRules.cs`

## Cấu trúc dữ liệu

Mỗi dòng phụ kiện gồm các trường chính:

- `Name`: tên vật tư
- `Position`: vị trí lắp
- `Unit`: đơn vị
- `CalcRule`: quy tắc tính
- `Factor`: hệ số nhân
- `CategoryScope`: phạm vi hạng mục áp dụng
- `SpecKey`: mã spec áp dụng
- `Application`: loại ứng dụng áp dụng
- `WasteFactor`: hao hụt bổ sung theo %
- `Adjustment`: cộng/trừ thủ công
- `IsManualOnly`: nếu `true` thì bỏ tính tự động, chỉ lấy số điều chỉnh

## Công thức tổng quát

- `BasisValue =` giá trị cơ sở lấy từ `CalcRule`
- `AutoQuantity = BasisValue * Factor`
- `FinalQuantity = AutoQuantity * (1 + WasteFactor / 100) + Adjustment`
- Nếu `IsManualOnly = true` thì `FinalQuantity = Adjustment`
- Kết quả cuối được chặn không âm và làm tròn 2 số lẻ

## Quy tắc tính

| CalcRule | Cơ sở tính | Công thức |
|---|---|---|
| `PER_WALL_LENGTH` | Chiều dài vách | `Length / 1000` |
| `PER_WALL_HEIGHT` | Chiều cao vách | `Height / 1000` |
| `PER_TOP_EDGE_LENGTH` | Cạnh trên lộ | `TopEdgeLength / 1000` |
| `PER_BOTTOM_EDGE_LENGTH` | Cạnh dưới lộ | `BottomEdgeLength / 1000` |
| `PER_EXPOSED_END_LENGTH` | Tổng đầu/cuối lộ | `ExposedEndLength / 1000` |
| `PER_TOTAL_EXPOSED_EDGE_LENGTH` | Tổng cạnh lộ thiên | `TotalExposedEdgeLength / 1000` |
| `PER_OUTSIDE_CORNER_HEIGHT` | Tổng chiều cao góc ngoài | `OutsideCornerHeight / 1000` |
| `PER_INSIDE_CORNER_HEIGHT` | Tổng chiều cao góc trong | `InsideCornerHeight / 1000` |
| `PER_PANEL_QTY` | Số tấm ước tính | `EstimatedPanelCount` |
| `PER_JOINT_LENGTH` | Tổng chiều dài mối nối | `max(0, EstimatedPanelCount - 1) * PanelSpan / 1000` |
| `PER_OPENING_PERIMETER` | Chu vi lỗ mở | `TotalOpeningPerimeter / 1000` |
| `PER_OPENING_PERIMETER_TWO_FACES` | Chu vi lỗ mở 2 mặt | `TotalOpeningPerimeterTwoFaces / 1000` |
| `PER_DOOR_OPENING_PERIMETER` | Chu vi cửa đi | `TotalDoorOpeningPerimeter / 1000` |
| `PER_NON_DOOR_OPENING_PERIMETER` | Chu vi cửa sổ/lỗ kỹ thuật | `TotalNonDoorOpeningPerimeter / 1000` |
| `PER_OPENING_QTY` | Số lượng lỗ mở | `TotalOpeningCount` |
| `PER_DOOR_OPENING_QTY` | Số lượng cửa đi | `TotalDoorOpeningCount` |
| `PER_NON_DOOR_OPENING_QTY` | Số lượng cửa sổ/lỗ kỹ thuật | `TotalNonDoorOpeningCount` |
| `PER_OPENING_VERTICAL_EDGES` | Cạnh đứng lỗ mở | `TotalOpeningVerticalEdges / 1000` |
| `PER_OPENING_HORIZONTAL_TOP_LENGTH` | Cạnh đầu lỗ mở | `TotalOpeningHorizontalTopLength / 1000` |
| `PER_OPENING_SILL_LENGTH` | Cạnh sill lỗ mở | `TotalOpeningSillLength / 1000` |
| `PER_NET_AREA` | Diện tích net | `NetAreaM2` |
| `PER_COLD_STORAGE_T_SUSPENSION_LENGTH` | Tổng chiều dài tuyến treo T nhôm kho lạnh | `ceil(min(Length, Height) / TSpacing) * max(Length, Height) / 1000` |
| `PER_COLD_STORAGE_T_SUSPENSION_POINT_QTY` | Số điểm treo T nhôm kho lạnh | `ceil((T line total length mm) / 1450)` |
| `PER_COLD_STORAGE_T_WIRE_ROPE_LENGTH` | Tổng chiều dài cáp treo T nhôm kho lạnh | `T point qty * CableDropLengthMm / 1000` |
| `PER_COLD_STORAGE_MUSHROOM_SUSPENSION_LENGTH` | Tổng chiều dài tuyến treo bulong nấm kho lạnh | `ceil(min(Length, Height) / MushroomSpacing) * max(Length, Height) / 1000` |
| `PER_COLD_STORAGE_MUSHROOM_SUSPENSION_POINT_QTY` | Số điểm treo bulong nấm kho lạnh | `ceil((Mushroom line total length mm) / 1450)` |
| `PER_COLD_STORAGE_MUSHROOM_WIRE_ROPE_LENGTH` | Tổng chiều dài cáp treo bulong nấm kho lạnh | `Mushroom point qty * CableDropLengthMm / 1000` |
| `PER_COLD_STORAGE_MUSHROOM_BOLT_QTY` | Số lượng bulong nấm kho lạnh | `Mushroom line qty * EstimatedPanelCount` |
| `FIXED_PER_WALL` | Số lượng theo từng vách | `1` |

## Mapping mặc định theo ứng dụng

Lưu ý:

- `CategoryScope` mặc định hiện tại là `Vách`
- `SpecKey` mặc định là `Tất cả`
- `Position` là cột tách riêng, không ghép vào `Name`
- `B2S-TEK` ngoài nhà có auto-size theo chiều dày panel

### 1. Ngoài nhà

| Name | Position | Unit | CalcRule | Factor | Ghi chú |
|---|---|---|---|---:|---|
| Úp nóc | Cạnh trên | md | `PER_TOP_EDGE_LENGTH` | 1.0 | |
| Úp chân | Cạnh dưới | md | `PER_BOTTOM_EDGE_LENGTH` | 1.0 | |
| Úp cạnh hở | Đầu/cuối | md | `PER_EXPOSED_END_LENGTH` | 1.0 | |
| Úp góc ngoài | Góc ngoài | md | `PER_OUTSIDE_CORNER_HEIGHT` | 1.0 | |
| Úp nối dọc | Mối nối | md | `PER_JOINT_LENGTH` | 1.0 | |
| Viền lỗ mở | Cửa đi | md | `PER_DOOR_OPENING_PERIMETER` | 1.0 | |
| Viền lỗ mở | Cửa sổ/lỗ KT | md | `PER_NON_DOOR_OPENING_PERIMETER` | 1.0 | |
| Bộ cửa đi | Cửa đi | bộ | `PER_DOOR_OPENING_QTY` | 1.0 | Tính sơ bộ theo SL cửa đi |
| B2S-TEK | Panel→thép | cái | `PER_PANEL_QTY` | 12.0 | Auto-size theo chiều dày panel |
| Sealant MS-617 | Mối nối | md | `PER_JOINT_LENGTH` | 1.0 | MS Hybrid Adhesive Sealant |
| Sealant MS-617 | Cạnh đứng LM | md | `PER_OPENING_VERTICAL_EDGES` | 1.0 | |
| Sealant MS-617 | Cạnh đầu LM | md | `PER_OPENING_HORIZONTAL_TOP_LENGTH` | 1.0 | |
| Sealant MS-617 | Cạnh sill LM | md | `PER_OPENING_SILL_LENGTH` | 1.0 | |
| Sealant MS-617 | Đỉnh vách | md | `PER_TOP_EDGE_LENGTH` | 1.0 | Hoàn thiện lộ thiên |
| Sealant MS-617 | Chân vách | md | `PER_BOTTOM_EDGE_LENGTH` | 1.0 | Hoàn thiện lộ thiên |
| Sealant MS-617 | Cạnh hở | md | `PER_EXPOSED_END_LENGTH` | 1.0 | Hoàn thiện lộ thiên |
| Băng keo butyl | Mối nối | md | `PER_JOINT_LENGTH` | 1.0 | |
| Rive Ø4.2×12 | Vách | cái | `PER_WALL_LENGTH` | 6.0 | |

### 2. Phòng sạch

| Name | Position | Unit | CalcRule | Factor | Ghi chú |
|---|---|---|---|---:|---|
| Silicone SN-505 | Mối nối | chai | `PER_JOINT_LENGTH` | 0.0667 | Quy đổi 15 md/chai tại mối nối |
| Nẹp bo cong R40 | Cạnh dưới | md | `PER_BOTTOM_EDGE_LENGTH` | 1.0 | Chuẩn hóa từ tên cũ `Nẹp chân vệ sinh` |
| Nẹp bo cong R40 | Cạnh trên | md | `PER_TOP_EDGE_LENGTH` | 1.0 | |
| Nẹp cạnh hở | Đầu/cuối | md | `PER_EXPOSED_END_LENGTH` | 1.0 | |
| Viền lỗ mở 2 mặt | Lỗ mở | md | `PER_OPENING_PERIMETER_TWO_FACES` | 1.0 | |
| Bộ cửa đi phòng sạch | Cửa đi | bộ | `PER_DOOR_OPENING_QTY` | 1.0 | Tính sơ bộ theo SL cửa đi |
| Silicone SN-505 | Cạnh đứng LM | md | `PER_OPENING_VERTICAL_EDGES` | 2.0 | Tính 2 mặt |
| Silicone SN-505 | Cạnh đầu LM | md | `PER_OPENING_HORIZONTAL_TOP_LENGTH` | 2.0 | Tính 2 mặt |
| Silicone SN-505 | Cạnh sill LM | md | `PER_OPENING_SILL_LENGTH` | 2.0 | Tính 2 mặt |
| Silicone SN-505 | Đỉnh vách | md | `PER_TOP_EDGE_LENGTH` | 1.0 | Hoàn thiện vệ sinh |
| Silicone SN-505 | Chân vách | md | `PER_BOTTOM_EDGE_LENGTH` | 1.0 | Hoàn thiện vệ sinh |
| Silicone SN-505 | Cạnh hở | md | `PER_EXPOSED_END_LENGTH` | 1.0 | Hoàn thiện vệ sinh |
| Rive Ø4.2×12 | Nhôm | Vách | cái | `PER_WALL_LENGTH` | 6.0 | |

### 3. Kho lạnh

Phần `Trần + Kho lạnh` hiện đã tự động hóa sơ bộ cho bộ điểm treo:

- Hướng tuyến treo tạm lấy theo cạnh dài của vùng pick, số tuyến treo lấy theo cạnh ngắn.
- `Thả cáp (mm)` được nhập tay trực tiếp trên bảng Tender để quy đổi `Cáp treo Ø12`.
- Bước chia điểm treo mặc định dùng `1450 mm`.
- `Bulong nấm nhựa` hiện tính theo `số tuyến bulong nấm × số tấm panel ước tính`.

Hiện trạng code mặc định đang khai báo ở `AccessoryDataManager.GetColdStorageDefaults()`:

| Name | Material | Position | Unit | CalcRule | Factor | Nhận xét |
|---|---|---|---|---|---:|---|
| Diềm 01 | Tole | Cạnh trên | md | `PER_TOP_EDGE_LENGTH` | 1.0 | Đang quá tổng quát, chưa bám đúng detail kho lạnh |
| Diềm 01 | Tole | Cạnh dưới | md | `PER_BOTTOM_EDGE_LENGTH` | 1.0 | Đang quá tổng quát, chưa tách đúng biên dạng |
| Diềm 01 | Tole | Đầu/cuối | md | `PER_EXPOSED_END_LENGTH` | 1.0 | Đang quá tổng quát, chưa tách đúng biên dạng |
| Diềm 01 | Tole | Mối nối | md | `PER_JOINT_LENGTH` | 1.0 | Đang quá tổng quát, chưa đúng với bộ detail PDF |
| Diềm 01 | Tole | Lỗ mở | md | `PER_OPENING_PERIMETER_TWO_FACES` | 1.0 | Đang quá tổng quát, chưa đúng với bộ detail PDF |
| Bộ cửa đi kho lạnh |  | Cửa đi | bộ | `PER_DOOR_OPENING_QTY` | 1.0 | Có thể giữ lại |
| B2S-TEK | Inox | Panel→thép | cái | `PER_PANEL_QTY` | 12.0 | Không thấy trong bộ detail liên kết kho lạnh đang xét |
| Sealant MC-202 |  | Mối nối | chai | `PER_JOINT_LENGTH` | 0.1667 | Giữa ngàm panel, quy đổi 6 md/chai |
| Silicone SN-505 |  | Cạnh đứng LM | md | `PER_OPENING_VERTICAL_EDGES` | 1.0 | Không thấy callout trong bộ detail PDF này |
| Silicone SN-505 |  | Cạnh đầu LM | md | `PER_OPENING_HORIZONTAL_TOP_LENGTH` | 1.0 | Không thấy callout trong bộ detail PDF này |
| Silicone SN-505 |  | Cạnh sill LM | md | `PER_OPENING_SILL_LENGTH` | 1.0 | Không thấy callout trong bộ detail PDF này |
| Silicone SN-505 |  | Đỉnh vách | md | `PER_TOP_EDGE_LENGTH` | 1.0 | PDF chỉ ghi chung là `Silicone (Typical)` |
| Silicone SN-505 |  | Chân vách | md | `PER_BOTTOM_EDGE_LENGTH` | 1.0 | PDF chỉ ghi chung là `Silicone (Typical)` |
| Silicone SN-505 |  | Cạnh hở | md | `PER_EXPOSED_END_LENGTH` | 1.0 | PDF chỉ ghi chung là `Silicone (Typical)` |
| Rive Ø4.2×12 | Nhôm | Vách | cái | `PER_WALL_LENGTH` | 6.0 | Gần đúng cho spacing `@500`, nhưng mới là xấp xỉ |

Đề xuất mapping chuẩn theo bản vẽ `CHI TIẾT LIÊN KẾT KHO LẠNH.pdf`:

| Name | Material | Position | Unit | CalcRule | Factor | Trạng thái | Ghi chú |
|---|---|---|---|---|---:|---|---|
| V 80x80 | Nhôm | Góc liên kết DT-09, DT-10 | md | `PER_TOP_EDGE_LENGTH` | 1.0 | Tham chiếu | Anodized; cùng biên dạng và cùng kích thước thì dùng chung một mã; code hiện tại chưa có rule riêng để nhận diện đúng các tuyến này |
| Bo góc R40 | Nhôm | Góc trong | md | `PER_TOP_EDGE_LENGTH` | 1.0 | Tham chiếu | Anodized; xuất hiện ở DT-09 đến DT-13, nhưng rule hiện tại chưa có `InsideCornerHeight` nên chưa tự tính đúng |
| Diềm 01 | Tole | Mối nối kho lạnh - buffer | md | `PER_JOINT_LENGTH` | 1.0 | Dùng được ngay | Áp cho `Flashing 70mmW` ở DT-13 nếu chốt đây là một biên dạng tôn riêng |
| Silicone (Typical) | Sealant | Mối nối / hoàn thiện | md | `PER_TOTAL_EXPOSED_EDGE_LENGTH` | 1.0 | Tham chiếu | PDF không chỉ rõ mã silicone; code hiện tại chỉ có `Silicone SN-505` làm placeholder |
| Foam PU bơm tại chỗ | Foam PU | Mối nối / góc trong | md | `PER_JOINT_LENGTH` | 1.0 | Tham chiếu | Về bản chất nên quy đổi từ thể tích khe; hiện chưa có rule thể tích |
| Rive nhôm Ø4.2×12 | Nhôm | Tuyến có rivet @500 | cái | `PER_WALL_LENGTH` | 2.0 | Dùng tạm | Spacing 500 mm tương đương 2 cái/m; nếu cần chính xác theo từng line phải có rule đếm theo tuyến detail |
| Thanh U 40x153x40 | Thép | Chân vách / nền | md | `PER_BOTTOM_EDGE_LENGTH` | 1.0 | Dùng được ngay | Theo DT-14 |
| Màng HDPE 0.15 mm | Màng HDPE | Nền | m² | `PER_NET_AREA` | 2.0 | Dùng được ngay | DT-14 ghi 2 lớp; phần chồng mí 100 mm nên xử lý bằng `WasteFactor` |
| Vít No.5 + tắc kê nhựa |  | Chân U-channel | cái | `PER_BOTTOM_EDGE_LENGTH` | 2.0 | Dùng được ngay | Theo spacing @500 ở DT-14 |
| Keo trám rãnh 10x20 | Sealant | Chân vách / nền | md | `PER_BOTTOM_EDGE_LENGTH` | 1.0 | Dùng được ngay | DT-14 |
| Foam Apollo 750 ml | Foam PU | Chân vách / nền | md | `PER_BOTTOM_EDGE_LENGTH` | 1.0 | Tham chiếu | Nên quy đổi theo thể tích khe, chưa có rule riêng |
| Diềm 02 | Tole | Bọc chân vách cắt tại chỗ | md | `PER_BOTTOM_EDGE_LENGTH` | 1.0 | Tham chiếu | DT-14 có `Cut metal sheet on-site`; chỉ nên đặt mã diềm riêng sau khi chốt biên dạng cắt |
| Thanh T 68x75 | Nhôm | Điểm treo trần | cái | `PER_OPENING_QTY` | 1.0 | Tham chiếu | Anodized; DT-15 là cấu tạo 1 điểm treo; code chưa có dữ liệu số điểm treo nên chưa tự tính đúng |
| Bản mã 50x325x3 | Thép | Điểm treo trần | cái | `PER_OPENING_QTY` | 1.0 | Tham chiếu | 1 cái/điểm treo nếu dùng chi tiết DT-15 |
| Tăng đơ M12 | Thép mạ kẽm | Điểm treo trần | cái | `PER_OPENING_QTY` | 1.0 | Tham chiếu | Có ở DT-15, DT-16 |
| Cáp bọc nhựa Ø12 | Cáp | Điểm treo trần | md | `PER_OPENING_QTY` | 1.0 | Tham chiếu | Phải nhân thêm chiều dài cáp thực tế mỗi điểm; code chưa có input này |
| Cùm cáp Ø12 | Thép mạ kẽm | Điểm treo trần | cái | `PER_OPENING_QTY` | 2.0 | Tham chiếu | Bộ detail cho thấy mỗi điểm có nhiều cùm; cần chốt định mức cuối cùng trước khi code hóa |
| Ống PVC Ø114 | PVC | Điểm treo trần | cái | `PER_OPENING_QTY` | 1.0 | Tham chiếu | Chỉ xuất hiện ở DT-15 |
| Bulông nấm nhựa | Nhựa | Điểm treo trần | cái | `PER_OPENING_QTY` | 1.0 | Tham chiếu | Chỉ xuất hiện ở DT-16 |
| Thanh C 100x50 | Thép mạ kẽm | Điểm treo trần | cái | `PER_OPENING_QTY` | 1.0 | Tham chiếu | Chỉ xuất hiện ở DT-16 |
| Ty ren M10 | Thép | Điểm treo trần | cái | `PER_OPENING_QTY` | 1.0 | Tham chiếu | Chỉ xuất hiện ở DT-16 |

Nguyên tắc đặt mã cho kho lạnh:

- Chỉ phụ kiện tôn/tole mới đặt dạng `Diềm xx`.
- `Name` chỉ ghi mã hoặc quy cách/biên dạng, không lặp lại vật liệu.
- Phụ kiện nhôm ghi quy cách trong `Name`, còn `Material = Nhôm`, ví dụ `V 80x80`, `Bo góc R40`, `Thanh T 68x75`.
- Các finish như `anodized`, `mạ kẽm` nên ưu tiên đưa vào `Note` hoặc để trong `Material` khi cần phân biệt.
- Chỉ gộp chung mã `Diềm` khi cùng biên dạng và cùng kích thước.
- Với bộ bản vẽ này, `Flashing 70mmW` có thể coi là một mã diềm riêng; `Cut metal sheet on-site` ở DT-14 chưa đủ dữ liệu để gán mã nếu chưa chốt biên dạng gia công.
- Các chi tiết treo trần DT-15, DT-16 hiện mới đủ để lập catalog phụ kiện, chưa đủ dữ liệu đầu vào để hệ thống tự tính tổng khối lượng.

Convention đặt tên chính thức cho Kho lạnh:

| Nhóm phụ kiện | Name | Material | Ví dụ |
|---|---|---|---|
| Diềm / flashing / tôn gấp | Ghi mã hoặc quy cách biên dạng | `Tole` | `Name = Diềm 01`, `Material = Tole` |
| Nhôm định hình / nẹp nhôm | Ghi quy cách hoặc biên dạng | `Nhôm` | `Name = V 80x80`, `Material = Nhôm` |
| Thép hình / bản mã / ty ren / thanh C | Ghi quy cách hoặc tên cấu kiện | `Thép`, `Thép mạ kẽm` | `Name = Bản mã 50x325x3`, `Material = Thép` |
| PVC / HDPE / vật liệu tấm, ống | Ghi quy cách sản phẩm | `PVC`, `HDPE` | `Name = Ống Ø114`, `Material = PVC` |
| Silicone / Sealant / Foam / keo | Giữ tên vật tư hoặc chủng loại | Để trống hoặc ghi nhóm vật liệu khi cần | `Name = Silicone SN-505`, `Name = Sealant MC-202`, `Name = Foam PU` |
| Fastener / rive / vít / cùm cáp | Ghi tên + quy cách | Để trống hoặc ghi vật liệu nếu cần tách loại | `Name = Rive Ø4.2×12`, `Name = Vít No.5 + tắc kê nhựa` |

Quy tắc áp dụng:

- Không lặp lại vật liệu trong `Name` nếu cột `Material` đã thể hiện rõ.
- `Name` ưu tiên ngắn, đủ để nhận diện đúng biên dạng hoặc đúng chủng loại.
- Với hóa chất và vật tư tiêu hao như `Silicone`, `Sealant`, `Foam`, tên thương mại/chủng loại vẫn nằm ở `Name`, không ép chuyển thành quy cách hình học.
- Khi cần phân biệt finish như `anodized`, `mạ kẽm`, ưu tiên ghi ở `Note`; chỉ đẩy sang `Material` nếu finish đó ảnh hưởng trực tiếp đến chào giá.

## Quy tắc mở rộng dữ liệu legacy

Khi normalize cấu hình cũ, hệ thống đang tự mở rộng một số dòng tổng quát thành các dòng chi tiết:

- `Position = Lỗ mở` hoặc `Lỗ mở 2 mặt`
  - tách thành `Cạnh đứng LM`
  - tách thành `Cạnh đầu LM`
  - tách thành `Cạnh sill LM`
- `Position = Vách` với `Sealant MS-617` hoặc `Silicone SN-505`
  - tách thành `Đỉnh vách`
  - tách thành `Chân vách`
  - tách thành `Cạnh hở`

## Quy tắc áp dụng theo phạm vi

Một dòng phụ kiện chỉ được tính cho vách nếu cùng lúc thỏa:

- `CategoryScope` khớp `wall.Category` hoặc là `Tất cả`
- `SpecKey` khớp `wall.SpecKey` hoặc là `Tất cả`
- `Application` khớp `wall.Application` hoặc là `Tất cả`

## Ghi chú triển khai

- UI tab Tender đang hiển thị `Name` và `Position` ở 2 cột riêng.
- Nếu muốn test pass theo convention mới, test nên assert theo cặp `Name + Position`, không nên kỳ vọng tên dài ghép sẵn vị trí.
- Nếu muốn đổi convention sang tên dài cho Excel/UI, nên quyết định ở một lớp riêng, không đổi dữ liệu gốc trong model.
