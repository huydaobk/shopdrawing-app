# Tasks Checklist: Manual CUI

## Giai đoạn 1: Chuẩn bị CUI & Lệnh (Ribbon & Commands)
- [x] Khai báo lệnh `SD_MANUAL` (Code ở file Commands backend).
- [x] Tìm hình ảnh (hoặc dùng icon chuẩn) để load ảnh cho nút bấm Manual.
- [x] Chèn nút Manual vào Panel System ở `RibbonInitializer.cs`.

## Giai đoạn 2: Thiết kế Giao diện (WPF Dialog)
- [x] Dựng Win GUI: Tạo file `UI/Manual/ManualWindow.xaml` dọn dẹp các control basic.
- [x] Trang trí Tab "Tender" dạng cấu trúc Timeline/Steppers (Theo luồng 1->2...->6).
- [x] Bật Tab "Tips & Các mẹo" thêm phím tắt vặt.
- [x] Thêm link (bấm để mở command).

## Giai đoạn 3: Code Logic "Quick Launcher" & Rà soát QA
- [x] Lập trình sự kiện `Hyperlink_Click` trong file XAML.cs. Đóng form và Push string command vào hệ thống AutoCAD cho execute lập tức.
- [x] Build & Test trên AutoCAD thật để đảm bảo Text/Icon đẹp, và send command ok. 
