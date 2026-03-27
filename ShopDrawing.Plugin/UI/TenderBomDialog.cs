using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using ShopDrawing.Plugin.Core;
using ShopDrawing.Plugin.Models;

namespace ShopDrawing.Plugin.UI
{
    /// <summary>
    /// Dialog quản lý khối lượng chào giá — 2 tabs (Nhập vách + BOM tổng hợp).
    /// Light theme — đồng bộ với WasteManagerDialog.
    /// </summary>
    public class TenderBomDialog : Window
    {
        // ═══ Color constants (match WasteManagerDialog) ═══
        private static readonly Brush BgWindow = new SolidColorBrush(Color.FromRgb(250, 250, 252));  // #FAFAFC
        private static readonly Brush FgDark = new SolidColorBrush(Color.FromRgb(44, 62, 80));       // #2C3E50
        private static readonly Brush FgGray = Brushes.Gray;
        private static readonly Brush AltRow = new SolidColorBrush(Color.FromRgb(245, 248, 255));    // #F5F8FF
        private static readonly Brush AccentBlue = new SolidColorBrush(Color.FromRgb(41, 128, 185)); // #2980B9
        private static readonly Brush AccentGreen = new SolidColorBrush(Color.FromRgb(39, 174, 96)); // #27AE60
        private static readonly Brush AccentRed = new SolidColorBrush(Color.FromRgb(231, 76, 60));   // #E74C3C
        private static readonly Brush AccentOrange = new SolidColorBrush(Color.FromRgb(243, 156, 18)); // #F39C12
        private static readonly Brush BtnGray = new SolidColorBrush(Color.FromRgb(149, 165, 166));   // #95A5A6

        private readonly TenderProject _project;
        private readonly ObservableCollection<TenderWallRow> _wallRows;
        private readonly ObservableCollection<TenderOpeningRow> _openingRows;
        private DataGrid _wallGrid = null!;
        private DataGrid _openingGrid = null!;
        private TextBlock _lblFooter = null!;
        private TextBlock _lblStatus = null!;
        private TextBlock _lblBomFooter = null!;
        private DataGrid _panelSummaryGrid = null!;
        private DataGrid _accessoryBasisGrid = null!;
        private DataGrid _accessorySummaryGrid = null!;
        private DataGrid _panelBreakdownGrid = null!;
        private CheckBox _chkAutoCadPreview = null!;
        private readonly DispatcherTimer _cadPreviewTimer;
        private TenderWallRow? _pendingPreviewRow;
        private string? _lastCadPreviewKey;

        // Highlight nguồn gốc và preview overlay tách riêng để chọn dòng không làm CAD lag.
        private readonly List<Autodesk.AutoCAD.DatabaseServices.ObjectId> _highlightedSourceEntityIds = new();
        private readonly List<Autodesk.AutoCAD.DatabaseServices.ObjectId> _previewEntityIds = new();

        public TenderBomDialog(TenderProject project)
        {
            _project = project;
            Title = $"Quản lý khối lượng đấu thầu - {project.ProjectName}";
            Width = 1100;
            Height = 700;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = BgWindow;

            _wallRows = new ObservableCollection<TenderWallRow>();
            _openingRows = new ObservableCollection<TenderOpeningRow>();
            _cadPreviewTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
            _cadPreviewTimer.Tick += OnCadPreviewTimerTick;
            LoadProjectData();

            Content = BuildLayout();
        }

