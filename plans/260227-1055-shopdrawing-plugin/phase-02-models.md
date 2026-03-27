# Phase 02: Core Models & Spec Config
Status: ✅ Complete
Sprint: 1 (T1-2)
Dependencies: Phase 01

## Objective
Định nghĩa các model C# và SpecConfigManager (đọc/ghi panel_specs.json).

## Models to Create

### Models/Panel.cs
```csharp
public class Panel
{
    public string PanelId { get; set; }     // WP-A3-01
    public string WallCode { get; set; }    // A3
    public double X { get; set; }
    public double Y { get; set; }
    public double WidthMm { get; set; }
    public double LengthMm { get; set; }
    public int ThickMm { get; set; }
    public string Spec { get; set; }        // Spec1
    public JointType JointLeft { get; set; }
    public JointType JointRight { get; set; }
    public bool IsReused { get; set; }
    public string SourceId { get; set; }    // null nếu mới
}

public enum JointType { Male, Female, Cut }
```

### Models/WastePanel.cs
```csharp
public class WastePanel
{
    public int Id { get; set; }
    public string PanelCode { get; set; }
    public double WidthMm { get; set; }
    public double LengthMm { get; set; }
    public int ThickMm { get; set; }
    public string PanelSpec { get; set; }
    public JointType JointLeft { get; set; }
    public JointType JointRight { get; set; }
    public string SourceWall { get; set; }
    public string Project { get; set; }
    public string Status { get; set; }      // available | used | discarded
}
```

### Models/LayoutResult.cs
```csharp
public class LayoutResult
{
    public List<Panel> FullPanels { get; set; }
    public Panel RemnantPanel { get; set; }   // null nếu vừa khít
    public WastePanel SuggestedWaste { get; set; } // null nếu không có match
    public int TotalCount => FullPanels.Count + (RemnantPanel != null ? 1 : 0);
}
```

### Models/PanelSpec.cs
```csharp
public class PanelSpec
{
    public string Key { get; set; }          // "Spec1"
    public string Description { get; set; } // "Tôn/Tôn"
    public string DisplayName => $"{Key} — {Description}";
}
```

## Core/SpecConfigManager.cs
```csharp
public class SpecConfigManager
{
    private readonly string _configPath;
    private Dictionary<string, string> _specs;

    public List<PanelSpec> GetAll() { ... }
    public void Save(List<PanelSpec> specs) { ... } // Ghi lại JSON
    public PanelSpec GetByKey(string key) { ... }
}
```

## Files to Create
- `Models/Panel.cs`
- `Models/WastePanel.cs`
- `Models/LayoutResult.cs`
- `Models/PanelSpec.cs`
- `Core/SpecConfigManager.cs`

## Test Criteria
- [ ] Serialize/Deserialize panel_specs.json thành công
- [ ] JointType enum hoạt động đúng (M/F/C)
- [ ] Build không lỗi

---
Next: [Phase 03 — SQLite](phase-03-database.md)
