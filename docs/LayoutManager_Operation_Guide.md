# Hướng Dẫn Vận Hành Layout Manager

Tài liệu này mô tả cách vận hành `Layout Manager` theo đúng flow đang chạy trong plugin hiện tại.

Tài liệu liên quan:
- PRD: `docs/PRD/LayoutManager_PRD_v5.md`
- Command mở palette: `SD_LAYOUT`

## 1. Mục đích

`Layout Manager` dùng để:
- Chọn một vùng model và tự tạo 1 hoặc nhiều layout giấy.
- Tự chia trang nếu vùng chọn dài hơn khổ giấy.
- Tự tạo view title dưới viewport.
- Tự gán số bản vẽ `SD-001`, `SD-002`, ...
- Tự đồng bộ `00-DMBD`.
- Hỗ trợ title block mặc định hoặc title block khách hàng từ file `.dwg`.
- Hỗ trợ revision khi tạo mới hoặc cập nhật layout đã tồn tại.

## 2. Điều kiện trước khi chạy

Trước khi thao tác, cần kiểm tra:
- Đang mở đúng file `.dwg` cần xuất layout.
- `CANNOSCALE` của bản vẽ đã đúng tỷ lệ mong muốn, vì plugin lấy scale trực tiếp từ đây.
- Nội dung model đã nằm đúng hướng để in landscape.
- Nếu dùng title block khách hàng, file `.dwg` phải chứa block có attribute.
- Người dùng có quyền ghi vào drawing hiện tại, vì plugin sẽ lưu metadata và mapping vào NOD.

## 3. Mở công cụ

Trong command line AutoCAD, chạy:

```text
SD_LAYOUT
```

Palette `Layout Manager` sẽ mở ở bên phải màn hình.

## 4. Cấu hình trên palette

### 4.1. Khổ giấy

Chọn một trong các giá trị:
- `A3`
- `A2`
- `A1`

Hiện tại plugin luôn dùng hướng `Landscape`.

### 4.2. Margin

Thiết lập:
- `Trái`: mặc định `25`
- `Còn lại`: mặc định `5`

`Còn lại` được áp dụng đồng thời cho phải, trên và dưới.

### 4.3. Tỷ lệ

Palette chỉ hiển thị tỷ lệ đang lấy từ `CANNOSCALE`.

Lưu ý:
- View title hiển thị dạng `TỶ LỆ 1:100`.
- Title block và DMBD lưu tỷ lệ dạng `1:100`.

### 4.4. Tên dự án

Nhập tên dự án nếu đây là file mới.

Nếu ô này để trống:
- Plugin sẽ cố lấy `ProjectName` từ các layout đã có trong drawing.
- Tên này tiếp tục được dùng cho title block và `00-DMBD`.

### 4.5. Title block

Có 2 mode:
- `Mặc định (TCVN)`: plugin tự vẽ title block built-in.
- `Khách hàng`: plugin insert block từ file `.dwg` ngoài và fill attribute theo mapping đã lưu.

## 5. Quy trình tạo layout

### 5.1. Tạo layout với title block mặc định

1. Mở `SD_LAYOUT`.
2. Chọn khổ giấy và margin.
3. Nhập `Tên dự án`.
4. Giữ mode `Mặc định (TCVN)`.
5. Nhập `Tên bản vẽ`.
6. Bấm `Chọn vùng & Tạo Layout`.
7. Trên model, pick góc 1.
8. Pick góc 2 để xác định vùng in.
9. Xác nhận popup revision.

Kết quả:
- Nếu vùng vừa một trang: tạo 1 layout.
- Nếu vùng dài: tự chia nhiều trang, có hậu tố `(PHẦN x/n)`.
- Tự tạo viewport.
- Tự tạo view title 2 dòng dưới viewport.
- Tự gán số bản vẽ `SD-00n`.
- Tự sync `00-DMBD`.

### 5.2. Tạo layout với title block khách hàng

1. Trong palette, chọn `Khách hàng`.
2. Bấm `Chọn .DWG...`.
3. Chọn file title block khách hàng.
4. Nếu lần đầu, xác nhận mapping attribute.
5. Kiểm tra phần tóm tắt mapping trên palette.
6. Nhập `Tên bản vẽ`.
7. Bấm `Chọn vùng & Tạo Layout`.
8. Pick vùng như flow ở trên.
9. Xác nhận popup revision.

Kết quả:
- Plugin insert external block vào paper space.
- Attribute được fill theo mapping đã lưu.
- Nếu block không hợp lệ hoặc không có attribute, plugin fallback sang title block built-in.

## 6. Quy tắc revision

