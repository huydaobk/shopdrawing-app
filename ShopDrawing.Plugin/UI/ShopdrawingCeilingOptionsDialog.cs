using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Autodesk.AutoCAD.DatabaseServices;
using ShopDrawing.Plugin.Core;
using ShopDrawing.Plugin.Modules.Panel;
using AcPolyline = Autodesk.AutoCAD.DatabaseServices.Polyline;

namespace ShopDrawing.Plugin.UI
{
    internal sealed class ShopdrawingCeilingOptionsDialog : Window
    {
        private readonly AcPolyline _boundary;
        private readonly int _thicknessMm;
        private readonly ComboBox _panelDirectionCombo;
        private readonly ComboBox _panelStartEdgeCombo;
        private readonly ComboBox _suspensionDirectionCombo;
        private readonly ComboBox _suspensionOriginCombo;
        private readonly TextBox _tSpacingTextBox;
        private readonly TextBox _tClearGapTextBox;
        private readonly TextBox _mushroomDivisionTextBox;
        private readonly TextBox _baySpanTextBox;
        private readonly TextBlock _sizeText;
        private readonly TextBlock _previewSummaryText;
        private readonly Canvas _previewCanvas;
        private readonly ListBox _baySpanListBox;
        private readonly Button _toggleMushroomButton;
        private readonly List<double> _baySpansMm = new();
        private readonly List<bool> _bayHasMushroomFlags = new();
        private int _lastMushroomDivisionCount;

        public CeilingDrawingOptions? Result { get; private set; }

        public ShopdrawingCeilingOptionsDialog(
            AcPolyline boundary,
            int thicknessMm,
            LayoutDirection initialPanelDirection,
            StartEdge initialPanelStartEdge,
            LayoutDirection initialSuspensionDirection,
            bool initialDivideFromMaxSide,
            double initialTSpacingMm,
            double initialTClearGapMm,
            int initialMushroomDivisionCount,
            IReadOnlyList<double>? initialBaySpansMm,
            IReadOnlyList<bool>? initialBayHasMushroomFlags)
        {
            _boundary = boundary;
            _thicknessMm = thicknessMm;

            Title = "Cấu hình vẽ trần";
            Width = 980;
            Height = 700;
            MinWidth = 940;
            MinHeight = 680;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            ResizeMode = ResizeMode.CanResizeWithGrip;
            Background = new SolidColorBrush(Color.FromRgb(250, 250, 252));
            FontFamily = new FontFamily("Segoe UI");

            var root = new Grid { Margin = new Thickness(18) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var intro = new TextBlock
            {
                Text = "Chọn hướng chia tấm, mô hình tuyến T và chỉnh trực tiếp từng nhịp ngay trong popup trước khi vẽ.",
                TextWrapping = TextWrapping.Wrap,
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 10)
            };
            Grid.SetRow(intro, 0);
            root.Children.Add(intro);

            _sizeText = new TextBlock
            {
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(90, 90, 90)),
                Margin = new Thickness(0, 0, 0, 12)
            };
            Grid.SetRow(_sizeText, 1);
            root.Children.Add(_sizeText);

