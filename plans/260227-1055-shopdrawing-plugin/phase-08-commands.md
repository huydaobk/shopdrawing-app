# Phase 08: BOM Live Table + Commands Integration
Status: ⬜ Pending
Sprint: 4 (T7-8)
Dependencies: Phase 05, 06, 07

## Objective
Tích hợp toàn bộ vào 4 AutoCAD commands. Implement BOM auto-update TABLE.

---

## Commands

### Commands/WallCreateCommand.cs — `SD_WALL_CREATE`
```csharp
[CommandMethod("SD_WALL_CREATE")]
public void Execute()
{
    // 1. Mở WallCreateDialog
    var dialog = new WallCreateDialog(specConfigManager);
    if (dialog.ShowDialog() != true) return;

    // 2. User chọn boundary polyline (dialog ẩn tạm)
    var polyline = PromptSelectPolyline();

    // 3. Tính layout
    var result = layoutEngine.Calculate(dialog.BuildRequest(polyline));

    // 4. Check waste match cho tấm lẻ
    if (result.RemnantPanel != null)
    {
        var match = wasteMatcher.FindBestMatch(result.RemnantPanel);
        if (match != null)
        {
            var suggest = new WasteSuggestionDialog(result.RemnantPanel, match);
            if (suggest.ShowDialog() == true && suggest.UseFromStock)
            {
                wasteMatcher.AcceptReuse(match.Id);
                result.RemnantPanel.IsReused = true;
                result.RemnantPanel.SourceId = match.PanelCode;
            }
        }
    }

    // 5. Vẽ tất cả vào DWG
    using var tr = doc.Database.TransactionManager.StartTransaction();
    blockManager.DrawAllPanels(result.AllPanels, tr);
    tr.Commit();

    // 6. Hỏi lưu tấm lẻ vào kho
    if (result.RemnantPanel != null && !result.RemnantPanel.IsReused)
        AskSaveRemnant(result.RemnantPanel, dialog.WallCode);

    // 7. Refresh BOM
    bomManager.Refresh();
}
```

### Commands/SpecCommand.cs — `SD_SPEC`
```csharp
[CommandMethod("SD_SPEC")]
public void Execute()
{
    var dialog = new SpecManagerDialog(specConfigManager);
    dialog.ShowDialog();
    // SpecManagerDialog.Save() đã ghi JSON → không cần làm thêm
}
```

### Commands/WasteCommand.cs — `SD_WASTE`
```csharp
[CommandMethod("SD_WASTE")]
public void Execute()
{
    // Mở dialog xem/thêm thủ công tấm lẻ
    // DataGrid hiển thị tất cả panels (available/used/discarded)
}
```

### Commands/BomCommand.cs — `SD_BOM`
```csharp
[CommandMethod("SD_BOM")]
public void Execute()
{
    bomManager.CreateOrRefresh(); // Tạo mới nếu chưa có, refresh nếu đã có
}
```

---

## Core/BomManager.cs — Live BOM Table

### Auto-update via ObjectModified Reactor
```csharp
public class BomManager
{
    private ObjectId _tableId = ObjectId.Null;

    // Đăng ký event khi plugin load
    public void RegisterReactor()
    {
        doc.Database.ObjectModified += OnObjectModified;
    }

    private void OnObjectModified(object sender, ObjectEventArgs e)
    {
        // Chỉ refresh khi AttributeReference của SD_PANEL_TAG thay đổi
        if (e.DBObject is AttributeReference attr &&
            IsPartOfPanelTag(attr))
        {
            // Debounce: chờ 500ms để batch nhiều thay đổi cùng lúc
            RefreshDebounced();
        }
    }

    public void Refresh()
    {
        // 1. Scan toàn bộ BlockReference tên "SD_PANEL_TAG"
        // 2. Đọc attributes: PANEL_ID, SPEC, DIMENSIONS, THICKNESS, STATUS
        // 3. Group by (PANEL_ID, SPEC, DIMENSIONS, WALL, STATUS) → tính Qty
        // 4. Update AutoCAD TABLE entity
    }

    private void UpdateTable(List<BomRow> rows)
    {
        // Nếu _tableId chưa có → tạo TABLE mới ở góc trên phải model space
        // Nếu có → update cells từng row
        // Header: PANEL_ID | SPEC | L×T | QTY | WALL | STATUS
    }
}
```

### BOM Table Layout
```
Tên bảng: "BẢNG THỐNG KÊ TẤM — TOÀN BỘ BẢN VẼ"
Cột: PANEL_ID (60) | SPEC (40) | L×T (50) | QTY (25) | WALL (30) | STATUS (60)
Row tấm tái dùng: màu xanh lam để phân biệt
```

---

## Files to Create
- `Commands/WallCreateCommand.cs`
- `Commands/SpecCommand.cs`
- `Commands/WasteCommand.cs`
- `Commands/BomCommand.cs`
- `Core/BomManager.cs`

## Test Criteria
- [ ] SD_WALL_CREATE: full flow — dialog → chọn polyline → vẽ → lưu kho → BOM update
- [ ] SD_SPEC: thêm Spec4, reload dialog → xuất hiện trong dropdown
- [ ] SD_BOM: tạo TABLE đúng vị trí, đúng cột
- [ ] ObjectModified: chỉnh attribute tấm → BOM tự cập nhật (test bằng double-click tag)
- [ ] BOM không crash khi DWG không có tấm nào

---

## 🎯 Integration Test Checklist
- [ ] Layout 15 tấm Wall A3 → BOM hiện WP-A3-01 qty 14, WP-A3-02 qty 1
- [ ] Confirm dùng tấm từ kho → tấm lẻ hatch xanh, SOURCE_ID đúng, kho mark used
- [ ] Sửa WALL CODE của tấm → BOM refresh, grouping thay đổi
- [ ] SD_SPEC thêm Spec4 → dropdown trong WallCreateDialog có Spec4 ngay
