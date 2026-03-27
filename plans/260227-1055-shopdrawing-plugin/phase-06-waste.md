# Phase 06: Waste Match Logic
Status: ✅ Complete
Sprint: 3 (T5)
Dependencies: Phase 03, 04

## Objective
Implement WasteMatcher — kết nối LayoutEngine output với WasteRepository để gợi ý và lưu tấm lẻ.

## Core/WasteMatcher.cs

```csharp
public class WasteMatcher
{
    private readonly WasteRepository _repo;
    private const double DefaultTolerance = 20.0; // mm

    // Tìm match cho tấm lẻ từ layout result
    public WastePanel? FindBestMatch(Panel remnant)
    {
        var matches = _repo.FindMatches(
            remnant.WidthMm, remnant.LengthMm, remnant.ThickMm,
            remnant.Spec, remnant.JointLeft, remnant.JointRight,
            DefaultTolerance);
        return matches.FirstOrDefault(); // Sort theo ABS(width diff) đã làm trong SQL
    }

    // Sau khi user confirm dùng tấm từ kho
    public void AcceptReuse(int wasteId)
    {
        _repo.MarkAsUsed(wasteId); // status = 'used'
    }

    // Sau khi layout xong, lưu tấm lẻ vào kho
    public void SaveRemnant(Panel remnant, string wallCode, string project)
    {
        if (remnant.WidthMm < 200) return; // Min threshold

        var waste = new WastePanel
        {
            PanelCode = $"{remnant.PanelId}-REM",
            WidthMm = remnant.WidthMm,
            LengthMm = remnant.LengthMm,
            ThickMm = remnant.ThickMm,
            PanelSpec = remnant.Spec,
            JointLeft = remnant.JointLeft,
            JointRight = remnant.JointRight,
            SourceWall = wallCode,
            Project = project,
            Status = "available"
        };
        _repo.AddPanel(waste);
    }
}
```

## Full Flow (kết nối với WallCreateCommand)
```
LayoutEngine.Calculate() → LayoutResult
    │
    ├── result.RemnantPanel != null?
    │       └── WasteMatcher.FindBestMatch(remnant)
    │               ├── Found → Show WasteSuggestionDialog
    │               │       ├── Accept → AcceptReuse(wasteId) → panel.IsReused = true
    │               │       └── Reject → dùng tấm mới
    │               └── Not found → dùng tấm mới
    │
    └── Sau khi vẽ xong:
            └── Hỏi "Lưu tấm lẻ vào kho?"
                    └── Yes → SaveRemnant()
```

## Files to Create
- `Core/WasteMatcher.cs`

## Test Criteria
- [ ] Remnant < 200mm → không lưu kho, không gợi ý
- [ ] Match đúng theo 6 tiêu chí (spec, thick, width±20, length±20, jLeft, jRight)
- [ ] MarkAsUsed → FindBestMatch không trả về nữa
- [ ] SaveRemnant tạo entry với status = 'available'

---
Next: [Phase 07 — WPF Dialogs](phase-07-dialogs.md)
