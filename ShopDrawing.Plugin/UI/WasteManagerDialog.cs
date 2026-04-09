using System;

using System.Collections.Generic;

using System.Collections.ObjectModel;

using System.Linq;

using System.Windows;

using System.Windows.Controls;

using System.Windows.Data;

using System.Windows.Media;

using System.Windows.Controls.Primitives;

using System.Windows.Shapes;

using System.Reflection;

using Autodesk.AutoCAD.ApplicationServices;

using ShopDrawing.Plugin.Commands;

using ShopDrawing.Plugin.Core;

using ShopDrawing.Plugin.Models;

using ShopDrawing.Plugin.Modules.Accessories;

using ShopDrawing.Plugin.Runtime;

using Application = Autodesk.AutoCAD.ApplicationServices.Application;



namespace ShopDrawing.Plugin.UI

{

    public partial class WasteManagerDialog : Window

    {

        // === TAB 1: BOM Chi Tiet ===

        private DataGrid _bomGrid = null!;

        private ObservableCollection<BomRow> _bomRows = new();



        // === TAB 2: BOM San Xuat ===

        private DataGrid _factoryGrid = null!;



        // === TAB 3: Kho Le ===

        public ObservableCollection<WastePanel> Wastes { get; set; } = new();

        private List<WastePanel> _allWaste = new();

        private DataGrid _wasteGrid = null!;

        private TextBlock _wasteSubtotalText = null!;

        private ComboBox _cboStatus = null!;

        private ComboBox _cboThickness = null!;

        private TextBox _txtSearch = null!;



        // Sort bars (moi tab 1 instance, tai su dung DataGridSortBar)

        private DataGridSortBar _bomSortBar = null!;

        private DataGridSortBar _factorySortBar = null!;

        private DataGridSortBar _wasteSortBar = null!;

        private DataGridSortBar _accessorySortBar = null!;



        // Nguon goc chua sort (can giu de re-sort khi them level)

        private List<BomDisplayRow>     _bomSource     = new();

        private List<FactoryDisplayRow> _factorySource = new();

        private List<AccessoryDisplayRow> _accessorySource = new();









        // === TAB 4: Tong Hop ===

        private TextBlock _txtSummary = null!;

        private Canvas _gaugeCanvas = null!;

        private Canvas _barChartCanvas = null!;

        private TextBlock _bomFooterText = null!;

        private TextBlock _factoryFooterText = null!;

        private DataGrid _accessoryGrid = null!;

        private TextBlock _accessoryFooterText = null!;

        private TabControl _tabs = null!;

        private readonly ShopdrawingAccessoryScanner _ceilingAccessoryScanner = new();

        private readonly ShopdrawingWallAccessoryScanner _wallAccessoryScanner = new();

        private readonly ShopdrawingPlanAccessoryScanner _planAccessoryScanner = new();

        private readonly ShopdrawingAccessoryBomService _accessoryBomService = new();

        private bool _isLoadingData;

        private bool _isApplyingWasteFilter;

        private DateTime _lastLoadRequestedUtc = DateTime.MinValue;

        public WasteManagerDialog()

        {

            Title = "Bảng Quản Lý Khối Lượng";

            Width = 1080;

            Height = 680;

            WindowStartupLocation = WindowStartupLocation.CenterScreen;

            Background = new SolidColorBrush(Color.FromRgb(250, 250, 252));

            var root = new Grid { Margin = new Thickness(12) };

            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var header = new TextBlock

            {

                Text = "BẢNG QUẢN LÝ KHỐI LƯỢNG",

                FontSize = 18,

                FontWeight = FontWeights.Bold,

                Foreground = new SolidColorBrush(Color.FromRgb(44, 62, 80)),

                Margin = new Thickness(0, 0, 0, 8)

            };

            Grid.SetRow(header, 0);

            root.Children.Add(header);

            _tabs = new TabControl { FontSize = 13 };

            _tabs.Items.Add(CreateBomTab());

            _tabs.Items.Add(CreateFactoryTab());

            _tabs.Items.Add(CreateWasteTab());

            _tabs.Items.Add(CreateAccessoryTab());

            _tabs.Items.Add(CreateSummaryTab());

            Grid.SetRow(_tabs, 1);

            root.Children.Add(_tabs);

            var footer = new StackPanel

            {

                Orientation = Orientation.Horizontal,

                HorizontalAlignment = HorizontalAlignment.Right,

                Margin = new Thickness(0, 10, 0, 0)

            };

            var btnClose = new Button

            {

                Content = "Đóng",

                Width = 90,

                Height = 34,

                Background = new SolidColorBrush(Color.FromRgb(149, 165, 166)),

                Foreground = Brushes.White,

                FontWeight = FontWeights.SemiBold

            };

            btnClose.Click += (_, _) => Close();

            footer.Children.Add(btnClose);

            Grid.SetRow(footer, 2);

            root.Children.Add(footer);

            Content = root;

            UiText.NormalizeWindow(this);

            System.Action wasteRefreshHandler = () =>

                Dispatcher.BeginInvoke(new System.Action(() => RequestLoadAllData(force: true)));

            ShopDrawingCommands.WasteUpdated += wasteRefreshHandler;

            Closed += (_, _) => 
            {
                ShopDrawingCommands.WasteUpdated -= wasteRefreshHandler;
                ClearHighlight();
            };

            _tabs.SelectionChanged += (s, e) =>

            {

                if (!ReferenceEquals(e.OriginalSource, _tabs))

                {

                    e.Handled = true;

                    return;

                }

                if (_tabs.SelectedIndex == 2)

                {

                    ApplyWasteFilter();

                }
                else
                {
                    ClearHighlight();
                }

            };

            Activated += (_, _) => RequestLoadAllData(force: false);

            RequestLoadAllData(force: true);

        }

        private TabItem CreateBomTab()

        {

            var tab = new TabItem { Header = "BOM Chi Tiết" };

            var panel = new Grid { Margin = new Thickness(8) };

            panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            panel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var info = new TextBlock

            {

                Text = "Toàn bộ tấm panel trên bản vẽ hiện tại, bao gồm tấm mới, tái sử dụng và tấm cắt.",

                Foreground = Brushes.Gray,

                Margin = new Thickness(0, 0, 0, 6)

            };

            Grid.SetRow(info, 0);

            panel.Children.Add(info);

            _bomSortBar = new DataGridSortBar(

                new[] {

                    ("Mã Tấm", "Id"),

                    ("Cấu Tạo", "Spec"),

                    ("Rộng", "WidthMm"),

                    ("Dài", "LengthMm"),

                    ("Diện Tích", "TotalAreaM2"),

                    ("Số Lượng", "Qty"),

                    ("Mã vùng", "WallCode"),

                },

                () => _bomGrid.ItemsSource = _bomSortBar.Apply(_bomSource)

            );

            Grid.SetRow(_bomSortBar.Panel, 1);

            panel.Children.Add(_bomSortBar.Panel);

            _bomGrid = new DataGrid

            {

                AutoGenerateColumns = false,

                CanUserAddRows = false,

                IsReadOnly = true,

                HeadersVisibility = DataGridHeadersVisibility.Column,

                AlternatingRowBackground = new SolidColorBrush(Color.FromRgb(245, 248, 255))

            };

            _bomGrid.Columns.Add(Col("STT", "Stt", 45));

            _bomGrid.Columns.Add(Col("Mã Tấm", "Id", 100));

            _bomGrid.Columns.Add(Col("Cấu Tạo", "Spec", 90));

            _bomGrid.Columns.Add(Col("Rộng (mm)", "WidthMm", 80, "F0"));

            _bomGrid.Columns.Add(Col("Dài (mm)", "LengthMm", 80, "F0"));

            _bomGrid.Columns.Add(Col("Ngàm T/P", "JointDisplay", 70));

            _bomGrid.Columns.Add(Col("Số Lượng", "Qty", 70));

            _bomGrid.Columns.Add(Col("Diện Tích (m²)", "TotalAreaM2", 100, "F3"));

            _bomGrid.Columns.Add(Col("Trạng Thái", "Status", 90));

            _bomGrid.Columns.Add(ColStar("Mã vùng", "WallCode"));

            Grid.SetRow(_bomGrid, 2);

            panel.Children.Add(_bomGrid);

            var bomFooter = new TextBlock

            {

                Name = "BomFooter",

                Foreground = new SolidColorBrush(Color.FromRgb(44, 62, 80)),

                FontWeight = FontWeights.SemiBold,

                Margin = new Thickness(0, 6, 0, 0)

            };

            Grid.SetRow(bomFooter, 3);

            panel.Children.Add(bomFooter);

            _bomFooterText = bomFooter;

            tab.Content = panel;

            return tab;

        }

        private TabItem CreateFactoryTab()

