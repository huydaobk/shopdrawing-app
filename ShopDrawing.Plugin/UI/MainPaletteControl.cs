using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ShopDrawing.Plugin.Commands;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using ShopDrawing.Plugin.Data;
using ShopDrawing.Plugin.Models;
using ShopDrawing.Plugin.Core;

namespace ShopDrawing.Plugin.UI
{
    public class MainPaletteControl : UserControl
    {
        private Label _lblTotalPanels;
        private Label _lblTotalWaste;
        private Label _lblWastePercent;
        private TextBlock _lblThickness;
        private TextBlock _lblPanelWidth;
        private TextBlock _lblScale;

        public MainPaletteControl()
        {
            var S = typeof(SdPaletteStyles); // shorthand
            var mainStack = new StackPanel { Margin = new Thickness(10) };

            // ═══ Header ═══
            mainStack.Children.Add(SdPaletteStyles.CreateHeader("SHOPDRAWING APP"));

            // ═══ Wall Tools ═══
            var wallSection = new StackPanel();
            wallSection.Children.Add(SdPaletteStyles.CreateSectionHeader("WALL TOOLS"));
            wallSection.Children.Add(CreateCommandButton("🏗️ Tạo Tường Mới", "_SD_WALL_QUICK", SdPaletteStyles.AccentBlueBrush));
            mainStack.Children.Add(SdPaletteStyles.CreateSectionBorder(wallSection));

            // ═══ Manage ═══
            var manageSection = new StackPanel();
            manageSection.Children.Add(SdPaletteStyles.CreateSectionHeader("QUẢN LÝ"));
            manageSection.Children.Add(CreateActionButton("⚙️ Quản lý Spec", () =>
            {
                var cmd = new ShopDrawingCommands();
                cmd.ManageSpecs();
            }));
            manageSection.Children.Add(CreateActionButton("📊 Quản Lý Khối Lượng", () =>
            {
                var cmd = new ShopDrawingCommands();
                cmd.ShowWaste();
            }, SdPaletteStyles.AccentBlueBrush));
            manageSection.Children.Add(CreateActionButton("📥 Xuất BOM Excel", () =>
            {
                var cmd = new ShopDrawingCommands();
                cmd.ExportBom();
            }, SdPaletteStyles.AccentGreenBrush));
            mainStack.Children.Add(SdPaletteStyles.CreateSectionBorder(manageSection));

            // ═══ Live Status ═══
            var statusSection = new StackPanel();
            statusSection.Children.Add(SdPaletteStyles.CreateSectionHeader("TRẠNG THÁI"));
            
            _lblTotalPanels = new Label
            {
                Content = "● Đang thiết kế: -",
                FontWeight = FontWeights.SemiBold,
                FontFamily = SdPaletteStyles.Font,
                Foreground = SdPaletteStyles.AccentGreenBrush,
                Padding = new Thickness(0, 2, 0, 2)
            };
            statusSection.Children.Add(_lblTotalPanels);
            
            _lblTotalWaste = new Label
            {
                Content = "● Kho lẻ: -",
                FontWeight = FontWeights.SemiBold,
                FontFamily = SdPaletteStyles.Font,
                Foreground = SdPaletteStyles.AccentOrangeBrush,
                Padding = new Thickness(0, 2, 0, 2)
            };
            statusSection.Children.Add(_lblTotalWaste);

            _lblWastePercent = new Label
            {
                Content = "● Hao phí: - %",
                FontWeight = FontWeights.Bold,
                FontFamily = SdPaletteStyles.Font,
                Foreground = SdPaletteStyles.AccentRedBrush,
                Padding = new Thickness(0, 2, 0, 2)
            };
            statusSection.Children.Add(_lblWastePercent);

            var btnReload = SdPaletteStyles.CreateOutlineButton("🔄 Cập nhật UI");
            btnReload.Click += (s, e) => UpdateStatus();
            statusSection.Children.Add(btnReload);
            mainStack.Children.Add(SdPaletteStyles.CreateSectionBorder(statusSection));

            // ═══ Quick Settings ═══
            var settingsSection = new StackPanel();
            settingsSection.Children.Add(SdPaletteStyles.CreateSectionHeader("CÀI ĐẶT NHANH"));

            // Spec
            var stackSpec = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 5) };
            stackSpec.Children.Add(MakeLabel("Spec:", 80));
            var cboSpec = new ComboBox { Width = 100, DisplayMemberPath = "Key", SelectedValuePath = "Key" };
            cboSpec.ItemsSource = ShopDrawingCommands.SpecManager.GetAll();
            if (ShopDrawingCommands.SpecManager.GetAll().Count > 0) cboSpec.SelectedIndex = 0;
            stackSpec.Children.Add(cboSpec);
            settingsSection.Children.Add(stackSpec);

