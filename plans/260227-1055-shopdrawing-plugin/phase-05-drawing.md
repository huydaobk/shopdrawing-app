# Phase 05: AutoCAD Drawing (BlockManager)
Status: ✅ Complete
Sprint: 2 (T3-4)
Dependencies: Phase 04

## Objective
Vẽ mỗi tấm panel thành 4-layer entity trong AutoCAD và group lại.

## Core/BlockManager.cs

### Hatch Pattern Map (theo chiều dày)
```csharp
private static readonly Dictionary<int, (string pattern, double scale)> HatchMap = new()
{
    { 50,  ("DOTS",   0.5) },
    { 60,  ("LINE",   1.0) },
    { 75,  ("LINE",   1.5) },
    { 80,  ("ANSI31", 1.0) },
    { 100, ("ANSI31", 1.5) },
    { 125, ("ANSI32", 1.0) },
    { 150, ("ANSI33", 1.0) },
    { 180, ("ANSI33", 1.5) },
    { 200, ("STEEL",  1.0) },
};
// Tấm tái sử dụng: color = Color.Blue (index 5)
// Tấm mới: color = default layer color
```

### Methods
```csharp
// 1. Vẽ Polyline biên tấm
private ObjectId DrawOutline(Panel panel, Transaction tr);

// 2. Vẽ Hatch (theo thickness, màu theo isReused)
private ObjectId DrawHatch(ObjectId outlineId, Panel panel, Transaction tr);

// 3. Vẽ Lines ngàm đực/cái/cut
private List<ObjectId> DrawJointLines(Panel panel, Transaction tr);

// 4. Insert SD_PANEL_TAG block + fill attributes
private ObjectId InsertTag(Panel panel, Transaction tr);
// SD_PANEL_TAG tạo programmatically nếu chưa có trong DWG

// 5. Group tất cả thành AutoCAD Group
private void CreatePanelGroup(Panel panel, List<ObjectId> entityIds, Transaction tr);

// Main entry
public void DrawPanel(Panel panel, Transaction tr);
public void DrawAllPanels(List<Panel> panels, Transaction tr);
```

### SD_PANEL_TAG Block Definition (tạo bằng code)
```csharp
// Kiểm tra block đã có chưa, nếu chưa thì tạo:
private void EnsureTagBlockExists(Transaction tr)
{
    // Tạo BlockTableRecord "SD_PANEL_TAG"
    // Thêm 6 AttributeDefinition:
    //   PANEL_ID, SPEC, DIMENSIONS, THICKNESS, STATUS, SOURCE_ID
    // Tạo rectangle outline cho block
}
```

### Layers cần tạo
| Layer | Color | Print |
|-------|-------|-------|
| SD_PANEL | White (7) | Yes |
| SD_HATCH | 253 | No |
| SD_JOINT | Red (1) / Cyan (4) | No |
| SD_TAG | Cyan (4) | No |

## Files to Create
- `Core/BlockManager.cs`

## Test Criteria
- [ ] Vẽ 1 tấm: có đủ 4 layer entity
- [ ] Hatch 100mm = ANSI31, 200mm = STEEL
- [ ] Tấm tái dùng: hatch màu blue
- [ ] SD_PANEL_TAG tạo được và điền đủ 6 attributes
- [ ] Group selection: click vào tấm → chọn cả nhóm
- [ ] EXPLODE được group

---
Next: [Phase 06 — Waste Matcher](phase-06-waste.md)
