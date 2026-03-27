# Smart Dimension — PRD v1 (FINAL ✅)

> ✅ Tất cả câu hỏi đã chốt. Sẵn sàng code.
>
> ⚠️ **Nguyên tắc:** Mọi thao tác qua **PALETTE** — KHÔNG dùng command line.

---

## Quyết định đã chốt

| # | Nội dung | Quyết định |
|---|---------|------------|
| Q1 | Dim chiều cao bên nào? | **User chọn** Trái/Phải trong palette |
| Q2 | Dim opening | Kích thước W×H + vị trí (từ mép trái + đáy) |
| Q3 | Elevation đơn vị | **mm** (+2500, +5000...) |
| Q4 | Anti-duplicate | **Xóa cũ → vẽ lại** hoàn toàn |
| Q5 | Dim offset | **Tự động** |
| Q6 | Manual mode | **Loop liên tục** tới ESC |
| Q7 | Joint-to-joint | **Không cần** |
| Q8 | Auto 2 chiều | **Có** — mặc định BẬT |
| Q9 | Dim chéo | **Không** |
| Q10 | Tolerance | **Không** |
| Q11 | Layer | **Tách**: `SD_DIM_PANEL` + `SD_DIM_OPENING` |

---

## 1. Auto Dim — Output mong muốn

```
Ví dụ mặt đứng vách W1 (3 panel + 1 opening):

  dim chiều rộng từng panel (hàng 1)
  ←1200→ ←1200→  ←1000→  ←1200→
  dim tổng (hàng 2)
  ←──────── 4600 ──────────────→

  ┌────────┬────────┬──[  ]──┬────────┐
  │  P01   │  P02   │ OPNG   │  P03   │   dim chiều cao
  │        │        │ W×H    │        │   (bên trái hoặc phải)
  └────────┴────────┴────────┴────────┘   ↕ 3500

  ← dim opening vị trí từ mép trái →
  ← dim opening W →
  ↕ dim opening H

  EL +0     ─── đáy panel
  EL +3500  ─── đỉnh panel
  EL +1000  ─── đáy opening
  EL +2200  ─── đỉnh opening
```

---

## 2. Layers

| Layer | Màu | Chứa |
|-------|------|------|
| `SD_DIM_PANEL` | Cyan (4) | Dim width + height + overall + elevation |
| `SD_DIM_OPENING` | Magenta (6) | Dim opening (W, H, vị trí) |

---

## 3. Anti-Duplicate Logic (Q4)

**Mục tiêu:** Chạy Auto Dim lần 2 cho cùng wallCode → xóa sạch dim cũ → vẽ lại.

```
Flow khi user ấn "Tạo Dim Tự Động" cho W1:

1. Gắn XData "SD_WALL_CODE" = "W1" vào MỌI dim object khi vẽ
2. Trước khi vẽ mới:
   a. Scan ModelSpace → tìm Dimension trên layer SD_DIM_PANEL + SD_DIM_OPENING
   b. Đọc XData → nếu SD_WALL_CODE == "W1" → Erase
3. Vẽ dim mới

XData format:
  AppName: "SD_DIM_APP"
  Data: [("SD_WALL_CODE", "W1")]
```

**Nút "🗑 Xóa Dim W1"** trong palette cũng dùng cùng logic (bước 2) — chỉ xóa, không vẽ lại.

---

## 4. DimStyle Update

```
EnsureDimStyle(db, tr):
  if SD_DIM_STYLE exists:
    → open ForWrite → update Dimtxt, Dimasz, Dimexe, Dimexo, Dimgap
  else:
    → create new

textH = DefaultTextHeightMm × scaleFactor
scaleFactor = CANNOSCALE denominator / numerator
```

> Mỗi lần Auto/Manual chạy đều gọi `EnsureDimStyle` → tự cập nhật khi scale đổi.

---

## 5. Auto Dim — Chi tiết từng loại

### 5a. Dim chiều rộng (đã có — cải thiện)
- Scan `SD_TAG` text → tìm `SD_PANEL` polyline chứa text
- Dim từng panel width (hàng 1, offset nhỏ)
- Dim overall (hàng 2, offset lớn hơn)
- **Mới:** dim cả ngang + dọc cùng lúc (Q8)

### 5b. Dim chiều cao panel (mới)
- Lấy extents từ polyline `SD_PANEL` → dim dọc cạnh **trái hoặc phải** (Q1, user chọn)
- Chỉ vẽ 1 dim chiều cao (panel đầu tiên hoặc cao nhất)

### 5c. Dim opening (mới)
- Scan layer `SD_OPENING` → lấy polyline/hatch bounds
- Dim vị trí: khoảng cách từ mép trái tường tới mép trái opening
- Dim kích thước: W (ngang), H (dọc) của opening
- Layer: `SD_DIM_OPENING`