            // Độ dày
            var stackThick = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 5) };
            stackThick.Children.Add(MakeLabel("Độ dày (mm):", 80));
            _lblThickness = new TextBlock
            {
                Width = 100, Text = ShopDrawingCommands.DefaultThickness.ToString(),
                VerticalAlignment = VerticalAlignment.Center, Foreground = SdPaletteStyles.TextPrimaryBrush,
                FontFamily = SdPaletteStyles.Font, Padding = new Thickness(3, 0, 0, 0)
            };
            stackThick.Children.Add(_lblThickness);
            settingsSection.Children.Add(stackThick);

            // Rộng tấm
            var stackPW = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 5) };
            stackPW.Children.Add(MakeLabel("Rộng tấm:", 80));
            _lblPanelWidth = new TextBlock
            {
                Width = 100, Text = ShopDrawingCommands.DefaultPanelWidth.ToString(),
                VerticalAlignment = VerticalAlignment.Center, Foreground = SdPaletteStyles.TextPrimaryBrush,
                FontFamily = SdPaletteStyles.Font, Padding = new Thickness(3, 0, 0, 0)
            };
            stackPW.Children.Add(_lblPanelWidth);
            settingsSection.Children.Add(stackPW);

            // Spec SelectionChanged
            cboSpec.SelectionChanged += (s, e) =>
            {
                if (cboSpec.SelectedItem is PanelSpec spec)
                {
                    ShopDrawingCommands.DefaultSpec = spec.Key;
                    if (spec.Thickness > 0) { _lblThickness.Text = spec.Thickness.ToString(); ShopDrawingCommands.DefaultThickness = spec.Thickness; }
                    if (spec.PanelWidth > 0) { _lblPanelWidth.Text = spec.PanelWidth.ToString(); ShopDrawingCommands.DefaultPanelWidth = spec.PanelWidth; }
                }
            };
            if (cboSpec.SelectedItem is PanelSpec initSpec)
            {
                ShopDrawingCommands.DefaultSpec = initSpec.Key;
                if (initSpec.Thickness > 0) { _lblThickness.Text = initSpec.Thickness.ToString(); ShopDrawingCommands.DefaultThickness = initSpec.Thickness; }
                if (initSpec.PanelWidth > 0) { _lblPanelWidth.Text = initSpec.PanelWidth.ToString(); ShopDrawingCommands.DefaultPanelWidth = initSpec.PanelWidth; }
            }

            // Cao chữ
            var stackTH = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 5) };
            stackTH.Children.Add(MakeLabel("Cao chữ:", 80));
            var txtTextH = new TextBox
            {
                Width = 50, Text = ShopDrawingCommands.DefaultTextHeightMm.ToString("F1"),
                VerticalContentAlignment = VerticalAlignment.Center,
                ToolTip = "Chiều cao chữ trên giấy (mm)"
            };
            txtTextH.LostFocus += (s, e) =>
            {
                if (double.TryParse(txtTextH.Text.Replace(",", "."),
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out double h) && h > 0)
                {
                    ShopDrawingCommands.DefaultTextHeightMm = h;
                    txtTextH.ClearValue(TextBox.BackgroundProperty);
                    new ShopDrawingCommands().UpdateAllPluginText();
                }
                else
                {
                    txtTextH.Background = new SolidColorBrush(Color.FromRgb(100, 40, 40));
                    txtTextH.ToolTip = "❌ Nhập số dương, VD: 1.8";
                }
            };
            var btnUpdateText = SdPaletteStyles.CreateCompactButton("🔄 Cập nhật Text");
            btnUpdateText.Click += (s, e) =>
            {
                if (double.TryParse(txtTextH.Text.Replace(",", "."),
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out double h) && h > 0)
                    ShopDrawingCommands.DefaultTextHeightMm = h;

                new ShopDrawingCommands().UpdateAllPluginText();
            };
            stackTH.Children.Add(txtTextH);
            stackTH.Children.Add(btnUpdateText);
            settingsSection.Children.Add(stackTH);

            // Scale
            var stackScale = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 3) };
            stackScale.Children.Add(MakeLabel("Tỷ lệ:", 80));
            string initScale = ShopDrawingCommands.DefaultAnnotationScales.Split(',')[0].Trim();
            _lblScale = new TextBlock
            {
                Width = 100, Text = initScale, VerticalAlignment = VerticalAlignment.Center,
                Foreground = SdPaletteStyles.TextPrimaryBrush, FontFamily = SdPaletteStyles.Font, Padding = new Thickness(3, 0, 0, 0)
            };
            stackScale.Children.Add(_lblScale);
            settingsSection.Children.Add(stackScale);

            ShopDrawingCommands.AnnotationScaleChanged += scaleName =>
            {
                _lblScale.Dispatcher.Invoke(() => _lblScale.Text = scaleName);
            };

            Core.SpecConfigManager.SpecsChanged += () =>
            {
                cboSpec.Dispatcher.Invoke(() =>
                {
                    var specs = ShopDrawingCommands.SpecManager.GetAll();
                    cboSpec.ItemsSource = null;
                    cboSpec.ItemsSource = specs;
                    if (specs.Count > 0) cboSpec.SelectedIndex = 0;
                });
            };

            // Hướng
            var stackDir = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 5) };
            stackDir.Children.Add(MakeLabel("Hướng:", 80));
            var cboDir = new ComboBox { Width = 100 };
            cboDir.Items.Add("Ngang"); cboDir.Items.Add("Dọc");
            cboDir.SelectedIndex = 0;
            cboDir.SelectionChanged += (s, e) => ShopDrawingCommands.DefaultDirection = cboDir.SelectedIndex == 0 ? LayoutDirection.Horizontal : LayoutDirection.Vertical;
            stackDir.Children.Add(cboDir);
            settingsSection.Children.Add(stackDir);

            // Chiều
            var stackEdge = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 5) };
            stackEdge.Children.Add(MakeLabel("Chiều:", 80));
            var cboEdge = new ComboBox { Width = 100 };
            cboEdge.Items.Add("Từ trái qua");
            cboEdge.Items.Add("Từ phải qua");
            cboEdge.SelectedIndex = 0;
            cboEdge.SelectionChanged += (s, e) => ShopDrawingCommands.DefaultStartEdge = cboEdge.SelectedIndex == 0 ? StartEdge.Left : StartEdge.Right;
            stackEdge.Children.Add(cboEdge);
            settingsSection.Children.Add(stackEdge);

            // Khe hở
            var stackGap = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 5) };
            stackGap.Children.Add(MakeLabel("Khe hở (mm):", 80));
            var txtGap = new TextBox
            {
                Width = 50, Text = ShopDrawingCommands.DefaultJointGap.ToString("F0"),
                VerticalContentAlignment = VerticalAlignment.Center
            };
            txtGap.LostFocus += (s, e) =>
            {
                if (double.TryParse(txtGap.Text.Replace(",", "."),
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out double g) && g >= 0)
                {
                    ShopDrawingCommands.DefaultJointGap = g;
                    txtGap.ClearValue(TextBox.BackgroundProperty);
                }
                else
                {
                    txtGap.Background = new SolidColorBrush(Color.FromRgb(100, 40, 40));
                }
            };
            stackGap.Children.Add(txtGap);
            settingsSection.Children.Add(stackGap);

            // Opening toggle
            var chkOpening = new CheckBox
            {
                Content = "Cắt lỗ (Openings)?",
                IsChecked = ShopDrawingCommands.EnableOpeningCut,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = SdPaletteStyles.TextPrimaryBrush,
                FontFamily = SdPaletteStyles.Font,
                Margin = new Thickness(0, 4, 0, 0)
            };
            chkOpening.Checked += (s, e) => ShopDrawingCommands.EnableOpeningCut = true;
            chkOpening.Unchecked += (s, e) => ShopDrawingCommands.EnableOpeningCut = false;
            settingsSection.Children.Add(chkOpening);
            mainStack.Children.Add(SdPaletteStyles.CreateSectionBorder(settingsSection));

            // ═══ Chi tiết phụ kiện ═══
            var detailSection = new StackPanel();
            detailSection.Children.Add(SdPaletteStyles.CreateSectionHeader("CHI TIẾT PHỤ KIỆN"));

            var comboDetailType = new ComboBox { Margin = new Thickness(0, 0, 0, 5) };
            comboDetailType.ItemsSource = Enum.GetValues(typeof(DetailType));
            comboDetailType.SelectedItem = ShopDrawingCommands.CurrentDetailType;
            comboDetailType.SelectionChanged += (s, e) =>
            {
                if (comboDetailType.SelectedItem is DetailType dt)
                    ShopDrawingCommands.CurrentDetailType = dt;
            };
            detailSection.Children.Add(comboDetailType);
            detailSection.Children.Add(CreateCommandButton("📐 Chèn Detail vào Polyline", "_SD_DETAIL", new SolidColorBrush(Color.FromRgb(149, 117, 205))));
            mainStack.Children.Add(SdPaletteStyles.CreateSectionBorder(detailSection));

            Content = SdPaletteStyles.WrapInScrollViewer(mainStack);
            Background = SdPaletteStyles.BgPrimaryBrush;
            Loaded += (s, e) => UpdateStatus();
        }

        public void UpdateStatus()
        {
            try
            {
                var doc = Autodesk.AutoCAD.ApplicationServices.Core.Application.DocumentManager.MdiActiveDocument;
                double wasteArea = 0;
                if (ShopDrawingCommands.WasteRepo != null)
                {
                    try
                    {
                        var calc = new WasteCalculator();
                        var stats = calc.CalculateLiveStats(doc, ShopDrawingCommands.WasteRepo);
                        wasteArea = stats.TotalWasteAreaMm2;
                        _lblWastePercent.Content = $"● Hao phí: {stats.WastePercentage:F1} %";
                    }
                    catch { _lblWastePercent.Content = "● Hao phí: - %"; }
                }
                else { _lblWastePercent.Content = "● Waste DB chưa sẵn sàng"; }

                if (doc != null)
                {
                    try
                    {
                        int panelCount = 0;
                        using (var tr = doc.Database.TransactionManager.StartTransaction())
                        {
                            var bt = (BlockTable)tr.GetObject(doc.Database.BlockTableId, OpenMode.ForRead);
                            var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);
                            foreach (ObjectId id in ms)
                            {
                                var ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                                if (ent != null && ent.Layer == "SD_PANEL" && ent is Polyline)
                                    panelCount++;
                            }
                            tr.Commit();
                        }
                        _lblTotalPanels.Content = $"● Đang thiết kế: {panelCount} tấm";
                    }
                    catch { _lblTotalPanels.Content = "● Đang thiết kế: -"; }
                }

                if (ShopDrawingCommands.WasteRepo != null)
                {
                    try
                    {
                        var wastes = ShopDrawingCommands.WasteRepo.GetAll("available");
                        _lblTotalWaste.Content = $"● Kho lẻ: {wastes.Count} tấm ({wasteArea / 1_000_000.0:F2} m²)";
                    }
                    catch { _lblTotalWaste.Content = "● Kho lẻ: Lỗi DB"; }
                }
                else { _lblTotalWaste.Content = "● Kho lẻ: DB chưa sẵn sàng"; }
            }
            catch { }
        }

        private static TextBlock MakeLabel(string text, double width = 0)
        {
            var lbl = SdPaletteStyles.CreateLabel(text);
            lbl.VerticalAlignment = VerticalAlignment.Center;
            if (width > 0) lbl.Width = width;
            return lbl;
        }

        private Button CreateCommandButton(string text, string cmdName, Brush bg)
        {
            var btn = SdPaletteStyles.CreateActionButton(text, bg);
            btn.Click += (s, e) =>
            {
                var doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
                doc?.SendStringToExecute(cmdName + " ", true, false, false);
            };
            return btn;
        }

        private Button CreateActionButton(string text, Action action, Brush? bg = null)
        {
            var btn = SdPaletteStyles.CreateActionButton(text, bg ?? SdPaletteStyles.BtnDefaultBrush);
            btn.Click += (s, e) =>
            {
                try { action(); }
                catch (Exception ex)
                {
                    MessageBox.Show($"Lỗi: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            };
            return btn;
        }
    }
}
