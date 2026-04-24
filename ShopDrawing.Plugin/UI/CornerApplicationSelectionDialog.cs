using System.Windows;
using System.Windows.Controls;
using ShopDrawing.Plugin.Core;

namespace ShopDrawing.Plugin.UI
{
    internal sealed class CornerApplicationSelectionDialog : Window
    {
        private readonly ComboBox _cboApplication;
        private readonly TextBox _txtCableDrop;
        private readonly bool _showCableDrop;

        public string SelectedApplication { get; private set; } = AccessoryDataManager.AppExterior;
        public double CableDropMm { get; private set; }

        public CornerApplicationSelectionDialog(bool showCableDropInput = false, double initialCableDropMm = 1500)
        {
            Title = showCableDropInput ? "Cấu hình điểm treo" : "Chọn hạng mục ứng dụng";
            Width = 320;
            Height = showCableDropInput ? 200 : 160;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            ResizeMode = ResizeMode.NoResize;
            _showCableDrop = showCableDropInput;
            CableDropMm = initialCableDropMm;

            var grid = new Grid { Margin = new Thickness(15) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            if (showCableDropInput)
            {
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            }
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var textBlock = new TextBlock
            {
                Text = showCableDropInput ? "Chọn hạng mục và chiều dài thả cáp:" : "Vui lòng chọn hạng mục cho phụ kiện góc:",
                Margin = new Thickness(0, 0, 0, 10)
            };
            Grid.SetRow(textBlock, 0);
            grid.Children.Add(textBlock);

            _cboApplication = new ComboBox
            {
                Margin = new Thickness(0, 0, 0, 15)
            };
            
            _cboApplication.Items.Add(new ComboBoxItem { Content = "Phòng sạch", Tag = AccessoryDataManager.AppCleanroom });
            _cboApplication.Items.Add(new ComboBoxItem { Content = "Kho lạnh", Tag = AccessoryDataManager.AppColdStorage });
            
            _cboApplication.SelectedIndex = 0;

            Grid.SetRow(_cboApplication, 1);
            grid.Children.Add(_cboApplication);

            if (showCableDropInput)
            {
                var cableDropPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 15) };
                cableDropPanel.Children.Add(new TextBlock { Text = "Thả cáp (mm):", VerticalAlignment = VerticalAlignment.Center, Width = 90 });
                
                _txtCableDrop = new TextBox
                {
                    Text = initialCableDropMm.ToString("F0"),
                    Width = 100,
                    VerticalAlignment = VerticalAlignment.Center
                };
                cableDropPanel.Children.Add(_txtCableDrop);

                Grid.SetRow(cableDropPanel, 2);
                grid.Children.Add(cableDropPanel);
            }

            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom
            };
            Grid.SetRow(buttonPanel, showCableDropInput ? 3 : 2);

            var btnOk = new Button
            {
                Content = "OK",
                Width = 80,
                Margin = new Thickness(0, 0, 10, 0),
                IsDefault = true
            };
            btnOk.Click += (s, e) =>
            {
                if (_cboApplication.SelectedItem is ComboBoxItem selectedItem && selectedItem.Tag is string appStr)
                {
                    SelectedApplication = appStr;
                }
                
                if (_showCableDrop && _txtCableDrop != null)
                {
                    if (double.TryParse(_txtCableDrop.Text, out double parsedVal) && parsedVal >= 0)
                    {
                        CableDropMm = parsedVal;
                    }
                }
                
                DialogResult = true;
                Close();
            };

            var btnCancel = new Button
            {
                Content = "Hủy",
                Width = 80,
                IsCancel = true
            };
            btnCancel.Click += (s, e) =>
            {
                DialogResult = false;
                Close();
            };

            buttonPanel.Children.Add(btnOk);
            buttonPanel.Children.Add(btnCancel);
            grid.Children.Add(buttonPanel);

            Content = grid;
        }
    }
}