### 5d. Dim elevation (mới)
- Dựa vào extents panel + opening → vẽ text/dim tại cạnh trái
- Format: `EL +3500` (mm, Q3)
- Các mốc: đáy panel, đỉnh panel, đáy opening, đỉnh opening

---

## 6. Manual Mode Loop (Q6)

```
User click "↔ Dim Ngang" trên palette:
  loop:
    Pick pt1 (hoặc ESC → thoát)
    Pick pt2 (rubber-band từ pt1)
    Pick dim position
    Vẽ dim → lặp lại
```

> Giống DIMLINEAR native. ESC hoặc Right-click → thoát loop.

---

## 7. Palette UI v1.0

```
┌──────────────────────────────┐
│  SMART DIMENSION              │
├──────────────────────────────┤
│  Tường: [W1 ▼][🔍] Cao:[▸▼] │
│  ☑Width ☑Height ☑Open ☑Elev │
│  [📏 Tạo Dim] [🗑 Xóa Dim]  │
├──────────────────────────────┤
│  [↔ Dim Ngang] [↕ Dim Dọc]  │
├──────────────────────────────┤
│  1:100 · 2.5mm (TAG)         │
│  ✅ 12 dim cho W1             │
└──────────────────────────────┘
```

> **[🔍]** = scan bản vẽ → list wallCode có sẵn vào dropdown.
> **Cao:[▸▼]** = Trái/Phải cho dim chiều cao (Q1).
> Manual buttons tự loop tới ESC.

---

## 8. Kiến trúc code

```
SmartDimEngine.cs
├── EnsureDimLayer(db, tr, layerName)    ← mở rộng nhận tên layer
├── EnsureDimStyle(db, tr)               ← update existing thay vì skip
├── ScanWallCodes()                      ← 🆕 scan SD_TAG → list unique codes
├── ClearWallDims(wallCode)              ← 🆕 xóa dim cũ theo XData
├── AutoDimWall(request)                 ← 🔧 nhận AutoDimRequest record
│   ├── ScanPanelPositions()             ← có sẵn
│   ├── DimPanelWidths()                 ← có sẵn, gắn XData
│   ├── DimOverall()                     ← có sẵn, gắn XData
│   ├── DimPanelHeight(side)             ← 🆕 left/right
│   ├── ScanOpenings()                   ← 🆕
│   ├── DimOpenings()                    ← 🆕
│   └── DimElevations()                  ← 🆕
├── ManualDimLoop(rotation)              ← 🆕 while loop + ESC check
├── ManualDim(rotation)                  ← giữ nguyên (1 shot)
├── AttachWallXData(dim, wallCode)       ← 🆕
└── GetDimsByWallCode(wallCode)          ← 🆕

AutoDimRequest record:
  WallCode, DimWidth, DimHeight, DimOpening, DimElevation,
  HeightSide (Left/Right), IsHorizontal

SmartDimPaletteControl.cs
├── Auto Tab
│   ├── WallCode input
│   ├── HeightSide dropdown (Trái/Phải)     ← 🆕
│   ├── 4 checkboxes (Width/Height/Open/Elev) ← 🆕
│   ├── "Tạo Dim" button
│   └── "Xóa Dim" button                    ← 🆕
└── Manual Tab
    ├── 3 buttons (loop mode)                ← 🔧
    └── Scale + Status info
```

---

## 9. Thứ tự triển khai

| Phase | Feature | Effort |
|-------|---------|--------|
| P1 | Anti-duplicate (XData + xóa cũ) | ⭐ |
| P2 | DimStyle update thay vì skip | ⭐ |
| P3 | Manual mode loop (ESC to stop) | ⭐⭐ |
| P4 | Auto dim 2 chiều cùng lúc | ⭐⭐ |
| P5 | Dim chiều cao panel (left/right) | ⭐⭐ |
| P6 | Dim opening (W×H + vị trí) | ⭐⭐⭐ |
| P7 | Dim elevation (mm format) | ⭐⭐ |
| P8 | Palette UI upgrade (checkboxes, dropdown) | ⭐⭐ |

> ⭐ = 0.5 ngày | ⭐⭐ = 1 ngày | ⭐⭐⭐ = 1.5 ngày

---

## 10. Acceptance Criteria

- [ ] Auto Dim W1 → vẽ đầy đủ dim (width + height + opening + elevation)
- [ ] Chạy Auto lần 2 → dim cũ xóa sạch, dim mới thay thế
- [ ] Nút "Xóa Dim W1" → xóa sạch dim của W1
- [ ] Đổi CANNOSCALE → DimStyle tự cập nhật
- [ ] Manual dim loop → pick liên tục, ESC thoát
- [ ] Dim panel trên layer `SD_DIM_PANEL`, dim opening trên `SD_DIM_OPENING`
- [ ] Mọi thao tác chỉ qua palette — KHÔNG gõ lệnh
- [ ] Dim chiều cao panel: user chọn bên Trái/Phải
