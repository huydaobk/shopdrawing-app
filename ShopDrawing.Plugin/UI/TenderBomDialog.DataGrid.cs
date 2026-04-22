using System.Collections.Generic;

using System.Collections.ObjectModel;

using System.ComponentModel;

using System.Linq;

using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ShopDrawing.Plugin.Core;

using ShopDrawing.Plugin.Models;



namespace ShopDrawing.Plugin.UI

{

    public partial class TenderBomDialog

    {

        private DataGrid CreateWallGrid()

        {

            var grid = new DataGrid
            {

                AutoGenerateColumns = false,

                CanUserAddRows = false,

                CanUserDeleteRows = false,

                CanUserSortColumns = true,
                EnableColumnVirtualization = false,
                SelectionMode = DataGridSelectionMode.Extended,

                SelectionUnit = DataGridSelectionUnit.FullRow,

                HeadersVisibility = DataGridHeadersVisibility.Column,

                AlternatingRowBackground = AltRow,

                ItemsSource = _wallRows

            };

            ConfigureGridScrolling(grid);
            ScrollViewer.SetCanContentScroll(grid, false);


            grid.Columns.Add(Col("STT", "Index", 40));

            grid.Columns.Add(ColTemplateCombo("Hạng mục", "Category", 82, TenderWall.CategoryOptions));
            grid.Columns.Add(Col("Tầng", "Floor", 58));
            grid.Columns.Add(Col("Ký hiệu", "Name", 78));
            var colLength = Col("Dài (mm)", "Length", 82, "F0"); colLength.IsReadOnly = true; grid.Columns.Add(colLength);
            var colHeight = Col("Cao (mm)", "Height", 82, "F0"); colHeight.IsReadOnly = true; grid.Columns.Add(colHeight);
            grid.Columns.Add(Col("Thả cáp (mm)", "CableDropLengthMm", 102, "F0"));
            grid.Columns.Add(ColTemplateCombo("Mã spec", "SpecKey", 108, _project.Specs.Select(s => s.Key).ToArray()));
            grid.Columns.Add(ColTemplateCombo("Khổ tấm", "PanelWidth", 78, new[] { "900", "1000", "1100", "1150", "1200" }));
            grid.Columns.Add(ColTemplateCombo("Hướng", "LayoutDirection", 64, TenderWall.LayoutDirectionOptions));
            grid.Columns.Add(ColSuspensionLayout("Hướng PK", "SuspensionLayoutDirection", 82));
            grid.Columns.Add(ColTemplateCombo("Ứng dụng", "Application", 95, TenderWall.ApplicationOptions));
            grid.Columns[6] = ColCableDrop("Thả cáp (mm)", "CableDropLengthMm", 102, "F0");
            grid.Columns[8].IsReadOnly = true;
            grid.Columns.Add(ColTopPanelTreatment("Chi tiết đỉnh vách", "TopPanelTreatment", 150));
            grid.Columns.Add(ColEndPanelTreatment("Chi tiết đầu/cuối vách", "EndPanelTreatment", 162));
            grid.Columns.Add(ColBottomPanelTreatment("Chi tiết chân vách", "BottomPanelTreatment", 150));
            grid.Columns.Add(ColBottomEdgeToggle("Xử lý chân vách", "BottomEdgeExposed", 108));
            grid.Columns.Add(ColEndEdgeCheck("Xử lý mép trái", "StartEdgeExposed", 102));
            grid.Columns.Add(ColEndEdgeCheck("Xử lý mép phải", "EndEdgeExposed", 104));
            grid.Columns.Add(ColCornerLink("Liên kết góc", "OutsideCornerCount", 88));
            grid.Columns.Add(ColVerticalJoint("Khe đứng", "VerticalJointCount", 78));


            // Computed columns (read-only, blue text)

            var cellStyleBold = new Style(typeof(DataGridCell));

            cellStyleBold.Setters.Add(new Setter(DataGridCell.FontWeightProperty, FontWeights.Bold));

            var cellStyleSpecLocked = new Style(typeof(DataGridCell));

            cellStyleSpecLocked.Setters.Add(new Setter(DataGridCell.BackgroundProperty, new SolidColorBrush(Color.FromRgb(242, 244, 247))));

            cellStyleSpecLocked.Setters.Add(new Setter(DataGridCell.ForegroundProperty, FgDark));

            cellStyleSpecLocked.Setters.Add(new Setter(DataGridCell.FontWeightProperty, FontWeights.SemiBold));

            cellStyleSpecLocked.Setters.Add(new Setter(DataGridCell.ToolTipProperty, UiText.Normalize("Khổ tấm đang lấy tự động theo mã spec trong quản lý spec.")));
            grid.Columns[8].CellStyle = cellStyleSpecLocked;


            var colArea = Col("DT hình học (m²)", "WallAreaM2Display", 115); colArea.IsReadOnly = true; colArea.CellStyle = cellStyleBold;
            var colOp = Col("DT lỗ mở (m²)", "OpeningAreaM2Display", 100); colOp.IsReadOnly = true; colOp.CellStyle = cellStyleBold;
            var colNet = Col("DT nghiệm thu (m²)", "NetAreaM2Display", 95); colNet.IsReadOnly = true; colNet.CellStyle = cellStyleBold;
            var colPanels = Col("Số tấm", "EstimatedPanelCountDisplay", 68); colPanels.IsReadOnly = true; colPanels.CellStyle = cellStyleBold;


            grid.Columns.Add(colArea);

            grid.Columns.Add(colOp);

            grid.Columns.Add(colNet);

            grid.Columns.Add(colPanels);



            grid.SelectionChanged += OnWallSelectionChanged;
            grid.PreviewMouseLeftButtonDown += OnWallGridPreviewMouseLeftButtonDown;
            grid.BeginningEdit += OnWallBeginningEdit;
            grid.CellEditEnding += OnWallCellEditEnding;


            return grid;

        }



