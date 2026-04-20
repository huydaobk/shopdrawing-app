# Feature Specifications: Manual CUI

## 1. User Stories
- Như một _kỹ sư bóc tách khối lượng (Người dùng)_, tôi muốn có một nút bấm `Manual` hiển thị ngay trên thanh Ribbon (Tab System) để bất kỳ lúc nào vướng mắc tôi cũng có thể gọi ra đọc.
- Bấm vào bảng hướng dẫn, tôi muốn xem 1 lưu đồ (Timeline) rõ ràng giúp tôi biết phải gọi lệnh gì, theo trình tự nào cho một dự án bóc tường.
- Tôi muốn các liên kết Hướng dẫn này tự động gắn lệnh Autocad. Khi click vào HDSD, phần mềm tự chạy lệnh giúp tôi để tôi đỡ phải đi tìm lại trên thanh Ribbon (Quick Action).

## 2. Requirements & Business Logic
1. **Ribbon UI**: Nút nhấn có icon sổ tay (Manual), nằm ở group cuối "System" bên cạnh nút Update.
2. **Commands**: Command mới `SD_MANUAL` dùng để gọi popup.
3. **Data/Content (Static)**:
   - Hỗ trợ giao diện điều hướng bằng **TabControl**:
     - **Tab 1: Luồng Tender**: (1) Khai báo Input -> (2) Khai báo Tender Spec -> (3) Chọn Vách -> (4) Vẽ lỗ mở -> (5) Bản vẽ CAD -> (6) Xuất Tally BOM Excel.
     - **Tab 2: Tips & Tricks**: Mẹo dùng cực nhanh.
4. **Actionable Links (Quick Start)**: Bấm vào dòng chữ "Cấu hình Input ngay >>", tự động nhả lệnh `SD_INPUT` vào CAD editor để mở lên luồng làm việc cho user.
