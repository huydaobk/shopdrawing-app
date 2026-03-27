# Phase 04: Layout Engine
Status: ✅ Complete
Sprint: 2 (T3)
Dependencies: Phase 02, 03

## Objective
Implement LayoutEngine — tính toán vị trí tấm dựa trên boundary polyline.

## Core/LayoutEngine.cs

### Input
```csharp
public class LayoutRequest
{
    public Polyline BoundaryPolyline { get; set; }
    public LayoutDirection Direction { get; set; }  // Horizontal | Vertical
    public StartEdge StartEdge { get; set; }        // Left | Right
    public double PanelWidthMm { get; set; }        // 1060/1070/1100/1120
    public int ThicknessMm { get; set; }
    public string Spec { get; set; }
    public double JointGapMm { get; set; }          // user nhập tay
    public string WallCode { get; set; }
}

public enum LayoutDirection { Horizontal, Vertical }
public enum StartEdge { Left, Right }
```

### Algorithm
```
1. GetBoundingBox(polyline) → width, height
2. total_span = width (nếu Horizontal) hoặc height (nếu Vertical)
3. panel_slot = PanelWidthMm + JointGapMm
4. n_full = Floor(total_span / panel_slot)
5. remnant_width = total_span - (n_full × panel_slot)

6. Gán ngàm (xen kẽ đực/cái):
   - Tấm 1: JointLeft = Cut (tiếp giáp tường), JointRight = Male
   - Tấm 2: JointLeft = Female, JointRight = Male
   - ... (xen kẽ)
   - Tấm lẻ cuối: JointLeft = Female, JointRight = Cut

7. Gán PANEL_ID theo BOM-style grouping:
   - Group full panels cùng length+thick+spec → ID chung
   - Remnant → ID riêng (width khác)

8. Tính tọa độ (x, y) cho từng tấm theo direction + startEdge
```

### Output
```csharp
public LayoutResult Calculate(LayoutRequest request);
```

## Files to Create
- `Core/LayoutEngine.cs`
- `Core/PanelIdGenerator.cs` — BOM-style grouping logic

## Test Criteria
- [ ] Tường 15800mm, panel 1100mm, gap 2mm → 14 tấm full + 1 lẻ 380mm
- [ ] Ngàm xen kẽ đúng M/F/C
- [ ] Tấm lẻ < 200mm → remnant = null (không lưu kho)
- [ ] PANEL_ID grouping: cùng length+thick+spec → cùng ID

---
Next: [Phase 05 — BlockManager](phase-05-drawing.md)
