using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ShopDrawing.Plugin.Commands;
using ShopDrawing.Plugin.Core;

namespace ShopDrawing.Plugin.UI
{
    /// <summary>
    /// Smart Dim palette, unified with the shared palette styles.
    /// </summary>
    public class SmartDimPaletteControl : UserControl
    {
        private readonly SmartDimEngine _engine = new();

        private readonly ComboBox _cboWall;
        private readonly ComboBox _cboHeightSide;
        private readonly CheckBox _chkWidth;
        private readonly CheckBox _chkHeight;
        private readonly CheckBox _chkOpening;
        private readonly CheckBox _chkElevation;
        private readonly TextBlock _lblStatus;

        public SmartDimPaletteControl()
        {
            var mainStack = new StackPanel { Margin = new Thickness(10) };

            // Header
            mainStack.Children.Add(SdPaletteStyles.CreateHeader("SMART DIMENSION"));

            // Wall and height side
            var wallSection = new StackPanel();

            var row1 = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            row1.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row1.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row1.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row1.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row1.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.3, GridUnitType.Star) });

            var lblWall = SdPaletteStyles.CreateLabel("Tường:");
            lblWall.VerticalAlignment = VerticalAlignment.Center;
            lblWall.Margin = new Thickness(0, 0, 4, 0);
            Grid.SetColumn(lblWall, 0);
            row1.Children.Add(lblWall);

            _cboWall = new ComboBox
            {
                IsEditable = true,
                Text = "W1",
                ToolTip = "Chọn mã tường hoặc nhập thủ công",
                VerticalContentAlignment = VerticalAlignment.Center,
                Height = 24
            };
            Grid.SetColumn(_cboWall, 1);
            row1.Children.Add(_cboWall);

            var btnScan = SdPaletteStyles.CreateCompactButton("Scan");
            btnScan.Width = 48;
            btnScan.Height = 24;
            btnScan.Margin = new Thickness(4, 0, 8, 0);
            btnScan.ToolTip = "Quét bản vẽ để tìm tất cả mã tường";
            btnScan.Click += (_, _) => ScanWallCodes();
            Grid.SetColumn(btnScan, 2);
            row1.Children.Add(btnScan);

            var lblHeight = SdPaletteStyles.CreateLabel("Cao:");
            lblHeight.VerticalAlignment = VerticalAlignment.Center;
            lblHeight.Margin = new Thickness(0, 0, 4, 0);
            Grid.SetColumn(lblHeight, 3);
            row1.Children.Add(lblHeight);

            _cboHeightSide = new ComboBox
            {
                ToolTip = "Tường ngang: Phải/Trái. Tường dọc: Trên/Dưới.",
                Height = 24,
                VerticalContentAlignment = VerticalAlignment.Center
            };
            _cboHeightSide.Items.Add("Phải / Trên");
            _cboHeightSide.Items.Add("Trái / Dưới");
            _cboHeightSide.SelectedIndex = 0;
            Grid.SetColumn(_cboHeightSide, 4);
            row1.Children.Add(_cboHeightSide);
            wallSection.Children.Add(row1);

            // Checkboxes
            var row2 = new WrapPanel { Margin = new Thickness(0, 0, 0, 10) };
            _chkWidth = MakeCheckBox("Width", true);
            _chkHeight = MakeCheckBox("Height", true);
            _chkOpening = MakeCheckBox("Opening", true);
            _chkElevation = MakeCheckBox("Elev", true);
            _chkElevation.Margin = new Thickness(0);
            row2.Children.Add(_chkWidth);
            row2.Children.Add(_chkHeight);
            row2.Children.Add(_chkOpening);
            row2.Children.Add(_chkElevation);
            wallSection.Children.Add(row2);

            // Auto buttons
            var row3 = new Grid();
            row3.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row3.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var btnCreate = SdPaletteStyles.CreateActionButton("Tạo dim", SdPaletteStyles.AccentBlueBrush);
            Grid.SetColumn(btnCreate, 0);
            btnCreate.Margin = new Thickness(0, 0, 4, 0);
            btnCreate.Click += (_, _) => RunAutoDim();
            row3.Children.Add(btnCreate);

            var btnDelete = SdPaletteStyles.CreateActionButton("Xóa dim", SdPaletteStyles.BtnDefaultBrush);
            Grid.SetColumn(btnDelete, 1);
            btnDelete.Margin = new Thickness(4, 0, 0, 0);
            btnDelete.Click += (_, _) => DeleteDims();
            row3.Children.Add(btnDelete);
            wallSection.Children.Add(row3);
            mainStack.Children.Add(SdPaletteStyles.CreateSectionBorder(wallSection));

            // Manual
            var manualSection = new StackPanel();
            manualSection.Children.Add(SdPaletteStyles.CreateSectionHeader("MANUAL DIM"));

            var rowManual = new Grid();
            rowManual.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            rowManual.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var btnH = SdPaletteStyles.CreateOutlineButton("Dim Ngang");
            Grid.SetColumn(btnH, 0);
            btnH.Margin = new Thickness(0, 0, 4, 0);
            btnH.Click += (_, _) => RunManualLoop(0);
            rowManual.Children.Add(btnH);

            var btnV = SdPaletteStyles.CreateOutlineButton("Dim Doc");
            Grid.SetColumn(btnV, 1);
            btnV.Margin = new Thickness(4, 0, 0, 0);
            btnV.Click += (_, _) => RunManualLoop(Math.PI / 2);
            rowManual.Children.Add(btnV);
            manualSection.Children.Add(rowManual);
            mainStack.Children.Add(SdPaletteStyles.CreateSectionBorder(manualSection));

            // Footer
            var scaleFactor = LayoutManagerEngine.GetCurrentScaleDenominator();
            mainStack.Children.Add(new TextBlock
            {
                Text = $"1:{scaleFactor:F0} | {ShopDrawingCommands.DefaultTextHeightMm:F1}mm (TAG)",
                FontSize = SdPaletteStyles.FontSizeSmall,
                FontFamily = SdPaletteStyles.Font,
                Foreground = SdPaletteStyles.TextMutedBrush,
                Margin = new Thickness(0, 4, 0, 0)
            });

            _lblStatus = SdPaletteStyles.CreateStatusText();
            mainStack.Children.Add(_lblStatus);

            Content = SdPaletteStyles.WrapInScrollViewer(mainStack);
            Background = SdPaletteStyles.BgPrimaryBrush;
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            ScanWallCodes();
        }

        private void ScanWallCodes()
        {
            if (!HasActiveDocument())
            {
                SetStatus("Chưa có bản vẽ đang mở.", true);
                return;
            }

            try
            {
                var codes = SmartDimEngine.ScanWallCodes();
                string prev = _cboWall.Text;
                _cboWall.Items.Clear();
                foreach (var c in codes)
                {
                    _cboWall.Items.Add(c);
                }

                if (codes.Count > 0)
                {
                    if (codes.Contains(prev))
                    {
                        _cboWall.Text = prev;
                    }
                    else
                    {
                        _cboWall.SelectedIndex = 0;
                    }

                    SetStatus($"Tìm thấy {codes.Count} tường: {string.Join(", ", codes)}", false);
                }
                else
                {
                    SetStatus("Không tìm thấy tường nào (chưa có SD_TAG).", true);
                }
            }
            catch (Exception ex)
            {
                SetStatus($"Lỗi quét: {ex.Message}", true);
            }
        }

        private void RunAutoDim()
        {
            if (!HasActiveDocument())
            {
                SetStatus("Chưa có bản vẽ đang mở.", true);
                return;
            }

            try
            {
                string wallCode = (_cboWall.Text ?? string.Empty).Trim().ToUpper();
                if (string.IsNullOrEmpty(wallCode))
                {
                    SetStatus("Chọn mã tường trước.", true);
                    return;
                }

                var request = new SmartDimEngine.AutoDimRequest(
                    WallCode: wallCode,
                    DimWidth: _chkWidth.IsChecked == true,
                    DimHeight: _chkHeight.IsChecked == true,
                    DimOpening: _chkOpening.IsChecked == true,
                    DimElevation: _chkElevation.IsChecked == true,
                    HeightOnRight: _cboHeightSide.SelectedIndex == 0);

                var result = _engine.AutoDimWall(request);
                SetStatus(result.Message, result.DimCount == 0);
            }
            catch (Exception ex)
            {
                SetStatus(ex.Message, true);
            }
        }

        private void DeleteDims()
        {
            if (!HasActiveDocument())
            {
                SetStatus("Chưa có bản vẽ đang mở.", true);
                return;
            }

            try
            {
                string wallCode = (_cboWall.Text ?? string.Empty).Trim().ToUpper();
                if (string.IsNullOrEmpty(wallCode))
                {
                    SetStatus("Chọn mã tường trước.", true);
                    return;
                }

                int count = SmartDimEngine.ClearWallDims(wallCode);
                SetStatus(
                    count > 0
                        ? $"Đã xóa {count} dim của {wallCode}."
                        : $"Không tìm thấy dim nào cho {wallCode}.",
                    count == 0);
            }
            catch (Exception ex)
            {
                SetStatus(ex.Message, true);
            }
        }

        private void RunManualLoop(double rotation)
        {
            if (!HasActiveDocument())
            {
                SetStatus("Chưa có bản vẽ đang mở.", true);
                return;
            }

            try
            {
                int count = _engine.ManualDimLoop(rotation);
                SetStatus(count > 0 ? $"Đã vẽ {count} dim thủ công." : "Đã hủy.", count == 0);
            }
            catch (Exception ex)
            {
                SetStatus(ex.Message, true);
            }
        }

        private void SetStatus(string msg, bool isWarning)
        {
            if (isWarning)
            {
                UiStatus.ApplyWarning(_lblStatus, msg);
                return;
            }

            UiStatus.ApplySuccess(_lblStatus, msg);
        }

        private static bool HasActiveDocument()
        {
            return AutoCadUiContext.HasActiveDocument();
        }

        private static CheckBox MakeCheckBox(string label, bool isChecked)
        {
            return new CheckBox
            {
                Content = label,
                IsChecked = isChecked,
                Foreground = SdPaletteStyles.TextPrimaryBrush,
                FontFamily = SdPaletteStyles.Font,
                VerticalContentAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 12, 0)
            };
        }
    }
}
