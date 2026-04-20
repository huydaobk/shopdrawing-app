# Technical Design: Manual CUI

## 1. Thành phần UI Cần Tạo (Frontend/Views)
- **Ribbon Button**: Sửa tại `ShopDrawing.Plugin.UI.RibbonInitializer.cs`. Cần bổ sung icon (Dùng Stock icon hoặc icon hệ thống).
- **ManualWindow.xaml**: 
  - Một `Window` của WPF, set style chuẩn của tool (Theme hiện có).
  - Sử dụng thẻ `<TabControl>` để chia nhánh thông tin. 
  - Giao diện "Timeline": Sử dụng cấu trúc Grid kết hợp các Bullet (Circle + Text).
  - TextBlock hỗ trợ Hyperlink (`<Hyperlink Click="...">`) để hứng sự kiện Click.
  
## 2. Lệnh/Command (Backend Logic)
- Khởi tạo File `ManualCommands.cs` hoặc gộp chung vào lớp Commands hiện có. Gắn Attribute `[CommandMethod("SHOPDRAWING", "SD_MANUAL", CommandFlags.Modal)]`.
- Method điều khiển gọi bảng `new ManualWindow().ShowDialog()`.

## 3. Kiến trúc Luồng Dữ Liệu
- Do text HDSD không đổi thường xuyên, sẽ trực tiếp fix logic dạng template trên file `XAML` luôn để hiển thị cho tối ưu Layout. Đỡ tạo file Config rườm rà.
- Khi người dùng bấm kích hoạt hyperlink: đóng Window (Close()), và ngay lập tức chạy dòng lệnh:  
`Application.DocumentManager.MdiActiveDocument.SendStringToExecute("SD_INPUT ", true, false, false);`