        private UIElement BuildLayout()
        {
            var root = new Grid { Margin = new Thickness(12) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });                   // 0: header
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // 1: tabs
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });                   // 2: footer

            // ═══ Header ═══
            var header = new TextBlock
            {
                Text = "QUẢN LÝ KHỐI LƯỢNG CHÀO GIÁ",
                FontSize = 18, FontWeight = FontWeights.Bold,
                Foreground = FgDark,
                Margin = new Thickness(0, 0, 0, 8)
            };
            Grid.SetRow(header, 0);
            root.Children.Add(header);

            // ═══ TabControl ═══
            var tabs = new TabControl { FontSize = 13 };
            tabs.Items.Add(CreateInputTab());
            tabs.Items.Add(CreateBomTab());
            tabs.SelectionChanged += (s, e) =>
            {
                if (e.OriginalSource != tabs) return;
                if (tabs.SelectedIndex == 1)
                {
                    _cadPreviewTimer.Stop();
                    ClearHighlight();
                    RefreshBomSummary();
                }
                else if (tabs.SelectedIndex == 0 && _wallGrid?.SelectedItem is TenderWallRow selRow)
                {
                    RequestCadPreview(selRow);
                }
            };
            Grid.SetRow(tabs, 1);
            root.Children.Add(tabs);

            // ═══ Footer ═══
            var footer = new DockPanel { Margin = new Thickness(0, 10, 0, 0) };

            _lblFooter = new TextBlock
            {
                Text = GetFooterText(),
                FontSize = 13, FontWeight = FontWeights.SemiBold,
                Foreground = FgDark,
                VerticalAlignment = VerticalAlignment.Center
            };
            DockPanel.SetDock(_lblFooter, Dock.Left);
            footer.Children.Add(_lblFooter);

            var footerRight = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            var btnClose = new Button
            {
                Content = "Đóng", Width = 90, Height = 34,
                Background = AccentBlue, Foreground = Brushes.White,
                FontWeight = FontWeights.SemiBold
            };
            btnClose.Click += (s, e) => Close();
            footerRight.Children.Add(btnClose);
            DockPanel.SetDock(footerRight, Dock.Right);
            footer.Children.Add(footerRight);

            Grid.SetRow(footer, 2);
            root.Children.Add(footer);

            return root;
        }

        // ═══════════════════════════════════════════════════
        // TAB 1: NHẬP VÁCH & OPENING
        // ═══════════════════════════════════════════════════

        private TabItem CreateInputTab()
        {
            var tab = new TabItem { Header = "📝 Nhập vách và lỗ mở" };

            var panel = new Grid { Margin = new Thickness(8) };
            panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });                   // 0: info
            panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });                   // 1: toolbar
            panel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(3, GridUnitType.Star) }); // 2: wall grid
            panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });                   // 3: opening header
            panel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(2, GridUnitType.Star) }); // 4: opening grid
            panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });                   // 5: breakdown header
            panel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // 6: breakdown grid
            panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });                   // 7: status

            // Info
            var info = new TextBlock
            {
                Text = "Nhập danh sách vách/trần, kích thước và lỗ mở. Pick từ CAD hoặc nhập thủ công.",
                Foreground = FgGray, Margin = new Thickness(0, 0, 0, 6)
            };
            Grid.SetRow(info, 0);
            panel.Children.Add(info);

            var edgeGuide = new TextBlock
            {
                Text = "Quy tắc nhập nhanh: mỗi đầu vách chỉ chọn 1 trạng thái. Nếu đã là góc ngoài/góc trong thì không tick đầu lộ/cuối lộ cùng vị trí.",
                Foreground = AccentOrange,
                FontSize = 11,
                Margin = new Thickness(0, 18, 0, 0)
            };
            Grid.SetRow(edgeGuide, 0);
            panel.Children.Add(edgeGuide);

            // Toolbar
            var toolbar = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };

            toolbar.Children.Add(Btn("＋ Thêm", AccentGreen, Brushes.White, OnAddWall));

            var cboFloor = new ComboBox
            {
                Width = 70, FontSize = 12, Margin = new Thickness(4, 0, 0, 0),
                ToolTip = "Lọc theo tầng"
            };
            cboFloor.Items.Add("-- Tầng --");
            cboFloor.SelectedIndex = 0;
            toolbar.Children.Add(cboFloor);

            toolbar.Children.Add(Btn("📏 Pick Dài", AccentBlue, Brushes.White, OnPickLength));
            toolbar.Children.Add(Btn("📐 Pick DT", AccentBlue, Brushes.White, OnPickArea));
            toolbar.Children.Add(Btn("👁 Xem CAD", BtnGray, Brushes.White, OnPreviewCad));
            toolbar.Children.Add(Btn("🔄 Pick lại", AccentOrange, Brushes.White, OnRepickWall));
            toolbar.Children.Add(Btn("🗑 Xóa vách", AccentRed, Brushes.White, OnDeleteWall));

            _chkAutoCadPreview = new CheckBox
            {
                Content = "Auto preview CAD",
                Margin = new Thickness(8, 4, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                IsChecked = false
            };
            _chkAutoCadPreview.Checked += (s, e) =>
            {
                if (_wallGrid?.SelectedItem is TenderWallRow row)
                    RequestCadPreview(row);
            };
            toolbar.Children.Add(_chkAutoCadPreview);

            Grid.SetRow(toolbar, 1);
            panel.Children.Add(toolbar);

            // Wall DataGrid
            _wallGrid = CreateWallGrid();
            Grid.SetRow(_wallGrid, 2);
            panel.Children.Add(_wallGrid);

            // Opening Header + buttons
            var openingBar = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 6, 0, 4)
            };
            openingBar.Children.Add(new TextBlock
            {
                Text = "LỖ MỞ (của vách đang chọn) ",
                FontWeight = FontWeights.SemiBold, FontSize = 12,
                Foreground = AccentOrange, VerticalAlignment = VerticalAlignment.Center
            });
            openingBar.Children.Add(Btn("＋ Thêm lỗ mở", AccentGreen, Brushes.White, OnAddOpening));
            openingBar.Children.Add(Btn("📐 Pick mặt bằng", AccentBlue, Brushes.White, (s, e) => PickOpeningFromCad(false, null)));
            openingBar.Children.Add(Btn("📐 Pick mặt đứng", AccentBlue, Brushes.White, (s, e) => PickOpeningFromCad(true, null)));
            openingBar.Children.Add(Btn("🔄 Pick lại", AccentOrange, Brushes.White, OnRepickOpening));
            openingBar.Children.Add(Btn("🗑", AccentRed, Brushes.White, OnDeleteOpening, 28));
            Grid.SetRow(openingBar, 3);
            panel.Children.Add(openingBar);

            // Opening DataGrid
            _openingGrid = CreateOpeningGrid();
            Grid.SetRow(_openingGrid, 4);
            panel.Children.Add(_openingGrid);

            // Panel Breakdown Header
            var breakdownBar = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 6, 0, 4)
            };
            breakdownBar.Children.Add(new TextBlock
            {
                Text = "THỐNG KÊ TẤM SƠ BỘ ",
                FontWeight = FontWeights.SemiBold, FontSize = 12,
                Foreground = AccentBlue, VerticalAlignment = VerticalAlignment.Center
            });
            Grid.SetRow(breakdownBar, 5);
            panel.Children.Add(breakdownBar);

            // Panel Breakdown DataGrid (read-only)
            _panelBreakdownGrid = new DataGrid
            {
                AutoGenerateColumns = false,
                CanUserAddRows = false,
                CanUserDeleteRows = false,
                IsReadOnly = true,
                SelectionMode = DataGridSelectionMode.Single,
                HeadersVisibility = DataGridHeadersVisibility.Column,
                AlternatingRowBackground = AltRow,
                MaxHeight = 120
            };

            var cellStyleBold = new Style(typeof(DataGridCell));
            cellStyleBold.Setters.Add(new Setter(DataGridCell.FontWeightProperty, FontWeights.Bold));

            _panelBreakdownGrid.Columns.Add(Col("Loại", "Label", 90));
            _panelBreakdownGrid.Columns.Add(Col("Rộng (mm)", "WidthMm", 80, "F0"));
            _panelBreakdownGrid.Columns.Add(Col("Dài (mm)", "LengthMm", 80, "F0"));
            _panelBreakdownGrid.Columns.Add(Col("SL", "Count", 45));
            var colBkArea = Col("DT (m²)", "AreaM2Display", 80);
            colBkArea.CellStyle = cellStyleBold;
            _panelBreakdownGrid.Columns.Add(colBkArea);

            Grid.SetRow(_panelBreakdownGrid, 6);
            panel.Children.Add(_panelBreakdownGrid);

            // Status
            _lblStatus = new TextBlock
            {
                Text = "Sẵn sàng",
                Foreground = FgGray, FontSize = 11,
                Margin = new Thickness(0, 4, 0, 0)
            };
            Grid.SetRow(_lblStatus, 7);
            panel.Children.Add(_lblStatus);

            tab.Content = panel;
            return tab;
        }

        // ═══════════════════════════════════════════════════
        // TAB 2: BOM TỔNG HỢP
        // ═══════════════════════════════════════════════════

        private TabItem CreateBomTab()
        {
            var tab = new TabItem { Header = "📊 Khối lượng tổng hợp" };

            var panel = new Grid { Margin = new Thickness(8) };
            panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });                       // 0: toolbar
            panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });                       // 1: panel header
            panel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1.1, GridUnitType.Star) }); // 2: panel grid
            panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });                       // 3: basis header
            panel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1.1, GridUnitType.Star) }); // 4: basis grid
            panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });                       // 5: summary header
            panel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1.2, GridUnitType.Star) }); // 6: summary grid
            panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });                       // 7: footer

            var toolbar = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
            toolbar.Children.Add(Btn("🔄 Tính lại khối lượng", AccentBlue, Brushes.White, (s, e) => RefreshBomSummary()));
            toolbar.Children.Add(Btn("⚙️ Cấu hình phụ kiện", BtnGray, Brushes.White, OnEditAccessories));
            Grid.SetRow(toolbar, 0);
            panel.Children.Add(toolbar);

            var panelLabel = new TextBlock
            {
                Text = "TỔNG HỢP TẤM PANEL",
                FontWeight = FontWeights.SemiBold,
                FontSize = 12,
                Foreground = AccentBlue,
                Margin = new Thickness(0, 0, 0, 4)
            };
            Grid.SetRow(panelLabel, 1);
            panel.Children.Add(panelLabel);

            _panelSummaryGrid = new DataGrid
            {
                AutoGenerateColumns = false,
                CanUserAddRows = false,
                IsReadOnly = true,
                HeadersVisibility = DataGridHeadersVisibility.Column,
                AlternatingRowBackground = AltRow
            };
            _panelSummaryGrid.Columns.Add(Col("Tầng", "Floor", 60));
            _panelSummaryGrid.Columns.Add(Col("Hạng mục", "Category", 70));
            _panelSummaryGrid.Columns.Add(Col("Mã spec", "SpecKey", 100));
            _panelSummaryGrid.Columns.Add(Col("Số vách", "WallCount", 60));
            _panelSummaryGrid.Columns.Add(Col("Tổng dài (m)", "TotalLengthM", 85, "F1"));
            _panelSummaryGrid.Columns.Add(Col("DT vách (m²)", "WallAreaM2", 95, "F2"));
            _panelSummaryGrid.Columns.Add(Col("DT lỗ mở (m²)", "OpeningAreaM2", 105, "F2"));
            _panelSummaryGrid.Columns.Add(Col("DT net (m²)", "NetAreaM2", 85, "F2"));
            _panelSummaryGrid.Columns.Add(Col("Số tấm", "EstimatedPanels", 55));

            var wasteStyle = new Style(typeof(DataGridCell));
            wasteStyle.Setters.Add(new Setter(DataGridCell.ForegroundProperty, AccentOrange));
            wasteStyle.Setters.Add(new Setter(DataGridCell.FontWeightProperty, FontWeights.SemiBold));

            var colOrdered = Col("DT đặt hàng (m²)", "OrderedAreaM2", 100, "F2");
            var colWaste = Col("Hao hụt (m²)", "WasteAreaM2", 90, "F2");
            var colPct = Col("Hao hụt (%)", "WastePercent", 80, "F1");
            colWaste.CellStyle = wasteStyle;
            colPct.CellStyle = wasteStyle;
            _panelSummaryGrid.Columns.Add(colOrdered);
            _panelSummaryGrid.Columns.Add(colWaste);
            _panelSummaryGrid.Columns.Add(colPct);
            Grid.SetRow(_panelSummaryGrid, 2);
            panel.Children.Add(_panelSummaryGrid);

            var basisLabel = new TextBlock
            {
                Text = "CƠ SỞ TÍNH PHỤ KIỆN",
                FontWeight = FontWeights.SemiBold,
                FontSize = 12,
                Foreground = AccentOrange,
                Margin = new Thickness(0, 8, 0, 4)
            };
            Grid.SetRow(basisLabel, 3);
            panel.Children.Add(basisLabel);

            _accessoryBasisGrid = new DataGrid
            {
                AutoGenerateColumns = false,
                CanUserAddRows = false,
                IsReadOnly = true,
                HeadersVisibility = DataGridHeadersVisibility.Column,
                AlternatingRowBackground = AltRow
            };
            _accessoryBasisGrid.Columns.Add(Col("STT", "Index", 40));
            _accessoryBasisGrid.Columns.Add(Col("Tầng", "Floor", 55));
            _accessoryBasisGrid.Columns.Add(Col("Hạng mục", "Category", 70));
            _accessoryBasisGrid.Columns.Add(Col("Ký hiệu vách", "WallName", 90));
            _accessoryBasisGrid.Columns.Add(Col("Ứng dụng", "Application", 85));
            _accessoryBasisGrid.Columns.Add(Col("Mã spec", "SpecKey", 85));
            _accessoryBasisGrid.Columns.Add(Col("Phụ kiện", "AccessoryName", 150));
            _accessoryBasisGrid.Columns.Add(Col("Vật liệu", "Material", 90));
            _accessoryBasisGrid.Columns.Add(Col("Vị trí", "Position", 100));
            _accessoryBasisGrid.Columns.Add(Col("Quy tắc", "RuleLabel", 120));
            _accessoryBasisGrid.Columns.Add(Col("Cơ sở tính", "BasisLabel", 110));
            _accessoryBasisGrid.Columns.Add(Col("Giá trị", "BasisValue", 75, "F2"));
            _accessoryBasisGrid.Columns.Add(Col("Hệ số", "Factor", 60, "F2"));
            _accessoryBasisGrid.Columns.Add(Col("KL tự động", "AutoQuantity", 85, "F2"));
            _accessoryBasisGrid.Columns.Add(ColStar("Ghi chú", "Note"));
            Grid.SetRow(_accessoryBasisGrid, 4);
            panel.Children.Add(_accessoryBasisGrid);

            var summaryLabel = new TextBlock
            {
                Text = "TỔNG HỢP PHỤ KIỆN ĐẤU THẦU",
                FontWeight = FontWeights.SemiBold,
                FontSize = 12,
                Foreground = AccentGreen,
                Margin = new Thickness(0, 8, 0, 4)
            };
            Grid.SetRow(summaryLabel, 5);
            panel.Children.Add(summaryLabel);

            _accessorySummaryGrid = new DataGrid
            {
                AutoGenerateColumns = false,
                CanUserAddRows = false,
                IsReadOnly = true,
                HeadersVisibility = DataGridHeadersVisibility.Column,
                AlternatingRowBackground = AltRow
            };
            _accessorySummaryGrid.Columns.Add(Col("STT", "Index", 40));
            _accessorySummaryGrid.Columns.Add(Col("Phạm vi HM", "CategoryScope", 85));
            _accessorySummaryGrid.Columns.Add(Col("Ứng dụng", "Application", 85));
            _accessorySummaryGrid.Columns.Add(Col("Mã spec", "SpecKey", 85));
            _accessorySummaryGrid.Columns.Add(Col("Phụ kiện", "Name", 150));
            _accessorySummaryGrid.Columns.Add(Col("Vật liệu", "Material", 90));
            _accessorySummaryGrid.Columns.Add(Col("Vị trí", "Position", 100));
            _accessorySummaryGrid.Columns.Add(Col("Đơn vị", "Unit", 60));
            _accessorySummaryGrid.Columns.Add(Col("Quy tắc", "RuleLabel", 120));
            _accessorySummaryGrid.Columns.Add(Col("Cơ sở tính", "BasisLabel", 110));
            _accessorySummaryGrid.Columns.Add(Col("Giá trị", "BasisValue", 75, "F2"));
            _accessorySummaryGrid.Columns.Add(Col("Hệ số", "Factor", 60, "F2"));
            _accessorySummaryGrid.Columns.Add(Col("Hao hụt (%)", "WastePercent", 85, "F1"));
            _accessorySummaryGrid.Columns.Add(Col("KL tự động", "AutoQuantity", 85, "F2"));
            _accessorySummaryGrid.Columns.Add(Col("Điều chỉnh", "Adjustment", 80, "F2"));
            _accessorySummaryGrid.Columns.Add(Col("KL chốt", "FinalQuantity", 85, "F2"));
            _accessorySummaryGrid.Columns.Add(ColStar("Ghi chú", "Note"));
            Grid.SetRow(_accessorySummaryGrid, 6);
            panel.Children.Add(_accessorySummaryGrid);

            _lblBomFooter = new TextBlock
            {
                Foreground = FgDark,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 6, 0, 0)
            };
            Grid.SetRow(_lblBomFooter, 7);
            panel.Children.Add(_lblBomFooter);

            tab.Content = panel;
            return tab;
        }

        private void RefreshBomSummary()
        {
            SyncAllWallRowsSpecData();
            var walls = GetWallModels();
            _project.Accessories = EnsureProjectAccessoriesConfigured();
            var calculator = new TenderBomCalculator();

            var panelSummary = calculator.CalculatePanelSummary(walls);
            _panelSummaryGrid.ItemsSource = panelSummary;

            var accessoryReport = calculator.CalculateAccessoryReport(walls, _project.Accessories);
            _accessoryBasisGrid.ItemsSource = accessoryReport.BasisRows
                .Select((row, index) => TenderAccessoryBasisViewRow.FromReportRow(row, index + 1))
                .ToList();

            _accessorySummaryGrid.ItemsSource = accessoryReport.SummaryRows
                .Select((row, index) => TenderAccessorySummaryViewRow.FromReportRow(row, index + 1))
                .ToList();

            _lblBomFooter.Text =
                $"Cơ sở tính: {accessoryReport.BasisRows.Count} dòng | " +
                $"Tổng hợp phụ kiện: {accessoryReport.SummaryRows.Count} dòng | " +
                $"Khối lượng chốt: {accessoryReport.SummaryRows.Sum(r => r.FinalQuantity):F2}";

            _lblStatus.Text =
                $"Khối lượng: {panelSummary.Count} nhóm panel | " +
                $"Tổng {panelSummary.Sum(p => p.EstimatedPanels)} tấm | " +
                $"Phụ kiện chốt {accessoryReport.SummaryRows.Sum(r => r.FinalQuantity):F2}";
        }

        private List<TenderAccessory> EnsureProjectAccessoriesConfigured()
        {
            return AccessoryDataManager.NormalizeConfiguredAccessories(_project.Accessories);
        }

        /// <summary>
        /// Tra cứu chiều dày panel theo SpecKey.
        /// Fallback = 50mm nếu không tìm thấy spec khớp.
        /// </summary>
        private int GetThicknessForSpec(string? specKey)
        {
            if (!string.IsNullOrWhiteSpace(specKey))
            {
                var match = _project.Specs.FirstOrDefault(s =>
                    string.Equals(s.Key, specKey, StringComparison.OrdinalIgnoreCase));
                if (match != null)
                    return match.Thickness;
            }
            return _project.Specs.FirstOrDefault()?.Thickness ?? 50;
        }

        /// <summary>
        /// Tra cứu khổ tấm (PanelWidth) theo SpecKey.
        /// Fallback = 1100mm nếu không tìm thấy spec khớp.
        /// </summary>
        private int GetWidthForSpec(string? specKey)
        {
            if (!string.IsNullOrWhiteSpace(specKey))
            {
                var match = _project.Specs.FirstOrDefault(s =>
                    string.Equals(s.Key, specKey, StringComparison.OrdinalIgnoreCase));
                if (match != null)
                    return match.PanelWidth;
            }
            return _project.Specs.FirstOrDefault()?.PanelWidth ?? 1100;
        }

        /// <summary>
        /// Commit bất kỳ cell đang edit trước khi Refresh để tránh lỗi WPF
        /// "Refresh is not allowed during an AddNew or EditItem transaction".
        /// </summary>
        private void SafeRefreshWallGrid()
        {
            try { _wallGrid.CommitEdit(DataGridEditingUnit.Row, true); } catch { }
            SafeRefreshWallGrid();
        }

        // ═══════════════════════════════════════════════════
        // WALL GRID
        // ═══════════════════════════════════════════════════

        private DataGrid CreateWallGrid()
        {
            var grid = new DataGrid
            {
                AutoGenerateColumns = false,
                CanUserAddRows = false,
                CanUserDeleteRows = false,
                CanUserSortColumns = true,
                SelectionMode = DataGridSelectionMode.Extended,
                SelectionUnit = DataGridSelectionUnit.FullRow,
                HeadersVisibility = DataGridHeadersVisibility.Column,
                AlternatingRowBackground = AltRow,
                ItemsSource = _wallRows
            };

            grid.Columns.Add(Col("STT", "Index", 40));
            grid.Columns.Add(ColTemplateCombo("Hạng mục", "Category", 75, new[] { "Vách", "Trần", "Nền", "Ốp cột" }));
            grid.Columns.Add(Col("Tầng", "Floor", 50));
            grid.Columns.Add(Col("Ký hiệu", "Name", 70));
            grid.Columns.Add(Col("Dài (mm)", "Length", 80, "F0"));
            grid.Columns.Add(Col("Cao (mm)", "Height", 80, "F0"));
            grid.Columns.Add(Col("Thả cáp (mm)", "CableDropLengthMm", 90, "F0"));
            grid.Columns.Add(ColTemplateCombo("Mã spec", "SpecKey", 100, _project.Specs.Select(s => s.Key).ToArray()));
            grid.Columns.Add(ColTemplateCombo("Khổ tấm", "PanelWidth", 70, new[] { "900", "1000", "1100", "1150", "1200" }));
            grid.Columns.Add(ColTemplateCombo("Hướng", "LayoutDirection", 60, TenderWall.LayoutDirectionOptions));
            grid.Columns.Add(ColTemplateCombo("Ứng dụng", "Application", 85, TenderWall.ApplicationOptions));
            grid.Columns[8].IsReadOnly = true;
            grid.Columns.Add(ColCheck("Lộ trên", "TopEdgeExposed", 58));
            grid.Columns.Add(ColCheck("Lộ dưới", "BottomEdgeExposed", 58));
            grid.Columns.Add(ColCheck("Đầu lộ", "StartEdgeExposed", 58));
            grid.Columns.Add(ColCheck("Cuối lộ", "EndEdgeExposed", 60));
            grid.Columns.Add(Col("Góc ngoài", "OutsideCornerCount", 65));
            grid.Columns.Add(Col("Góc trong", "InsideCornerCount", 65));
            grid.Columns.Add(Col("Khe noi", "VerticalJointCount", 65));

            // Computed columns (read-only, blue text)
            var cellStyleBold = new Style(typeof(DataGridCell));
            cellStyleBold.Setters.Add(new Setter(DataGridCell.FontWeightProperty, FontWeights.Bold));
            var cellStyleSpecLocked = new Style(typeof(DataGridCell));
            cellStyleSpecLocked.Setters.Add(new Setter(DataGridCell.BackgroundProperty, new SolidColorBrush(Color.FromRgb(242, 244, 247))));
            cellStyleSpecLocked.Setters.Add(new Setter(DataGridCell.ForegroundProperty, FgDark));
            cellStyleSpecLocked.Setters.Add(new Setter(DataGridCell.FontWeightProperty, FontWeights.SemiBold));
            cellStyleSpecLocked.Setters.Add(new Setter(DataGridCell.ToolTipProperty, "Khổ tấm đang lấy tự động theo Mã spec trong Quản lý Spec"));
            grid.Columns[8].CellStyle = cellStyleSpecLocked;

            var colArea = Col("DT tường", "WallAreaM2Display", 75); colArea.IsReadOnly = true; colArea.CellStyle = cellStyleBold;
            var colOp = Col("DT lỗ mở", "OpeningAreaM2Display", 75); colOp.IsReadOnly = true; colOp.CellStyle = cellStyleBold;
            var colNet = Col("DT Net", "NetAreaM2Display", 65); colNet.IsReadOnly = true; colNet.CellStyle = cellStyleBold;
            var colPanels = Col("Số tấm", "EstimatedPanelCountDisplay", 55); colPanels.IsReadOnly = true; colPanels.CellStyle = cellStyleBold;

            grid.Columns.Add(colArea);
            grid.Columns.Add(colOp);
            grid.Columns.Add(colNet);
            grid.Columns.Add(colPanels);

            grid.SelectionChanged += OnWallSelectionChanged;
            grid.CellEditEnding += OnWallCellEditEnding;

            return grid;
        }

        private void OnWallSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (_wallGrid.SelectedItem is TenderWallRow row)
                {
                    LoadOpeningsForWall(row);
                    RefreshPanelBreakdown(row);
                    RequestCadPreview(row);
                }
                else
                {
                    _cadPreviewTimer.Stop();
                    ClearHighlight();
                }
            }
            catch (Exception ex) { _lblStatus.Text = $"⚠️ {ex.Message}"; }
        }

        private void OnWallCellEditEnding(object? sender, DataGridCellEditEndingEventArgs e)
        {
            // Use Background priority to run AFTER the cell value commits to source
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, new Action(() =>
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
                        editedRow._prevCategory = editedRow.Category;
                        editedRow.Refresh();
                    }
                    SafeRefreshWallGrid();
                    RefreshFooter();
                    if (_chkAutoCadPreview?.IsChecked == true
                        && ReferenceEquals(_wallGrid.SelectedItem, e.Row.Item)
                        && e.Row.Item is TenderWallRow selectedWr)
                    {
                        RefreshPanelBreakdown(selectedWr);
                        RequestCadPreview(selectedWr);
                    }
                }
                catch { }
            }));
        }

        // ═══════════════════════════════════════════════════
        // OPENING GRID
        // ═══════════════════════════════════════════════════

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

            grid.Columns.Add(ColTemplateCombo("Loại", "Type", 100, TenderOpening.TypeOptions));
            grid.Columns.Add(Col("Rộng (mm)", "Width", 85, "F0"));
            grid.Columns.Add(Col("Cao (mm)", "Height", 85, "F0"));
            grid.Columns.Add(Col("Số lượng", "Quantity", 70));

            var cellStyleBold = new Style(typeof(DataGridCell));
            cellStyleBold.Setters.Add(new Setter(DataGridCell.FontWeightProperty, FontWeights.Bold));
            var colDt = Col("DT (m²)", "TotalAreaDisplay", 80); colDt.IsReadOnly = true; colDt.CellStyle = cellStyleBold;
            grid.Columns.Add(colDt);

            grid.CellEditEnding += OnOpeningCellEditEnding;
            return grid;
        }

        private void OnOpeningCellEditEnding(object? sender, DataGridCellEditEndingEventArgs e)
        {
            // Use Background priority to run AFTER the cell value commits to source
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, new Action(() =>
            {
                try
                {
                    foreach (var row in _openingRows) row.Refresh();
                    _openingGrid.Items.Refresh();

                    if (_wallGrid.SelectedItem is TenderWallRow wallRow)
                    {
                        wallRow.SyncOpenings(_openingRows);
                        wallRow.Refresh();
                        SafeRefreshWallGrid();
                        RefreshFooter();
                        RefreshPanelBreakdown(wallRow);
                    }
                }
                catch { }
            }));
        }

        // ═══════════════════════════════════════════════════
        // ACTIONS
        // ═══════════════════════════════════════════════════

        private void OnAddWall(object sender, RoutedEventArgs e)
        {
            try
            {
                int nextIndex = _wallRows.Count + 1;
                _wallRows.Add(new TenderWallRow
                {
                    Index = nextIndex,
                    Category = "Vách",
                    Floor = _wallRows.LastOrDefault()?.Floor ?? "T1",
                    Name = $"{TenderWall.GetCategoryPrefix("Vách")}-{(char)('A' + (nextIndex - 1) % 26)}{nextIndex}",
                    PanelWidth = GetWidthForSpec(_project.Specs.FirstOrDefault()?.Key ?? ""),
                    PanelThickness = GetThicknessForSpec(_project.Specs.FirstOrDefault()?.Key ?? ""),
                    LayoutDirection = "Dọc",
                    SpecKey = _project.Specs.FirstOrDefault()?.Key ?? ""
                });
                RefreshFooter();
                _lblStatus.Text = $"✅ Đã thêm vách #{nextIndex}";
            }
            catch (Exception ex) { _lblStatus.Text = $"❌ {ex.Message}"; }
        }

        private void OnDeleteWall(object sender, RoutedEventArgs e)
        {
            var selected = _wallGrid.SelectedItems.Cast<TenderWallRow>().ToList();
            if (selected.Count == 0) { _lblStatus.Text = "⚠️ Chọn vách để xóa"; return; }
            foreach (var row in selected) _wallRows.Remove(row);
            ReindexWalls();
            _openingRows.Clear();
            _panelBreakdownGrid.ItemsSource = null;
            _cadPreviewTimer.Stop();
            ClearHighlight();
            RefreshFooter();
            _lblStatus.Text = $"🗑 Đã xóa {selected.Count} vách";
        }

        private void OnAddOpening(object sender, RoutedEventArgs e)
        {
            if (_wallGrid.SelectedItem is not TenderWallRow wallRow)
            {
                _lblStatus.Text = "⚠️ Chọn 1 vách trước khi thêm opening";
                return;
            }
            _openingRows.Add(new TenderOpeningRow { Type = "Cửa đi", Width = 2000, Height = 2500, Quantity = 1 });
            wallRow.SyncOpenings(_openingRows);
            wallRow.Refresh();
            SafeRefreshWallGrid();
            RefreshFooter();
            _lblStatus.Text = $"✅ Thêm opening cho {wallRow.Name}";
        }

        private void OnDeleteOpening(object sender, RoutedEventArgs e)
        {
            var selected = _openingGrid.SelectedItems.Cast<TenderOpeningRow>().ToList();
            foreach (var row in selected) _openingRows.Remove(row);
            if (_wallGrid.SelectedItem is TenderWallRow wallRow)
            {
                wallRow.SyncOpenings(_openingRows);
                wallRow.Refresh();
                SafeRefreshWallGrid();
                RefreshFooter();
            }
        }

        private void OnRepickOpening(object sender, RoutedEventArgs e)
        {
            if (!(_openingGrid.SelectedItem is TenderOpeningRow selectedOp))
            {
                _lblStatus.Text = "⚠️ Chọn opening cần re-pick!";
                return;
            }

            // Ask plan or elevation
            var choice = System.Windows.MessageBox.Show(
                "Chọn chế độ pick:\n\nYES = Mặt bằng (chỉ lấy Rộng)\nNO = Mặt đứng (lấy Rộng + Cao)",
                "🔄 Re-pick Opening",
                MessageBoxButton.YesNoCancel, MessageBoxImage.Question);

            if (choice == MessageBoxResult.Cancel) return;
            PickOpeningFromCad(choice == MessageBoxResult.No, selectedOp);
        }

        private void OnRepickWall(object sender, RoutedEventArgs e)
        {
            if (!(_wallGrid.SelectedItem is TenderWallRow selectedRow))
            {
                _lblStatus.Text = "⚠️ Chọn vách cần re-pick trước!";
                return;
            }

            var choice = System.Windows.MessageBox.Show(
                "Chọn chế độ pick:\n\nYES = Pick Line (chỉ lấy Chiều dài)\nNO = Pick Polyline kín (lấy Dài × Cao)",
                "🔄 Re-pick Vách",
                MessageBoxButton.YesNoCancel, MessageBoxImage.Question);

            if (choice == MessageBoxResult.Cancel) return;
            RepickWallFromCad(selectedRow, pickArea: choice == MessageBoxResult.No);
        }

        private void OnPreviewCad(object sender, RoutedEventArgs e)
        {
            if (_wallGrid.SelectedItem is not TenderWallRow row)
            {
                _lblStatus.Text = "⚠️ Chọn vách hoặc trần cần preview CAD";
                return;
            }

            RequestCadPreview(row, true);
        }

        private void RepickWallFromCad(TenderWallRow targetRow, bool pickArea)
        {
            Hide();
            try
            {
                var doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
                if (doc == null) { Show(); return; }
                var ed = doc.Editor;

                string prompt = pickArea
                    ? "\n→ Click closed Polyline để lấy Dài × Cao:"
                    : "\n→ Click Line hoặc Polyline để lấy chiều dài:";

                var opt = new Autodesk.AutoCAD.EditorInput.PromptEntityOptions(prompt);
                opt.SetRejectMessage("\nPhải là Line hoặc Polyline!");
                opt.AddAllowedClass(typeof(Autodesk.AutoCAD.DatabaseServices.Line), true);
                opt.AddAllowedClass(typeof(Autodesk.AutoCAD.DatabaseServices.Polyline), true);

                var result = ed.GetEntity(opt);
                if (result.Status != Autodesk.AutoCAD.EditorInput.PromptStatus.OK) { Show(); return; }

                using (var tr = doc.Database.TransactionManager.StartTransaction())
                {
                    var ent = tr.GetObject(result.ObjectId, Autodesk.AutoCAD.DatabaseServices.OpenMode.ForRead);
                    targetRow.CadHandle = ent.Handle.ToString();

                    if (ent is Autodesk.AutoCAD.DatabaseServices.Line line)
                    {
                        targetRow.Length = line.Length;
                    }
                    else if (ent is Autodesk.AutoCAD.DatabaseServices.Polyline pl)
                    {
                        var vertices = new System.Collections.Generic.List<double[]>();
                        for (int i = 0; i < pl.NumberOfVertices; i++)
                        {
                            var pt = pl.GetPoint2dAt(i);
                            vertices.Add(new[] { pt.X, pt.Y });
                        }
                        double minVx = vertices.Min(v => v[0]);
                        double maxVx = vertices.Max(v => v[0]);
                        double minVy = vertices.Min(v => v[1]);
                        double maxVy = vertices.Max(v => v[1]);
                        targetRow.Length = maxVx - minVx;
                        targetRow.Height = maxVy - minVy;
                        targetRow.PolygonVertices = IsRectangleByVertices(vertices) ? null : vertices;
                    }
                    tr.Commit();
                }

                Dispatcher.Invoke(() =>
                {
                    targetRow.Refresh();
                    SafeRefreshWallGrid();
                    RefreshFooter();
                    RefreshPanelBreakdown(targetRow);
                    RequestCadPreview(targetRow, true);
                    _lblStatus.Text = $"✅ Đã re-pick vách {targetRow.Name}";
                });
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() => _lblStatus.Text = $"❌ Re-pick lỗi: {ex.Message}");
            }
            finally
            {
                Dispatcher.Invoke(Show);
            }
        }

        private void OnPickLength(object sender, RoutedEventArgs e) => PickFromCad(false);
        private void OnPickArea(object sender, RoutedEventArgs e) => PickFromCad(true);

        private void PickFromCad(bool pickArea)
        {
            Hide();
            try
            {
                var doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
                if (doc == null) { Show(); return; }
                var ed = doc.Editor;

                string prompt = pickArea
                    ? "\n→ Click closed Polyline để lấy diện tích (chữ nhật hoặc đa giác):"
                    : "\n→ Click Line hoặc Polyline để lấy chiều dài:";

                var opt = new Autodesk.AutoCAD.EditorInput.PromptEntityOptions(prompt);
                opt.SetRejectMessage("\nPhải là Line hoặc Polyline!");
                opt.AddAllowedClass(typeof(Autodesk.AutoCAD.DatabaseServices.Line), true);
                opt.AddAllowedClass(typeof(Autodesk.AutoCAD.DatabaseServices.Polyline), true);

                var result = ed.GetEntity(opt);
                if (result.Status != Autodesk.AutoCAD.EditorInput.PromptStatus.OK) { Show(); return; }

                using (var tr = doc.Database.TransactionManager.StartTransaction())
                {
                    var ent = tr.GetObject(result.ObjectId, Autodesk.AutoCAD.DatabaseServices.OpenMode.ForRead);
                    var template = _wallGrid.SelectedItem as TenderWallRow;
                    string category = template?.Category ?? "Vách";
                    var newRow = new TenderWallRow
                    {
                        Index = _wallRows.Count + 1,
                        Category = category,
                        Floor = template?.Floor ?? _wallRows.LastOrDefault()?.Floor ?? "T1",
                        SpecKey = template?.SpecKey ?? _project.Specs.FirstOrDefault()?.Key ?? "",
                        PanelWidth = GetWidthForSpec(template?.SpecKey ?? _project.Specs.FirstOrDefault()?.Key ?? ""),
                        PanelThickness = GetThicknessForSpec(template?.SpecKey ?? _project.Specs.FirstOrDefault()?.Key ?? ""),
                        LayoutDirection = template?.LayoutDirection ?? TenderWall.DefaultLayoutDirection(category),
                        Application = template?.Application ?? "Ngoài nhà",
                        CableDropLengthMm = template?.CableDropLengthMm ?? 0,
                        ColdStorageDivideFromMaxSide = template?.ColdStorageDivideFromMaxSide ?? false,
                        CadHandle = ent.Handle.ToString()
                    };

                    if (ent is Autodesk.AutoCAD.DatabaseServices.Line line)
                    {
                        newRow.Length = line.Length;
                        newRow.Name = $"{TenderWall.GetCategoryPrefix(category)}-{_wallRows.Count + 1}";
                    }
                    else if (ent is Autodesk.AutoCAD.DatabaseServices.Polyline pl)
                    {
                        // Lấy OCS vertices của polyline
                        var vertices = new List<double[]>();
                        for (int i = 0; i < pl.NumberOfVertices; i++)
                        {
                            var pt = pl.GetPoint2dAt(i);
                            vertices.Add(new[] { pt.X, pt.Y });
                        }

                        // Detect "đóng" — chính thức (Closed=true) hoặc thực tế (vertex cuối ≈ vertex đầu)
                        bool isClosed = pl.Closed;
                        if (!isClosed && vertices.Count >= 4)
                        {
                            var first = vertices[0];
                            var last  = vertices[vertices.Count - 1];
                            double closingDist = Math.Sqrt(
                                Math.Pow(last[0] - first[0], 2) + Math.Pow(last[1] - first[1], 2));
                            isClosed = closingDist < 1.0; // < 1mm = đóng thực tế
                        }

                        if (pickArea && isClosed && vertices.Count >= 3)
                        {
                            // Xóa vertex trùng cuối (đóng thực tế) nếu có
                            if (!pl.Closed && vertices.Count >= 2)
                            {
                                var first = vertices[0]; var last = vertices[vertices.Count - 1];
                                if (Math.Abs(last[0]-first[0]) < 1 && Math.Abs(last[1]-first[1]) < 1)
                                    vertices.RemoveAt(vertices.Count - 1);
                            }

                            // Tính bounding box từ OCS vertices
                            double minVx = vertices.Min(v => v[0]);
                            double maxVx = vertices.Max(v => v[0]);
                            double minVy = vertices.Min(v => v[1]);
                            double maxVy = vertices.Max(v => v[1]);
                            newRow.Length = maxVx - minVx;
                            newRow.Height = maxVy - minVy;

                            // Chỉ lưu vertices nếu KHÔNG phải chữ nhật → dùng scan-line
                            bool isRectangle = IsRectangleByVertices(vertices);
                            if (!isRectangle)
                                newRow.PolygonVertices = vertices;
                        }
                        else
                        {
                            // Polyline hở → lấy chiều dài cạnh dài nhất
                            double maxSeg = 0;
                            for (int i = 0; i < pl.NumberOfVertices - 1; i++)
                                maxSeg = Math.Max(maxSeg, pl.GetLineSegmentAt(i).Length);
                            newRow.Length = maxSeg > 0 ? maxSeg : pl.Length;
                        }
                        newRow.Name = $"{TenderWall.GetCategoryPrefix(category)}-{_wallRows.Count + 1}";
                    }

                    if (pickArea && IsColdStorageCeilingRow(newRow))
                    {
                        bool? divideFromMaxSide = PromptColdStorageDivideDirection(newRow);
                        if (!divideFromMaxSide.HasValue)
                        {
                            tr.Commit();
                            Show();
                            return;
                        }

                        newRow.ColdStorageDivideFromMaxSide = divideFromMaxSide.Value;
                    }

                    tr.Commit();
                    _wallRows.Add(newRow);
                    _wallGrid.SelectedItem = newRow;
                    _wallGrid.ScrollIntoView(newRow);
                    RequestCadPreview(newRow, true);
                    RefreshFooter();
                    string polygonTag = newRow.PolygonVertices != null ? " [Polygon]" : " [Rect]";
                    ed.WriteMessage($"\n✅ Pick: {newRow.Name}{polygonTag} | Dài={newRow.Length:F0}mm | Cao={newRow.Height:F0}mm");
                }
            }
            catch (Exception ex) { _lblStatus.Text = $"❌ Pick lỗi: {ex.Message}"; }
            finally { Show(); }
        }

        /// <summary>Detect chữ nhật thuần 2D từ OCS vertices (cross product check)</summary>
        private static bool IsRectangleByVertices(List<double[]> v)
        {
            if (v.Count != 4) return false;
            const double tolerance = 0.05;
            for (int i = 0; i < 4; i++)
            {
                // Vector của 2 cạnh liên tiếp
                var a = v[i];
                var b = v[(i + 1) % 4];
                var c = v[(i + 2) % 4];
                double ax = b[0] - a[0], ay = b[1] - a[1];
                double bx = c[0] - b[0], by = c[1] - b[1];
                // Dot product: 0 = vuông góc
                double dot = ax * bx + ay * by;
                double lenA = Math.Sqrt(ax * ax + ay * ay);
                double lenB = Math.Sqrt(bx * bx + by * by);
                if (lenA < 1 || lenB < 1) return false;
                if (Math.Abs(dot) / (lenA * lenB) > tolerance) return false;
            }
            return true;
        }

        /// <summary>
        /// Pick opening dimensions from CAD by 2 points.
        /// Plan mode: 2 points along wall → Width only, prompt Height.
        /// Elevation mode: 2 diagonal points → Width + Height.
        /// </summary>
        private void PickOpeningFromCad(bool isElevation, TenderOpeningRow? existingRow)
        {
            if (!(_wallGrid.SelectedItem is TenderWallRow wallRow))
            {
                _lblStatus.Text = "⚠️ Chọn vách trước khi pick opening!";
                return;
            }

            Hide();
            try
            {
                var doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
                if (doc == null) { Show(); return; }
                var ed = doc.Editor;

                string mode = isElevation ? "MẶT ĐỨNG" : "MẶT BẰNG";
                ed.WriteMessage($"\n═══ PICK OPENING ({mode}) ═══");

                // Point 1
                var p1Result = ed.GetPoint(new Autodesk.AutoCAD.EditorInput.PromptPointOptions(
                    "\n→ Click điểm 1 của lỗ mở:"));
                if (p1Result.Status != Autodesk.AutoCAD.EditorInput.PromptStatus.OK) { Show(); return; }

                // Point 2
                var p2Opt = new Autodesk.AutoCAD.EditorInput.PromptPointOptions(
                    "\n→ Click điểm 2 (đối diện) của lỗ mở:");
                p2Opt.UseBasePoint = true;
                p2Opt.BasePoint = p1Result.Value;
                var p2Result = ed.GetPoint(p2Opt);
                if (p2Result.Status != Autodesk.AutoCAD.EditorInput.PromptStatus.OK) { Show(); return; }

                var p1 = p1Result.Value;
                var p2 = p2Result.Value;

                double dx = Math.Abs(p2.X - p1.X);
                double dy = Math.Abs(p2.Y - p1.Y);

                double widthMm, heightMm;

                if (isElevation)
                {
                    // Elevation: larger dim = Height (typical for openings)
                    double dim1 = Math.Round(dx);
                    double dim2 = Math.Round(dy);
                    widthMm = Math.Min(dim1, dim2);
                    heightMm = Math.Max(dim1, dim2);
                    ed.WriteMessage($"\n✅ Mặt đứng: Rộng={widthMm:F0}mm | Cao={heightMm:F0}mm");
                }
                else
                {
                    // Plan: distance between 2 points = Width
                    widthMm = Math.Round(Math.Sqrt(dx * dx + dy * dy));
                    ed.WriteMessage($"\n✅ Mặt bằng: Rộng={widthMm:F0}mm");

                    // Prompt for height
                    var hOpt = new Autodesk.AutoCAD.EditorInput.PromptDoubleOptions(
                        "\n→ Nhập chiều cao lỗ mở (mm):");
                    hOpt.DefaultValue = 2500;
                    hOpt.AllowNegative = false;
                    hOpt.AllowZero = false;
                    var hResult = ed.GetDouble(hOpt);
                    if (hResult.Status != Autodesk.AutoCAD.EditorInput.PromptStatus.OK) { Show(); return; }
                    heightMm = Math.Round(hResult.Value);
                    ed.WriteMessage($" | Cao={heightMm:F0}mm");
                }

                // Show confirmation dialog with swap option
                double finalW = widthMm, finalH = heightMm;
                bool confirmed = false;

                Dispatcher.Invoke(() =>
                {
                    var dlg = new Window
                    {
                        Title = "Xác nhận Opening",
                        Width = 320, Height = 180,
                        WindowStartupLocation = WindowStartupLocation.CenterScreen,
                        ResizeMode = ResizeMode.NoResize,
                        Background = new SolidColorBrush(Color.FromRgb(250, 250, 252))
                    };

                    var sp = new StackPanel { Margin = new Thickness(16) };

                    var lblInfo = new TextBlock
                    {
                        Text = $"Rộng: {finalW:F0} mm\nCao:  {finalH:F0} mm",
                        FontSize = 15, FontWeight = FontWeights.SemiBold,
                        Margin = new Thickness(0, 0, 0, 12)
                    };
                    sp.Children.Add(lblInfo);

                    var btnBar = new StackPanel { Orientation = Orientation.Horizontal };

                    var btnSwap = Btn("🔄 Đổi chiều", AccentOrange, Brushes.White, (s2, e2) =>
                    {
                        double tmp = finalW; finalW = finalH; finalH = tmp;
                        lblInfo.Text = $"Rộng: {finalW:F0} mm\nCao:  {finalH:F0} mm";
                    });
                    btnBar.Children.Add(btnSwap);

                    var btnOk = Btn("✅ OK", AccentGreen, Brushes.White, (s2, e2) =>
                    {
                        confirmed = true;
                        dlg.Close();
                    });
                    btnBar.Children.Add(btnOk);

                    var btnCancel = Btn("❌ Hủy", BtnGray, Brushes.White, (s2, e2) => dlg.Close());
                    btnBar.Children.Add(btnCancel);

                    sp.Children.Add(btnBar);
                    dlg.Content = sp;
                    dlg.ShowDialog();
                });

                if (!confirmed) return;

                // Add or update opening row
                Dispatcher.Invoke(() =>
                {
                    if (existingRow != null)
                    {
                        // Update existing
                        existingRow.Width = finalW;
                        existingRow.Height = finalH;
                        existingRow.Type = finalH >= 2000 ? "Cửa đi" : "Cửa sổ";
                        existingRow.Refresh();
                    }
                    else
                    {
                        // Add new
                        var openingRow = new TenderOpeningRow
                        {
                            Type = finalH >= 2000 ? "Cửa đi" : "Cửa sổ",
                            Width = finalW,
                            Height = finalH,
                            Quantity = 1
                        };
                        _openingRows.Add(openingRow);
                    }

                    if (_wallGrid.SelectedItem is TenderWallRow wr)
                    {
                        wr.SyncOpenings(_openingRows);
                        wr.Refresh();
                    }
                    SafeRefreshWallGrid();
                    _openingGrid.Items.Refresh();
                    RefreshFooter();
                    string action = existingRow != null ? "Cập nhật" : "Đã thêm";
                    _lblStatus.Text = $"✅ {action} opening {finalW:F0}×{finalH:F0}mm";
                });
            }
            catch (Exception ex) { _lblStatus.Text = $"❌ Pick opening lỗi: {ex.Message}"; }
            finally { Show(); }
        }

        /// <summary>
        /// Clear any existing highlight overlay entities from model space.
        /// </summary>
        private void ClearHighlight()
        {
            try
            {
                if (_highlightedSourceEntityIds.Count == 0 && _previewEntityIds.Count == 0) return;
                var doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
                if (doc == null)
                {
                    _highlightedSourceEntityIds.Clear();
                    _previewEntityIds.Clear();
                    return;
                }

                using (doc.LockDocument())
                using (var tr = doc.Database.TransactionManager.StartTransaction())
                {
                    foreach (var objId in _highlightedSourceEntityIds)
                    {
                        if (objId != Autodesk.AutoCAD.DatabaseServices.ObjectId.Null
                            && !objId.IsErased
                            && tr.GetObject(objId, Autodesk.AutoCAD.DatabaseServices.OpenMode.ForRead, false) is Autodesk.AutoCAD.DatabaseServices.Entity sourceEnt)
                        {
                            sourceEnt.Unhighlight();
                        }
                    }

                    foreach (var objId in _previewEntityIds)
                    {
                        if (objId != Autodesk.AutoCAD.DatabaseServices.ObjectId.Null && !objId.IsErased)
                        {
                            var dbObj = tr.GetObject(objId, Autodesk.AutoCAD.DatabaseServices.OpenMode.ForWrite, false);
                            dbObj?.Erase();
                        }
                    }

                    tr.Commit();
                }
                _highlightedSourceEntityIds.Clear();
                _previewEntityIds.Clear();
            }
            catch
            {
                _highlightedSourceEntityIds.Clear();
                _previewEntityIds.Clear();
            }
        }

        private void OnCadPreviewTimerTick(object? sender, EventArgs e)
        {
            _cadPreviewTimer.Stop();

            if (_pendingPreviewRow != null)
                ShowCadPreview(_pendingPreviewRow);
        }

        private void RequestCadPreview(TenderWallRow row, bool force = false)
        {
            _pendingPreviewRow = row;
            _cadPreviewTimer.Stop();

            if (force)
            {
                ShowCadPreview(row, true);
                return;
            }

            if (_chkAutoCadPreview?.IsChecked != true)
            {
                if (!string.IsNullOrWhiteSpace(row.CadHandle))
                {
                    HighlightEntity(row.CadHandle);
                    _lastCadPreviewKey = BuildCadPreviewKey(row);
                    _lblStatus.Text = $"📍 Vị trí: {row.Name}";
                }
                else
                {
                    ClearHighlight();
                    _lastCadPreviewKey = null;
                }
                return;
            }

            _cadPreviewTimer.Start();
        }

        /// <summary>
        /// Ensure SD_HIGHLIGHT layer exists, return its ObjectId.
        /// </summary>
        private static Autodesk.AutoCAD.DatabaseServices.ObjectId EnsureHighlightLayer(
            Autodesk.AutoCAD.DatabaseServices.Database db,
            Autodesk.AutoCAD.DatabaseServices.Transaction tr)
        {
            var lt = (Autodesk.AutoCAD.DatabaseServices.LayerTable)tr.GetObject(
                db.LayerTableId, Autodesk.AutoCAD.DatabaseServices.OpenMode.ForRead);

            if (lt.Has("SD_HIGHLIGHT"))
                return lt["SD_HIGHLIGHT"];

            lt.UpgradeOpen();
            var layer = new Autodesk.AutoCAD.DatabaseServices.LayerTableRecord
            {
                Name = "SD_HIGHLIGHT",
                Color = Autodesk.AutoCAD.Colors.Color.FromColorIndex(
                    Autodesk.AutoCAD.Colors.ColorMethod.ByAci, 1), // Red
                LineWeight = Autodesk.AutoCAD.DatabaseServices.LineWeight.LineWeight211
            };
            lt.Add(layer);
            tr.AddNewlyCreatedDBObject(layer, true);
            return layer.ObjectId;
        }

        /// <summary>
        /// Zoom to entity and draw a bright rectangle highlight box around it.
        /// </summary>
        private void HighlightEntity(string handleStr)
        {
            try
            {
                ClearHighlight();

                var doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
                if (doc == null) { _lblStatus.Text = "âš ï¸ KhÃ´ng tÃ¬m tháº¥y document"; return; }

                using (doc.LockDocument())
                using (var tr = doc.Database.TransactionManager.StartTransaction())
                {
                    var handle = new Autodesk.AutoCAD.DatabaseServices.Handle(Convert.ToInt64(handleStr, 16));
                    if (!doc.Database.TryGetObjectId(handle, out var objId))
                    {
                        _lblStatus.Text = "âš ï¸ Entity khÃ´ng tá»“n táº¡i (báº£n váº½ Ä‘Ã£ thay Ä‘á»•i?)";
                        tr.Commit(); return;
                    }

                    var ent = tr.GetObject(objId, Autodesk.AutoCAD.DatabaseServices.OpenMode.ForRead)
                              as Autodesk.AutoCAD.DatabaseServices.Entity;
                    if (ent == null) { tr.Commit(); return; }

                    ent.Highlight();
                    _highlightedSourceEntityIds.Add(objId);

                    tr.Commit();
                }
            }
            catch (Exception ex) { _lblStatus.Text = $"âš ï¸ Highlight: {ex.Message}"; }
        }

        private void ZoomToEntity(string handleStr)
        {
            try
            {
                ClearHighlight();

                var doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
                if (doc == null) { _lblStatus.Text = "⚠️ Không tìm thấy document"; return; }

                using (doc.LockDocument())
                using (var tr = doc.Database.TransactionManager.StartTransaction())
                {
                    var handle = new Autodesk.AutoCAD.DatabaseServices.Handle(Convert.ToInt64(handleStr, 16));
                    if (!doc.Database.TryGetObjectId(handle, out var objId))
                    {
                        _lblStatus.Text = "⚠️ Entity không tồn tại (bản vẽ đã thay đổi?)";
                        tr.Commit(); return;
                    }

                    var ent = tr.GetObject(objId, Autodesk.AutoCAD.DatabaseServices.OpenMode.ForRead)
                              as Autodesk.AutoCAD.DatabaseServices.Entity;
                    if (ent == null) { tr.Commit(); return; }

                    // Clone the original entity to match its exact contour
                    var clone = ent.Clone() as Autodesk.AutoCAD.DatabaseServices.Entity;
                    if (clone == null) { tr.Commit(); return; }

                    // Get/create highlight layer
                    var layerId = EnsureHighlightLayer(doc.Database, tr);

                    // Style the clone: red, thick line, on highlight layer
                    clone.LayerId = layerId;
                    clone.ColorIndex = 1; // Red
                    clone.LineWeight = Autodesk.AutoCAD.DatabaseServices.LineWeight.LineWeight211;

                    // Add clone to model space
                    var bt = (Autodesk.AutoCAD.DatabaseServices.BlockTable)tr.GetObject(
                        doc.Database.BlockTableId, Autodesk.AutoCAD.DatabaseServices.OpenMode.ForRead);
                    var btr = (Autodesk.AutoCAD.DatabaseServices.BlockTableRecord)tr.GetObject(
                        bt[Autodesk.AutoCAD.DatabaseServices.BlockTableRecord.ModelSpace],
                        Autodesk.AutoCAD.DatabaseServices.OpenMode.ForWrite);
                    btr.AppendEntity(clone);
                    tr.AddNewlyCreatedDBObject(clone, true);
                    _previewEntityIds.Add(clone.ObjectId);

                    // Zoom to fit entity with 1.5x padding
                    var ext = ent.GeometricExtents;
                    var view = doc.Editor.GetCurrentView();
                    view.CenterPoint = new Autodesk.AutoCAD.Geometry.Point2d(
                        (ext.MinPoint.X + ext.MaxPoint.X) / 2,
                        (ext.MinPoint.Y + ext.MaxPoint.Y) / 2);
                    view.Height = (ext.MaxPoint.Y - ext.MinPoint.Y) * 1.5;
                    view.Width = (ext.MaxPoint.X - ext.MinPoint.X) * 1.5;
                    doc.Editor.SetCurrentView(view);

                    tr.Commit();
                }
            }
            catch (Exception ex) { _lblStatus.Text = $"⚠️ Highlight: {ex.Message}"; }
        }

        private void ShowCadPreview(TenderWallRow row, bool force = false)
        {
            string previewKey = BuildCadPreviewKey(row);
            if (!force
                && (_highlightedSourceEntityIds.Count > 0 || _previewEntityIds.Count > 0)
                && string.Equals(_lastCadPreviewKey, previewKey, StringComparison.Ordinal))
            {
                return;
            }

            if (TryDrawColdStorageCeilingPreview(row))
            {
                _lastCadPreviewKey = previewKey;
                _lblStatus.Text = $"📍 Preview trần kho lạnh: {row.Name}";
                return;
            }

            if (!string.IsNullOrEmpty(row.CadHandle))
            {
                ZoomToEntity(row.CadHandle);
                _lastCadPreviewKey = previewKey;
                _lblStatus.Text = $"📍 Vị trí: {row.Name}";
            }
            else
            {
                ClearHighlight();
                _lastCadPreviewKey = null;
            }
        }

        private static string BuildCadPreviewKey(TenderWallRow row)
        {
            string handle = row.CadHandle ?? "";
            string length = row.Length.ToString("F0");
            string height = row.Height.ToString("F0");
            string drop = row.CableDropLengthMm.ToString("F0");

            return string.Join("|",
                row.Category,
                row.Application,
                row.Name,
                handle,
                row.LayoutDirection,
                row.PanelWidth,
                row.PanelThickness,
                row.ColdStorageDivideFromMaxSide,
                length,
                height,
                drop);
        }

        private bool TryDrawColdStorageCeilingPreview(TenderWallRow row)
        {
            if (!IsColdStorageCeilingRow(row) || string.IsNullOrWhiteSpace(row.CadHandle))
                return false;

            var doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            if (doc == null)
                return false;

            ClearHighlight();

            try
            {
                using (doc.LockDocument())
                using (var tr = doc.Database.TransactionManager.StartTransaction())
                {
                    var handle = new Autodesk.AutoCAD.DatabaseServices.Handle(Convert.ToInt64(row.CadHandle, 16));
                    if (!doc.Database.TryGetObjectId(handle, out var objId))
                    {
                        tr.Commit();
                        return false;
                    }

                    var pl = tr.GetObject(objId, Autodesk.AutoCAD.DatabaseServices.OpenMode.ForRead)
                        as Autodesk.AutoCAD.DatabaseServices.Polyline;
                    if (pl == null || !pl.Closed)
                    {
                        tr.Commit();
                        return false;
                    }

                    var vertices = GetPolylineVertices(pl);
                    if (vertices.Count < 3)
                    {
                        tr.Commit();
                        return false;
                    }

                    var layerId = EnsureHighlightLayer(doc.Database, tr);
                    var bt = (Autodesk.AutoCAD.DatabaseServices.BlockTable)tr.GetObject(
                        doc.Database.BlockTableId, Autodesk.AutoCAD.DatabaseServices.OpenMode.ForRead);
                    var btr = (Autodesk.AutoCAD.DatabaseServices.BlockTableRecord)tr.GetObject(
                        bt[Autodesk.AutoCAD.DatabaseServices.BlockTableRecord.ModelSpace],
                        Autodesk.AutoCAD.DatabaseServices.OpenMode.ForWrite);

                    var boundary = pl.Clone() as Autodesk.AutoCAD.DatabaseServices.Entity;
                    if (boundary != null)
                    {
                        boundary.LayerId = layerId;
                        boundary.ColorIndex = 1;
                        boundary.LineWeight = Autodesk.AutoCAD.DatabaseServices.LineWeight.LineWeight070;
                        btr.AppendEntity(boundary);
                        tr.AddNewlyCreatedDBObject(boundary, true);
                        _previewEntityIds.Add(boundary.ObjectId);
                    }

                    AddPanelPreviewLines(vertices, row, layerId, btr, tr);

                    var preview = TenderBomCalculator.GetColdStorageCeilingPreviewData(row.ToModel());
                    if (preview.HasValue)
                    {
                        bool runAlongX = IsColdStorageRunAlongX(row);
                        AddSuspensionPreviewLines(
                            vertices,
                            runAlongX,
                            row.ColdStorageDivideFromMaxSide,
                            preview.Value.TSpacingMm,
                            preview.Value.TLineCount,
                            "T",
                            3,
                            Autodesk.AutoCAD.DatabaseServices.LineWeight.LineWeight050,
                            true,
                            layerId,
                            btr,
                            tr);

                        AddSuspensionPreviewLines(
                            vertices,
                            runAlongX,
                            row.ColdStorageDivideFromMaxSide,
                            preview.Value.MushroomSpacingMm,
                            preview.Value.MushroomLineCount,
                            "M",
                            30,
                            Autodesk.AutoCAD.DatabaseServices.LineWeight.LineWeight035,
                            false,
                            layerId,
                            btr,
                            tr);
                    }

                    AddPreviewSummaryText(vertices, row, layerId, btr, tr);

                    var ext = pl.GeometricExtents;
                    var view = doc.Editor.GetCurrentView();
                    view.CenterPoint = new Autodesk.AutoCAD.Geometry.Point2d(
                        (ext.MinPoint.X + ext.MaxPoint.X) / 2,
                        (ext.MinPoint.Y + ext.MaxPoint.Y) / 2);
                    view.Height = (ext.MaxPoint.Y - ext.MinPoint.Y) * 1.5;
                    view.Width = (ext.MaxPoint.X - ext.MinPoint.X) * 1.5;
                    doc.Editor.SetCurrentView(view);

                    tr.Commit();
                    return true;
                }
            }
            catch (Exception ex)
            {
                _lblStatus.Text = $"⚠️ Preview trần kho lạnh lỗi: {ex.Message}";
                return false;
            }
        }

        private bool? PromptColdStorageDivideDirection(TenderWallRow row)
        {
            bool runAlongX = IsColdStorageRunAlongX(row);
            bool? result = null;

            Dispatcher.Invoke(() =>
            {
                string primaryLabel = runAlongX ? "Từ cạnh dưới" : "Từ cạnh trái";
                string secondaryLabel = runAlongX ? "Từ cạnh trên" : "Từ cạnh phải";
                string axisText = runAlongX ? "theo bề rộng đứng" : "theo bề rộng ngang";

                var dlg = new Window
                {
                    Title = "Chọn phương chia tuyến treo",
                    Width = 360,
                    Height = 210,
                    WindowStartupLocation = WindowStartupLocation.CenterScreen,
                    ResizeMode = ResizeMode.NoResize,
                    Background = new SolidColorBrush(Color.FromRgb(250, 250, 252))
                };

                var root = new StackPanel { Margin = new Thickness(16) };
                root.Children.Add(new TextBlock
                {
                    Text = $"Vùng pick sẽ chia tuyến {axisText}.\nChọn cạnh gốc để bắt đầu chia nhịp:",
                    TextWrapping = TextWrapping.Wrap,
                    FontSize = 14,
                    Margin = new Thickness(0, 0, 0, 12)
                });

                var buttonBar = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Left
                };

                buttonBar.Children.Add(Btn(primaryLabel, AccentBlue, Brushes.White, (s, e) =>
                {
                    result = false;
                    dlg.Close();
                }, 140));

                buttonBar.Children.Add(Btn(secondaryLabel, AccentOrange, Brushes.White, (s, e) =>
                {
                    result = true;
                    dlg.Close();
                }, 140));

                root.Children.Add(buttonBar);

                var btnCancel = Btn("Hủy", BtnGray, Brushes.White, (s, e) =>
                {
                    result = null;
                    dlg.Close();
                }, 80);
                btnCancel.Margin = new Thickness(0, 12, 0, 0);
                root.Children.Add(btnCancel);

                dlg.Content = root;
                dlg.ShowDialog();
            });

            return result;
        }

        private static bool IsColdStorageCeilingRow(TenderWallRow row)
        {
            return string.Equals(row.Category, "Trần", StringComparison.OrdinalIgnoreCase)
                && string.Equals(row.Application, "Kho lạnh", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsColdStorageRunAlongX(TenderWallRow row)
        {
            return !string.Equals(row.LayoutDirection, "Ngang", StringComparison.OrdinalIgnoreCase);
        }

        private static List<double[]> GetPolylineVertices(Autodesk.AutoCAD.DatabaseServices.Polyline pl)
        {
            var vertices = new List<double[]>();
            for (int i = 0; i < pl.NumberOfVertices; i++)
            {
                var pt = pl.GetPoint2dAt(i);
                vertices.Add(new[] { pt.X, pt.Y });
            }

            if (vertices.Count >= 2)
            {
                var first = vertices[0];
                var last = vertices[vertices.Count - 1];
                if (Math.Abs(last[0] - first[0]) < 1e-6 && Math.Abs(last[1] - first[1]) < 1e-6)
                    vertices.RemoveAt(vertices.Count - 1);
            }

            return vertices;
        }

        private void AddPanelPreviewLines(
            List<double[]> vertices,
            TenderWallRow row,
            Autodesk.AutoCAD.DatabaseServices.ObjectId layerId,
            Autodesk.AutoCAD.DatabaseServices.BlockTableRecord btr,
            Autodesk.AutoCAD.DatabaseServices.Transaction tr)
        {
            if (row.PanelWidth <= 0)
                return;

            bool horizontal = string.Equals(row.LayoutDirection, "Ngang", StringComparison.OrdinalIgnoreCase);
            double min = horizontal ? vertices.Min(v => v[1]) : vertices.Min(v => v[0]);
            double max = horizontal ? vertices.Max(v => v[1]) : vertices.Max(v => v[0]);

            for (double pos = min + row.PanelWidth; pos < max - 1.0; pos += row.PanelWidth)
            {
                var segments = GetScanSegments(vertices, pos, horizontal);
                foreach (var segment in segments)
                {
                    var start = horizontal
                        ? new Autodesk.AutoCAD.Geometry.Point3d(segment.Start, pos, 0)
                        : new Autodesk.AutoCAD.Geometry.Point3d(pos, segment.Start, 0);
                    var end = horizontal
                        ? new Autodesk.AutoCAD.Geometry.Point3d(segment.End, pos, 0)
                        : new Autodesk.AutoCAD.Geometry.Point3d(pos, segment.End, 0);

                    AddPreviewLine(
                        start,
                        end,
                        4,
                        Autodesk.AutoCAD.DatabaseServices.LineWeight.LineWeight025,
                        layerId,
                        btr,
                        tr);
                }
            }
        }

        private void AddSuspensionPreviewLines(
            List<double[]> vertices,
            bool runAlongX,
            bool divideFromMaxSide,
            double spacingMm,
            int lineCount,
            string linePrefix,
            short colorIndex,
            Autodesk.AutoCAD.DatabaseServices.LineWeight lineWeight,
            bool useCircleMarker,
            Autodesk.AutoCAD.DatabaseServices.ObjectId layerId,
            Autodesk.AutoCAD.DatabaseServices.BlockTableRecord btr,
            Autodesk.AutoCAD.DatabaseServices.Transaction tr)
        {
            var positions = BuildSuspensionLinePositions(vertices, runAlongX, divideFromMaxSide, spacingMm, lineCount);
            for (int index = 0; index < positions.Count; index++)
            {
                double pos = positions[index];
                var segments = GetScanSegments(vertices, pos, runAlongX);
                foreach (var segment in segments)
                {
                    var start = runAlongX
                        ? new Autodesk.AutoCAD.Geometry.Point3d(segment.Start, pos, 0)
                        : new Autodesk.AutoCAD.Geometry.Point3d(pos, segment.Start, 0);
                    var end = runAlongX
                        ? new Autodesk.AutoCAD.Geometry.Point3d(segment.End, pos, 0)
                        : new Autodesk.AutoCAD.Geometry.Point3d(pos, segment.End, 0);

                    AddPreviewLine(start, end, colorIndex, lineWeight, layerId, btr, tr);
                    AddSuspensionPointMarkers(
                        start,
                        end,
                        colorIndex,
                        useCircleMarker,
                        layerId,
                        btr,
                        tr);
                    AddSuspensionLineLabel(
                        start,
                        end,
                        $"{linePrefix}{index + 1} @{spacingMm:F0}",
                        colorIndex,
                        layerId,
                        btr,
                        tr);
                }
            }
        }

        private static List<double> BuildSuspensionLinePositions(
            List<double[]> vertices,
            bool runAlongX,
            bool divideFromMaxSide,
            double spacingMm,
            int lineCount)
        {
            var positions = new List<double>();
            if (lineCount <= 0)
                return positions;

            double min = runAlongX ? vertices.Min(v => v[1]) : vertices.Min(v => v[0]);
            double max = runAlongX ? vertices.Max(v => v[1]) : vertices.Max(v => v[0]);
            double width = max - min;
            if (width <= 0)
                return positions;

            if (lineCount == 1 || spacingMm <= 0)
            {
                positions.Add(divideFromMaxSide ? max - 0.5 : min + 0.5);
                return positions;
            }

            for (int i = 0; i < lineCount; i++)
            {
                double pos = divideFromMaxSide
                    ? max - i * spacingMm - 0.5
                    : min + i * spacingMm + 0.5;

                if (pos > max - 0.5)
                    pos = max - 0.5;
                if (pos < min + 0.5)
                    pos = min + 0.5;

                if (positions.Count == 0 || Math.Abs(positions[positions.Count - 1] - pos) > 1.0)
                    positions.Add(pos);
            }

            return positions;
        }

        private static List<(double Start, double End)> GetScanSegments(
            List<double[]> vertices,
            double scanPos,
            bool horizontalLine)
        {
            var intersections = new List<double>();
            int count = vertices.Count;

            for (int i = 0; i < count; i++)
            {
                var p1 = vertices[i];
                var p2 = vertices[(i + 1) % count];

                double s1 = horizontalLine ? p1[1] : p1[0];
                double s2 = horizontalLine ? p2[1] : p2[0];
                double m1 = horizontalLine ? p1[0] : p1[1];
                double m2 = horizontalLine ? p2[0] : p2[1];

                if ((s1 <= scanPos && s2 > scanPos) || (s2 <= scanPos && s1 > scanPos))
                {
                    double t = (scanPos - s1) / (s2 - s1);
                    intersections.Add(m1 + t * (m2 - m1));
                }
            }

            intersections.Sort();
            var segments = new List<(double Start, double End)>();
            for (int i = 0; i + 1 < intersections.Count; i += 2)
            {
                double start = intersections[i];
                double end = intersections[i + 1];
                if (end - start > 1.0)
                    segments.Add((start, end));
            }

            return segments;
        }

        private void AddPreviewLine(
            Autodesk.AutoCAD.Geometry.Point3d start,
            Autodesk.AutoCAD.Geometry.Point3d end,
            short colorIndex,
            Autodesk.AutoCAD.DatabaseServices.LineWeight lineWeight,
            Autodesk.AutoCAD.DatabaseServices.ObjectId layerId,
            Autodesk.AutoCAD.DatabaseServices.BlockTableRecord btr,
            Autodesk.AutoCAD.DatabaseServices.Transaction tr)
        {
            var line = new Autodesk.AutoCAD.DatabaseServices.Line(start, end)
            {
                LayerId = layerId,
                ColorIndex = colorIndex,
                LineWeight = lineWeight
            };

            btr.AppendEntity(line);
            tr.AddNewlyCreatedDBObject(line, true);
            _previewEntityIds.Add(line.ObjectId);
        }

        private void AddSuspensionPointMarkers(
            Autodesk.AutoCAD.Geometry.Point3d start,
            Autodesk.AutoCAD.Geometry.Point3d end,
            short colorIndex,
            bool useCircleMarker,
            Autodesk.AutoCAD.DatabaseServices.ObjectId layerId,
            Autodesk.AutoCAD.DatabaseServices.BlockTableRecord btr,
            Autodesk.AutoCAD.DatabaseServices.Transaction tr)
        {
            double length = start.DistanceTo(end);
            if (length <= 1.0)
                return;

            int pointCount = Math.Max(1, (int)Math.Ceiling(length / TenderBomCalculator.ColdStorageSuspensionPointSpacingMm));
            double actualSpacing = length / pointCount;
            var direction = (end - start).GetNormal();
            var normal = new Autodesk.AutoCAD.Geometry.Vector3d(-direction.Y, direction.X, 0.0);
            const double markerSize = 60.0;

            for (int i = 0; i < pointCount; i++)
            {
                double distance = actualSpacing * (i + 0.5);
                var center = start + (direction * distance);

                if (useCircleMarker)
                {
                    AddPreviewCircle(center, markerSize, colorIndex, layerId, btr, tr);
                }
                else
                {
                    AddPreviewCross(center, direction, normal, markerSize, colorIndex, layerId, btr, tr);
                }
            }
        }

        private void AddPreviewCircle(
            Autodesk.AutoCAD.Geometry.Point3d center,
            double radius,
            short colorIndex,
            Autodesk.AutoCAD.DatabaseServices.ObjectId layerId,
            Autodesk.AutoCAD.DatabaseServices.BlockTableRecord btr,
            Autodesk.AutoCAD.DatabaseServices.Transaction tr)
        {
            var circle = new Autodesk.AutoCAD.DatabaseServices.Circle(center, Autodesk.AutoCAD.Geometry.Vector3d.ZAxis, radius)
            {
                LayerId = layerId,
                ColorIndex = colorIndex,
                LineWeight = Autodesk.AutoCAD.DatabaseServices.LineWeight.LineWeight025
            };

            btr.AppendEntity(circle);
            tr.AddNewlyCreatedDBObject(circle, true);
            _previewEntityIds.Add(circle.ObjectId);
        }

        private void AddPreviewCross(
            Autodesk.AutoCAD.Geometry.Point3d center,
            Autodesk.AutoCAD.Geometry.Vector3d direction,
            Autodesk.AutoCAD.Geometry.Vector3d normal,
            double markerSize,
            short colorIndex,
            Autodesk.AutoCAD.DatabaseServices.ObjectId layerId,
            Autodesk.AutoCAD.DatabaseServices.BlockTableRecord btr,
            Autodesk.AutoCAD.DatabaseServices.Transaction tr)
        {
            AddPreviewLine(
                center - direction * markerSize,
                center + direction * markerSize,
                colorIndex,
                Autodesk.AutoCAD.DatabaseServices.LineWeight.LineWeight025,
                layerId,
                btr,
                tr);

            AddPreviewLine(
                center - normal * markerSize,
                center + normal * markerSize,
                colorIndex,
                Autodesk.AutoCAD.DatabaseServices.LineWeight.LineWeight025,
                layerId,
                btr,
                tr);
        }

        private void AddSuspensionLineLabel(
            Autodesk.AutoCAD.Geometry.Point3d start,
            Autodesk.AutoCAD.Geometry.Point3d end,
            string text,
            short colorIndex,
            Autodesk.AutoCAD.DatabaseServices.ObjectId layerId,
            Autodesk.AutoCAD.DatabaseServices.BlockTableRecord btr,
            Autodesk.AutoCAD.DatabaseServices.Transaction tr)
        {
            var direction = end - start;
            if (direction.Length <= 1.0)
                return;

            var normal = new Autodesk.AutoCAD.Geometry.Vector3d(-direction.Y, direction.X, 0.0).GetNormal();
            var labelPoint = start + direction.MultiplyBy(0.08) + normal.MultiplyBy(140.0);
            AddPreviewText(labelPoint, text, colorIndex, 140.0, layerId, btr, tr);
        }

        private void AddPreviewSummaryText(
            List<double[]> vertices,
            TenderWallRow row,
            Autodesk.AutoCAD.DatabaseServices.ObjectId layerId,
            Autodesk.AutoCAD.DatabaseServices.BlockTableRecord btr,
            Autodesk.AutoCAD.DatabaseServices.Transaction tr)
        {
            double minX = vertices.Min(v => v[0]);
            double maxY = vertices.Max(v => v[1]);
            string dropText = row.CableDropLengthMm > 0 ? $"Drop {row.CableDropLengthMm:F0}" : "Drop ?";
            string text = $"PW{row.PanelWidth} | {row.PanelThickness}T | {dropText}";
            var point = new Autodesk.AutoCAD.Geometry.Point3d(minX + 180.0, maxY - 180.0, 0);
            AddPreviewText(point, text, 2, 160.0, layerId, btr, tr);
        }

        private void AddPreviewText(
            Autodesk.AutoCAD.Geometry.Point3d position,
            string text,
            short colorIndex,
            double height,
            Autodesk.AutoCAD.DatabaseServices.ObjectId layerId,
            Autodesk.AutoCAD.DatabaseServices.BlockTableRecord btr,
            Autodesk.AutoCAD.DatabaseServices.Transaction tr)
        {
            var dbText = new Autodesk.AutoCAD.DatabaseServices.DBText
            {
                Position = position,
                Height = height,
                TextString = text,
                LayerId = layerId,
                ColorIndex = colorIndex,
                TextStyleId = BlockManager.EnsureArialStyle(btr.Database, tr)
            };

            btr.AppendEntity(dbText);
            tr.AddNewlyCreatedDBObject(dbText, true);
            _previewEntityIds.Add(dbText.ObjectId);
        }

        private void OnEditAccessories(object sender, RoutedEventArgs e)
        {
            try
            {
                _project.Accessories = AccessoryDataManager.NormalizeConfiguredAccessories(_project.Accessories);
                var dialog = new TenderAccessoryEditorDialog(
                    _project.Accessories,
                    _project.Specs.Select(spec => spec.Key));

                dialog.Owner = this;
                bool? result = dialog.ShowDialog();
                if (result != true)
                    return;

                _project.Accessories = dialog.GetAccessories();
                RefreshBomSummary();
                _lblStatus.Text = $"✅ Đã cập nhật {_project.Accessories.Count} dòng phụ kiện";
            }
            catch (Exception ex)
            {
                _lblStatus.Text = $"❌ Sửa phụ kiện lỗi: {ex.Message}";
            }
        }

        // ═══════════════════════════════════════════════════
        // DATA HELPERS
        // ═══════════════════════════════════════════════════

        private string GetFooterText()
        {
            if (_wallRows.Count == 0) return "TỔNG: 0 tấm | 0 m²";
            var walls = GetWallModels();
            int totalPanels = walls.Sum(w => w.EstimatedPanelCount);
            double netArea = walls.Sum(w => w.NetAreaM2);
            double orderedArea = walls.Sum(w => w.OrderedAreaM2);
            double wasteArea = Math.Max(0, orderedArea - netArea);
            double wastePct = orderedArea > 0 ? wasteArea / orderedArea * 100 : 0;
            return $"TỔNG: {totalPanels} tấm | {netArea:F2} m² net | HH ~{wasteArea:F2} m² ({wastePct:F1}%)";
        }

        private void RefreshFooter()
        {
            _lblFooter.Text = GetFooterText();
        }

        private void RefreshPanelBreakdown(TenderWallRow wallRow)
        {
            var model = wallRow.ToModel();
            var breakdown = model.GetPanelBreakdown();
            var viewRows = breakdown.Select(e => new TenderPanelEntryRow
            {
                Label = e.Label,
                WidthMm = e.WidthMm,
                LengthMm = e.LengthMm,
                Count = e.Count
            }).ToList();
            _panelBreakdownGrid.ItemsSource = viewRows;
        }

        private void LoadProjectData()
        {
            _wallRows.Clear();
            int idx = 1;
            foreach (var wall in _project.Walls)
                _wallRows.Add(TenderWallRow.FromModel(wall, idx++));

            SyncAllWallRowsSpecData();
        }

        private void SyncAllWallRowsSpecData()
        {
            foreach (var row in _wallRows)
                SyncWallRowSpecData(row);

            _wallGrid?.Items.Refresh();
        }

        private void SyncWallRowSpecData(TenderWallRow row)
        {
            if (row == null)
                return;

            row.PanelWidth = GetWidthForSpec(row.SpecKey);
            row.PanelThickness = GetThicknessForSpec(row.SpecKey);
            row.Refresh();
        }

        private void LoadOpeningsForWall(TenderWallRow wallRow)
        {
            _openingRows.Clear();
            foreach (var op in wallRow.Openings)
                _openingRows.Add(new TenderOpeningRow { Type = op.Type, Width = op.Width, Height = op.Height, Quantity = op.Quantity });
        }

        private List<TenderWall> GetWallModels() => _wallRows.Select(r => r.ToModel()).ToList();

        private void ReindexWalls()
        {
            for (int i = 0; i < _wallRows.Count; i++) _wallRows[i].Index = i + 1;
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            _cadPreviewTimer.Stop();
            ClearHighlight();
            _project.Walls = GetWallModels();
            base.OnClosing(e);
        }

        // ═══════════════════════════════════════════════════
        // COLUMN + BUTTON FACTORIES
        // ═══════════════════════════════════════════════════

        private static DataGridTextColumn Col(string header, string binding, double width,
            string? format = null)
        {
            return new DataGridTextColumn
            {
                Header = header,
                Binding = new Binding(binding) { StringFormat = format },
                Width = new DataGridLength(width)
            };
        }

        /// <summary>
        /// Safe ComboBox column using DataGridTemplateColumn.
        /// Display = TextBlock, Edit = ComboBox. No IEditableObject needed.
        /// </summary>
        private static DataGridTemplateColumn ColTemplateCombo(string header, string binding,
            double width, string[] options)
        {
            var col = new DataGridTemplateColumn
            {
                Header = header,
                Width = new DataGridLength(width)
            };

            // Display: TextBlock
            var displayFactory = new FrameworkElementFactory(typeof(TextBlock));
            displayFactory.SetBinding(TextBlock.TextProperty, new Binding(binding));
            displayFactory.SetValue(TextBlock.MarginProperty, new Thickness(4, 2, 4, 2));
            col.CellTemplate = new DataTemplate { VisualTree = displayFactory };

            // Edit: ComboBox (IsEditable=true for custom input)
            var editFactory = new FrameworkElementFactory(typeof(ComboBox));
            editFactory.SetBinding(ComboBox.TextProperty,
                new Binding(binding) { UpdateSourceTrigger = UpdateSourceTrigger.LostFocus });
            editFactory.SetValue(ComboBox.IsEditableProperty, true);
            editFactory.SetValue(ComboBox.ItemsSourceProperty, options);
            editFactory.SetValue(ComboBox.IsDropDownOpenProperty, true);
            col.CellEditingTemplate = new DataTemplate { VisualTree = editFactory };

            return col;
        }

        private static DataGridTextColumn ColStar(string header, string binding)
            => new DataGridTextColumn
            {
                Header = header,
                Binding = new Binding(binding),
                Width = new DataGridLength(1, DataGridLengthUnitType.Star)
            };

        private static DataGridCheckBoxColumn ColCheck(string header, string binding, double width)
            => new DataGridCheckBoxColumn
            {
                Header = header,
                Binding = new Binding(binding) { UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged },
                Width = new DataGridLength(width)
            };

        private static Button Btn(string text, Brush bg, Brush fg, RoutedEventHandler click, double width = 0)
        {
            var btn = new Button
            {
                Content = text,
                Background = bg,
                Foreground = fg,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Height = 28,
                Padding = new Thickness(8, 2, 8, 2),
                Margin = new Thickness(0, 0, 4, 0),
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            if (width > 0) btn.Width = width;
            btn.Click += click;
            return btn;
        }
    }

    // ═══════════════════════════════════════════════════
    // VIEW MODELS
    // ═══════════════════════════════════════════════════

    public class TenderWallRow : INotifyPropertyChanged
    {
        public int Index { get; set; }
        public string Category { get; set; } = "Vách";
        public string Floor { get; set; } = "";
        public string Name { get; set; } = "";
        public double Length { get; set; }
        public double Height { get; set; }
        public string SpecKey { get; set; } = "";
        public int PanelWidth { get; set; } = 1100;
        public int PanelThickness { get; set; } = 50;
        public string LayoutDirection { get; set; } = "Dọc";
        public string Application { get; set; } = "Ngoài nhà";
        public double CableDropLengthMm { get; set; }
        public bool ColdStorageDivideFromMaxSide { get; set; }
        public bool TopEdgeExposed { get; set; } = true;
        public bool BottomEdgeExposed { get; set; } = true;
        public bool StartEdgeExposed { get; set; }
        public bool EndEdgeExposed { get; set; }
        public int OutsideCornerCount { get; set; }
        public int InsideCornerCount { get; set; }
        public string? CadHandle { get; set; }
        /// <summary>So duong noi chay doc (Ngang layout) - dung tinh Omega, Foam, Gioang xop.</summary>
        public int VerticalJointCount { get; set; }
        public List<TenderOpening> Openings { get; set; } = new();
        public List<double[]>? PolygonVertices { get; set; }

        // Track previous category for auto-reset LayoutDirection
        internal string? _prevCategory;

        /// <summary>Chiều chia tấm (mm) — Dọc: chiều dài, Ngang: chiều cao</summary>
        private double DivisionSpan => LayoutDirection == "Ngang" ? Height : Length;
        /// <summary>Chiều span tấm (mm) — Dọc: chiều cao, Ngang: chiều dài</summary>
        private double PanelSpan => LayoutDirection == "Ngang" ? Length : Height;

        public string WallAreaM2Display => (Length * Height / 1_000_000.0).ToString("F2");
        public string OpeningAreaM2Display => Openings.Sum(o => o.TotalAreaM2).ToString("F2");
        public string NetAreaM2Display => Math.Max(0, Length * Height / 1e6 - Openings.Sum(o => o.TotalAreaM2)).ToString("F2");
        public string EstimatedPanelCountDisplay
        {
            get
            {
                if (PanelWidth <= 0 || DivisionSpan <= 0) return "0";
                return ((int)Math.Ceiling(DivisionSpan / PanelWidth)).ToString();
            }
        }

        public void Refresh()
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(WallAreaM2Display)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(OpeningAreaM2Display)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(NetAreaM2Display)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(EstimatedPanelCountDisplay)));
        }

        public void SyncOpenings(IEnumerable<TenderOpeningRow> rows)
        {
            Openings = rows.Select(r => new TenderOpening
            {
                Type = r.Type, Width = r.Width, Height = r.Height, Quantity = r.Quantity
            }).ToList();
        }

        public TenderWall ToModel() => new TenderWall
        {
            Category = Category, Floor = Floor, Name = Name,
            Length = Length, Height = Height, SpecKey = SpecKey,
            PanelWidth = PanelWidth, PanelThickness = PanelThickness,
            LayoutDirection = LayoutDirection,
            Application = Application,
            CableDropLengthMm = CableDropLengthMm,
            ColdStorageDivideFromMaxSide = ColdStorageDivideFromMaxSide,
            TopEdgeExposed = TopEdgeExposed,
            BottomEdgeExposed = BottomEdgeExposed,
            StartEdgeExposed = StartEdgeExposed,
            EndEdgeExposed = EndEdgeExposed,
            OutsideCornerCount = OutsideCornerCount,
            InsideCornerCount = InsideCornerCount,
            CadHandle = CadHandle, Openings = Openings,
            VerticalJointCount = VerticalJointCount,
            PolygonVertices = PolygonVertices
        };


        public static TenderWallRow FromModel(TenderWall w, int index) => new TenderWallRow
        {
            Index = index, Category = w.Category, Floor = w.Floor, Name = w.Name,
            Length = w.Length, Height = w.Height, SpecKey = w.SpecKey,
            PanelWidth = w.PanelWidth, PanelThickness = w.PanelThickness,
            LayoutDirection = w.LayoutDirection,
            Application = w.Application,
            CableDropLengthMm = w.CableDropLengthMm,
            ColdStorageDivideFromMaxSide = w.ColdStorageDivideFromMaxSide,
            TopEdgeExposed = w.TopEdgeExposed,
            BottomEdgeExposed = w.BottomEdgeExposed,
            StartEdgeExposed = w.StartEdgeExposed,
            EndEdgeExposed = w.EndEdgeExposed,
            OutsideCornerCount = w.OutsideCornerCount,
            InsideCornerCount = w.InsideCornerCount,
            CadHandle = w.CadHandle, Openings = w.Openings,
            PolygonVertices = w.PolygonVertices,
            VerticalJointCount = w.VerticalJointCount,
            _prevCategory = w.Category
        };

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    public class TenderOpeningRow : INotifyPropertyChanged
    {
        public string Type { get; set; } = "Cửa đi";
        public double Width { get; set; }
        public double Height { get; set; }
        public int Quantity { get; set; } = 1;

        public string TotalAreaDisplay => (Width * Height * Quantity / 1_000_000.0).ToString("F2");

        public void Refresh()
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TotalAreaDisplay)));
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    public class TenderPanelEntryRow
    {
        public string Label { get; set; } = "";
        public double WidthMm { get; set; }
        public double LengthMm { get; set; }
        public int Count { get; set; }
        public string AreaM2Display => (WidthMm * LengthMm * Count / 1_000_000.0).ToString("F2");
    }

    public class TenderAccessoryBasisViewRow
    {
        public int Index { get; set; }
        public string Category { get; set; } = "";
        public string Floor { get; set; } = "";
        public string WallName { get; set; } = "";
        public string Application { get; set; } = "";
        public string SpecKey { get; set; } = "";
        public string AccessoryName { get; set; } = "";
        public string Material { get; set; } = "";
        public string Position { get; set; } = "";
        public string RuleLabel { get; set; } = "";
        public string BasisLabel { get; set; } = "";
        public double BasisValue { get; set; }
        public double Factor { get; set; }
        public double AutoQuantity { get; set; }
        public string Note { get; set; } = "";

        public static TenderAccessoryBasisViewRow FromReportRow(
            TenderBomCalculator.AccessoryBasisRow row, int index)
        {
            return new TenderAccessoryBasisViewRow
            {
                Index = index,
                Category = row.Category,
                Floor = row.Floor,
                WallName = row.WallName,
                Application = row.Application,
                SpecKey = row.SpecKey,
                AccessoryName = row.AccessoryName,
                Material = row.Material,
                Position = row.Position,
                RuleLabel = row.RuleLabel,
                BasisLabel = row.BasisLabel,
                BasisValue = row.BasisValue,
                Factor = row.Factor,
                AutoQuantity = row.AutoQuantity,
                Note = row.Note
            };
        }
    }

    public class TenderAccessorySummaryViewRow
    {
        public int Index { get; set; }
        public string CategoryScope { get; set; } = "";
        public string Application { get; set; } = "";
        public string SpecKey { get; set; } = "";
        public string Name { get; set; } = "";
        public string Material { get; set; } = "";
        public string Position { get; set; } = "";
        public string Unit { get; set; } = "";
        public string RuleLabel { get; set; } = "";
        public string BasisLabel { get; set; } = "";
        public double BasisValue { get; set; }
        public double Factor { get; set; }
        public double WastePercent { get; set; }
        public double AutoQuantity { get; set; }
        public double Adjustment { get; set; }
        public double FinalQuantity { get; set; }
        public string Note { get; set; } = "";

        public static TenderAccessorySummaryViewRow FromReportRow(
            TenderBomCalculator.AccessorySummaryRow row, int index)
        {
            return new TenderAccessorySummaryViewRow
            {
                Index = index,
                CategoryScope = row.CategoryScope,
                Application = row.Application,
                SpecKey = row.SpecKey,
                Name = row.Name,
                Material = row.Material,
                Position = row.Position,
                Unit = row.Unit,
                RuleLabel = row.RuleLabel,
                BasisLabel = row.BasisLabel,
                BasisValue = row.BasisValue,
                Factor = row.Factor,
                WastePercent = row.WastePercent,
                AutoQuantity = row.AutoQuantity,
                Adjustment = row.Adjustment,
                FinalQuantity = row.FinalQuantity,
                Note = row.Note
            };
        }
    }
}