        {

            var tab = new TabItem { Header = "BOM Sản Xuất" };

            var panel = new Grid { Margin = new Thickness(8) };

            panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            panel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var info = new TextBlock

            {

                Text = "Số lượng tấm cần đặt hàng nhà máy sản xuất, gom theo kích thước và loại trừ tấm tái sử dụng.",

                Foreground = Brushes.Gray,

                Margin = new Thickness(0, 0, 0, 6)

            };

            Grid.SetRow(info, 0);

            panel.Children.Add(info);

            _factorySortBar = new DataGridSortBar(

                new[] {

                    ("Ưu Tiên",  "BatchNo"),

                    ("Ký Hiệu",   "PanelIds"),

                    ("Cấu Tạo",  "Spec"),

                    ("Rộng",      "WidthMm"),

                    ("Dài",        "LengthMm"),

                    ("Số Lượng",  "Qty"),

                    ("Diện Tích", "TotalAreaM2"),

                    ("Mã vùng",     "Note"),

                },

                () => _factoryGrid.ItemsSource = _factorySortBar.Apply(_factorySource)

            );

            Grid.SetRow(_factorySortBar.Panel, 1);

            panel.Children.Add(_factorySortBar.Panel);

            _factoryGrid = new DataGrid

            {

                AutoGenerateColumns = false,

                CanUserAddRows = false,

                IsReadOnly = false,

                HeadersVisibility = DataGridHeadersVisibility.Column,

                AlternatingRowBackground = new SolidColorBrush(Color.FromRgb(255, 248, 240))

            };

            _factoryGrid.Columns.Add(Col("STT", "Stt", 40));

            var batchCol = new DataGridTextColumn

            {

                Header = "Ưu Tiên",

                Binding = new Binding("BatchNo") { UpdateSourceTrigger = UpdateSourceTrigger.LostFocus },

                Width = 70,

                EditingElementStyle = new Style(typeof(TextBox))

            };

            var cellStyle = new Style(typeof(DataGridCell));

            cellStyle.Setters.Add(new Setter(DataGridCell.ForegroundProperty,

                new SolidColorBrush(Color.FromRgb(41, 128, 185))));

            cellStyle.Setters.Add(new Setter(DataGridCell.FontWeightProperty, FontWeights.SemiBold));

            batchCol.CellStyle = cellStyle;

            _factoryGrid.Columns.Add(batchCol);

            _factoryGrid.CellEditEnding += (s, e) =>

            {

                if (e.Column != batchCol) return;

                if (e.Row.Item is not FactoryDisplayRow editRow) return;

                if (e.EditingElement is not TextBox tb) return;

                int.TryParse(tb.Text.Trim(), out int newVal);

                if (newVal < 0) newVal = 0;

                editRow.BatchNo = newVal;

                if (ShopDrawingCommands.WasteRepo == null) return;

                if (newVal > 0)

                    ShopDrawingCommands.WasteRepo.SetBatch(editRow.PanelIds, newVal);

                else

                    ShopDrawingCommands.WasteRepo.ClearBatch(editRow.PanelIds);

            };

            _factoryGrid.Columns.Add(Col("Ký Hiệu", "PanelIds", 120));

            _factoryGrid.Columns.Add(Col("Cấu Tạo", "Spec", 100));

            _factoryGrid.Columns.Add(Col("Rộng (mm)", "WidthMm", 80, "F0"));

            _factoryGrid.Columns.Add(Col("Dài (mm)", "LengthMm", 80, "F0"));

            _factoryGrid.Columns.Add(Col("Số Lượng", "Qty", 80));

            _factoryGrid.Columns.Add(Col("Diện Tích (m²)", "TotalAreaM2", 100, "F3"));

            _factoryGrid.Columns.Add(ColStar("Mã vùng", "Note"));

            Grid.SetRow(_factoryGrid, 2);

            panel.Children.Add(_factoryGrid);

            var factFooter = new TextBlock

            {

                Foreground = new SolidColorBrush(Color.FromRgb(44, 62, 80)),

                FontWeight = FontWeights.SemiBold,

                Margin = new Thickness(0, 6, 0, 0)

            };

            Grid.SetRow(factFooter, 3);

            panel.Children.Add(factFooter);

            _factoryFooterText = factFooter;

            tab.Content = panel;

            return tab;

        }

        private TabItem CreateWasteTab()

        {

            var tab = new TabItem { Header = "Kho Lẻ" };

            var panel = new Grid { Margin = new Thickness(8) };

            panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            panel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var filterBar = CreateFilterBar();

            Grid.SetRow(filterBar, 0);

            panel.Children.Add(filterBar);

            _wasteSortBar = new DataGridSortBar(

                new[] {

                    ("Mã Tấm",      "PanelCode"),

                    ("Rộng",         "WidthMm"),

                    ("Dài",          "LengthMm"),

                    ("Dày",          "ThickMm"),

                    ("Cấu Tạo",      "PanelSpec"),

                    ("Diện Tích",    "DienTich"),

                    ("Trạng Thái",   "StatusDisplay"),

                    ("Mã vùng",        "SourceWall"),

                },

                () => ApplyWasteFilter()

            );

            Grid.SetRow(_wasteSortBar.Panel, 1);

            panel.Children.Add(_wasteSortBar.Panel);

            _wasteGrid = new DataGrid

            {

                AutoGenerateColumns = false,

                CanUserAddRows = false,

                IsReadOnly = true,

                ItemsSource = Wastes,

                SelectionMode = DataGridSelectionMode.Extended,

                HeadersVisibility = DataGridHeadersVisibility.Column,

                AlternatingRowBackground = new SolidColorBrush(Color.FromRgb(245, 245, 245))

            };
            
            _wasteGrid.SelectionChanged += OnWasteSelectionChanged;

            _wasteGrid.Columns.Add(Col("ID", "Id", 45));

            _wasteGrid.Columns.Add(Col("Mã Tấm", "PanelCode", 100));

            _wasteGrid.Columns.Add(Col("Rộng", "WidthMm", 70, "F0"));

            _wasteGrid.Columns.Add(Col("Dài", "LengthMm", 70, "F0"));

            _wasteGrid.Columns.Add(Col("Dày", "ThickMm", 55));

            _wasteGrid.Columns.Add(Col("Cấu Tạo", "PanelSpec", 90));

            _wasteGrid.Columns.Add(Col("Ngàm T", "JointLeftDisplay", 55));

            _wasteGrid.Columns.Add(Col("Ngàm P", "JointRightDisplay", 55));

            _wasteGrid.Columns.Add(Col("Diện Tích (m²)", "DienTich", 100, "F3"));

            _wasteGrid.Columns.Add(Col("Trạng Thái", "StatusDisplay", 90));

            _wasteGrid.Columns.Add(Col("Nguồn", "SourceTypeDisplay", 80));

            _wasteGrid.Columns.Add(ColStar("Mã vùng", "SourceWall"));

            Grid.SetRow(_wasteGrid, 2);

            panel.Children.Add(_wasteGrid);

            _wasteSubtotalText = new TextBlock

            {

                Margin = new Thickness(4, 6, 4, 2),

                FontSize = 12,

                FontWeight = FontWeights.SemiBold,

                TextWrapping = TextWrapping.Wrap,

                LineHeight = 20,

                Foreground = new SolidColorBrush(Color.FromRgb(44, 62, 80))

            };

            Grid.SetRow(_wasteSubtotalText, 3);

            panel.Children.Add(_wasteSubtotalText);

            var footer = new StackPanel

            {

                Orientation = Orientation.Horizontal,

                HorizontalAlignment = HorizontalAlignment.Right,

                Margin = new Thickness(0, 8, 0, 0)

            };

            footer.Children.Add(MakeButton("Thêm Tấm", Color.FromRgb(46, 204, 113), BtnAdd_Click));

            footer.Children.Add(MakeButton("Xóa Chọn", Color.FromRgb(231, 76, 60), BtnDelete_Click));

            footer.Children.Add(MakeButton("Tải Lại", Color.FromRgb(149, 165, 166), (s, e) => RequestLoadAllData()));

            Grid.SetRow(footer, 4);

            panel.Children.Add(footer);

            tab.Content = panel;

            return tab;

        }

        private TabItem CreateAccessoryTab()

        {

            var tab = new TabItem { Header = "Phụ Kiện" };

            var panel = new Grid { Margin = new Thickness(8) };

            panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            panel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var info = new TextBlock

            {

                Text = "Khối lượng phụ kiện Shopdrawing được bóc trực tiếp từ dữ liệu vách, trần và marker trên bản vẽ hiện tại.",

                Foreground = Brushes.Gray,

                Margin = new Thickness(0, 0, 0, 6)

            };

            Grid.SetRow(info, 0);

            panel.Children.Add(info);

            _accessorySortBar = new DataGridSortBar(

                new[] {

                    ("Hạng Mục", "CategoryScope"),

                    ("Ứng Dụng", "Application"),

                    ("Mã Spec", "SpecKey"),

                    ("Phụ Kiện", "Name"),

                    ("Vị Trí", "Position"),

                    ("Đơn Vị", "Unit"),

                    ("Cơ Sở", "BasisValue"),

                    ("Khối Lượng", "Quantity"),

                },

                () => _accessoryGrid.ItemsSource = _accessorySortBar.Apply(_accessorySource)

            );

            Grid.SetRow(_accessorySortBar.Panel, 1);

            panel.Children.Add(_accessorySortBar.Panel);

            _accessoryGrid = new DataGrid

            {

                AutoGenerateColumns = false,

                CanUserAddRows = false,

                IsReadOnly = true,

                HeadersVisibility = DataGridHeadersVisibility.Column,

                AlternatingRowBackground = new SolidColorBrush(Color.FromRgb(244, 249, 244))

            };

            _accessoryGrid.Columns.Add(Col("STT", "Stt", 40));

            _accessoryGrid.Columns.Add(Col("Hạng Mục", "CategoryScope", 80));

            _accessoryGrid.Columns.Add(Col("Ứng Dụng", "Application", 90));

            _accessoryGrid.Columns.Add(Col("Mã Spec", "SpecKey", 90));

            _accessoryGrid.Columns.Add(Col("Phụ Kiện", "Name", 170));

            _accessoryGrid.Columns.Add(Col("Vật Liệu", "Material", 100));

            _accessoryGrid.Columns.Add(Col("Vị Trí", "Position", 110));

            _accessoryGrid.Columns.Add(Col("Đơn Vị", "Unit", 60));

            _accessoryGrid.Columns.Add(Col("Quy Tắc", "RuleLabel", 150));

            _accessoryGrid.Columns.Add(Col("Cơ Sở Tính", "BasisLabel", 130));

            _accessoryGrid.Columns.Add(Col("Giá Trị", "BasisValue", 80, "F2"));

            _accessoryGrid.Columns.Add(Col("Hệ Số", "Factor", 65, "F3"));

            _accessoryGrid.Columns.Add(Col("Khối Lượng", "Quantity", 90, "F2"));

            _accessoryGrid.Columns.Add(ColStar("Ghi Chú", "Note"));

            Grid.SetRow(_accessoryGrid, 2);

            panel.Children.Add(_accessoryGrid);

            _accessoryFooterText = new TextBlock

            {

                Foreground = new SolidColorBrush(Color.FromRgb(44, 62, 80)),

                FontWeight = FontWeights.SemiBold,

                Margin = new Thickness(0, 6, 0, 0)

            };

            Grid.SetRow(_accessoryFooterText, 3);

            panel.Children.Add(_accessoryFooterText);

            tab.Content = panel;

            return tab;

        }