        private void OnWallBeginningEdit(object? sender, DataGridBeginningEditEventArgs e)
        {
            if (e.Row.Item is TenderWallRow wallRow)
            {
                if (IsVerticalJointColumn(e.Column) && !wallRow.IsVerticalJointEditable)
                {
                    e.Cancel = true;
                    SetStatus("Khe đứng chỉ nhập cho Vách + Ngoài nhà.");
                    return;
                }

                if (IsSuspensionLayoutColumn(e.Column) && !wallRow.IsSuspensionLayoutEditable)
                {
                    e.Cancel = true;
                    SetStatus("Hướng PK chỉ nhập cho hạng mục Trần.");
                    return;
                }

                if (IsCornerLinkColumn(e.Column) && !wallRow.IsCornerLinkEditable)
                {
                    e.Cancel = true;
                    SetStatus("Liên kết góc chỉ nhập cho Vách + Ngoài nhà.");
                    return;
                }

                if (IsTopPanelTreatmentColumn(e.Column) && !wallRow.IsTopPanelTreatmentEditable)
                {
                    e.Cancel = true;
                    SetStatus(wallRow.IsTopPanelTreatmentFixed
                        ? "Vách + Ngoài nhà: đỉnh vách đang tự động (Diềm 01 và phụ kiện đi kèm)."
                        : "Chi tiết đỉnh vách chỉ nhập cho hạng mục Vách.");
                    return;
                }

                if (IsEndPanelTreatmentColumn(e.Column) && !wallRow.IsEndPanelTreatmentEditable)
                {
                    e.Cancel = true;
                    SetStatus("Chi tiết đầu/cuối vách không áp dụng cho Vách + Ngoài nhà.");
                    return;
                }

                if (IsBottomPanelTreatmentColumn(e.Column) && !wallRow.IsBottomPanelTreatmentEditable)
                {
                    e.Cancel = true;
                    SetStatus(wallRow.IsBottomPanelTreatmentFixed
                        ? "Vách + Ngoài nhà: chân vách đang tự động theo check box Xử lý chân."
                        : "Chi tiết chân vách chỉ nhập cho hạng mục Vách.");
                    return;
                }
            }

            _cadPreviewTimer.Stop();
            _pendingPreviewRow = null;
            _lastCadPreviewKey = null;
            ForceClearHighlight();
            _isEditingCell = true;
        }



        private void OnWallSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isEditingCell || _suspendCadOperations) return; // Suppress CAD ops during cell edit/programmatic updates
            try

