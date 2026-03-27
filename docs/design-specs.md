# Design Specifications: Smart Dimension Palette UI (Clean Concept)

## 🎨 Color Palette (WPF Compatible)
| Name | Hex | Usage |
|------|-----|-------|
| Background | `#F8F9FA` | Main palette background |
| Primary Blue | `#0078D7` | Main action buttons (Tạo Dim) |
| Primary Hover | `#106EBE` | Hover state for Primary Blue |
| Secondary Gray | `#E5E5E5` | Secondary action buttons (Xóa Dim) |
| Secondary Hover | `#D4D4D4` | Hover state for Secondary Gray |
| Outline Border | `#CCCCCC` | Borders for secondary buttons (Dim Ngang/Dọc) |
| Outline Hover BG | `#F3F3F3` | Hover background for outlined buttons |
| Text Dark | `#333333` | Primary text (Labels, TextBlocks) |
| Text Muted | `#666666` | Secondary text, hints |
| Primary Text | `#FFFFFF` | Text on Primary Blue buttons |

## 📝 Typography
- **Font Family:** `Segoe UI` (default standard for WPF) or `Inter` if available.
- **Title (H1):** 16px, Bold, Slate Gray (`#444444`)
- **Body Text:** 12px or 13px, Regular, `#333333`
- **Button Text:** 13px, SemiBold

## 📐 Spacing & Layout
- **Margin Main:** `8px` outside padding
- **Row Gap:** `10px` between vertical rows
- **Col Gap:** `8px` between horizontal elements
- **Button Height:** `28px` to `30px`
- **Dropdown/Textbox Height:** `24px` to `26px`

## 🔲 Borders
- **Corner Radius:** `4px` for all Buttons and ComboBox/TextBox elements.
- **Border Thickness:** `1px` (for outline buttons and inputs)

## 📱 Component Breakdown
1. **Title Row:** TextBlock, FontSize 16, Bold, Margin (0,0,0,12)
2. **Filters Row:** StackPanel Horizontal. ComboBoxes for Tường and Cao.
3. **Options Row:** WrapPanel with 4 CheckBoxes. Margin top 8px.
4. **Primary Actions:** Grid with 2 columns. 
   - Left: "Tạo Dim", Background `#0078D7`, Foreground `#FFFFFF`.
   - Right: "Xóa Dim", Background `#E5E5E5`, Foreground `#333333`.
5. **Secondary Actions:** Grid with 2 columns.
   - Left & Right: "Dim Ngang" / "Dim Dọc", Outline style (Border `#CCCCCC`, Background `Transparent` or `#FFFFFF`).