            var form = new Grid();
            form.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(190) });
            form.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            for (int i = 0; i < 7; i++)
            {
                form.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            }

            AddLabel(form, "Hướng chia tấm:", 0);
            _panelDirectionCombo = AddCombo(form, 0, "Dọc", "Ngang");
            AddLabel(form, "Chiều đặt tấm:", 1);
            _panelStartEdgeCombo = AddCombo(form, 1);
            AddLabel(form, "Hướng tuyến treo:", 2);
            _suspensionDirectionCombo = AddCombo(form, 2, "Dọc", "Ngang");
            AddLabel(form, "Bắt đầu chia từ:", 3);
            _suspensionOriginCombo = AddCombo(form, 3);
            AddLabel(form, "Nhịp T mặc định (mm):", 4);
            _tSpacingTextBox = AddTextBox(form, 4);
            AddLabel(form, "Khe hở tại T (mm):", 5);
            _tClearGapTextBox = AddTextBox(form, 5);
            AddLabel(form, "n bulông dù:", 6);
            _mushroomDivisionTextBox = AddTextBox(form, 6);
            Grid.SetRow(form, 2);
            root.Children.Add(form);

            var contentGrid = new Grid { Margin = new Thickness(0, 16, 0, 0) };
            contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.45, GridUnitType.Star) });
            contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
            Grid.SetRow(contentGrid, 3);
            root.Children.Add(contentGrid);

            var previewBorder = CreateSectionBorder();
            Grid.SetColumn(previewBorder, 0);
            contentGrid.Children.Add(previewBorder);

            var previewGrid = new Grid();
            previewGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            previewGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(250) });
            previewGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            previewBorder.Child = previewGrid;

            var previewHeader = new TextBlock
            {
                Text = "Preview chiều dài tấm theo tuyến T",
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 10)
            };
            Grid.SetRow(previewHeader, 0);
            previewGrid.Children.Add(previewHeader);

            _previewCanvas = new Canvas
            {
                Height = 250,
                Background = new SolidColorBrush(Color.FromRgb(250, 252, 255))
            };
            Grid.SetRow(_previewCanvas, 1);
            previewGrid.Children.Add(_previewCanvas);

            _previewSummaryText = new TextBlock
            {
                Margin = new Thickness(0, 12, 0, 0),
                TextWrapping = TextWrapping.Wrap,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(44, 62, 80))
            };
            Grid.SetRow(_previewSummaryText, 2);
            previewGrid.Children.Add(_previewSummaryText);

            var editorBorder = CreateSectionBorder();
            Grid.SetColumn(editorBorder, 1);
            contentGrid.Children.Add(editorBorder);

            var editorGrid = new Grid();
            editorGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            editorGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            editorGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            editorGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            editorBorder.Child = editorGrid;

            var editorHeader = new TextBlock
            {
                Text = "Danh sách nhịp T",
                FontSize = 13,
                FontWeight = FontWeights.SemiBold
            };
            Grid.SetRow(editorHeader, 0);
            editorGrid.Children.Add(editorHeader);

            var editorNote = new TextBlock
            {
                Text = "Mỗi nhịp là khoảng cách từ biên bắt đầu chia đến tim T đầu tiên, rồi tiếp tục từ tim T này sang tim T kế tiếp. Giữ Ctrl để chọn nhiều nhịp và sửa cùng lúc.",
                Margin = new Thickness(0, 8, 0, 10),
                TextWrapping = TextWrapping.Wrap,
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(92, 99, 111))
            };
            Grid.SetRow(editorNote, 1);
            editorGrid.Children.Add(editorNote);

            _baySpanListBox = new ListBox
            {
                MinHeight = 220,
                SelectionMode = SelectionMode.Extended,
                BorderBrush = new SolidColorBrush(Color.FromRgb(214, 221, 229)),
                BorderThickness = new Thickness(1)
            };
            _baySpanListBox.SelectionChanged += (_, _) =>
            {
                if (_baySpanTextBox == null)
                {
                    return;
                }

                List<int> selectedIndices = GetSelectedBayIndices();
                if (selectedIndices.Count == 0)
                {
                    return;
                }

                double firstValue = _baySpansMm[selectedIndices[0]];
                bool sameValue = selectedIndices.All(index => Math.Abs(_baySpansMm[index] - firstValue) < 0.001);
                _baySpanTextBox.Text = sameValue
                    ? firstValue.ToString("F0", CultureInfo.InvariantCulture)
                    : string.Empty;
            };
            Grid.SetRow(_baySpanListBox, 2);
            editorGrid.Children.Add(_baySpanListBox);

            var editPanel = new Grid { Margin = new Thickness(0, 12, 0, 0) };
            editPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            editPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            editPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            editPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            editPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            editPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Grid.SetRow(editPanel, 3);
            editorGrid.Children.Add(editPanel);

            _baySpanTextBox = new TextBox
            {
                MinWidth = 120,
                Margin = new Thickness(0, 0, 8, 0),
                VerticalContentAlignment = VerticalAlignment.Center
            };
            _baySpanTextBox.TextChanged += (_, _) => RefreshPreview();
            Grid.SetColumn(_baySpanTextBox, 0);
            editPanel.Children.Add(_baySpanTextBox);

            var addButton = CreateButton("Thêm", Color.FromRgb(46, 134, 193), 76);
            addButton.Click += (_, _) => AddBaySpan();
            Grid.SetColumn(addButton, 1);
            editPanel.Children.Add(addButton);

            var updateButton = CreateButton("Sửa", Color.FromRgb(52, 152, 110), 76);
            updateButton.Margin = new Thickness(8, 0, 0, 0);
            updateButton.Click += (_, _) => UpdateSelectedBaySpan();
            Grid.SetColumn(updateButton, 2);
            editPanel.Children.Add(updateButton);

            var removeButton = CreateButton("Xóa", Color.FromRgb(192, 57, 43), 76);
            removeButton.Margin = new Thickness(8, 0, 0, 0);
            removeButton.Click += (_, _) => RemoveSelectedBaySpan();
            Grid.SetColumn(removeButton, 3);
            editPanel.Children.Add(removeButton);

            var resetButton = CreateButton("Reset", Color.FromRgb(127, 140, 141), 76);
            resetButton.Margin = new Thickness(8, 0, 0, 0);
            resetButton.Click += (_, _) => ResetBaySpansToDefault();
            Grid.SetColumn(resetButton, 4);
            editPanel.Children.Add(resetButton);

            _toggleMushroomButton = CreateButton("Bật/tắt nấm", Color.FromRgb(39, 174, 96), 104);
            _toggleMushroomButton.Margin = new Thickness(8, 0, 0, 0);
            _toggleMushroomButton.Click += (_, _) => ToggleSelectedBayMushroom();
            Grid.SetColumn(_toggleMushroomButton, 5);
            editPanel.Children.Add(_toggleMushroomButton);

            var buttonBar = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 16, 0, 0)
            };
            Grid.SetRow(buttonBar, 4);
            root.Children.Add(buttonBar);

            var btnOk = CreateButton("Vẽ trần", Color.FromRgb(46, 134, 193), 110);
            btnOk.Click += (_, _) => Submit();
            buttonBar.Children.Add(btnOk);

            var btnCancel = CreateButton("Hủy", Color.FromRgb(149, 165, 166), 110);
            btnCancel.Margin = new Thickness(10, 0, 0, 0);
            btnCancel.Click += (_, _) => Close();
            buttonBar.Children.Add(btnCancel);

            Content = root;
            UiText.NormalizeWindow(this);

            _panelDirectionCombo.SelectedIndex = initialPanelDirection == LayoutDirection.Vertical ? 0 : 1;
            _suspensionDirectionCombo.SelectedIndex = initialSuspensionDirection == LayoutDirection.Vertical ? 0 : 1;
            _tSpacingTextBox.Text = initialTSpacingMm.ToString("F0", CultureInfo.InvariantCulture);
            _tClearGapTextBox.Text = initialTClearGapMm.ToString("F0", CultureInfo.InvariantCulture);
            _mushroomDivisionTextBox.Text = initialMushroomDivisionCount.ToString(CultureInfo.InvariantCulture);
            _lastMushroomDivisionCount = Math.Max(0, initialMushroomDivisionCount);

            RefreshPanelStartOptions(initialPanelStartEdge);
            RefreshSuspensionOriginOptions(initialDivideFromMaxSide);
            LoadInitialBaySettings(initialBaySpansMm, initialBayHasMushroomFlags);
            RefreshBaySpanList();
            RefreshPreview();

            _panelDirectionCombo.SelectionChanged += (_, _) =>
            {
                RefreshPanelStartOptions(GetPanelStartEdge());
                RefreshPreview();
            };
            _panelStartEdgeCombo.SelectionChanged += (_, _) => RefreshPreview();
            _suspensionDirectionCombo.SelectionChanged += (_, _) =>
            {
                RefreshSuspensionOriginOptions(GetDivideFromMaxSide());
                AutoGenerateBaySpansFromCurrentInputs();
                RefreshPreview();
            };
            _suspensionOriginCombo.SelectionChanged += (_, _) => RefreshPreview();
            _tSpacingTextBox.TextChanged += (_, _) =>
            {
                AutoGenerateBaySpansFromCurrentInputs();
                RefreshPreview();
            };
            _tClearGapTextBox.TextChanged += (_, _) => RefreshPreview();
            _mushroomDivisionTextBox.TextChanged += (_, _) => HandleMushroomDivisionCountChanged();
            Loaded += (_, _) => RefreshPreview();
            SizeChanged += (_, _) => RefreshPreview();
        }

        private void Submit()
        {
            if (!TryGetTSpacingMm(out double tSpacingMm))
            {
                UiFeedback.ShowWarning("Nhịp T mặc định phải là số dương.", "Lỗi dữ liệu");
                _tSpacingTextBox.Focus();
                return;
            }

            if (!TryGetTClearGapMm(out double tClearGapMm))
            {
                UiFeedback.ShowWarning("Khe hở tại T phải là số >= 0.", "Lỗi dữ liệu");
                _tClearGapTextBox.Focus();
                return;
            }

            if (!TryGetMushroomDivisionCount(out int mushroomDivisionCount))
            {
                UiFeedback.ShowWarning("n bulông dù phải là số nguyên >= 0.", "Lỗi dữ liệu");
                _mushroomDivisionTextBox.Focus();
                return;
            }

            if (!TryBuildPreview(out var preview, out string errorMessage))
            {
                UiFeedback.ShowWarning(errorMessage, "Lỗi dữ liệu");
                return;
            }

            if (preview != null && preview.BaySpansMm.Count > 0 && tClearGapMm >= preview.BaySpansMm.Min() - 1.0)
            {
                UiFeedback.ShowWarning("Khe hở tại T phải nhỏ hơn nhịp T nhỏ nhất.", "Lỗi dữ liệu");
                _tClearGapTextBox.Focus();
                return;
            }

            Result = new CeilingDrawingOptions(
                GetPanelDirection(),
                GetPanelStartEdge(),
                GetSuspensionDirection(),
                GetDivideFromMaxSide(),
                tSpacingMm,
                tClearGapMm,
                mushroomDivisionCount,
                _baySpansMm.ToList(),
                _bayHasMushroomFlags.ToList());

            DialogResult = true;
            Close();
        }

        private void RefreshPanelStartOptions(StartEdge currentValue)
        {
            string firstLabel = GetPanelDirection() == LayoutDirection.Vertical ? "Từ trái qua" : "Từ dưới lên";
            string secondLabel = GetPanelDirection() == LayoutDirection.Vertical ? "Từ phải qua" : "Từ trên xuống";
            SetComboOptions(_panelStartEdgeCombo, currentValue == StartEdge.Left ? 0 : 1, firstLabel, secondLabel);
        }

        private void RefreshSuspensionOriginOptions(bool divideFromMaxSide)
        {
            bool runAlongX = GetSuspensionDirection() == LayoutDirection.Vertical;
            string firstLabel = runAlongX ? "Từ cạnh dưới" : "Từ cạnh trái";
            string secondLabel = runAlongX ? "Từ cạnh trên" : "Từ cạnh phải";
            SetComboOptions(_suspensionOriginCombo, divideFromMaxSide ? 1 : 0, firstLabel, secondLabel);
        }

        private void LoadInitialBaySettings(
            IReadOnlyList<double>? initialBaySpansMm,
            IReadOnlyList<bool>? initialBayHasMushroomFlags)
        {
            var normalizedSpans = CeilingSuspensionPreviewService.NormalizeBaySpans(initialBaySpansMm);
            if (normalizedSpans.Count > 0)
            {
                _baySpansMm.Clear();
                _baySpansMm.AddRange(normalizedSpans);
                _bayHasMushroomFlags.Clear();
                _bayHasMushroomFlags.AddRange(
                    CeilingSuspensionPreviewService.NormalizeBayHasMushroomFlags(initialBayHasMushroomFlags, _baySpansMm.Count));
                SyncMushroomFlagsForDivisionCount(_lastMushroomDivisionCount, _lastMushroomDivisionCount);
                return;
            }

            AutoGenerateBaySpansFromCurrentInputs();
        }

        private void AutoGenerateBaySpansFromCurrentInputs()
        {
            if (!TryGetTSpacingMm(out double tSpacingMm))
            {
                return;
            }

            _baySpansMm.Clear();
            _baySpansMm.AddRange(BuildDefaultBaySpans(tSpacingMm, GetSuspensionDirection()));
            _bayHasMushroomFlags.Clear();
            _bayHasMushroomFlags.AddRange(Enumerable.Repeat(ShouldEnableMushroomByDefault(), _baySpansMm.Count));
            RefreshBaySpanList();
        }

        private void AddBaySpan()
        {
            if (!TryGetBaySpanInput(out double baySpanMm))
            {
                UiFeedback.ShowWarning("Nhịp thêm mới phải là số dương.", "Lỗi dữ liệu");
                _baySpanTextBox.Focus();
                return;
            }

            _baySpansMm.Add(baySpanMm);
            _bayHasMushroomFlags.Add(ShouldEnableMushroomByDefault());
            RefreshBaySpanList();
            _baySpanListBox.SelectedIndex = _baySpansMm.Count - 1;
            RefreshPreview();
        }

        private void UpdateSelectedBaySpan()
        {
            List<int> selectedIndices = GetSelectedBayIndices();
            if (selectedIndices.Count == 0)
            {
                UiFeedback.ShowWarning("Chọn ít nhất một nhịp để sửa.", "Thiếu lựa chọn");
                return;
            }

            if (!TryGetBaySpanInput(out double baySpanMm))
            {
                UiFeedback.ShowWarning("Nhịp sửa phải là số dương.", "Lỗi dữ liệu");
                _baySpanTextBox.Focus();
                return;
            }

            foreach (int index in selectedIndices)
            {
                _baySpansMm[index] = baySpanMm;
            }
            RefreshBaySpanList();
            ReselectBaySpans(selectedIndices);
            RefreshPreview();
        }

        private void RemoveSelectedBaySpan()
        {
            List<int> selectedIndices = GetSelectedBayIndices();
            if (selectedIndices.Count == 0)
            {
                UiFeedback.ShowWarning("Chọn ít nhất một nhịp để xóa.", "Thiếu lựa chọn");
                return;
            }

            foreach (int index in selectedIndices.OrderByDescending(i => i))
            {
                _baySpansMm.RemoveAt(index);
                if (index < _bayHasMushroomFlags.Count)
                {
                    _bayHasMushroomFlags.RemoveAt(index);
                }
            }
            RefreshBaySpanList();
            if (_baySpansMm.Count > 0)
            {
                _baySpanListBox.SelectedIndex = Math.Min(selectedIndices.Min(), _baySpansMm.Count - 1);
            }
            RefreshPreview();
        }

        private void ResetBaySpansToDefault()
        {
            if (!TryGetTSpacingMm(out double tSpacingMm))
            {
                UiFeedback.ShowWarning("Nhịp T mặc định phải là số dương trước khi reset.", "Lỗi dữ liệu");
                _tSpacingTextBox.Focus();
                return;
            }

            AutoGenerateBaySpansFromCurrentInputs();
            RefreshPreview();
        }

        private void ToggleSelectedBayMushroom()
        {
            List<int> selectedIndices = GetSelectedBayIndices();
            if (selectedIndices.Count == 0)
            {
                UiFeedback.ShowWarning("Chọn ít nhất một nhịp để bật/tắt nấm.", "Thiếu lựa chọn");
                return;
            }

            if (!TryGetMushroomDivisionCount(out int mushroomDivisionCount) || mushroomDivisionCount <= 0)
            {
                UiFeedback.ShowWarning("Đặt n bulông dù > 0 trước khi bật nấm cho nhịp.", "Thiếu dữ liệu");
                return;
            }

            bool anyDisabled = selectedIndices.Any(index => !GetEffectiveBayHasMushroom(index));
            bool nextState = anyDisabled;
            foreach (int index in selectedIndices)
            {
                if (index >= 0 && index < _bayHasMushroomFlags.Count)
                {
                    _bayHasMushroomFlags[index] = nextState;
                }
            }

            RefreshBaySpanList();
            ReselectBaySpans(selectedIndices);
            RefreshPreview();
        }

        private void RefreshBaySpanList()
        {
            _baySpanListBox.Items.Clear();
            for (int i = 0; i < _baySpansMm.Count; i++)
            {
                bool hasMushroom = GetEffectiveBayHasMushroom(i);
                _baySpanListBox.Items.Add($"Nhịp {i + 1}: {_baySpansMm[i]:F0} mm | {(hasMushroom ? "Có nấm" : "Không nấm")}");
            }

            UpdateToggleMushroomButtonText();
        }

        private List<int> GetSelectedBayIndices()
        {
            return _baySpanListBox.SelectedItems
                .Cast<object>()
                .Select(item => _baySpanListBox.Items.IndexOf(item))
                .Where(index => index >= 0 && index < _baySpansMm.Count)
                .Distinct()
                .OrderBy(index => index)
                .ToList();
        }

        private void ReselectBaySpans(IEnumerable<int> indices)
        {
            _baySpanListBox.SelectedItems.Clear();
            foreach (int index in indices.Where(i => i >= 0 && i < _baySpanListBox.Items.Count))
            {
                _baySpanListBox.SelectedItems.Add(_baySpanListBox.Items[index]);
            }
        }

        private void RefreshPreview()
        {
            var ext = _boundary.GeometricExtents;
            double width = Math.Max(0, ext.MaxPoint.X - ext.MinPoint.X);
            double height = Math.Max(0, ext.MaxPoint.Y - ext.MinPoint.Y);
            double axisSpan = GetAxisSpan(GetSuspensionDirection());
            _sizeText.Text = $"Biên dạng pick: {width:F0} x {height:F0} mm | Trục nhịp đang xem: {axisSpan:F0} mm | Dày panel: {_thicknessMm} mm";

            if (!TryBuildPreview(out var preview, out string errorMessage))
            {
                _previewSummaryText.Text = errorMessage;
                DrawEmptyPreview(errorMessage);
                return;
            }

            DrawPreview(preview!);

            string panelDirection = GetPanelDirection() == LayoutDirection.Vertical ? "Dọc" : "Ngang";
            string panelStart = _panelStartEdgeCombo.SelectedItem as string ?? "-";
            string suspensionDirection = GetSuspensionDirection() == LayoutDirection.Vertical ? "Dọc" : "Ngang";
            string suspensionOrigin = _suspensionOriginCombo.SelectedItem as string ?? "-";
            string baySummary = preview!.BaySpansMm.Count == 0
                ? "Không có nhịp T nội bộ."
                : string.Join(" | ", preview.BaySpansMm.Select(span => span.ToString("F0", CultureInfo.InvariantCulture)));
            string panelSummary = preview.PanelSpansMm.Count == 0
                ? "-"
                : string.Join(" | ", preview.PanelSpansMm.Select(span => span.ToString("F0", CultureInfo.InvariantCulture)));
            string mushroomRule = preview.MushroomDivisionCount <= 0
                ? "n = 0 nên toàn bộ nhịp đang ở trạng thái Không nấm."
                : "Mặc định các nhịp đều Có nấm; giữ Ctrl để chọn nhịp cần tắt nấm.";

            _previewSummaryText.Text =
                $"Chia tấm: {panelDirection}\n" +
                $"Chiều đặt: {panelStart}\n" +
                $"Tuyến treo: {suspensionDirection}\n" +
                $"Bắt đầu chia: {suspensionOrigin}\n\n" +
                $"Nhịp T dùng để vẽ: {baySummary}\n" +
                $"Khe tại T: {preview.TClearGapMm:F0} mm\n" +
                $"Chiều dài tấm dự kiến: {panelSummary}\n" +
                $"Đường T: {preview.TLineCount} tuyến\n" +
                $"Bulông dù: n = {preview.MushroomDivisionCount} | {preview.MushroomLineCount} tuyến\n" +
                mushroomRule;
        }

        private bool TryBuildPreview(out CeilingSuspensionPreview? preview, out string errorMessage)
        {
            preview = null;
            errorMessage = string.Empty;

            if (!TryGetTSpacingMm(out double tSpacingMm))
            {
                errorMessage = "Nhịp T mặc định chưa hợp lệ.";
                return false;
            }

            if (!TryGetTClearGapMm(out double tClearGapMm))
            {
                errorMessage = "Khe hở tại T chưa hợp lệ.";
                return false;
            }

            if (!TryGetMushroomDivisionCount(out int mushroomDivisionCount))
            {
                errorMessage = "n bulông dù chưa hợp lệ.";
                return false;
            }

            preview = CeilingSuspensionPreviewService.Calculate(
                _boundary,
                GetSuspensionDirection(),
                GetDivideFromMaxSide(),
                tSpacingMm,
                mushroomDivisionCount,
                tClearGapMm,
                _baySpansMm,
                _bayHasMushroomFlags);

            if (preview == null)
            {
                errorMessage = "Không tạo được preview với dữ liệu hiện tại.";
                return false;
            }

            return true;
        }

        private void DrawEmptyPreview(string message)
        {
            _previewCanvas.Children.Clear();
            _previewCanvas.Children.Add(new TextBlock
            {
                Text = message,
                Foreground = new SolidColorBrush(Color.FromRgb(192, 57, 43)),
                FontWeight = FontWeights.SemiBold
            });
        }

        private void DrawPreview(CeilingSuspensionPreview preview)
        {
            _previewCanvas.Children.Clear();

            double axisSpan = GetAxisSpan(GetSuspensionDirection());
            if (axisSpan <= 1.0)
            {
                DrawEmptyPreview("Biên dạng quá nhỏ để tạo preview.");
                return;
            }

            double canvasWidth = Math.Max(520, _previewCanvas.ActualWidth);
            double marginX = 28;
            double marginTop = 52;
            double schematicHeight = 88;
            double usableWidth = Math.Max(120, canvasWidth - (marginX * 2));
            double scale = usableWidth / axisSpan;

            var frame = new Rectangle
            {
                Width = usableWidth,
                Height = schematicHeight,
                RadiusX = 8,
                RadiusY = 8,
                Stroke = new SolidColorBrush(Color.FromRgb(141, 161, 178)),
                StrokeThickness = 1.2,
                Fill = new SolidColorBrush(Color.FromRgb(247, 249, 252))
            };
            Canvas.SetLeft(frame, marginX);
            Canvas.SetTop(frame, marginTop);
            _previewCanvas.Children.Add(frame);

            DrawCanvasText("Biên bắt đầu", marginX, 18, Brushes.DimGray, FontWeights.SemiBold);
            DrawCanvasText("Biên còn lại", marginX + usableWidth - 72, 18, Brushes.DimGray, FontWeights.SemiBold);

            IReadOnlyList<double> tPositions = BuildDisplayTPositions(axisSpan, GetDivideFromMaxSide(), preview.BaySpansMm);
            IReadOnlyList<double> mushroomPositions = BuildDisplayMushroomPositions(
                axisSpan,
                GetDivideFromMaxSide(),
                preview.BaySpansMm,
                preview.BayHasMushroomFlags,
                preview.MushroomDivisionCount);
            double halfGap = preview.TClearGapMm / 2.0;
            double cursor = 0;

            for (int i = 0; i < tPositions.Count; i++)
            {
                double tPosition = tPositions[i];
                double panelEnd = Math.Min(axisSpan, tPosition - halfGap);
                DrawPanelSegment(cursor, panelEnd, i + 1, scale, marginX, marginTop, schematicHeight);

                double gapStart = Math.Max(cursor, tPosition - halfGap);
                double gapEnd = Math.Min(axisSpan, tPosition + halfGap);
                DrawGapSegment(gapStart, gapEnd, i + 1, scale, marginX, marginTop, schematicHeight);
                cursor = Math.Max(cursor, gapEnd);
            }

            DrawPanelSegment(cursor, axisSpan, tPositions.Count + 1, scale, marginX, marginTop, schematicHeight);

            for (int i = 0; i < mushroomPositions.Count; i++)
            {
                DrawMushroomGuide(mushroomPositions[i], i + 1, scale, marginX, marginTop, schematicHeight);
            }

            double baselineY = marginTop + schematicHeight + 24;
            var baseline = new System.Windows.Shapes.Line
            {
                X1 = marginX,
                X2 = marginX + usableWidth,
                Y1 = baselineY,
                Y2 = baselineY,
                Stroke = new SolidColorBrush(Color.FromRgb(155, 166, 178)),
                StrokeThickness = 1
            };
            _previewCanvas.Children.Add(baseline);

            DrawCanvasText($"Trục preview: {axisSpan:F0} mm", marginX, baselineY + 8, Brushes.DimGray, FontWeights.Normal);
            DrawCanvasText($"Khe T: {preview.TClearGapMm:F0} mm", marginX + 180, baselineY + 8, new SolidColorBrush(Color.FromRgb(192, 57, 43)), FontWeights.SemiBold);
        }

        private void DrawPanelSegment(double start, double end, int index, double scale, double marginX, double marginTop, double schematicHeight)
        {
            double length = end - start;
            if (length <= 1.0)
            {
                return;
            }

            double left = marginX + (start * scale);
            double width = Math.Max(1.5, length * scale);
            var rect = new Rectangle
            {
                Width = width,
                Height = schematicHeight,
                Fill = new SolidColorBrush(Color.FromRgb(214, 234, 248)),
                Stroke = new SolidColorBrush(Color.FromRgb(52, 152, 219)),
                StrokeThickness = 1
            };
            Canvas.SetLeft(rect, left);
            Canvas.SetTop(rect, marginTop);
            _previewCanvas.Children.Add(rect);

            if (width >= 54)
            {
                DrawCanvasText($"{length:F0}", left + (width / 2) - 16, marginTop + 32, new SolidColorBrush(Color.FromRgb(33, 47, 61)), FontWeights.SemiBold);
            }

            if (width >= 76)
            {
                DrawCanvasText($"Tấm {index}", left + (width / 2) - 22, marginTop + 12, new SolidColorBrush(Color.FromRgb(73, 94, 120)), FontWeights.Normal);
            }
        }

        private void DrawGapSegment(double start, double end, int index, double scale, double marginX, double marginTop, double schematicHeight)
        {
            double width = end - start;
            if (width <= 0.2)
            {
                return;
            }

            double left = marginX + (start * scale);
            var rect = new Rectangle
            {
                Width = Math.Max(2.0, width * scale),
                Height = schematicHeight + 12,
                Fill = new SolidColorBrush(Color.FromArgb(130, 231, 76, 60)),
                Stroke = new SolidColorBrush(Color.FromRgb(192, 57, 43)),
                StrokeThickness = 1
            };
            Canvas.SetLeft(rect, left);
            Canvas.SetTop(rect, marginTop - 6);
            _previewCanvas.Children.Add(rect);

            DrawCanvasText($"T{index}", left - 8, marginTop - 24, new SolidColorBrush(Color.FromRgb(192, 57, 43)), FontWeights.SemiBold);
        }

        private void DrawMushroomGuide(double position, int index, double scale, double marginX, double marginTop, double schematicHeight)
        {
            double x = marginX + (position * scale);
            var line = new System.Windows.Shapes.Line
            {
                X1 = x,
                X2 = x,
                Y1 = marginTop - 18,
                Y2 = marginTop + schematicHeight + 18,
                Stroke = new SolidColorBrush(Color.FromRgb(39, 174, 96)),
                StrokeThickness = 1.6,
                StrokeDashArray = new DoubleCollection { 4, 3 }
            };
            _previewCanvas.Children.Add(line);

            var marker = new System.Windows.Shapes.Ellipse
            {
                Width = 8,
                Height = 8,
                Fill = new SolidColorBrush(Color.FromRgb(39, 174, 96)),
                Stroke = Brushes.White,
                StrokeThickness = 1
            };
            Canvas.SetLeft(marker, x - 4);
            Canvas.SetTop(marker, marginTop + (schematicHeight / 2.0) - 4);
            _previewCanvas.Children.Add(marker);

            DrawCanvasText($"B{index}", x - 8, marginTop + schematicHeight + 22, new SolidColorBrush(Color.FromRgb(39, 174, 96)), FontWeights.SemiBold);
        }

        private void HandleMushroomDivisionCountChanged()
        {
            if (!TryGetMushroomDivisionCount(out int mushroomDivisionCount))
            {
                UpdateToggleMushroomButtonText();
                RefreshPreview();
                return;
            }

            SyncMushroomFlagsForDivisionCount(mushroomDivisionCount, _lastMushroomDivisionCount);
            _lastMushroomDivisionCount = mushroomDivisionCount;
            RefreshBaySpanList();
            RefreshPreview();
        }

        private void SyncMushroomFlagsForDivisionCount(int mushroomDivisionCount, int previousDivisionCount)
        {
            while (_bayHasMushroomFlags.Count < _baySpansMm.Count)
            {
                _bayHasMushroomFlags.Add(true);
            }

            if (_bayHasMushroomFlags.Count > _baySpansMm.Count)
            {
                _bayHasMushroomFlags.RemoveRange(_baySpansMm.Count, _bayHasMushroomFlags.Count - _baySpansMm.Count);
            }

            if (mushroomDivisionCount <= 0)
            {
                for (int i = 0; i < _bayHasMushroomFlags.Count; i++)
                {
                    _bayHasMushroomFlags[i] = false;
                }
                return;
            }

            if (previousDivisionCount <= 0)
            {
                for (int i = 0; i < _bayHasMushroomFlags.Count; i++)
                {
                    _bayHasMushroomFlags[i] = true;
                }
            }
        }

        private bool ShouldEnableMushroomByDefault()
        {
            return TryGetMushroomDivisionCount(out int mushroomDivisionCount) && mushroomDivisionCount > 0;
        }

        private bool GetEffectiveBayHasMushroom(int index)
        {
            if (!TryGetMushroomDivisionCount(out int mushroomDivisionCount) || mushroomDivisionCount <= 0)
            {
                return false;
            }

            return index >= 0 && index < _bayHasMushroomFlags.Count
                ? _bayHasMushroomFlags[index]
                : true;
        }

        private void UpdateToggleMushroomButtonText()
        {
            if (!TryGetMushroomDivisionCount(out int mushroomDivisionCount) || mushroomDivisionCount <= 0)
            {
                _toggleMushroomButton.Content = "Không có nấm";
                _toggleMushroomButton.IsEnabled = false;
                _toggleMushroomButton.Opacity = 0.7;
                return;
            }

            _toggleMushroomButton.Content = "Bật/tắt nấm";
            _toggleMushroomButton.IsEnabled = true;
            _toggleMushroomButton.Opacity = 1.0;
        }

        private void DrawCanvasText(string text, double left, double top, Brush brush, FontWeight fontWeight)
        {
            var textBlock = new TextBlock
            {
                Text = text,
                Foreground = brush,
                FontSize = 11,
                FontWeight = fontWeight
            };
            Canvas.SetLeft(textBlock, Math.Max(0, left));
            Canvas.SetTop(textBlock, Math.Max(0, top));
            _previewCanvas.Children.Add(textBlock);
        }

        private List<double> BuildDefaultBaySpans(double tSpacingMm, LayoutDirection suspensionDirection)
        {
            double axisSpan = GetAxisSpan(suspensionDirection);
            return CeilingSuspensionPreviewService.GenerateSymmetricBaySpans(axisSpan, tSpacingMm).ToList();
        }

        private IReadOnlyList<double> BuildDisplayTPositions(double axisSpan, bool divideFromMaxSide, IReadOnlyList<double> baySpansMm)
        {
            var positions = new List<double>();
            double current = divideFromMaxSide ? axisSpan : 0;

            foreach (double baySpan in baySpansMm)
            {
                double next = divideFromMaxSide ? current - baySpan : current + baySpan;
                if (next <= 0.5 || next >= axisSpan - 0.5)
                {
                    break;
                }

                positions.Add(next);
                current = next;
            }

            positions.Sort();
            return positions;
        }

        private IReadOnlyList<double> BuildDisplayMushroomPositions(
            double axisSpan,
            bool divideFromMaxSide,
            IReadOnlyList<double> baySpansMm,
            IReadOnlyList<bool> bayHasMushroomFlags,
            int mushroomDivisionCount)
        {
            var positions = new List<double>();
            if (mushroomDivisionCount <= 0)
            {
                return positions;
            }

            double current = divideFromMaxSide ? axisSpan : 0;
            for (int bayIndex = 0; bayIndex < baySpansMm.Count; bayIndex++)
            {
                double baySpan = baySpansMm[bayIndex];
                double next = divideFromMaxSide ? current - baySpan : current + baySpan;
                if (next <= 0.5 || next >= axisSpan - 0.5)
                {
                    current = next;
                    continue;
                }

                bool hasMushroom = bayIndex < bayHasMushroomFlags.Count ? bayHasMushroomFlags[bayIndex] : true;
                if (hasMushroom)
                {
                    double part = baySpan / (mushroomDivisionCount + 1);
                    for (int i = 1; i <= mushroomDivisionCount; i++)
                    {
                        positions.Add(divideFromMaxSide ? current - (part * i) : current + (part * i));
                    }
                }

                current = next;
            }

            positions.Sort();
            return positions;
        }

        private double GetAxisSpan(LayoutDirection suspensionDirection)
        {
            var ext = _boundary.GeometricExtents;
            bool runAlongX = suspensionDirection != LayoutDirection.Horizontal;
            return runAlongX
                ? Math.Max(0, ext.MaxPoint.Y - ext.MinPoint.Y)
                : Math.Max(0, ext.MaxPoint.X - ext.MinPoint.X);
        }

        private bool TryGetTSpacingMm(out double value)
        {
            return double.TryParse(_tSpacingTextBox.Text.Replace(",", "."), NumberStyles.Float, CultureInfo.InvariantCulture, out value)
                && value > 0;
        }

        private bool TryGetTClearGapMm(out double value)
        {
            return double.TryParse(_tClearGapTextBox.Text.Replace(",", "."), NumberStyles.Float, CultureInfo.InvariantCulture, out value)
                && value >= 0;
        }

        private bool TryGetMushroomDivisionCount(out int value)
        {
            return int.TryParse(_mushroomDivisionTextBox.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value)
                && value >= 0;
        }

        private bool TryGetBaySpanInput(out double value)
        {
            string raw = string.IsNullOrWhiteSpace(_baySpanTextBox.Text) ? _tSpacingTextBox.Text : _baySpanTextBox.Text;
            return double.TryParse(raw.Replace(",", "."), NumberStyles.Float, CultureInfo.InvariantCulture, out value)
                && value > 0;
        }

        private LayoutDirection GetPanelDirection()
            => _panelDirectionCombo.SelectedIndex == 1 ? LayoutDirection.Horizontal : LayoutDirection.Vertical;

        private StartEdge GetPanelStartEdge()
            => _panelStartEdgeCombo.SelectedIndex == 1 ? StartEdge.Right : StartEdge.Left;

        private LayoutDirection GetSuspensionDirection()
            => _suspensionDirectionCombo.SelectedIndex == 1 ? LayoutDirection.Horizontal : LayoutDirection.Vertical;

        private bool GetDivideFromMaxSide()
            => _suspensionOriginCombo.SelectedIndex == 1;

        private static Border CreateSectionBorder()
        {
            return new Border
            {
                Padding = new Thickness(14),
                CornerRadius = new CornerRadius(8),
                Background = new SolidColorBrush(Color.FromRgb(242, 247, 250)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(211, 220, 227)),
                BorderThickness = new Thickness(1)
            };
        }

        private static void AddLabel(Grid grid, string text, int row)
        {
            var label = new TextBlock
            {
                Text = text,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 12, 8),
                FontWeight = FontWeights.SemiBold
            };
            Grid.SetRow(label, row);
            Grid.SetColumn(label, 0);
            grid.Children.Add(label);
        }

        private static ComboBox AddCombo(Grid grid, int row, params string[] items)
        {
            var combo = new ComboBox { MinWidth = 220, Margin = new Thickness(0, 0, 0, 8) };
            foreach (var item in items)
            {
                combo.Items.Add(item);
            }

            Grid.SetRow(combo, row);
            Grid.SetColumn(combo, 1);
            grid.Children.Add(combo);
            return combo;
        }

        private static TextBox AddTextBox(Grid grid, int row)
        {
            var textBox = new TextBox { MinWidth = 220, Margin = new Thickness(0, 0, 0, 8) };
            Grid.SetRow(textBox, row);
            Grid.SetColumn(textBox, 1);
            grid.Children.Add(textBox);
            return textBox;
        }

        private static void SetComboOptions(ComboBox combo, int selectedIndex, params string[] items)
        {
            combo.Items.Clear();
            foreach (var item in items)
            {
                combo.Items.Add(item);
            }

            combo.SelectedIndex = Math.Clamp(selectedIndex, 0, Math.Max(0, items.Length - 1));
        }

        private static Button CreateButton(string text, Color color, double width)
        {
            return new Button
            {
                Content = text,
                Width = width,
                Height = 34,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White,
                Background = new SolidColorBrush(color)
            };
        }
    }
}
