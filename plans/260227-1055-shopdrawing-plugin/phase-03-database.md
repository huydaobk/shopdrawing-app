# Phase 03: SQLite Database Layer
Status: ✅ Complete
Sprint: 1 (T2)
Dependencies: Phase 02

## Objective
Implement WasteRepository — CRUD operations cho waste panel DB.

## Database Schema
```sql
CREATE TABLE IF NOT EXISTS panels (
  id          INTEGER PRIMARY KEY AUTOINCREMENT,
  panel_code  TEXT NOT NULL,
  width_mm    REAL NOT NULL,
  length_mm   REAL NOT NULL,
  thick_mm    INTEGER NOT NULL,
  panel_spec  TEXT NOT NULL,
  joint_left  TEXT NOT NULL,   -- 'M' | 'F' | 'C'
  joint_right TEXT NOT NULL,   -- 'M' | 'F' | 'C'
  source_wall TEXT,
  project     TEXT,
  added_at    TEXT DEFAULT (datetime('now')),
  status      TEXT DEFAULT 'available'
);
```

## Data/DatabaseSchema.cs
- `Initialize(string dbPath)` — tạo DB + table nếu chưa có
- Migration-safe: `CREATE TABLE IF NOT EXISTS`

## Data/WasteRepository.cs
```csharp
public class WasteRepository
{
    // Thêm tấm lẻ vào kho
    public void AddPanel(WastePanel panel);

    // Match: trả về danh sách khớp, sort by ABS(width - required)
    public List<WastePanel> FindMatches(
        double widthMm, double lengthMm, int thickMm,
        string spec, JointType jointLeft, JointType jointRight,
        double toleranceMm = 20.0);

    // Đánh dấu đã dùng (KHÔNG DELETE)
    public void MarkAsUsed(int id);

    // Lấy toàn bộ để hiển thị trong SD_WASTE dialog
    public List<WastePanel> GetAll(string statusFilter = "available");

    // Xóa hẳn (discard)
    public void Discard(int id);
}
```

## Match Logic SQL
```sql
SELECT * FROM panels
WHERE panel_spec = @spec
  AND thick_mm = @thick
  AND width_mm  BETWEEN @width  - @tol AND @width  + @tol
  AND length_mm BETWEEN @length - @tol AND @length + @tol
  AND joint_left  = @jLeft
  AND joint_right = @jRight
  AND status = 'available'
ORDER BY ABS(width_mm - @width) ASC
```

## Files to Create
- `Data/DatabaseSchema.cs`
- `Data/WasteRepository.cs`

## Test Criteria
- [ ] Tạo DB file thành công
- [ ] Insert 1 panel, query lại được
- [ ] Match logic: tolerance ±20mm hoạt động đúng
- [ ] MarkAsUsed → không xuất hiện trong FindMatches nữa
- [ ] Joint mismatch → không trả về kết quả

---
Next: [Phase 04 — Layout Engine](phase-04-layout.md)
