using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ShopDrawing.Plugin.Core;
using ShopDrawing.Plugin.Models;
using ShopDrawing.Plugin.Modules.Panel;

namespace ShopDrawing.Plugin.UI
{
    public class MainPaletteControl : UserControl
    {
        private enum DrawingMode
        {
            Wall,
            Ceiling
        }

        private readonly ShopdrawingPaletteFacade _paletteFacade = new();
        private readonly List<UIElement> _wallModeElements = new();
        private readonly List<UIElement> _ceilingModeElements = new();
        private bool _subscriptionsAttached;
        private DrawingMode _drawingMode = DrawingMode.Wall;
        private TextBlock _lblTotalPanels = null!;
        private TextBlock _lblTotalWaste = null!;
        private TextBlock _lblWastePercent = null!;
        private TextBlock _lblThickness = null!;
        private TextBlock _lblPanelWidth = null!;
        private TextBlock _lblScale = null!;
        private ComboBox? _cboSpec;

        public MainPaletteControl()
        {
            var initialSettings = _paletteFacade.GetSettingsSnapshot();
            var mainStack = new StackPanel { Margin = new Thickness(10) };

            mainStack.Children.Add(SdPaletteStyles.CreateHeader("SHOPDRAWING APP"));
            mainStack.Children.Add(BuildQuickSettingsSection(initialSettings));

            var wallSection = new StackPanel();
            wallSection.Children.Add(SdPaletteStyles.CreateSectionHeader("CÔNG CỤ TƯỜNG"));
            wallSection.Children.Add(CreateCommandButton("Tạo tường mới", "_SD_WALL_QUICK", SdPaletteStyles.AccentBlueBrush));
            wallSection.Children.Add(CreateCommandButton("Pick góc ngoài", "_SD_PICK_OUTSIDE_CORNER", SdPaletteStyles.AccentOrangeBrush));
            wallSection.Children.Add(CreateCommandButton("Pick góc trong", "_SD_PICK_INSIDE_CORNER", SdPaletteStyles.AccentGreenBrush));
            mainStack.Children.Add(SdPaletteStyles.CreateSectionBorder(wallSection));

            var ceilingSection = new StackPanel();
            ceilingSection.Children.Add(SdPaletteStyles.CreateSectionHeader("CÔNG CỤ TRẦN"));
            ceilingSection.Children.Add(CreateCommandButton("Tạo trần mới", "_SD_CEILING_QUICK", SdPaletteStyles.AccentGreenBrush));
            ceilingSection.Children.Add(CreateCommandButton("Pick điểm treo T", "_SD_PICK_T_HANGER", SdPaletteStyles.AccentBlueBrush));
            ceilingSection.Children.Add(CreateCommandButton("Pick điểm treo bulong", "_SD_PICK_MUSHROOM_HANGER", SdPaletteStyles.AccentOrangeBrush));
            mainStack.Children.Add(SdPaletteStyles.CreateSectionBorder(ceilingSection));

            var manageSection = new StackPanel();
            manageSection.Children.Add(SdPaletteStyles.CreateSectionHeader("QUẢN LÝ"));
            manageSection.Children.Add(CreateActionButton("Quản lý spec", () => _paletteFacade.ManageSpecs()));
            manageSection.Children.Add(CreateActionButton("Quản lý khối lượng", () => _paletteFacade.ShowWaste(), SdPaletteStyles.AccentBlueBrush));
            manageSection.Children.Add(CreateActionButton("Xuất BOM Excel", () => _paletteFacade.ExportBom(), SdPaletteStyles.AccentGreenBrush));
            mainStack.Children.Add(SdPaletteStyles.CreateSectionBorder(manageSection));

            var statusSection = new StackPanel();
            statusSection.Children.Add(SdPaletteStyles.CreateSectionHeader("TRẠNG THÁI"));

            _lblTotalPanels = new TextBlock
            {
                Text = "- Đang thiết kế: -",
                FontWeight = FontWeights.SemiBold,
                FontFamily = SdPaletteStyles.Font,
                FontSize = SdPaletteStyles.FontSizeNormal,
                Foreground = SdPaletteStyles.AccentGreenBrush,
                Margin = new Thickness(0, 2, 0, 2)
            };
            statusSection.Children.Add(_lblTotalPanels);

            _lblTotalWaste = new TextBlock
            {
                Text = "- Kho lẻ: -",
                FontWeight = FontWeights.SemiBold,
                FontFamily = SdPaletteStyles.Font,
                FontSize = SdPaletteStyles.FontSizeNormal,
                Foreground = SdPaletteStyles.AccentOrangeBrush,
                Margin = new Thickness(0, 2, 0, 2)
            };
            statusSection.Children.Add(_lblTotalWaste);

            _lblWastePercent = new TextBlock
            {
                Text = "- Hao hụt: - %",
                FontWeight = FontWeights.Bold,
                FontFamily = SdPaletteStyles.Font,
                FontSize = SdPaletteStyles.FontSizeNormal,
                Foreground = SdPaletteStyles.AccentRedBrush,
                Margin = new Thickness(0, 2, 0, 2)
            };
            statusSection.Children.Add(_lblWastePercent);

            var btnReload = SdPaletteStyles.CreateOutlineButton("Cập nhật UI");
            btnReload.Click += (_, _) => UpdateStatus();
            statusSection.Children.Add(btnReload);
            mainStack.Children.Add(SdPaletteStyles.CreateSectionBorder(statusSection));

            var detailSection = new StackPanel();
            detailSection.Children.Add(SdPaletteStyles.CreateSectionHeader("CHI TIẾT PHỤ KIỆN"));

            var comboDetailType = new ComboBox { Margin = new Thickness(0, 0, 0, 5) };
            comboDetailType.ItemsSource = Enum.GetValues(typeof(DetailType));
            comboDetailType.SelectedItem = initialSettings.CurrentDetailType;
            comboDetailType.SelectionChanged += (_, _) =>
            {
                if (comboDetailType.SelectedItem is DetailType detailType)
                {
                    _paletteFacade.SetCurrentDetailType(detailType);
                }
            };
            detailSection.Children.Add(comboDetailType);
            detailSection.Children.Add(CreateCommandButton("Chèn detail vào polyline", "_SD_DETAIL", new SolidColorBrush(Color.FromRgb(149, 117, 205))));
            mainStack.Children.Add(SdPaletteStyles.CreateSectionBorder(detailSection));

            Content = SdPaletteStyles.WrapInScrollViewer(mainStack);
            Background = SdPaletteStyles.BgPrimaryBrush;
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private UIElement BuildQuickSettingsSection(ShopdrawingPaletteSettingsSnapshot initialSettings)
        {
            var settingsSection = new StackPanel();
            settingsSection.Children.Add(SdPaletteStyles.CreateSectionHeader("CÀI ĐẶT NHANH"));

            var rowMode = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
            rowMode.Children.Add(MakeLabel("Chế độ vẽ:", 80));
            var cboMode = new ComboBox { Width = 120 };
            cboMode.Items.Add("Vẽ vách");
            cboMode.Items.Add("Vẽ trần");
            cboMode.SelectedIndex = 0;
            cboMode.SelectionChanged += (_, _) =>
            {
                _drawingMode = cboMode.SelectedIndex == 1 ? DrawingMode.Ceiling : DrawingMode.Wall;
                ApplyDrawingModeUi();
            };
            rowMode.Children.Add(cboMode);
            settingsSection.Children.Add(rowMode);

            settingsSection.Children.Add(new TextBlock
            {
                Text = "Tham số đúng ngữ cảnh sẽ sáng để nhập. Tham số không dùng cho lệnh hiện tại sẽ được làm tối.",
                TextWrapping = TextWrapping.Wrap,
                Foreground = SdPaletteStyles.TextMutedBrush,
                FontSize = SdPaletteStyles.FontSizeSmall,
                FontFamily = SdPaletteStyles.Font,
                Margin = new Thickness(0, 0, 0, 8)
            });

            var stackSpec = CreateSettingRow("Mã spec:", 80);
            var cboSpec = new ComboBox { Width = 100, DisplayMemberPath = "Key", SelectedValuePath = "Key" };
            _cboSpec = cboSpec;
            var specs = _paletteFacade.GetSpecs();
            cboSpec.ItemsSource = specs;
            if (specs.Count > 0 && !string.IsNullOrWhiteSpace(initialSettings.DefaultSpecKey))
            {
                cboSpec.SelectedValue = initialSettings.DefaultSpecKey;
            }

            if (cboSpec.SelectedItem == null && specs.Count > 0)
            {
                cboSpec.SelectedIndex = 0;
            }

            stackSpec.Children.Add(cboSpec);
            settingsSection.Children.Add(stackSpec);

            var stackApplication = CreateSettingRow("Ứng dụng:", 80);
            var cboApplication = new ComboBox { Width = 100, ItemsSource = TenderWall.ApplicationOptions };
            cboApplication.SelectedItem = initialSettings.DefaultApplication;
            if (cboApplication.SelectedItem == null)
            {
                cboApplication.SelectedIndex = 0;
            }

            cboApplication.SelectionChanged += (_, _) =>
            {
                if (cboApplication.SelectedItem is string application)
                {
                    _paletteFacade.SetApplication(application);
                }
            };
            stackApplication.Children.Add(cboApplication);
            settingsSection.Children.Add(stackApplication);

            var stackTopTreatment = CreateSettingRow("Đỉnh vách:", 80);
            var cboTopTreatment = new ComboBox { Width = 120, ItemsSource = TenderWall.TopPanelTreatmentOptions };
            cboTopTreatment.SelectedItem = initialSettings.DefaultWallTopPanelTreatment;
            cboTopTreatment.SelectionChanged += (_, _) =>
            {
                if (cboTopTreatment.SelectedItem is string treatment)
                {
                    _paletteFacade.SetWallTopTreatment(treatment);
                }
            };
            stackTopTreatment.Children.Add(cboTopTreatment);
            settingsSection.Children.Add(RegisterWallModeElement(stackTopTreatment));

            var stackStartTreatment = CreateSettingRow("Đầu trái:", 80);
            var cboStartTreatment = new ComboBox { Width = 120, ItemsSource = TenderWall.EndPanelTreatmentOptions };
            cboStartTreatment.SelectedItem = initialSettings.DefaultWallStartPanelTreatment;
            cboStartTreatment.SelectionChanged += (_, _) =>
            {
                if (cboStartTreatment.SelectedItem is string treatment)
                {
                    _paletteFacade.SetWallStartTreatment(treatment);
                }
            };
            stackStartTreatment.Children.Add(cboStartTreatment);
            settingsSection.Children.Add(RegisterWallModeElement(stackStartTreatment));

            var stackEndTreatment = CreateSettingRow("Đầu phải:", 80);
            var cboEndTreatment = new ComboBox { Width = 120, ItemsSource = TenderWall.EndPanelTreatmentOptions };
            cboEndTreatment.SelectedItem = initialSettings.DefaultWallEndPanelTreatment;
            cboEndTreatment.SelectionChanged += (_, _) =>
            {
                if (cboEndTreatment.SelectedItem is string treatment)
                {
                    _paletteFacade.SetWallEndTreatment(treatment);
                }
            };
            stackEndTreatment.Children.Add(cboEndTreatment);
            settingsSection.Children.Add(RegisterWallModeElement(stackEndTreatment));

            var chkBottomEdge = new CheckBox
            {
                Content = "Tính xử lý chân vách",
                IsChecked = initialSettings.DefaultWallBottomEdgeEnabled,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = SdPaletteStyles.TextPrimaryBrush,
                FontFamily = SdPaletteStyles.Font,
                Margin = new Thickness(0, 0, 0, 5)
            };
            chkBottomEdge.Checked += (_, _) => _paletteFacade.SetWallBottomEdgeEnabled(true);
            chkBottomEdge.Unchecked += (_, _) => _paletteFacade.SetWallBottomEdgeEnabled(false);
            settingsSection.Children.Add(RegisterWallModeElement(chkBottomEdge));

            var stackOpeningType = CreateSettingRow("Loại lỗ:", 80);
            var cboOpeningType = new ComboBox { Width = 120 };
            cboOpeningType.Items.Add("Cửa đi");
            cboOpeningType.Items.Add("Cửa sổ/LKT");
            cboOpeningType.SelectedItem = initialSettings.DefaultOpeningType;
            if (cboOpeningType.SelectedItem == null)
            {
                cboOpeningType.SelectedIndex = 1;
            }

            cboOpeningType.SelectionChanged += (_, _) =>
            {
                if (cboOpeningType.SelectedItem is string openingType)
                {
                    _paletteFacade.SetOpeningType(openingType);
                }
            };
            stackOpeningType.Children.Add(cboOpeningType);
            settingsSection.Children.Add(RegisterWallModeElement(stackOpeningType));

            var stackThick = CreateSettingRow("Độ dày (mm):", 80);
            _lblThickness = new TextBlock
            {
                Width = 100,
                Text = initialSettings.DefaultThickness.ToString(),
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = SdPaletteStyles.TextPrimaryBrush,
                FontFamily = SdPaletteStyles.Font,
                Padding = new Thickness(3, 0, 0, 0)
            };
            stackThick.Children.Add(_lblThickness);
            settingsSection.Children.Add(stackThick);

            var stackPanelWidth = CreateSettingRow("Rộng tấm:", 80);
            _lblPanelWidth = new TextBlock
            {
                Width = 100,
                Text = initialSettings.DefaultPanelWidth.ToString(),
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = SdPaletteStyles.TextPrimaryBrush,
                FontFamily = SdPaletteStyles.Font,
                Padding = new Thickness(3, 0, 0, 0)
            };
            stackPanelWidth.Children.Add(_lblPanelWidth);
            settingsSection.Children.Add(stackPanelWidth);

            cboSpec.SelectionChanged += (_, _) =>
            {
                if (cboSpec.SelectedItem is not PanelSpec spec)
                {
                    return;
                }

                var appliedSpec = _paletteFacade.ApplySpec(spec);
                _lblThickness.Text = appliedSpec.Thickness.ToString();
                _lblPanelWidth.Text = appliedSpec.PanelWidth.ToString();
            };

            if (cboSpec.SelectedItem is PanelSpec initialSpec)
            {
                var appliedSpec = _paletteFacade.ApplySpec(initialSpec);
                _lblThickness.Text = appliedSpec.Thickness.ToString();
                _lblPanelWidth.Text = appliedSpec.PanelWidth.ToString();
            }

            var stackTextHeight = CreateSettingRow("Cao chữ:", 80);
            var txtTextHeight = new TextBox
            {
                Width = 50,
                Text = initialSettings.DefaultTextHeightMm.ToString("F1"),
                VerticalContentAlignment = VerticalAlignment.Center,
                ToolTip = "Chiều cao chữ trên giấy (mm)"
            };
            txtTextHeight.LostFocus += (_, _) =>
            {
                if (_paletteFacade.TrySetTextHeight(txtTextHeight.Text))
                {
                    txtTextHeight.ClearValue(BackgroundProperty);
                    txtTextHeight.ToolTip = "Chiều cao chữ trên giấy (mm)";
                    _paletteFacade.UpdateAllPluginText();
                    return;
                }

                txtTextHeight.Background = new SolidColorBrush(Color.FromRgb(100, 40, 40));
                txtTextHeight.ToolTip = "Nhập số dương, ví dụ: 1.8";
            };

            var btnUpdateText = SdPaletteStyles.CreateCompactButton("Cập nhật text");
            btnUpdateText.Click += (_, _) =>
            {
                if (_paletteFacade.TrySetTextHeight(txtTextHeight.Text))
                {
                    txtTextHeight.ClearValue(BackgroundProperty);
                    txtTextHeight.ToolTip = "Chiều cao chữ trên giấy (mm)";
                }

                _paletteFacade.UpdateAllPluginText();
            };
            stackTextHeight.Children.Add(txtTextHeight);
            stackTextHeight.Children.Add(btnUpdateText);
            settingsSection.Children.Add(stackTextHeight);

            var stackScale = CreateSettingRow("Tỷ lệ:", 80);
            _lblScale = new TextBlock
            {
                Width = 100,
                Text = initialSettings.DefaultAnnotationScaleLabel,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = SdPaletteStyles.TextPrimaryBrush,
                FontFamily = SdPaletteStyles.Font,
                Padding = new Thickness(3, 0, 0, 0)
            };
            stackScale.Children.Add(_lblScale);
            settingsSection.Children.Add(stackScale);

            var stackDirection = CreateSettingRow("Hướng:", 80);
            var cboDirection = new ComboBox { Width = 100 };
            cboDirection.Items.Add("Ngang");
            cboDirection.Items.Add("Dọc");
            cboDirection.SelectedIndex = initialSettings.DefaultDirection == LayoutDirection.Horizontal ? 0 : 1;
            cboDirection.SelectionChanged += (_, _) => _paletteFacade.SetDirectionByIndex(cboDirection.SelectedIndex);
            stackDirection.Children.Add(cboDirection);
            settingsSection.Children.Add(RegisterWallModeElement(stackDirection));

            var stackEdge = CreateSettingRow("Chiều:", 80);
            var cboEdge = new ComboBox { Width = 100 };
            cboEdge.Items.Add("Từ trái qua");
            cboEdge.Items.Add("Từ phải qua");
            cboEdge.SelectedIndex = initialSettings.DefaultStartEdge == StartEdge.Left ? 0 : 1;
            cboEdge.SelectionChanged += (_, _) => _paletteFacade.SetStartEdgeByIndex(cboEdge.SelectedIndex);
            stackEdge.Children.Add(cboEdge);
            settingsSection.Children.Add(RegisterWallModeElement(stackEdge));

            var stackGap = CreateSettingRow("Khe hở (mm):", 80);
            var txtGap = new TextBox
            {
                Width = 50,
                Text = initialSettings.DefaultJointGap.ToString("F0"),
                VerticalContentAlignment = VerticalAlignment.Center
            };
            txtGap.LostFocus += (_, _) =>
            {
                if (_paletteFacade.TrySetJointGap(txtGap.Text))
                {
                    txtGap.ClearValue(BackgroundProperty);
                    txtGap.ToolTip = "Khe hở giữa các tấm (mm)";
                    return;
                }

                txtGap.Background = new SolidColorBrush(Color.FromRgb(100, 40, 40));
                txtGap.ToolTip = "Nhập số lớn hơn hoặc bằng 0";
            };
            stackGap.Children.Add(txtGap);
            settingsSection.Children.Add(stackGap);

            var stackCableDrop = CreateSettingRow("Thả cáp:", 80);
            var txtCableDrop = new TextBox
            {
                Width = 50,
                Text = initialSettings.DefaultCeilingCableDropMm.ToString("F0"),
                VerticalContentAlignment = VerticalAlignment.Center,
                ToolTip = "Chiều dài thả cáp/ty treo (mm)"
            };
            txtCableDrop.LostFocus += (_, _) =>
            {
                if (_paletteFacade.TrySetCeilingCableDrop(txtCableDrop.Text))
                {
                    txtCableDrop.ClearValue(BackgroundProperty);
                    txtCableDrop.ToolTip = "Chiều dài thả cáp/ty treo (mm)";
                    return;
                }

                txtCableDrop.Background = new SolidColorBrush(Color.FromRgb(100, 40, 40));
                txtCableDrop.ToolTip = "Nhập số lớn hơn hoặc bằng 0";
            };
            stackCableDrop.Children.Add(txtCableDrop);
            settingsSection.Children.Add(RegisterCeilingModeElement(stackCableDrop));

            var chkOpening = new CheckBox
            {
                Content = "Cắt lỗ (Openings)?",
                IsChecked = initialSettings.EnableOpeningCut,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = SdPaletteStyles.TextPrimaryBrush,
                FontFamily = SdPaletteStyles.Font,
                Margin = new Thickness(0, 4, 0, 0)
            };
            chkOpening.Checked += (_, _) => _paletteFacade.SetOpeningCut(true);
            chkOpening.Unchecked += (_, _) => _paletteFacade.SetOpeningCut(false);
            settingsSection.Children.Add(chkOpening);

            ApplyDrawingModeUi();
            return SdPaletteStyles.CreateSectionBorder(settingsSection);
        }

        private static StackPanel CreateSettingRow(string label, double width)
        {
            var row = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 0, 0, 5)
            };
            row.Children.Add(MakeLabel(label, width));
            return row;
        }

