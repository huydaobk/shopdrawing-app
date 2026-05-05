import sys, openpyxl
sys.stdout.reconfigure(encoding='utf-8')

def analyze_sheet(ws, sheet_name, label):
    print(f"\n{'='*60}")
    print(f"  {label}: {sheet_name}")
    print(f"{'='*60}")

    # Page setup
    ps = ws.page_setup
    ori = "Landscape" if ps.orientation == "landscape" else "Portrait"
    paper = ps.paperSize  # 9=A4
    print(f"  Orientation  : {ori}")
    print(f"  Paper size   : {paper} (9=A4, 1=Letter)")

    # Dimensions
    dims = ws.column_dimensions
    col_count = ws.max_column
    widths = [round(dims[openpyxl.utils.get_column_letter(i+1)].width or 0, 1) for i in range(col_count)]
    print(f"  Col widths   : {widths}")

    # Find header row (first row with fill)
    header_row = None
    for row in ws.iter_rows():
        for cell in row:
            if cell.fill and cell.fill.patternType == 'solid':
                try:
                    rgb = cell.fill.fgColor.rgb
                    if rgb and rgb != '00000000':
                        header_row = cell.row
                        break
                except:
                    pass
        if header_row:
            break

    if header_row:
        print(f"  Header row   : {header_row}")
        row = ws[header_row]
        for c in row:
            if c.value:
                try:
                    fg = c.fill.fgColor.rgb
                except:
                    fg = 'N/A'
                try:
                    fc = c.font.color.rgb
                except:
                    fc = 'N/A'
                print(f"    [{c.column_letter}] {str(c.value)[:25]:<25} fill={fg}  font={fc}")
    else:
        print("  Header row   : NOT FOUND (no solid fill detected)")
        # Print first few rows anyway
        for i, row in enumerate(ws.iter_rows(max_row=8)):
            for c in row:
                if c.value:
                    print(f"    Row {c.row} [{c.column_letter}]: {str(c.value)[:30]}")
            if i > 6:
                break

    # Row heights
    print(f"\n  Row heights (first 10):")
    for i in range(1, min(11, ws.max_row+1)):
        h = ws.row_dimensions[i].height
        print(f"    Row {i}: {h}")

def compare_files(exported_path, v9_path):
    print("\n" + "="*60)
    print("  EXPORTED FILE ANALYSIS")
    print("="*60)
    wb_exp = openpyxl.load_workbook(exported_path, data_only=True)
    for name in wb_exp.sheetnames:
        analyze_sheet(wb_exp[name], name, "EXPORTED")

    print("\n\n" + "="*60)
    print("  V9 REFERENCE FILE ANALYSIS")
    print("="*60)
    wb_v9 = openpyxl.load_workbook(v9_path, data_only=True)
    for name in wb_v9.sheetnames:
        analyze_sheet(wb_v9[name], name, "V9 REF")

compare_files('exported_latest.xlsx', 'v9_temp.xlsx')
