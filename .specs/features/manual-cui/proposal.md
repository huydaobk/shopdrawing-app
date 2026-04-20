# CUI: Manual Quick Guide
**Trạng thái:** Confirmed  
**Người khởi xướng:** User  
**Người hỗ trợ:** Antigravity  

## Tóm tắt vấn đề
- AutoCAD plugin hiện tại chưa có tài liệu/hướng dẫn sử dụng In-App. 
- Người dùng mới chưa hiểu luồng cơ bản (Input ban đầu -> Các thao tác Tender -> Trích xuất dữ liệu) làm cản trở quá trình onboarding.

## Hướng giải quyết đề xuất (Proposed Solution)
- Thiết kế một WPF Dialog "Manual Helper", được gọi ra thông qua một nút "Manual" dính liền vào Ribbon Panel "System" (lệnh `SD_MANUAL`).
- Chứa hướng dẫn dạng Timeline View để người dùng dễ hình dung trật tự các bước, chia làm nhiều Tab để sau này có thể mở rộng (Ví dụ: Tab Tender, Tab Tips & Tricks).
- Hỗ trợ "Quick Start Launcher" - Bấm vào các bước cấu hình thì tự động tắt Popup HDSD và gọi lệnh Command thực thị tương ứng.