Khi tạo hoặc cập nhật layout:
- Plugin mở popup nhập nội dung revision.
- Layout mới nhận revision mặc định `A`.
- Layout cũ được bump theo chuỗi `A -> B -> C -> ...`.
- Ngày được cập nhật theo ngày hiện tại.
- Nội dung revision được lưu vào metadata và hiển thị trong title block/DMBD.

## 7. Quy tắc numbering và thứ tự

### 7.1. Số bản vẽ

Số bản vẽ có format:

```text
SD-001
SD-002
SD-003
```

Nguyên tắc:
- Đếm tăng dần trong drawing hiện tại.
- Layout đã có số cũ thì giữ nguyên số đó khi cập nhật.

### 7.2. STT trong DMBD

`STT` trong `00-DMBD` được đánh lại mỗi lần sync.

Thứ tự hiển thị:
- Ưu tiên theo số bản vẽ `SD-001`, `SD-002`, ...
- Không còn sort đơn thuần theo tên layout.

## 8. Danh mục bản vẽ `00-DMBD`

Plugin tự tạo hoặc tự cập nhật layout:

```text
00-DMBD
```

DMBD hiện tại chứa:
- `STT`
- `TÊN BẢN VẼ`
- `SỐ BV`
- `TỶ LỆ`
- `NGÀY`
- `REV`
- `GHI CHÚ`

Tên dự án được hiển thị ở phần header của bảng.

Các trigger tự sync:
- Tạo layout mới.
- Cập nhật layout đã tồn tại.
- Xóa layout từ palette.
- Bấm `Sync DMBD`.

## 9. Xóa layout

Trong palette:
1. Chọn layout trong danh sách.
2. Bấm `Xóa`.

Kết quả:
- Layout bị xóa khỏi drawing.
- Metadata layout bị xóa.
- `00-DMBD` được sync lại.
- `STT` được đánh lại.

## 10. Hành vi cần lưu ý

Các điểm vận hành cần nhớ:
- Scale phụ thuộc hoàn toàn vào `CANNOSCALE`.
- View title dùng tên layout đầy đủ, kể cả `(PHẦN x/n)`.
- Tỷ lệ trong DMBD/title block là `1:n`, không có tiền tố `TỶ LỆ`.
- Tên dự án có thể tự lấy lại từ metadata nếu drawing đã từng tạo layout trước đó.
- Mapping external title block được lưu trong NOD của drawing hiện hành, không dùng chung cho mọi file.

## 11. Checklist thao tác chuẩn

Checklist đề xuất trước khi xuất layout:
- Kiểm tra `CANNOSCALE`.
- Kiểm tra vùng model đã sạch và đúng hướng.
- Chọn đúng khổ giấy.
- Kiểm tra margin.
- Nhập hoặc xác nhận `Tên dự án`.
- Kiểm tra mode title block.
- Nếu dùng external block, xác nhận mapping summary trước khi chạy.
- Sau khi tạo xong, mở `00-DMBD` để kiểm tra nhanh số bản vẽ, revision, ngày và ghi chú.

## 12. Xử lý lỗi thường gặp

### 12.1. Không tạo được layout

Kiểm tra:
- Đã pick đủ 2 góc vùng chưa.
- Drawing có đang bị khóa hoặc đang ở trạng thái không cho ghi không.
- Command có bị hủy ở popup revision không.

### 12.2. Tỷ lệ hiển thị sai

Kiểm tra:
- Giá trị `CANNOSCALE` hiện tại trong AutoCAD.
- Có đang dùng đúng annotation scale trước khi bấm tạo layout không.

### 12.3. External title block không chèn được

Kiểm tra:
- File `.dwg` còn tồn tại tại đúng đường dẫn đã chọn.
- Block trong file có attribute hay không.
- Mapping có bị thiếu field quan trọng không.

Khi lỗi xảy ra:
- Plugin sẽ cảnh báo trong command line.
- Plugin fallback sang title block built-in.

### 12.4. DMBD không đúng thứ tự

Thao tác:
1. Bấm `Sync DMBD`.
2. Kiểm tra metadata số bản vẽ của từng layout.

Thứ tự chuẩn hiện tại là theo `SD-001`, `SD-002`, ...

## 13. Phạm vi đã xác nhận

Đã xác nhận ở mức code và build:
- Flow tạo layout từ palette.
- Revision popup.
- Sync `00-DMBD`.
- Built-in/external title block flow.
- Fallback external -> built-in.
- Thứ tự DMBD theo số bản vẽ.

Chưa xác nhận hoàn toàn nếu không test trực tiếp trong AutoCAD runtime:
- Insert block khách hàng với nhiều biến thể `.dwg` thực tế.
- Tương thích của từng bộ attribute mapping theo tiêu chuẩn riêng của từng khách hàng.
