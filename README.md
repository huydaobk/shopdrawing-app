# shopdrawing-app

Plugin AutoCAD 2026 phục vụ triển khai bản vẽ shopdrawing, layout bản vẽ, BOM/tender, xuất PDF và quản lý dữ liệu phụ trợ cho quy trình làm shopdrawing.

## Status: 🛠 Active development

Repo này đã có codebase chạy thật, không còn ở giai đoạn planning-only.

## Những gì đang có trong repo

### Core plugin
- Plugin chính: `ShopDrawing.Plugin`
- Entry AutoCAD: `ShopDrawing.Plugin/ShopDrawingApp.cs`
- Command registry: `ShopDrawing.Plugin/Commands/ShopDrawingCommands.cs`
- Nền tảng: `net8.0-windows`, WPF, target AutoCAD 2026 x64

### Các module chính
- **Panel / BOM / Block**
  - `BomManager`
  - `BlockManager`
  - `PanelIdGenerator`
  - `OpeningCutter`
  - `DetailPlacer`
- **Layout / sheet generation**
  - `LayoutEngine`
  - `LayoutManagerEngine`
  - `LayoutManagerRules`
  - `LayoutPackingService`
  - `LayoutTitleBlockConfig*`
  - `DrawingListManager`
- **Tender / accessory / project data**
  - `TenderBomCalculator`
  - `TenderAccessoryRules`
  - `TenderProjectManager`
  - `AccessoryDataManager`
- **Output / export / dimension**
  - `PdfExportEngine`
  - `ExcelExporter`
  - `SmartDimEngine`
  - `PlotStyleInstaller`
- **Waste / reuse / data persistence**
  - `WasteRepository`
  - `WasteCalculator`
  - `WasteMatcher`
  - `DatabaseSchema`

### UI hiện có
- `MainPaletteControl`
- `SmartDimPaletteControl`
- `LayoutManagerPaletteControl`
- `ExportPdfPaletteControl`
- `TenderPaletteControl`
- `TenderBomDialog`
- `SpecManagerDialog`
- `WasteManagerDialog`
- `WasteSuggestionDialog`
- `TenderAccessoryEditorDialog`
- `ExternalTitleBlockMappingDialog`

### Test hiện có
Repo đã có test project `ShopDrawing.Tests` với nhiều test chuyên đề:
- `AccessoryDataManagerTests`
- `LayoutEngineTests`
- `LayoutManagerRulesTests`
- `LayoutPackingServiceTests`
- `PlotStyleInstallerTests`
- `TenderBomCalculatorTests`

## Cấu trúc thư mục chính
- `ShopDrawing.Plugin/` — plugin AutoCAD chính
- `ShopDrawing.Tests/` — test project
- `docs/` — tài liệu, design specs, brief, PRD
- `plans/` — kế hoạch / nhánh triển khai
- `artifacts/` — output, build/test artifacts, ảnh verify
- `public/` — dữ liệu public / sample output / sqlite db

## Stack kỹ thuật
- .NET 8 (`net8.0-windows`)
- WPF
- AutoCAD 2026 managed API
- SQLite (`Microsoft.Data.Sqlite`)
- NPOI (Excel)

## Lưu ý môi trường build
Project plugin hiện reference trực tiếp AutoCAD 2026 DLL theo path Windows local:
- `C:\Program Files\Autodesk\AutoCAD 2026\accoremgd.dll`
- `C:\Program Files\Autodesk\AutoCAD 2026\acdbmgd.dll`
- `C:\Program Files\Autodesk\AutoCAD 2026\acmgd.dll`
- `C:\Program Files\Autodesk\AutoCAD 2026\AdWindows.dll`

Vì vậy muốn build/run đầy đủ cần máy Windows có AutoCAD 2026 tương ứng.

## Nhận định hiện tại
README cũ mô tả repo như một dự án mới ở giai đoạn lên ý tưởng, nhưng thực tế repo đã là một codebase khá dày với plugin, UI, test, docs và artifacts. README này được cập nhật để phản ánh đúng trạng thái hiện tại hơn.

## Next steps hợp lý
1. Chuẩn hóa lại README cho workflow build/run thật
2. Viết thêm mục kiến trúc tổng quan (commands -> core -> data -> UI)
3. Bổ sung hướng dẫn setup môi trường Windows + AutoCAD 2026
4. Gắn trạng thái từng module: stable / in-progress / experimental