        private TabItem CreateSummaryTab()

        {

            var tab = new TabItem { Header = "Tổng Hợp" };

            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };

            var panel = new StackPanel { Margin = new Thickness(12) };

            _txtSummary = new TextBlock

            {

                FontSize = 13,

                LineHeight = 22,

                TextWrapping = TextWrapping.Wrap

            };

            panel.Children.Add(_txtSummary);

            var gaugeLabel = new TextBlock

            {

                Text = "TỶ LỆ HAO HỤT",

                FontSize = 14,

                FontWeight = FontWeights.Bold,

                Margin = new Thickness(0, 16, 0, 6)

            };

            panel.Children.Add(gaugeLabel);

            _gaugeCanvas = new Canvas

            {

                Height = 60,

                Background = new SolidColorBrush(Color.FromRgb(245, 245, 250)),

                Margin = new Thickness(0, 0, 0, 8)

            };

            panel.Children.Add(_gaugeCanvas);

            var barLabel = new TextBlock

            {

                Text = "CHI TIẼT HAO HỤT THEO NGUỒN",

                FontSize = 14,

                FontWeight = FontWeights.Bold,

                Margin = new Thickness(0, 12, 0, 6)

            };

            panel.Children.Add(barLabel);

            _barChartCanvas = new Canvas

            {

                Height = 160,

                Background = new SolidColorBrush(Color.FromRgb(245, 245, 250)),

                Margin = new Thickness(0, 0, 0, 8)

            };

            panel.Children.Add(_barChartCanvas);

            var btnExport = new Button

            {

                Content = "Xuất Excel (3 Sheet)",

                Width = 200,

                Height = 38,

                Margin = new Thickness(0, 20, 0, 0),

                HorizontalAlignment = HorizontalAlignment.Left,

                Background = new SolidColorBrush(Color.FromRgb(52, 152, 219)),

                Foreground = Brushes.White,

                FontWeight = FontWeights.SemiBold,

                FontSize = 14

            };

            btnExport.Click += BtnExport_Click;

            panel.Children.Add(btnExport);

            scroll.Content = panel;

            tab.Content = scroll;

            return tab;

        }

        private void RequestLoadAllData(bool force = true)

        {

            if (_isLoadingData)

            {

                return;

            }



            if (!force)

            {

                var now = DateTime.UtcNow;

                if (now - _lastLoadRequestedUtc < TimeSpan.FromMilliseconds(750))

                {

                    return;

                }



                _lastLoadRequestedUtc = now;

            }

            else

            {

                _lastLoadRequestedUtc = DateTime.UtcNow;

            }



            LoadAllData();

        }



        private void LoadAllData()

