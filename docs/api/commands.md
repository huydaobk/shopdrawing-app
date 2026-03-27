# AutoCAD Command Documentation

## `SD_WALL_CREATE`
**Description**: Main workflow for creating a wall layout.
1. User configures specs in a dialog.
2. User selects a Polyline boundary.
3. System calculates layout and suggests waste reuse.
4. System draws entities and updates the BOM table.

---

## `SD_SPEC`
**Description**: Opens the Specification Manager.
- View and edit panel types (Specs).
- Persistence to `panel_specs.json`.

---

## `SD_BOM`
**Description**: Manually refreshes the BOM Table.
- Scans model space for `SD_PANEL_TAG` blocks.
- Regenerates the statistics table.

---

## `SD_WASTE`
**Description**: Placeholder for future visual waste management.
