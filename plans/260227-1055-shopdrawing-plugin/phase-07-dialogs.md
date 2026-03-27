# Phase 07: WPF Dialogs
Status: ✅ Complete
Sprint: 3 (T5-6)
Dependencies: Phase 02, 06

## Objective
Tạo 3 WPF dialog windows cho toàn bộ user interaction.

---

## Dialog 1: WallCreateDialog.xaml

### Controls
- ComboBox: Độ dày (50/60/75/80/100/125/150/180/200 mm)
- ComboBox: Width chuẩn (1060/1070/1100/1120 mm)
- ComboBox: Spec (load từ panel_specs.json — hiển thị "Spec1 — Tôn/Tôn")
- TextBox: Khe hở ngàm (default "2", mm)
- RadioButton: Hướng layout (Ngang / Dọc)
- RadioButton: Điểm bắt đầu (Trái→Phải / Phải→Trái)
- TextBox: Wall Code (e.g. "A3")
- Button: "🖱️ Chọn Boundary Polyline" → ẩn dialog, user click DWG
- Label Preview: "Tổng: 14 tấm full + 1 tấm lẻ 380mm" (sau khi chọn polyline)
- Button: "✅ Tạo Layout"
- Button: "❌ Hủy"

### Logic
```csharp
// Sau khi chọn polyline → tính preview
private void OnPolylineSelected(Polyline pl)
{
    var result = _layoutEngine.Calculate(BuildRequest(pl));
    PreviewLabel.Content = $"Tổng: {result.FullPanels.Count} tấm + " +
                           $"lẻ {result.RemnantPanel?.WidthMm:F0}mm";
}

// Validate: WallCode không rỗng, JointGap > 0
```

---

## Dialog 2: WasteSuggestionDialog.xaml

### Controls
- Label: "Cần: {width} × {length} × {thick}mm"
- Label: "Kho có: {waste.width} × {waste.length} × {waste.thick}mm ✓"
- Label: "Nguồn: {waste.SourceWall} | {waste.Project}"
- Button: "✅ Dùng tấm này"
- Button: "➕ Tấm mới"

### Result Property
```csharp
public bool UseFromStock { get; private set; }
public WastePanel SelectedWaste { get; private set; }
```

---

## Dialog 3: SpecManagerDialog.xaml

### Controls
- DataGrid: Key | Description (editable)
- Button: "➕ Thêm Spec"
- Button: "🗑️ Xóa" (xóa dòng selected)
- Button: "💾 Lưu & Đóng" → ghi lại panel_specs.json
- Button: "Hủy"

### Logic
```csharp
// Load specs từ SpecConfigManager
// Validate: Key không trùng, không rỗng
// Save: SpecConfigManager.Save(specs) → ghi JSON file
```

---

## Files to Create
- `UI/WallCreateDialog.xaml` + `.xaml.cs`
- `UI/WasteSuggestionDialog.xaml` + `.xaml.cs`
- `UI/SpecManagerDialog.xaml` + `.xaml.cs`

## Test Criteria
- [ ] WallCreateDialog: ẩn/hiện lại khi chọn polyline thành công
- [ ] Preview label hiển thị đúng số tấm
- [ ] SpecManagerDialog: thêm/sửa/lưu → panel_specs.json cập nhật
- [ ] WasteSuggestionDialog: return đúng UseFromStock value

---
Next: [Phase 08 — BOM + Commands](phase-08-commands.md)