        {

            if (_isLoadingData)

            {

                return;

            }



            _isLoadingData = true;

            try

            {

            // Load BOM from current drawing FIRST
            var doc = Application.DocumentManager.MdiActiveDocument;
            List<BomRow> rows = new();
            List<ShopdrawingAccessorySummaryRow> accessoryRows = new();
            bool scanSuccess = false;

            if (doc != null)
            {
                try
                {
                    using (var cadLock = SafeCadLock.TryLock())
                    {
                        if (cadLock != null)
                        {
                            using (var tr = doc.Database.TransactionManager.StartTransaction())
                            {
                                rows = ShopDrawingCommands.BomManager.ScanDocumentForPanels(tr, doc.Database);

                                ShopDrawingRuntimeSettings settings = ShopDrawingRuntimeServices.Settings;
                                ShopdrawingAccessorySnapshot ceilingSnapshot = _ceilingAccessoryScanner.ScanCeiling(tr, doc.Database, settings);
                                IReadOnlyList<ShopdrawingWallAccessorySnapshot> wallSnapshots = _wallAccessoryScanner.ScanWalls(
                                    tr,
                                    doc.Database,
                                    settings.DefaultApplication,
                                    settings.DefaultSpec);
                                IReadOnlyList<ShopdrawingPlanAccessorySnapshot> planCornerSnapshots = _planAccessoryScanner.ScanWallCorners(
                                    tr,
                                    doc.Database,
                                    settings.DefaultApplication,
                                    settings.DefaultSpec);

                                accessoryRows = _accessoryBomService.BuildCeilingSummary(ceilingSnapshot)
                                    .Concat(_accessoryBomService.BuildWallSummary(wallSnapshots))
                                    .Concat(_accessoryBomService.BuildPlanCornerSummary(planCornerSnapshots))
                                    .OrderBy(x => x.CategoryScope, StringComparer.OrdinalIgnoreCase)
                                    .ThenBy(x => x.Application, StringComparer.OrdinalIgnoreCase)
                                    .ThenBy(x => x.Position, StringComparer.OrdinalIgnoreCase)
                                    .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                                    .ToList();

                                tr.Commit();
                                scanSuccess = true;
                            }
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    doc.Editor.WriteMessage($"\n[Dashboard] BOM scan error: {ex.Message}");
                }
            }

            // --- SYNC WASTE DATABASE ---
            if (scanSuccess && ShopDrawingCommands.WasteRepo != null)
            {
                var validBasePanelIds = new System.Collections.Generic.HashSet<string>(
                    rows.Select(r => r.Id).Where(id => !string.IsNullOrEmpty(id)).Select(id => {
                        int slashIdx = id.IndexOf('/');
                        return slashIdx > 0 ? id.Substring(0, slashIdx) : id;
                    }),
                    System.StringComparer.OrdinalIgnoreCase);

                var validWallCodes = new System.Collections.Generic.HashSet<string>(
                    rows.Select(r => r.WallCode).Where(w => !string.IsNullOrEmpty(w)),
                    System.StringComparer.OrdinalIgnoreCase);
                    
                var allCurrentWastes = ShopDrawingCommands.WasteRepo.GetAll();
                var toDelete = new System.Collections.Generic.List<int>();

                foreach (var waste in allCurrentWastes)
                {
                    if (string.IsNullOrEmpty(waste.PanelCode)) continue;
                    if (waste.PanelCode.StartsWith("MANUAL-") || waste.SourceType == "MANUAL") continue;
                    
                    // 1. Verify SourceWall (if wall is entirely gone)
                    if (!string.IsNullOrEmpty(waste.SourceWall) && !validWallCodes.Contains(waste.SourceWall))
                    {
                        toDelete.Add(waste.Id);
                        continue;
                    }

                    // 2. Verify specific originating panel (if panel was deleted/replaced)
                    string baseOrigin = waste.PanelCode;

                    // Strip known expected suffixes to extract the base panel ID.
                    string[] suffixes = { "-STEP", "-OPEN", "-TRIM", "-REM" };
                    foreach (var suffix in suffixes)
                    {
                        if (baseOrigin.EndsWith(suffix, System.StringComparison.OrdinalIgnoreCase))
                        {
                            baseOrigin = baseOrigin.Substring(0, baseOrigin.Length - suffix.Length);
                            break;
                        }
                    }

                    // Strip any spec tag after '/' to match validBasePanelIds format
                    int slashIdx = baseOrigin.IndexOf('/');
                    if (slashIdx > 0)
                    {
                        baseOrigin = baseOrigin.Substring(0, slashIdx);
                    }

                    // If the original base panel is no longer in the active BOM, the waste is stale.
                    if (!validBasePanelIds.Contains(baseOrigin))
                    {
                        toDelete.Add(waste.Id);
                        continue;
                    }
                }

                if (toDelete.Count > 0)

                {
                    foreach (var id in toDelete)
                    {
                         ShopDrawingCommands.WasteRepo.HardDelete(id);
                    }
                }
            }

            // Load Waste AFTER sync
            _allWaste = ShopDrawingCommands.WasteRepo?.GetAll()
                .OrderByDescending(x => x.Id).ToList() ?? new System.Collections.Generic.List<WastePanel>();
            ApplyWasteFilter();



            // Tab 1: BOM chi tiet

            // Spec fallback cho hien thi

            string bomSpecFallback = ShopDrawingCommands.DefaultSpec;

            if (string.IsNullOrEmpty(bomSpecFallback))

            {

                try { bomSpecFallback = ShopDrawingCommands.SpecManager?.GetAll()?.FirstOrDefault()?.Key ?? ""; } catch (System.Exception ex)

            {

                ShopDrawing.Plugin.Core.PluginLogger.Error("Suppressed exception in WasteManagerDialog.cs", ex);

            }

            }

            if (string.IsNullOrEmpty(bomSpecFallback)) bomSpecFallback = "N/A";



            var bomDisplay = new List<BomDisplayRow>();

            int stt = 1;

            double totalArea = 0; int totalQty = 0;

            foreach (var r in rows)

            {

                double area = r.AreaM2 * r.Qty;

                string rowSpec = r.Spec;

                if (string.IsNullOrEmpty(rowSpec) || rowSpec == "-") rowSpec = bomSpecFallback;



                bomDisplay.Add(new BomDisplayRow

                {

                    Stt = stt++,

                    Id = string.IsNullOrWhiteSpace(r.DisplayId) ? r.Id : r.DisplayId, Spec = rowSpec,

                    WidthMm = r.WidthMm, LengthMm = r.LengthMm,

                    JointDisplay = $"{r.JointLeft}/{r.JointRight}",

                    Qty = r.Qty,

                    TotalAreaM2 = Math.Round(area, 3),

                    Status = r.Status, WallCode = r.WallCode

                });

                totalArea += area;

                totalQty += r.Qty;

            }

            _bomSource = bomDisplay;

            _bomGrid.ItemsSource = _bomSortBar?.Apply(_bomSource) ?? _bomSource;

            _bomFooterText.Text = $"TỔNG: {totalQty} tấm  |  {totalArea:F2} m²";



            // Tab 2: BOM san xuat, chi tinh tam moi (loai tru tai su dung)

            // Chuan hoa width: nha may san xuat kho chuan, cong truong cat

            var factorySource = rows

                .Where(r => !IsReusedStatus(r.Status))

                .ToList();

            

            // Tim max WidthMm chuan cho moi WallCode (kho san xuat goc)

            var standardWidths = factorySource

                .GroupBy(r => r.WallCode)

                .ToDictionary(g => g.Key, g => g.Max(r => r.WidthMm));

            

            // Bat ky tam nao co width < chuan thi dung width chuan

            var factoryRows = factorySource

                .Select(r => {

                    double w = r.WidthMm;

                    if (standardWidths.ContainsKey(r.WallCode) && w < standardWidths[r.WallCode])

                    {

                        w = standardWidths[r.WallCode];

                    }

                    return new { r.Id, r.Spec, WidthMm = w, r.LengthMm, r.Qty, r.Status, r.WallCode };

                })

                .GroupBy(r => new { r.Id, r.Spec, r.WidthMm, r.LengthMm })  // them Id: moi PanelId = 1 dong

                .OrderBy(g => g.Key.Spec)

                .ThenByDescending(g => g.Key.LengthMm)

                .ThenBy(g => g.Key.Id)

                .ToList();



            // Spec override: neu BomManager khong tra ve Spec thi lay tu config

            string factorySpecFallback = ShopDrawingCommands.DefaultSpec;

            if (string.IsNullOrEmpty(factorySpecFallback))

            {

                var allSpecs = ShopDrawingCommands.SpecManager?.GetAll();

                if (allSpecs != null && allSpecs.Count > 0)

                    factorySpecFallback = allSpecs[0].Key;

            }



            var factDisplay = new List<FactoryDisplayRow>();

            int fstt = 1;

            double fTotalArea = 0; int fTotalQty = 0;

            foreach (var g in factoryRows)

            {

                int qty = g.Sum(x => x.Qty);

                double area = (g.Key.WidthMm * g.Key.LengthMm) / 1_000_000.0 * qty;

                var walls = g.Select(x => x.WallCode).Where(w => !string.IsNullOrEmpty(w)).Distinct();

                var panelIds = g.Select(x => x.Id).Where(id => !string.IsNullOrEmpty(id)).Distinct();

                bool hasCut = g.Any(x => IsCutStatus(x.Status));

                string note = hasCut ? "Cắt tại công trường" : "";

                if (walls.Any()) note += (note.Length > 0 ? " | " : "") + string.Join(", ", walls);



                // Spec: lay tu BomManager -> fallback DefaultSpec -> SpecManager -> N/A

                string displaySpec = g.Key.Spec;

                if (string.IsNullOrEmpty(displaySpec) || displaySpec == "-")

                {

                    displaySpec = ShopDrawingCommands.DefaultSpec;

                }

                if (string.IsNullOrEmpty(displaySpec) || displaySpec == "-")

                {

                    try { displaySpec = ShopDrawingCommands.SpecManager?.GetAll()?.FirstOrDefault()?.Key ?? ""; } catch (System.Exception ex)

            {

                ShopDrawing.Plugin.Core.PluginLogger.Error("Suppressed exception in WasteManagerDialog.cs", ex);

            }

                }

                if (string.IsNullOrEmpty(displaySpec)) displaySpec = "N/A";



                factDisplay.Add(new FactoryDisplayRow

                {

                    Stt = fstt++,

                    PanelIds = string.Join(", ", panelIds),

                    Spec = displaySpec,

                    WidthMm = g.Key.WidthMm,

                    LengthMm = g.Key.LengthMm,

                    Qty = qty,

                    TotalAreaM2 = Math.Round(area, 3),

                    Note = note

                });

                fTotalArea += area;

                fTotalQty += qty;

            }

            _factorySource = factDisplay;

            // Load batch numbers tu DB va gan vao tung dong

            if (ShopDrawingCommands.WasteRepo != null)

            {

                var batches = ShopDrawingCommands.WasteRepo.GetAllBatches();

                foreach (var row in _factorySource)

                    if (batches.TryGetValue(row.PanelIds, out int bno))

                        row.BatchNo = bno;

            }

            _factoryGrid.ItemsSource = _factorySortBar?.Apply(_factorySource) ?? _factorySource;

            _factoryFooterText.Text = $"TỔNG ĐẶT HÀNG: {fTotalQty} tấm  |  {fTotalArea:F2} m²";



            // Tab 4: Phu kien

            _accessorySource = BuildAccessoryDisplayRows(accessoryRows);

            _accessoryGrid.ItemsSource = _accessorySortBar?.Apply(_accessorySource) ?? _accessorySource;
            _accessoryFooterText.Text = BuildAccessoryFooter(_accessorySource);

            // Tab 5: Tong hop

            UpdateSummary(rows, _allWaste, totalQty, totalArea, fTotalQty, fTotalArea);

            }

            finally

            {

                _isLoadingData = false;

            }

        }



        private void UpdateSummary(List<BomRow> rows, List<WastePanel> waste,

            int totalQty, double totalArea, int fQty, double fArea)

        {

            double discarded = waste.Where(w => HasStatus(w, "discarded"))

                .Sum(w => (w.WidthMm * w.LengthMm) / 1_000_000.0);

            double available = waste.Where(w => HasStatus(w, "available"))

                .Sum(w => (w.WidthMm * w.LengthMm) / 1_000_000.0);

            // Hao hut tong phat sinh = phan bo + phan con lai trong kho le.
            double generatedWaste = available + discarded;
            double totalInput = totalArea + generatedWaste;
            double pct = totalInput > 0 ? (generatedWaste / totalInput) * 100.0 : 0;



            double stepA = WasteByType(waste, "STEP");

            double openA = WasteByType(waste, "OPEN");

            double trimA = WasteByType(waste, "TRIM");

            double remA = WasteByType(waste, "REM");

            int availCount = waste.Count(w => HasStatus(w, "available"));



            // Text summary

            _txtSummary.Inlines.Clear();

            var lines = new[]

            {

                ("TỔNG HỢP DỰ ÁN", true),

                ("", false),

                ($"Tổng số tấm trên bản vẽ:   {totalQty} tấm", false),

                ($"Tổng diện tích panel:       {totalArea:F2} m²", false),

                ("", false),

                ($"Tấm đặt hàng nhà máy:      {fQty} tấm  ({fArea:F2} m²)", false),

                ("", false),

                ($"Tấm lẻ còn dùng được:       {availCount} tấm ({available:F3} m²)", false),

            };

            foreach (var (text, bold) in lines)

            {

                var run = new System.Windows.Documents.Run(text + "\n");

                if (bold) run.FontWeight = FontWeights.Bold;

                _txtSummary.Inlines.Add(run);

            }



            // Gauge: progress bar ty le hao hut

            DrawWasteGauge(pct, generatedWaste);



            // Bar chart: chi tiet theo nguon

            var wasteData = new[]

            {

                ("Tấm lẻ (REM)", remA, Color.FromRgb(52, 152, 219)),

                ("Bậc thang (STEP)", stepA, Color.FromRgb(155, 89, 182)),

                ("Lỗ mở (OPEN)", openA, Color.FromRgb(230, 126, 34)),

                ("Cắt tận dụng (TRIM)", trimA, Color.FromRgb(46, 204, 113)),

            };

            DrawBarChart(wasteData);

        }



        private void DrawWasteGauge(double pct, double discardedM2)

        {

            _gaugeCanvas.Children.Clear();

            double canvasW = _gaugeCanvas.ActualWidth > 10 ? _gaugeCanvas.ActualWidth : 700;

            double barW = canvasW - 20;

            double barH = 24;

            double barY = 8;



            // Background track

            var track = new Rectangle

            {

                Width = barW, Height = barH,

                Fill = new SolidColorBrush(Color.FromRgb(220, 220, 230)),

                RadiusX = 4, RadiusY = 4

            };

            Canvas.SetLeft(track, 10); Canvas.SetTop(track, barY);

            _gaugeCanvas.Children.Add(track);



            // Fill bar (capped at 15% for display)

            double fillPct = Math.Min(pct, 15.0) / 15.0;

            Color barColor = pct < 5 ? Color.FromRgb(46, 204, 113)

                           : pct < 8 ? Color.FromRgb(241, 196, 15)

                                     : Color.FromRgb(231, 76, 60);

            double fillW = Math.Max(2, barW * fillPct);

            var fill = new Rectangle

            {

                Width = fillW, Height = barH,

                Fill = new SolidColorBrush(barColor),

                RadiusX = 4, RadiusY = 4

            };

            Canvas.SetLeft(fill, 10); Canvas.SetTop(fill, barY);

            _gaugeCanvas.Children.Add(fill);



            // Percentage text on bar

            var pctText = new TextBlock

            {

                Text = $"{pct:F1}%  ({discardedM2:F3} m2)",

                FontSize = 12, FontWeight = FontWeights.Bold,

                Foreground = fillW > 120 ? Brushes.White : new SolidColorBrush(Color.FromRgb(50, 50, 50))

            };

            Canvas.SetLeft(pctText, fillW > 120 ? 16 : fillW + 16);

            Canvas.SetTop(pctText, barY + 3);

            _gaugeCanvas.Children.Add(pctText);



            // Evaluation text

            string eval = pct < 5 ? "Tốt (< 5%)" : pct < 8 ? "Trung bình (5-8%)" : "Cần cải thiện (> 8%)";

            var evalText = new TextBlock

            {

                Text = eval,

                FontSize = 11, FontWeight = FontWeights.SemiBold,

                Foreground = new SolidColorBrush(barColor)

            };

            Canvas.SetLeft(evalText, 10); Canvas.SetTop(evalText, barY + barH + 6);

            _gaugeCanvas.Children.Add(evalText);



            // Threshold markers (5%, 8%)

            double mark5 = 10 + barW * (5.0 / 15.0);

            double mark8 = 10 + barW * (8.0 / 15.0);

            foreach (var (mx, label) in new[] { (mark5, "5%"), (mark8, "8%") })

            {

                var line = new Rectangle { Width = 1, Height = barH + 4, Fill = Brushes.Gray };

                Canvas.SetLeft(line, mx); Canvas.SetTop(line, barY - 2);

                _gaugeCanvas.Children.Add(line);

                var markText = new TextBlock { Text = label, FontSize = 9, Foreground = Brushes.Gray };

                Canvas.SetLeft(markText, mx - 6); Canvas.SetTop(markText, barY + barH + 6);

                _gaugeCanvas.Children.Add(markText);

            }

        }



        private void DrawBarChart((string Label, double Value, Color BarColor)[] data)

        {

            _barChartCanvas.Children.Clear();

            double canvasW = _barChartCanvas.ActualWidth > 10 ? _barChartCanvas.ActualWidth : 700;

            double maxVal = data.Max(d => d.Value);

            if (maxVal <= 0) maxVal = 1;



            double labelW = 140;  // Width for labels

            double barMaxW = canvasW - labelW - 100; // Leave room for value text

            double barH = 26;

            double gap = 8;

            double y = 8;



            foreach (var (label, value, color) in data)

            {

                // Label

                var lblText = new TextBlock

                {

                    Text = label, FontSize = 12,

                    Width = labelW, TextAlignment = TextAlignment.Right,

                    Foreground = new SolidColorBrush(Color.FromRgb(60, 60, 60))

                };

                Canvas.SetLeft(lblText, 0); Canvas.SetTop(lblText, y + 4);

                _barChartCanvas.Children.Add(lblText);



                // Bar background

                var bg = new Rectangle

                {

                    Width = barMaxW, Height = barH,

                    Fill = new SolidColorBrush(Color.FromRgb(235, 235, 240)),

                    RadiusX = 3, RadiusY = 3

                };

                Canvas.SetLeft(bg, labelW + 10); Canvas.SetTop(bg, y);

                _barChartCanvas.Children.Add(bg);



                // Bar fill

                double bw = Math.Max(2, barMaxW * (value / maxVal));

                var bar = new Rectangle

                {

                    Width = bw, Height = barH,

                    Fill = new SolidColorBrush(color),

                    RadiusX = 3, RadiusY = 3

                };

                Canvas.SetLeft(bar, labelW + 10); Canvas.SetTop(bar, y);

                _barChartCanvas.Children.Add(bar);



                // Value text

                var valText = new TextBlock

                {

                    Text = $"{value:F3} m2",

                    FontSize = 11, FontWeight = FontWeights.SemiBold,

                    Foreground = new SolidColorBrush(Color.FromRgb(80, 80, 80))

                };

                Canvas.SetLeft(valText, labelW + 10 + bw + 8);

                Canvas.SetTop(valText, y + 5);

                _barChartCanvas.Children.Add(valText);



                y += barH + gap;

            }

        }



        private double WasteByType(List<WastePanel> waste, string type)

            => waste
                .Where(w => string.Equals((w.SourceType ?? string.Empty).Trim(), type, StringComparison.OrdinalIgnoreCase))
                .Where(w => !HasStatus(w, "used"))
                .Sum(w => w.DienTich);



        private static bool HasStatus(WastePanel panel, string status)
            => string.Equals((panel.Status ?? string.Empty).Trim(), status, StringComparison.OrdinalIgnoreCase);

        private static List<AccessoryDisplayRow> BuildAccessoryDisplayRows(IReadOnlyList<ShopdrawingAccessorySummaryRow> sourceRows)
        {
            if (sourceRows == null || sourceRows.Count == 0)
            {
                return new List<AccessoryDisplayRow>();
            }

            var groupedRows = sourceRows
                .Where(row => row != null && row.Quantity > 0)
                .GroupBy(row => new
                {
                    NameKey = NormalizeAccessoryGroupKey(row.Name),
                    MaterialKey = NormalizeAccessoryGroupKey(row.Material),
                    UnitKey = NormalizeAccessoryGroupKey(row.Unit)
                })
                .Select(group =>
                {
                    var rows = group.ToList();
                    double basisValue = rows.Sum(x => x.BasisValue);
                    double quantity = rows.Sum(x => x.Quantity);

                    return new AccessoryDisplayRow
                    {
                        CategoryScope = BuildSummaryDisplayValue(rows.Select(x => x.CategoryScope), "Tổng hợp"),
                        Application = BuildSummaryDisplayValue(rows.Select(x => x.Application), "Tổng hợp"),
                        SpecKey = BuildSummaryDisplayValue(rows.Select(x => x.SpecKey), "Tổng hợp"),
                        Name = BuildSummaryDisplayValue(rows.Select(x => x.Name), "-"),
                        Material = BuildSummaryDisplayValue(rows.Select(x => x.Material), "-"),
                        Position = BuildSummaryDisplayValue(rows.Select(x => x.Position), "Tổng hợp vị trí", " + "),
                        Unit = BuildSummaryDisplayValue(rows.Select(x => x.Unit), "-"),
                        RuleLabel = BuildCommonValueOrDefault(rows.Select(x => TenderAccessoryRules.GetRuleLabel(x.Rule)), "Tổng hợp nhiều quy tắc"),
                        BasisLabel = BuildCommonValueOrDefault(rows.Select(x => TenderAccessoryRules.GetBasisLabel(x.Rule)), "Tổng hợp nhiều cơ sở"),
                        BasisValue = basisValue,
                        Factor = basisValue > 0 ? quantity / basisValue : rows[0].Factor,
                        Quantity = quantity,
                        Note = BuildGroupedNotes(rows.Select(x => x.Note))
                    };
                })
                .OrderBy(row => row.Application, StringComparer.OrdinalIgnoreCase)
                .ThenBy(row => row.Position, StringComparer.OrdinalIgnoreCase)
                .ThenBy(row => row.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            for (int i = 0; i < groupedRows.Count; i++)
            {
                groupedRows[i].Stt = i + 1;
            }

            return groupedRows;
        }

        private static string NormalizeAccessoryGroupKey(string? value)
            => string.IsNullOrWhiteSpace(value) ? "-" : value.Trim().ToUpperInvariant();

        private static string BuildSummaryDisplayValue(IEnumerable<string?> values, string fallback, string separator = ", ")
        {
            var distinctValues = values
                .Select(value => (value ?? string.Empty).Trim())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (distinctValues.Count == 0)
            {
                return fallback;
            }

            if (distinctValues.Count == 1)
            {
                return distinctValues[0];
            }

            if (separator == ", ")
            {
                return fallback;
            }

            return string.Join(separator, distinctValues);
        }

        private static string BuildCommonValueOrDefault(IEnumerable<string?> values, string fallback)
        {
            var distinctValues = values
                .Select(value => (value ?? string.Empty).Trim())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (distinctValues.Count == 0)
            {
                return fallback;
            }

            return distinctValues.Count == 1 ? distinctValues[0] : fallback;
        }

        private static string BuildGroupedNotes(IEnumerable<string?> notes)
        {
            var distinctNotes = notes
                .Select(note => (note ?? string.Empty).Trim())
                .Where(note => !string.IsNullOrWhiteSpace(note))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (distinctNotes.Count == 0)
            {
                return string.Empty;
            }

            if (distinctNotes.Count <= 4)
            {
                return string.Join(" | ", distinctNotes);
            }

            int remain = distinctNotes.Count - 4;
            var selected = distinctNotes.Take(4).ToList();
            selected.Add($"+ {remain} nhóm");
            return string.Join(" | ", selected);
        }

        private static string BuildAccessoryFooter(IReadOnlyList<AccessoryDisplayRow> rows)

        {

            if (rows == null || rows.Count == 0)

            {

                return "TỔNG PHỤ KIỆN: 0 dòng";

            }



            string quantityByUnit = string.Join(

                "  |  ",

                rows.GroupBy(row => string.IsNullOrWhiteSpace(row.Unit) ? "-" : row.Unit.Trim(), StringComparer.OrdinalIgnoreCase)

                    .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)

                    .Select(group => $"{group.Key}: {group.Sum(x => x.Quantity):F2}"));



            return $"TỔNG PHỤ KIỆN: {rows.Count} dòng  |  {quantityByUnit}";

        }



        private static bool IsReusedStatus(string? status)

        {

            if (string.IsNullOrWhiteSpace(status))

            {

                return false;

            }



            return status.Contains('\u267B')

                || status.Contains("TÁI", StringComparison.OrdinalIgnoreCase)

                || status.Contains("TAI", StringComparison.OrdinalIgnoreCase);

        }



        private static bool IsCutStatus(string? status)

        {

            if (string.IsNullOrWhiteSpace(status))

            {

                return false;

            }



            return status.Contains('\u2702')

                || status.Contains("CẮT", StringComparison.OrdinalIgnoreCase)

                || status.Contains("CAT", StringComparison.OrdinalIgnoreCase);

        }



        private void UpdateWasteSubtotals(IReadOnlyList<WastePanel> visibleItems)

        {

            if (_wasteSubtotalText == null)

            {

                return;

            }



            var all = _allWaste;

            var visible = visibleItems ?? Array.Empty<WastePanel>();



            double avail = all.Where(w => HasStatus(w, "available")).Sum(w => w.DienTich);

            double used  = all.Where(w => HasStatus(w, "used")).Sum(w => w.DienTich);

            double disc  = all.Where(w => HasStatus(w, "discarded")).Sum(w => w.DienTich);

            int cAvail   = all.Count(w => HasStatus(w, "available"));

            int cUsed    = all.Count(w => HasStatus(w, "used"));

            int cDisc    = all.Count(w => HasStatus(w, "discarded"));



            double visibleArea = visible.Sum(w => w.DienTich);

            double visibleWeight = visible.Sum(w => w.KhoiLuong);

            int visibleCount = visible.Count;



            _wasteSubtotalText.Text =

                $"Đang xem: {visibleCount} tấm - {visibleArea:F3} m² - {visibleWeight:F1} kg" + Environment.NewLine +

                $"Tổng kho | Sẵn sàng: {cAvail} tấm - {avail:F3} m²     Đã dùng: {cUsed} tấm - {used:F3} m²     Đã bỏ: {cDisc} tấm - {disc:F3} m²" + Environment.NewLine +

                $"Theo nguồn | Tấm lẻ: {CountBySource(all, "REM")} tấm - {AreaBySource(all, "REM"):F3} m²     Bậc thang: {CountBySource(all, "STEP")} tấm - {AreaBySource(all, "STEP"):F3} m²     Lỗ mở: {CountBySource(all, "OPEN")} tấm - {AreaBySource(all, "OPEN"):F3} m²     Cắt tận dụng: {CountBySource(all, "TRIM")} tấm - {AreaBySource(all, "TRIM"):F3} m²";

        }



        private static int CountBySource(IEnumerable<WastePanel> items, string sourceType)

            => items.Count(w => string.Equals((w.SourceType ?? string.Empty).Trim(), sourceType, StringComparison.OrdinalIgnoreCase));



        private static double AreaBySource(IEnumerable<WastePanel> items, string sourceType)

            => items.Where(w => string.Equals((w.SourceType ?? string.Empty).Trim(), sourceType, StringComparison.OrdinalIgnoreCase)).Sum(w => w.DienTich);



        // ================================================

        //  WASTE FILTER (Tab 3)

        // ================================================

        private StackPanel CreateFilterBar()

        {

            var bar = new StackPanel

            {

                Orientation = Orientation.Horizontal,

                Margin = new Thickness(0, 0, 0, 4),

                VerticalAlignment = VerticalAlignment.Center

            };



            bar.Children.Add(new TextBlock

            {

                Text = "Tr\u1ea1ng th\u00e1i:",

                VerticalAlignment = VerticalAlignment.Center,

                Margin = new Thickness(0, 0, 4, 0)

            });

            _cboStatus = new ComboBox

            {

                Width = 120, Margin = new Thickness(0, 0, 12, 0),

                DisplayMemberPath = "Display", SelectedValuePath = "Value"

            };

            _cboStatus.Items.Add(new { Display = "T\u1ea5t c\u1ea3",      Value = "" });

            _cboStatus.Items.Add(new { Display = "S\u1eb5n s\u00e0ng",   Value = "available" });

            _cboStatus.Items.Add(new { Display = "\u0110\u00e3 d\u00f9ng",     Value = "used" });

            _cboStatus.Items.Add(new { Display = "\u0110\u00e3 b\u1ecf",       Value = "discarded" });

            _cboStatus.SelectedIndex = 0;

            _cboStatus.SelectionChanged += (s, e) => ApplyWasteFilter();

            bar.Children.Add(_cboStatus);



            bar.Children.Add(new TextBlock

            {

                Text = "D\u00e0y:",

                VerticalAlignment = VerticalAlignment.Center,

                Margin = new Thickness(0, 0, 4, 0)

            });

            _cboThickness = new ComboBox { Width = 75, Margin = new Thickness(0, 0, 12, 0) };

            _cboThickness.Items.Add("T\u1ea5t c\u1ea3");

            foreach (var t in new[] { 50, 60, 75, 80, 100, 125, 150, 180, 200 }) _cboThickness.Items.Add(t.ToString());

            _cboThickness.SelectedIndex = 0;

            _cboThickness.SelectionChanged += (s, e) => ApplyWasteFilter();

            bar.Children.Add(_cboThickness);



            bar.Children.Add(new TextBlock

            {

                Text = "T\u00ecm:",

                VerticalAlignment = VerticalAlignment.Center,

                Margin = new Thickness(0, 0, 4, 0)

            });

            _txtSearch = new TextBox { Width = 160, VerticalAlignment = VerticalAlignment.Center };

            _txtSearch.TextChanged += (s, e) => ApplyWasteFilter();

            bar.Children.Add(_txtSearch);



            return bar;

        }



        private void ApplyWasteFilter()

        {

            if (_isApplyingWasteFilter)

            {

                return;

            }



            _isApplyingWasteFilter = true;

            try

            {

            var filtered = _allWaste.AsEnumerable();



            // Filter dung SelectedValue (Value field cua anonymous object)

            var selStatus = _cboStatus?.SelectedValue as string;

            if (!string.IsNullOrEmpty(selStatus))

                filtered = filtered.Where(x => HasStatus(x, selStatus));



            if (_cboThickness?.SelectedItem is string thickStr && thickStr != "Tất cả"

                && int.TryParse(thickStr, out int thick))

                filtered = filtered.Where(x => x.ThickMm == thick);



            if (_txtSearch != null && !string.IsNullOrWhiteSpace(_txtSearch.Text))

            {

                string q = _txtSearch.Text.Trim().ToLower();

                filtered = filtered.Where(x =>

                    (x.PanelCode?.ToLower().Contains(q) ?? false) ||

                    (x.SourceWall?.ToLower().Contains(q) ?? false) ||

                    (x.Project?.ToLower().Contains(q) ?? false) ||

                    (x.PanelSpec?.ToLower().Contains(q) ?? false));

            }



            // Ap sort len ket qua filter

            var sorted = _wasteSortBar != null

                ? _wasteSortBar.Apply(filtered)

                : filtered;



            var visibleItems = sorted.ToList();

            Wastes.Clear();

            foreach (var item in visibleItems) Wastes.Add(item);

            UpdateWasteSubtotals(visibleItems);

            }

            finally

            {

                _isApplyingWasteFilter = false;

            }

        }



        private void UpdateWasteSubtotals()

        {

            if (_wasteSubtotalText == null) return;

            var all = _allWaste;

            double avail = all.Where(w => HasStatus(w, "available")).Sum(w => w.DienTich);

            double used  = all.Where(w => HasStatus(w, "used")).Sum(w => w.DienTich);

            double disc  = all.Where(w => HasStatus(w, "discarded")).Sum(w => w.DienTich);

            int cAvail   = all.Count(w => HasStatus(w, "available"));

            int cUsed    = all.Count(w => HasStatus(w, "used"));

            int cDisc    = all.Count(w => HasStatus(w, "discarded"));

            _wasteSubtotalText.Text =

                $"Sẵn sàng: {cAvail} tấm - {avail:F3} m²     " +

                $"Đã dùng: {cUsed} tấm - {used:F3} m²     " +

                $"Đã bỏ: {cDisc} tấm - {disc:F3} m²";

        }



        // ================================================

        //  ACTIONS

        // ================================================

        private void BtnAdd_Click(object sender, RoutedEventArgs e)

        {

            if (ShopDrawingCommands.WasteRepo == null)

            {

                UiFeedback.ShowWarning("Waste DB chưa sẵn sàng!", "Lỗi");

                return;

            }

            var dlg = new AddWasteDialog();

            if (dlg.ShowDialog() == true)

            {

                ShopDrawingCommands.WasteRepo.AddPanel(dlg.ResultPanel);

                RequestLoadAllData();

            }

        }



        private void BtnDelete_Click(object sender, RoutedEventArgs e)

        {

            if (ShopDrawingCommands.WasteRepo == null)

            {

                UiFeedback.ShowWarning("Waste DB chưa sẵn sàng!", "Lỗi");

                return;

            }

            var selected = _wasteGrid.SelectedItems.Cast<WastePanel>().ToList();

            if (selected.Count == 0)

            {

                UiFeedback.ShowInfo("Vui lòng chọn ít nhất 1 tấm để xóa.");

                return;

            }

            string names = string.Join("\n", selected.Select(p => $"  - {p.PanelCode} ({p.WidthMm:F0}mm)"));

            

            // Custom scrollable confirmation dialog

            var confirmDlg = new Window

            {

                Title = "Xác nhận xóa",

                Width = 420, SizeToContent = SizeToContent.Height,

                MaxHeight = 550, WindowStartupLocation = WindowStartupLocation.CenterScreen,

                ResizeMode = ResizeMode.NoResize

            };

            var stack = new StackPanel { Margin = new Thickness(16) };

            stack.Children.Add(new TextBlock

            {

                Text = $"Xóa vĩnh viễn {selected.Count} tấm sau?",

                FontSize = 14, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 8)

            });

            var scroll = new ScrollViewer

            {

                MaxHeight = 350, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,

                Content = new TextBlock { Text = names, FontSize = 12, TextWrapping = TextWrapping.Wrap }

            };

            stack.Children.Add(scroll);

            stack.Children.Add(new TextBlock

            {

                Text = "Hành động này không thể hoàn tác.",

                Foreground = Brushes.Red, FontStyle = FontStyles.Italic, Margin = new Thickness(0, 8, 0, 12)

            });

            var btnPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };

            var btnYes = new Button

            {

                Content = "Xóa", Width = 80, Height = 30, Margin = new Thickness(0, 0, 8, 0),

                Background = new SolidColorBrush(Color.FromRgb(231, 76, 60)), Foreground = Brushes.White

            };

            btnYes.Click += (s2, e2) => { confirmDlg.DialogResult = true; };

            var btnNo = new Button { Content = "Hủy", Width = 80, Height = 30 };

            btnNo.Click += (s2, e2) => { confirmDlg.DialogResult = false; };

            btnPanel.Children.Add(btnYes);

            btnPanel.Children.Add(btnNo);

            stack.Children.Add(btnPanel);

            confirmDlg.Content = stack;

            

            var result = confirmDlg.ShowDialog() == true ? MessageBoxResult.Yes : MessageBoxResult.No;

            if (result == MessageBoxResult.Yes)

            {

                foreach (var p in selected) ShopDrawingCommands.WasteRepo.HardDelete(p.Id);

                RequestLoadAllData();

            }

        }



