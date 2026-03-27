SD_Black.ctb

Purpose
- Baseline plot style used by Layout Manager and Export PDF.
- Output is black, while lineweight hierarchy is primarily driven by SD_* layer lineweights in the drawing.

Current baseline
- SD_Black.ctb in this repo is seeded from AutoCAD's monochrome.ctb.
- This is intentional for a stable first-pass install: every color plots black and existing object/layer lineweights remain usable.

Operational flow
- The plugin copies SD_Black.ctb into the active AutoCAD "Plotters\\Plot Styles" folder on load.
- Layout page setup and PDF export both prefer SD_Black.ctb and fall back to monochrome.ctb if needed.

If the office wants a stricter CTB mapping later
- Edit SD_Black.ctb in AutoCAD Plot Style Table Editor.
- Keep the filename exactly: SD_Black.ctb.
- Commit the updated file back into Resources\\PlotStyles so future builds/installations stay consistent.