            {

                if (_wallGrid.SelectedItem is TenderWallRow row)

                {

                    SyncHeightSegmentsFromCadLines(row);
                    LoadOpeningsForWall(row);
                    RefreshPanelBreakdown(row);

                    RequestCadPreview(row);
                    UpdateLiveCanvasPreview(row);

                }

                else

                {

                    _cadPreviewTimer.Stop();

                    ClearHighlight();
                    _previewCanvas?.Children.Clear();
                }

            }

            catch (Exception ex) { SetStatus($"Canh bao: {ex.Message}"); }
        }

        private void OnWallGridPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_isEditingCell || _suspendCadOperations || _wallGrid == null)
                return;

            var source = e.OriginalSource as DependencyObject;
            var clickedRow = FindVisualParent<DataGridRow>(source);
            if (clickedRow != null)
            {
                // Click lại đúng dòng đang chọn thì ép refresh preview ngay,
                // tránh tình trạng phải bấm nhiều lần mới hiện highlight.
                if (ReferenceEquals(clickedRow.DataContext, _wallGrid.SelectedItem)
                    && clickedRow.DataContext is TenderWallRow sameRow)
                {
                    _lastCadPreviewKey = null;
                    RequestCadPreview(sameRow, force: true);
                }
                return;
            }

            _wallGrid.UnselectAll();
            _cadPreviewTimer.Stop();
            _pendingPreviewRow = null;
            _lastCadPreviewKey = null;
            ForceClearHighlight();
        }

        private void OnDialogBackgroundMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (_isEditingCell || _suspendCadOperations || _wallGrid == null || !_wallGrid.IsVisible)
                return;

            var source = e.OriginalSource as DependencyObject;
            // Chỉ clear khi click hoàn toàn ngoài bảng vách.
            // Không can thiệp các click bên trong grid để tránh phải bấm nhiều lần.
            if (IsDescendantOf(source, _wallGrid))
                return;

            _cadPreviewTimer.Stop();
            _pendingPreviewRow = null;
            _lastCadPreviewKey = null;
            ForceClearHighlight();
        }

        private static T? FindVisualParent<T>(DependencyObject? child) where T : DependencyObject
        {
            while (child != null)
            {
                if (child is T matched)
                    return matched;
                child = VisualTreeHelper.GetParent(child);
            }

            return null;
        }

        private static bool IsDescendantOf(DependencyObject? child, DependencyObject ancestor)
        {
            while (child != null)
            {
                if (ReferenceEquals(child, ancestor))
                    return true;
                child = VisualTreeHelper.GetParent(child);
            }

            return false;
        }


        private void OnWallCellEditEnding(object? sender, DataGridCellEditEndingEventArgs e)

        {

            _isEditingCell = true; // block CAD ops until commit finishes

            TenderWallRow? restorePreviewRow = null;

            // Use ContextIdle priority to run after WPF edit transactions are fully committed.

            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ContextIdle, new Action(() =>

            {

                try

                {

                    if (e.Row.Item is TenderWallRow editedRow)

                    {

                        SyncWallRowSpecData(editedRow);

                        string defaultDir = TenderWall.DefaultLayoutDirection(editedRow.Category);

                        if (editedRow._prevCategory != null && editedRow._prevCategory != editedRow.Category)

                        {

                            editedRow.LayoutDirection = defaultDir;

                        }

                        editedRow.NormalizeSuspensionLayoutDirection();
                        editedRow.NormalizeTopPanelTreatment();
                        editedRow.NormalizeEndPanelTreatment();
                        editedRow.NormalizeBottomPanelTreatment();
                        editedRow.NormalizeCornerLink();

                        if (!IsSuspendedCeilingRow(editedRow))

                        {

                            editedRow.ColdStorageDivideFromMaxSide = false;

                        }

                        editedRow.NormalizeCableDropLength();
                        editedRow._prevCategory = editedRow.Category;

                        editedRow.Refresh();

                    }

                    SafeRefreshWallGrid();

                    RefreshFooter();
                    RefreshBomSummary(allowDeferredRetry: false, forceWhenPendingEdits: true);

                    if (ReferenceEquals(_wallGrid.SelectedItem, e.Row.Item)

                        && e.Row.Item is TenderWallRow selectedWr)

                    {

                        RefreshPanelBreakdown(selectedWr);

                        _lastCadPreviewKey = null;

                        restorePreviewRow = selectedWr;

                    }

                }

                catch (System.Exception ex)

            {

                ShopDrawing.Plugin.Core.PluginLogger.Error("Suppressed exception in TenderBomDialog.cs", ex);

            }

                finally

                {

                    _isEditingCell = false; // Re-enable CAD highlight operations

                    if (restorePreviewRow != null)

                    {

                        Dispatcher.BeginInvoke(

                            System.Windows.Threading.DispatcherPriority.ContextIdle,

                            new Action(() =>

                            {

                                if (!_isEditingCell

                                    && !_suspendCadOperations

                                    && ReferenceEquals(_wallGrid.SelectedItem, restorePreviewRow))

                                {

                                    RequestCadPreview(restorePreviewRow);

                                }

                            }));

                    }

                }

            }));

        }



        private DataGrid CreateOpeningGrid()

        {

            var grid = new DataGrid

            {

                AutoGenerateColumns = false,

                CanUserAddRows = false,

                CanUserDeleteRows = false,

                SelectionMode = DataGridSelectionMode.Extended,

                HeadersVisibility = DataGridHeadersVisibility.Column,

                AlternatingRowBackground = AltRow,

                ItemsSource = _openingRows

            };

            ConfigureGridScrolling(grid);



            grid.Columns.Add(ColTemplateCombo("Loại", "Type", 100, TenderOpening.TypeOptions));
            var colOWidth = Col("Rộng (mm)", "Width", 85, "F0"); colOWidth.IsReadOnly = true; grid.Columns.Add(colOWidth);
            var colOHeight = Col("Cao (mm)", "Height", 85, "F0"); colOHeight.IsReadOnly = true; grid.Columns.Add(colOHeight);
            var colOBottom = Col("Cao độ đáy (mm)", "BottomElevationMm", 115, "F0"); colOBottom.IsReadOnly = true; grid.Columns.Add(colOBottom);
            grid.Columns.Add(Col("Số lượng", "Quantity", 70));


            var cellStyleBold = new Style(typeof(DataGridCell));

            cellStyleBold.Setters.Add(new Setter(DataGridCell.FontWeightProperty, FontWeights.Bold));

            var colDt = Col("DT (m²)", "TotalAreaDisplay", 80); colDt.IsReadOnly = true; colDt.CellStyle = cellStyleBold;
            grid.Columns.Add(colDt);



            grid.BeginningEdit += OnOpeningBeginningEdit;
            grid.CellEditEnding += OnOpeningCellEditEnding;

            return grid;

        }



        private void OnOpeningBeginningEdit(object? sender, DataGridBeginningEditEventArgs e)
        {
            // Fully readonly columns for dimensions are set at init. 
            // Reserved for future opening column edge-cases.
        }

        private void OnOpeningCellEditEnding(object? sender, DataGridCellEditEndingEventArgs e)

        {

            // Use ContextIdle priority to run after WPF edit transactions are fully committed.

            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ContextIdle, new Action(() =>

            {

                try

                {

                    foreach (var row in _openingRows)
                    {
                        row.Type = TenderOpening.ResolveTypeByBottomElevation(row.BottomElevationMm);
                        row.Refresh();
                    }


                    if (_wallGrid.SelectedItem is TenderWallRow wallRow)

                    {

                        wallRow.SyncOpenings(_openingRows);

                        wallRow.Refresh();

                        SafeRefreshWallGrid();

                        RefreshFooter();

                        RefreshBomSummary(allowDeferredRetry: false, forceWhenPendingEdits: true);

                        RefreshPanelBreakdown(wallRow);

                    }

                }

                catch (System.Exception ex)

            {

                ShopDrawing.Plugin.Core.PluginLogger.Error("Suppressed exception in TenderBomDialog.cs", ex);

            }

            }));

        }



        private static DataGridTextColumn ColNote(string header, string binding, double width)

        {

            var col = Col(header, binding, width);



            var noteStyle = new Style(typeof(TextBlock), col.ElementStyle);

            noteStyle.Setters.Add(new Setter(TextBlock.TextTrimmingProperty, TextTrimming.None));
            noteStyle.Setters.Add(new Setter(TextBlock.TextWrappingProperty, TextWrapping.NoWrap));
            col.ElementStyle = noteStyle;



            return col;

        }



        private static void ConfigureGridScrolling(DataGrid grid)
        {
            grid.CanUserResizeColumns = true;
            grid.CanUserReorderColumns = false;
            grid.HorizontalScrollBarVisibility = ScrollBarVisibility.Visible;
            ScrollViewer.SetHorizontalScrollBarVisibility(grid, ScrollBarVisibility.Visible);
            ScrollViewer.SetVerticalScrollBarVisibility(grid, ScrollBarVisibility.Auto);
            ScrollViewer.SetCanContentScroll(grid, true);
        }


        private static ComboBox CreatePresetCombo(IEnumerable<string> items, string? selectedItem, double width)

        {

            var normalizedItems = items.Select(UiText.Normalize).ToList();

            var normalizedSelectedItem = UiText.Normalize(selectedItem);

            var combo = new ComboBox

            {

                Width = width,

                Height = 28,
                MinWidth = 64,
                Margin = new Thickness(0, 0, 4, 0),

                ItemsSource = normalizedItems,

                VerticalContentAlignment = VerticalAlignment.Center

            };



            if (!string.IsNullOrWhiteSpace(normalizedSelectedItem))

                combo.SelectedItem = normalizedSelectedItem;



            if (combo.SelectedIndex < 0 && combo.Items.Count > 0)

                combo.SelectedIndex = 0;



            return combo;

        }



        private TenderWallRow BuildPickTemplateRow()

        {

            var selectedRow = _wallGrid?.SelectedItem as TenderWallRow;

            string category = _pickCategoryPreset?.SelectedItem as string

                ?? selectedRow?.Category

                ?? TenderWall.CategoryOptions[0];
            string specKey = _pickSpecPreset?.SelectedItem as string

                ?? selectedRow?.SpecKey

                ?? _project.Specs.FirstOrDefault()?.Key

                ?? string.Empty;



            var row = new TenderWallRow

            {

                Category = category,

                Floor = selectedRow?.Floor ?? _wallRows.LastOrDefault()?.Floor ?? "T1",
                SpecKey = specKey,
                PanelWidth = GetWidthForSpec(specKey),
                PanelThickness = GetThicknessForSpec(specKey),
                HeightSegments = new List<TenderHeightSegment>(),
                LayoutDirection = selectedRow?.LayoutDirection ?? TenderWall.DefaultLayoutDirection(category),
                Application = _pickApplicationPreset?.SelectedItem as string

                    ?? selectedRow?.Application

                    ?? TenderWall.ApplicationOptions[0],
                CableDropLengthMm = selectedRow?.CableDropLengthMm ?? 0,

                ColdStorageDivideFromMaxSide = selectedRow?.ColdStorageDivideFromMaxSide ?? false,

                SuspensionLayoutDirection = selectedRow?.SuspensionLayoutDirection ?? string.Empty,
                TopPanelTreatment = selectedRow?.TopPanelTreatment ?? TenderWall.TopPanelTreatmentNone,
                EndPanelTreatment = selectedRow?.EndPanelTreatment ?? TenderWall.EndPanelTreatmentNone,
                BottomPanelTreatment = selectedRow?.BottomPanelTreatment ?? TenderWall.BottomPanelTreatmentNone,
                TopEdgeExposed = selectedRow?.TopEdgeExposed ?? false,
                BottomEdgeExposed = selectedRow?.BottomEdgeExposed ?? true,
                StartEdgeExposed = selectedRow?.StartEdgeExposed ?? false,

                EndEdgeExposed = selectedRow?.EndEdgeExposed ?? false

            };
            row.NormalizeSuspensionLayoutDirection();
            row.NormalizeCableDropLength();
            row.NormalizeTopPanelTreatment();
            row.NormalizeEndPanelTreatment();
            row.NormalizeBottomPanelTreatment();
            row.NormalizeCornerLink();
            return row;
        }


        private static Button Btn(string text, Brush bg, Brush fg, RoutedEventHandler click, double width = 0)

        {

            var btn = new Button

            {

                Content = UiText.Normalize(text),

                Background = bg,

                Foreground = fg,

                FontSize = 12,

                FontWeight = FontWeights.SemiBold,

                Height = 28,

                Padding = new Thickness(8, 2, 8, 2),

                Margin = new Thickness(0, 0, 4, 0),

                BorderThickness = new Thickness(0),
                VerticalContentAlignment = VerticalAlignment.Center,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                Cursor = System.Windows.Input.Cursors.Hand
            };

            if (width > 0) btn.Width = width;

            btn.Click += click;

            return btn;

        }



    }

}