        private void BtnExport_Click(object sender, RoutedEventArgs e)

        {

            try

            {

                var doc = Application.DocumentManager.MdiActiveDocument;

                if (doc == null) return;



                // Hoi vi tri luu file

                var dlg = new Microsoft.Win32.SaveFileDialog

                {

                    Title = "Chọn nơi lưu file BOM Excel",

                    Filter = "Excel Files (*.xlsx)|*.xlsx",

                    FileName = $"ShopDrawing_BOM_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx",

                    InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop)

                };

                if (dlg.ShowDialog() != true) return;



                List<BomRow> rows;

                using (var cadLock = SafeCadLock.TryLock())

                {

                    if (cadLock == null) return;

                    using (var tr = doc.Database.TransactionManager.StartTransaction())

                    {

                    rows = ShopDrawingCommands.BomManager.ScanDocumentForPanels(tr, doc.Database);

                    tr.Commit();

                }

                }



                var exporter = new ExcelExporter();

                exporter.ExportFullBom(rows, ShopDrawingCommands.WasteRepo, dlg.FileName);



                UiFeedback.ShowInfo($"Xuất thành công!\n\n{dlg.FileName}", "Xuất Excel");

            }

            catch (Exception ex)

            {

                UiFeedback.ShowError($"Lỗi xuất Excel: {ex.Message}");

            }

        }



        // ================================================

        //  HELPERS

        // ================================================

        private DataGridTextColumn Col(string header, string binding, double width, string? format = null)

        {

            var b = new Binding(binding);

            if (format != null) b.StringFormat = format;

            return new DataGridTextColumn { Header = header, Binding = b, Width = width };

        }



        private DataGridTextColumn ColStar(string header, string binding)

            => new DataGridTextColumn

            {

                Header = header,

                Binding = new Binding(binding),

                Width = new DataGridLength(1, DataGridLengthUnitType.Star)

            };



        private Button MakeButton(string text, Color bg, RoutedEventHandler handler)

        {

            var btn = new Button

            {

                Content = text, Width = 110, Height = 34,

                Margin = new Thickness(0, 0, 8, 0),

                Background = new SolidColorBrush(bg),

                Foreground = Brushes.White, FontWeight = FontWeights.SemiBold

            };

            btn.Click += handler;

            return btn;

        }



        // =====================================================

        //  DataGridSortBar tai su dung cho moi DataGrid

        // =====================================================

        private class DataGridSortBar

        {

            private record SortLevel(string Label, string Prop, bool Asc);



            public readonly StackPanel Panel;

            private readonly (string Label, string Prop)[] _cols;

            private readonly Action _onChanged;

            private readonly List<SortLevel> _levels = new();



            // UI controls

            private readonly ComboBox  _cboCols;

            private readonly WrapPanel _chipsPanel;



            public DataGridSortBar((string Label, string Prop)[] cols, Action onChanged)

            {

                _cols      = cols;

                _onChanged = onChanged;



                // Outer container

                Panel = new StackPanel { Margin = new Thickness(0, 0, 0, 4) };



                // Row 1: controls

                var row1 = new WrapPanel { Orientation = Orientation.Horizontal };



                row1.Children.Add(new TextBlock

                {

                    Text = "S\u1eafp x\u1ebfp:",

                    VerticalAlignment = VerticalAlignment.Center,

                    Margin = new Thickness(0, 0, 4, 0)

                });



                _cboCols = new ComboBox

                {

                    Width = 130, Margin = new Thickness(0, 0, 4, 0),

                    ToolTip = "Chọn cột muốn sắp xếp"

                };

                _cboCols.Items.Add("-- Chọn cột --");

                foreach (var (label, _) in cols) _cboCols.Items.Add(label);

                _cboCols.SelectedIndex = 0;

                row1.Children.Add(_cboCols);





                // Nut them

                var btnAdd = MakeCtrlBtn("+ Thêm", Color.FromRgb(52, 152, 219));

                btnAdd.Click += (_, __) => AddLevel();

                row1.Children.Add(btnAdd);



                // Nut reset

                var btnReset = MakeCtrlBtn("\u21ba Xóa tất cả", Color.FromRgb(149, 165, 166));

                btnReset.Click += (_, __) => ResetAll();

                row1.Children.Add(btnReset);



                Panel.Children.Add(row1);



                // Row 2: chips

                _chipsPanel = new WrapPanel

                {

                    Orientation = Orientation.Horizontal,

                    Margin = new Thickness(0, 2, 0, 0)

                };

                Panel.Children.Add(_chipsPanel);

            }



            // Public: ap sort len danh sach bat ky kieu T

            public IEnumerable<T> Apply<T>(IEnumerable<T> source)

            {

                var list = source.ToList();

                if (_levels.Count == 0)

                {

                    // Khong sort van renumber STT theo thu tu hien tai

                    RenumberStt(list);

                    return list;

                }



                var type = typeof(T);

                IOrderedEnumerable<T>? ordered = null;



                foreach (var (i, lv) in _levels.Select((v, i) => (i, v)))

                {

                    var prop = type.GetProperty(lv.Prop);

                    if (prop == null) continue;



                    // Dung NaturalCompare cho string (W1 < W2 < W10 < W11)

                    var comparer = new NaturalComparer();

                    Func<T, object?> key = x => prop.GetValue(x);



                    if (ordered == null)

                        ordered = lv.Asc

                            ? list.OrderBy(key, comparer)

                            : list.OrderByDescending(key, comparer);

                    else

                        ordered = lv.Asc

                            ? ordered.ThenBy(key, comparer)

                            : ordered.ThenByDescending(key, comparer);

                }



                var result = (ordered != null ? ordered.ToList() : list);



                // Renumber STT theo thu tu moi

                RenumberStt(result);

                return result;

            }



            // Tu dong cap nhat STT = 1, 2, 3... neu T co property Stt

            private static void RenumberStt<T>(List<T> list)

            {

                var sttProp = typeof(T).GetProperty("Stt");

                if (sttProp == null || !sttProp.CanWrite) return;

                for (int i = 0; i < list.Count; i++)

                    sttProp.SetValue(list[i], i + 1);

            }



            // Natural sort comparer: "W2" < "W10" < "W11"

            private class NaturalComparer : IComparer<object?>

            {

                // Khong co _asc: huong sap xep do OrderBy/OrderByDescending quan

                // tranh double-negation bug (sign=-1 x OrderByDesc = Asc).

                public int Compare(object? x, object? y)

                {

                    if (x is string sx && y is string sy)

                        return NaturalCompare(sx, sy);

                    return Comparer<object?>.Default.Compare(x, y);

                }



                private static int NaturalCompare(string a, string b)

                {

                    // Tach chuoi thanh cac phan so va chu: "W10-01" -> ["W", "10", "-", "01", ...]

                    var pa = System.Text.RegularExpressions.Regex.Split(a ?? "", @"(\d+)");

                    var pb = System.Text.RegularExpressions.Regex.Split(b ?? "", @"(\d+)");

                    int len = Math.Min(pa.Length, pb.Length);

                    for (int i = 0; i < len; i++)

                    {

                        int cmp;

                        if (int.TryParse(pa[i], out int na) && int.TryParse(pb[i], out int nb))

                            cmp = na.CompareTo(nb);

                        else

                            cmp = string.Compare(pa[i], pb[i], StringComparison.OrdinalIgnoreCase);

                        if (cmp != 0) return cmp;

                    }

                    return pa.Length.CompareTo(pb.Length);

                }

            }



            // Private helpers

            private void AddLevel()

            {

                int idx = _cboCols.SelectedIndex - 1; // -1 vi index 0 la placeholder

                if (idx < 0 || idx >= _cols.Length) return;



                var (label, prop) = _cols[idx];



                // Khong cho them cung cot 2 lan

                if (_levels.Any(l => l.Prop == prop))

                {

                    // Toggle ASC/DESC neu da co

                    var existing = _levels.First(l => l.Prop == prop);

                    int li = _levels.IndexOf(existing);

                    _levels[li] = existing with { Asc = !existing.Asc };

                    RebuildChips();

                    _onChanged();

                    return;

                }



                _levels.Add(new SortLevel(label, prop, true)); // mac dinh tang, click chip de doi

                RebuildChips();

                _onChanged();

            }



            private void RemoveLevel(string prop)

            {

                _levels.RemoveAll(l => l.Prop == prop);

                RebuildChips();

                _onChanged();

            }



            private void ResetAll()

            {

                _levels.Clear();

                RebuildChips();

                _onChanged();

            }



            private void RebuildChips()

            {

                _chipsPanel.Children.Clear();

                for (int i = 0; i < _levels.Count; i++)

                {

                    var lv = _levels[i];

                    string dir = lv.Asc ? "\u2191" : "\u2193";

                    var chipBg = lv.Asc

                        ? Color.FromRgb(52, 152, 219)   // xanh = Asc

                        : Color.FromRgb(230, 126, 34);  // cam  = Desc



                    var chip = new Border

                    {

                        Background  = new SolidColorBrush(chipBg),

                        CornerRadius = new CornerRadius(4),

                        Margin      = new Thickness(0, 0, 4, 0),

                        Padding     = new Thickness(6, 2, 4, 2),

                        Cursor      = System.Windows.Input.Cursors.Hand,

                        ToolTip     = "Click để đổi hướng sắp xếp"

                    };



                    var inner = new StackPanel { Orientation = Orientation.Horizontal };



                    // So thu tu + ten cot + huong

                    inner.Children.Add(new TextBlock

                    {

                        Text       = $"{NumberCircle(i + 1)} {lv.Label} {dir}",

                        Foreground = Brushes.White,

                        FontWeight = FontWeights.SemiBold,

                        FontSize   = 11,

                        VerticalAlignment = VerticalAlignment.Center

                    });



                    // Nut x de xoa chip

                    var btnX = new TextBlock

                    {

                        Text       = "  \u00d7",

                        Foreground = new SolidColorBrush(Color.FromRgb(255, 220, 220)),

                        FontWeight = FontWeights.Bold,

                        FontSize   = 13,

                        VerticalAlignment = VerticalAlignment.Center,

                        Cursor     = System.Windows.Input.Cursors.Hand,

                        ToolTip    = "Xóa cấp sắp xếp này"

                    };

                    var captureProp = lv.Prop;

                    btnX.MouseLeftButtonUp += (_, __) => RemoveLevel(captureProp);

                    inner.Children.Add(btnX);



                    chip.Child = inner;



                    // Click chip = toggle ASC/DESC

                    var captureLv = lv;

                    chip.MouseLeftButtonUp += (_, __) =>

                    {

                        int li = _levels.IndexOf(captureLv);

                        if (li < 0) return;

                        _levels[li] = captureLv with { Asc = !captureLv.Asc };

                        captureLv = _levels[li];

                        RebuildChips();

                        _onChanged();

                    };



                    _chipsPanel.Children.Add(chip);

                }

            }



            private static string NumberCircle(int n) => n switch

            {

                1 => "\u2460", 2 => "\u2461", 3 => "\u2462", 4 => "\u2463",

                5 => "\u2464", 6 => "\u2465", 7 => "\u2466", 8 => "\u2467",

                _ => $"({n})"

            };





            private static Button MakeCtrlBtn(string text, Color bg)

                => new Button

                {

                    Content = text, Height = 26,

                    Padding = new Thickness(8, 0, 8, 0),

                    Margin  = new Thickness(0, 0, 4, 0),

                    Background = new SolidColorBrush(bg),

                    Foreground = Brushes.White,

                    BorderThickness = new Thickness(0)

                };

        }

    }



    // ==========================================

    // Dialog them tam moi

    // ==========================================

    public class AddWasteDialog : Window

    {

        public WastePanel ResultPanel { get; private set; } = new WastePanel();



        private TextBox _txtCode, _txtWidth, _txtLength, _txtSourceWall, _txtProject;

        private ComboBox _cboThick, _cboSpec, _cboJointLeft, _cboJointRight;



        public AddWasteDialog()

        {

            Title = "Thêm Tấm Lẻ Vào Kho";

            Width = 400;

            Height = 430;

            WindowStartupLocation = WindowStartupLocation.CenterOwner;

            ResizeMode = ResizeMode.NoResize;

            Background = Brushes.White;



            var stack = new StackPanel { Margin = new Thickness(20) };



            stack.Children.Add(new TextBlock

            {

                Text = "THÊM TẤM LẺ VÀO KHO",

                FontSize = 14, FontWeight = FontWeights.Bold,

                Foreground = new SolidColorBrush(Color.FromRgb(46, 204, 113)),

                Margin = new Thickness(0, 0, 0, 15)

            });



            _txtCode = AddLabeledTextBox(stack, "Mã tấm:", "VD: W-REM-001 hoặc C-REM-001");



            var dimPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };

            dimPanel.Children.Add(new Label { Content = "Rộng (mm):", Width = 80 });

            _txtWidth = new TextBox { Width = 70, Margin = new Thickness(0, 0, 15, 0) };

            dimPanel.Children.Add(_txtWidth);

            dimPanel.Children.Add(new Label { Content = "Dài (mm):", Width = 70 });

            _txtLength = new TextBox { Width = 70 };

            dimPanel.Children.Add(_txtLength);

            stack.Children.Add(dimPanel);



            var thickPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };

            thickPanel.Children.Add(new Label { Content = "Dày (mm):", Width = 80 });

            _cboThick = new ComboBox { Width = 80 };

            foreach (var t in new[] { 50, 60, 75, 80, 100, 125, 150, 180, 200 }) _cboThick.Items.Add(t);

            _cboThick.SelectedItem = ShopDrawingCommands.DefaultThickness;

            thickPanel.Children.Add(_cboThick);

            stack.Children.Add(thickPanel);



            var specPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };

            specPanel.Children.Add(new Label { Content = "Spec:", Width = 80 });

            _cboSpec = new ComboBox { Width = 150 };

            var specs = ShopDrawingCommands.SpecManager.GetAll();

            foreach (var sp in specs) _cboSpec.Items.Add(sp.Key);

            if (_cboSpec.Items.Count > 0) _cboSpec.SelectedIndex = 0;

            specPanel.Children.Add(_cboSpec);

            stack.Children.Add(specPanel);



            var jointPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };

            jointPanel.Children.Add(new Label { Content = "Ngàm trái:", Width = 80 });

            _cboJointLeft = new ComboBox { Width = 70, Margin = new Thickness(0, 0, 15, 0) };

            _cboJointLeft.Items.Add("Male"); _cboJointLeft.Items.Add("Female"); _cboJointLeft.Items.Add("Cut");

            _cboJointLeft.SelectedIndex = 0;

            jointPanel.Children.Add(_cboJointLeft);

            jointPanel.Children.Add(new Label { Content = "Ngàm phải:", Width = 80 });

            _cboJointRight = new ComboBox { Width = 70 };

            _cboJointRight.Items.Add("Male"); _cboJointRight.Items.Add("Female"); _cboJointRight.Items.Add("Cut");

            _cboJointRight.SelectedIndex = 1;

            jointPanel.Children.Add(_cboJointRight);

            stack.Children.Add(jointPanel);



            _txtSourceWall = AddLabeledTextBox(stack, "Nguồn vùng:", "VD: W1 hoặc C1");

            _txtProject = AddLabeledTextBox(stack, "Dự án:", "VD: Công trình ABC");



            var btnPanel = new StackPanel

            {

                Orientation = Orientation.Horizontal,

                HorizontalAlignment = HorizontalAlignment.Right,

                Margin = new Thickness(0, 20, 0, 0)

            };

            var btnSave = new Button

            {

                Content = "Lưu", Width = 100, Height = 35,

                Background = new SolidColorBrush(Color.FromRgb(46, 204, 113)),

                Foreground = Brushes.White, FontWeight = FontWeights.SemiBold,

                Margin = new Thickness(0, 0, 10, 0)

            };

            btnSave.Click += BtnSave_Click;

            btnPanel.Children.Add(btnSave);

            var btnCancel = new Button { Content = "Hủy", Width = 80, Height = 35 };

            btnCancel.Click += (s, e) => { DialogResult = false; Close(); };

            btnPanel.Children.Add(btnCancel);

            stack.Children.Add(btnPanel);

            Content = stack;

        }



        private void BtnSave_Click(object sender, RoutedEventArgs e)

        {

            if (!double.TryParse(_txtWidth.Text, out double w) || w <= 0)

            { UiFeedback.ShowWarning("Chiều rộng không hợp lệ!", "Lỗi"); return; }

            if (!double.TryParse(_txtLength.Text, out double l) || l <= 0)

            { UiFeedback.ShowWarning("Chiều dài không hợp lệ!", "Lỗi"); return; }

            if (_cboThick.SelectedItem == null)

            { UiFeedback.ShowWarning("Vui lòng chọn độ dày!", "Lỗi"); return; }



            string code = string.IsNullOrWhiteSpace(_txtCode.Text) || _txtCode.Foreground == Brushes.Gray

                ? $"MANUAL-{DateTime.Now:yyyyMMdd-HHmmss}"

                : _txtCode.Text.Trim();



            ResultPanel = new WastePanel

            {

                PanelCode = code,

                WidthMm = w,

                LengthMm = l,

                ThickMm = (int)_cboThick.SelectedItem,

                PanelSpec = _cboSpec.SelectedItem?.ToString() ?? "Default",

                JointLeft = Enum.TryParse<JointType>(_cboJointLeft.SelectedItem?.ToString(), out var jl) ? jl : JointType.Male,

                JointRight = Enum.TryParse<JointType>(_cboJointRight.SelectedItem?.ToString(), out var jr) ? jr : JointType.Female,

                SourceWall = _txtSourceWall.Foreground == Brushes.Gray ? "" : _txtSourceWall.Text.Trim(),

                Project = _txtProject.Foreground == Brushes.Gray ? "" : _txtProject.Text.Trim(),

                Status = "available"

            };

            DialogResult = true;

            Close();

        }



        private TextBox AddLabeledTextBox(StackPanel parent, string label, string placeholder)

        {

            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };

            row.Children.Add(new Label { Content = label, Width = 90 });

            var tb = new TextBox { Width = 230 };

            tb.GotFocus += (s, e) => { if (tb.Foreground == Brushes.Gray) { tb.Text = ""; tb.Foreground = Brushes.Black; } };

            tb.LostFocus += (s, e) => { if (string.IsNullOrWhiteSpace(tb.Text)) { tb.Text = placeholder; tb.Foreground = Brushes.Gray; } };

            tb.Text = placeholder;

            tb.Foreground = Brushes.Gray;

            row.Children.Add(tb);

            parent.Children.Add(row);

            return tb;

        }

    }



    // ==========================================

    // Display models for WPF DataGrid binding

    // (anonymous types are internal -> WPF cannot bind)

    // ==========================================

    public class BomDisplayRow

    {

        public int Stt { get; set; }

        public string Id { get; set; } = "";

        public string Spec { get; set; } = "";

        public double WidthMm { get; set; }

        public double LengthMm { get; set; }

        public string JointDisplay { get; set; } = "";

        public int Qty { get; set; }

        public double TotalAreaM2 { get; set; }

        public string Status { get; set; } = "";

        public string WallCode { get; set; } = "";

    }



    public class FactoryDisplayRow

    {

        public int Stt { get; set; }

        public int BatchNo { get; set; }         // Dot giao hang (0 = chua gan)

        public string PanelIds { get; set; } = "";

        public string Spec { get; set; } = "";

        public double WidthMm { get; set; }

        public double LengthMm { get; set; }

        public int Qty { get; set; }

        public double TotalAreaM2 { get; set; }

        public string Note { get; set; } = "";



        // Display helper: show blank when chua gan

        public string BatchDisplay => BatchNo > 0 ? $"Dot {BatchNo}" : "";

    }



    public class AccessoryDisplayRow

    {

        public int Stt { get; set; }

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

        public double Quantity { get; set; }

        public string Note { get; set; } = "";

    }

}



