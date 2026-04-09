using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ShopDrawing.Plugin.Models;

namespace ShopDrawing.Plugin.UI
{
    internal sealed class ProjectInputDialog : Window
    {
        private readonly ComboBox _cboProjectType;
        private readonly TextBox _txtProjectName;
        private readonly TextBox _txtProjectAddress;
        private readonly TextBox _txtCustomerName;

        public ProjectProfile ProjectProfile { get; }

        public ProjectInputDialog(ProjectProfile profile)
        {
            ProjectProfile = profile;

            Title = "INPUT - Thông tin dự án";
            Width = 560;
            Height = 370;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = SdPaletteStyles.BgPrimaryBrush;

            var root = new Grid { Margin = new Thickness(16) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var title = new TextBlock
            {
                Text = "KHAI BÁO THÔNG TIN DỰ ÁN",
                FontFamily = SdPaletteStyles.Font,
                FontSize = 15,
                FontWeight = FontWeights.Bold,
                Foreground = SdPaletteStyles.AccentBlueBrush,
                Margin = new Thickness(0, 0, 0, 10)
            };
            Grid.SetRow(title, 0);
            root.Children.Add(title);

            var subtitle = new TextBlock
            {
                Text = "Dữ liệu này là nguồn chung cho Tender và Shopdrawing.",
                TextWrapping = TextWrapping.Wrap,
                FontFamily = SdPaletteStyles.Font,
                FontSize = 11,
                Foreground = SdPaletteStyles.TextSecondaryBrush,
                Margin = new Thickness(0, 0, 0, 12)
            };
            Grid.SetRow(subtitle, 1);
            root.Children.Add(subtitle);

            _cboProjectType = CreateProjectTypeInput(root, 2, "Loại dự án", profile.ProjectType);
            _txtProjectName = CreateLabeledInput(root, 3, "Tên dự án", profile.ProjectName);
            _txtProjectAddress = CreateLabeledInput(root, 4, "Địa chỉ dự án", profile.ProjectAddress);
            _txtCustomerName = CreateLabeledInput(root, 5, "Khách hàng", profile.CustomerName, true);

            var footer = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 14, 0, 0)
            };

            var btnCancel = new Button
            {
                Content = "Hủy",
                Width = 90,
                Height = 30,
                Margin = new Thickness(0, 0, 8, 0),
                FontFamily = SdPaletteStyles.Font
            };
            btnCancel.Click += (_, _) => DialogResult = false;

            var btnSave = new Button
            {
                Content = "Lưu",
                Width = 100,
                Height = 30,
                IsDefault = true,
                FontFamily = SdPaletteStyles.Font,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White,
                Background = SdPaletteStyles.AccentBlueBrush,
                BorderThickness = new Thickness(0)
            };
            btnSave.Click += (_, _) => SaveAndClose();

            footer.Children.Add(btnCancel);
            footer.Children.Add(btnSave);
            Grid.SetRow(footer, 6);
            root.Children.Add(footer);

            Content = root;
            UiText.NormalizeWindow(this);
        }

        private static TextBox CreateLabeledInput(
            Grid root,
            int rowIndex,
            string label,
            string value,
            bool isLast = false)
        {
            var panel = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Margin = new Thickness(0, 0, 0, isLast ? 0 : 8)
            };

            panel.Children.Add(new TextBlock
            {
                Text = label,
                FontFamily = SdPaletteStyles.Font,
                FontSize = 11,
                Foreground = SdPaletteStyles.TextSecondaryBrush,
                Margin = new Thickness(0, 0, 0, 2)
            });

            var input = new TextBox
            {
                Text = value ?? string.Empty,
                Padding = new Thickness(7, 5, 7, 5),
                FontFamily = SdPaletteStyles.Font,
                FontSize = 12
            };
            panel.Children.Add(input);

            Grid.SetRow(panel, rowIndex);
            root.Children.Add(panel);
            return input;
        }

        private static ComboBox CreateProjectTypeInput(
            Grid root,
            int rowIndex,
            string label,
            string value)
        {
            var panel = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Margin = new Thickness(0, 0, 0, 8)
            };

            panel.Children.Add(new TextBlock
            {
                Text = label,
                FontFamily = SdPaletteStyles.Font,
                FontSize = 11,
                Foreground = SdPaletteStyles.TextSecondaryBrush,
                Margin = new Thickness(0, 0, 0, 2)
            });

            var combo = new ComboBox
            {
                Padding = new Thickness(7, 5, 7, 5),
                FontFamily = SdPaletteStyles.Font,
                FontSize = 12,
                IsEditable = false
            };

            combo.Items.Add("Tender");
            combo.Items.Add("Shopdrawing");
            combo.Items.Add("Production");

            if (!string.IsNullOrWhiteSpace(value) && combo.Items.Contains(value))
            {
                combo.SelectedItem = value;
            }
            else
            {
                combo.SelectedIndex = 0;
            }

            panel.Children.Add(combo);
            Grid.SetRow(panel, rowIndex);
            root.Children.Add(panel);
            return combo;
        }

        private void SaveAndClose()
        {
            ProjectProfile.ProjectType = _cboProjectType.SelectedItem?.ToString()?.Trim() ?? string.Empty;
            ProjectProfile.ProjectName = _txtProjectName.Text.Trim();
            ProjectProfile.ProjectAddress = _txtProjectAddress.Text.Trim();
            ProjectProfile.CustomerName = _txtCustomerName.Text.Trim();
            DialogResult = true;
        }
    }
}
