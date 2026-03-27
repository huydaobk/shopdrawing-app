# Plan: ShopDrawing AutoCAD Plugin
Created: 2026-02-27 10:55
Status: 🟡 In Progress

## Overview
AutoCAD 2026 Plugin (C# .NET 8) tự động hóa việc triển khai Shopdrawing tấm sandwich PIR.
PRD: `C:\Users\Administrator\.gemini\antigravity\brain\89d8c86d-6195-408b-b1fa-316ec6e68a1a\prd_shopdrawing_app.md`

## Tech Stack
- **Language:** C# .NET 8
- **AutoCAD API:** AutoCAD 2026 .NET API
- **Database:** SQLite (`Microsoft.Data.Sqlite`)
- **UI:** WPF (XAML Dialogs)
- **Config:** JSON (`System.Text.Json`)

## Phases

| Phase | Tên | Status | Progress |
|-------|-----|--------|----------|
| 01 | Project Setup | ✅ Complete | 100% |
| 02 | Core Models & Spec Config | ✅ Complete | 100% |
| 03 | SQLite Database Layer | ✅ Complete | 100% |
| 04 | Layout Engine | ✅ Complete | 100% |
| 05 | AutoCAD Drawing (BlockManager) | ✅ Complete | 100% |
| 06 | Waste Match Logic | ✅ Complete | 100% |
| 07 | WPF Dialogs | ✅ Complete | 100% |
| 08 | BOM Live Table + Commands | ✅ Complete | 100% |

## MVP Completed! 🚀
All core modules implemented and building successfully.
- Layout engine with male/female joint logic.
- Waste matching and database persistence.
- Live BOM table within AutoCAD.
- Pure C# UI for maximum compatibility.

## Key Decisions
- Panel geometry: Entity thuần (Polyline + Hatch + Group)
- Hatch: phân biệt theo **chiều dày**, tấm tái dùng màu xanh lam
- Tag: `SD_PANEL_TAG` Attribute Block (6 attributes: PANEL_ID, SPEC, DIMENSIONS, THICKNESS, STATUS, SOURCE_ID)
- PANEL_ID: BOM-style, nhóm theo (length, thickness, spec), không theo width
- Tấm tái sử dụng: không có PANEL_ID, chỉ SOURCE_ID
- Joint: 3 trạng thái M/F/C, check cả 2 ngàm khi match
- Spec: panel_specs.json + SD_SPEC dialog
- Joint gap: nhập tay, default 2mm
- BOM: AutoCAD TABLE entity, auto-update qua ObjectModified event, 1 bảng toàn DWG

## Quick Commands
- Start: `/code phase-01`
- Progress: `/next`
- Save: `/save-brain`
