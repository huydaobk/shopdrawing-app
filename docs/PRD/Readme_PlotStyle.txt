Hướng dẫn tạo Plot Style: SD_Black.ctb

Để chức năng in PDF (Tender & Shopdrawing) hoạt động đúng tiêu chuẩn nét in, bạn cần tạo file nét in `SD_Black.ctb` theo các bước sau trong AutoCAD:

1. Mở AutoCAD. Nhập lệnh `STYLESMANAGER` và nhấn Enter.
2. Thư mục chứa các file Plot Style sẽ hiện ra.
3. Kích đúp vào file "Add-A-Plot Style Table Wizard" để chạy trình hướng dẫn.
4. Chọn "Start from scratch" -> Next.
5. Chọn "Color-Dependent Plot Style Table" -> Next.
6. Ở ô "File name", nhập chính xác tên: SD_Black -> Next.
7. Click "Plot Style Table Editor..." để bắt đầu chỉnh sửa nét.
8. Ở tab "Form View", hãy thực hiện cấu hình như sau:
   - Chọn **Color 1 đến Color 255** (chọn tất cả):
     - Properties -> Color: Đổi thành `Black`
     - Properties -> Lineweight: Đổi thành `0.0900 mm` (hoặc nét mảnh mặc định của bạn).
   - Chọn **Color 1 (Red)**: Lineweight = `0.1300 mm` (Nét mờ / Hatch)
   - Chọn **Color 2 (Yellow)**: Lineweight = `0.1800 mm` (Nét thấy)
   - Chọn **Color 3 (Green)**: Lineweight = `0.2500 mm` (Nét cắt mỏng)
   - Chọn **Color 4 (Cyan)**: Lineweight = `0.3500 mm` (Nét cắt dày)
   - Chọn **Color 5 (Blue)**: Lineweight = `0.5000 mm` (Nét cắt rất dày / Viền khung chữ)
   - Chọn **Color 8 (Dark Gray)**: Color = `Use object color` (Giữ nguyên màu xám để in mờ)
   - Chọn **Color 252 (Gray)**: Color = `Use object color` (Trục toạ độ)
9. Nhấn "Save & Close". Xong!

Lưu ý: Các thiết lập Lineweight trên là dàn ý cơ bản cho bản vẽ kỹ thuật. Bạn có thể tự điều chỉnh lại độ dày mỏng tuỳ thuộc vào tiêu chuẩn công ty của bạn. Chỉ cần đảm bảo file có tên là `SD_Black.ctb`. Mặc định Plugin sẽ dùng file này, nếu không tìm thấy, nó sẽ lùi về pakai `monochrome.ctb`.