        private UIElement RegisterWallModeElement(UIElement element)
        {
            _wallModeElements.Add(element);
            return element;
        }

        private UIElement RegisterCeilingModeElement(UIElement element)
        {
            _ceilingModeElements.Add(element);
            return element;
        }

        private void ApplyDrawingModeUi()
        {
            bool wallMode = _drawingMode == DrawingMode.Wall;
            ApplyModeState(_wallModeElements, wallMode);
            ApplyModeState(_ceilingModeElements, !wallMode);
        }

        private static void ApplyModeState(IEnumerable<UIElement> elements, bool enabled)
        {
            foreach (var element in elements)
            {
                element.IsEnabled = enabled;
                element.Opacity = enabled ? 1.0 : 0.45;
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            AttachRuntimeSubscriptions();
            UiText.NormalizeTree(this);
            ApplyDrawingModeUi();
            UpdateStatus();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            DetachRuntimeSubscriptions();
        }

        private void AttachRuntimeSubscriptions()
        {
            if (_subscriptionsAttached)
            {
                return;
            }

            _paletteFacade.AnnotationScaleChanged += OnAnnotationScaleChanged;
            SpecConfigManager.SpecsChanged += OnSpecsChanged;
            _subscriptionsAttached = true;
        }

        private void DetachRuntimeSubscriptions()
        {
            if (!_subscriptionsAttached)
            {
                return;
            }

            _paletteFacade.AnnotationScaleChanged -= OnAnnotationScaleChanged;
            SpecConfigManager.SpecsChanged -= OnSpecsChanged;
            _subscriptionsAttached = false;
        }

        private void OnAnnotationScaleChanged(string scaleName)
        {
            _lblScale.Dispatcher.BeginInvoke(new Action(() => _lblScale.Text = scaleName));
        }

        private void OnSpecsChanged()
        {
            if (_cboSpec == null)
            {
                return;
            }

            _cboSpec.Dispatcher.BeginInvoke(new Action(() =>
            {
                var refreshedSpecs = _paletteFacade.GetSpecs();
                var selectedSpecKey = _cboSpec.SelectedValue as string
                    ?? _paletteFacade.DefaultSpec;
                _cboSpec.ItemsSource = null;
                _cboSpec.ItemsSource = refreshedSpecs;
                if (refreshedSpecs.Count > 0 && !string.IsNullOrWhiteSpace(selectedSpecKey))
                {
                    _cboSpec.SelectedValue = selectedSpecKey;
                }

                if (_cboSpec.SelectedItem == null && refreshedSpecs.Count > 0)
                {
                    _cboSpec.SelectedIndex = 0;
                }
            }));
        }

        public void UpdateStatus()
        {
            var snapshot = _paletteFacade.GetStatusSnapshot();
            _lblTotalPanels.Text = snapshot.PanelStatusText;
            _lblTotalWaste.Text = snapshot.WasteStatusText;
            _lblWastePercent.Text = snapshot.WastePercentText;
        }

        private static TextBlock MakeLabel(string text, double width = 0)
        {
            var label = SdPaletteStyles.CreateLabel(text);
            label.VerticalAlignment = VerticalAlignment.Center;
            if (width > 0)
            {
                label.Width = width;
            }

            return label;
        }

        private Button CreateCommandButton(string text, string cmdName, Brush background)
        {
            var button = SdPaletteStyles.CreateActionButton(text, background);
            button.Click += (_, _) =>
            {
                AutoCadUiContext.TrySendCommand(
                    cmdName + " ",
                    notifyWhenMissing: true,
                    missingDocumentMessage: "Chưa có bản vẽ đang mở.",
                    caption: "Thông báo");
            };
            return button;
        }

        private Button CreateActionButton(string text, Action action, Brush? background = null)
        {
            var button = SdPaletteStyles.CreateActionButton(text, background ?? SdPaletteStyles.BtnDefaultBrush);
            button.Click += (_, _) =>
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    UiFeedback.ShowWarning($"Lỗi: {ex.Message}", "Lỗi");
                }
            };
            return button;
        }
    }
}
