using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using ShopDrawing.Plugin.Models;
using ShopDrawing.Plugin.Core;

namespace ShopDrawing.Plugin.UI
{
    public class SpecManagerDialog : Window
    {
        private readonly SpecConfigManager _manager;
        private readonly DataGrid _dataGrid;
        public ObservableCollection<PanelSpec> Specs { get; set; }

        // Colors for grouped headers
        private static readonly SolidColorBrush TopHeaderBg = new SolidColorBrush(Color.FromRgb(41, 128, 185));   // Blue
        private static readonly SolidColorBrush BottomHeaderBg = new SolidColorBrush(Color.FromRgb(39, 174, 96)); // Green

        public SpecManagerDialog(SpecConfigManager manager)
        {
            _manager = manager;
            Title = "Quản lý cấu tạo tấm (Specs)";
            Width = 1200;
            Height = 650;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = new SolidColorBrush(Color.FromRgb(245, 245, 247));

            var mainGrid = new Grid { Margin = new Thickness(12) };
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });  // group header
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // datagrid
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });  // footer

            // --- HEADER ---
            var header = new TextBlock
            {
                Text = "Bảng quản lý Specs",
                FontSize = 16, FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(44, 62, 80)),
                Margin = new Thickness(0, 0, 0, 10)
            };
            Grid.SetRow(header, 0);
            mainGrid.Children.Add(header);

            // --- GROUPED HEADER ROW (▲ MẶT TRÊN / ▼ MẶT DƯỚI) ---
            var groupHeaderGrid = new Grid { Margin = new Thickness(0, 0, 0, 0) };
            // 3 columns: left-padding (identity+panelinfo), Mặt Trên, Mặt Dưới
            var colLeft = new ColumnDefinition { Width = new GridLength(500) };
            var colTop = new ColumnDefinition { Width = new GridLength(301) };
            var colBottom = new ColumnDefinition { Width = new GridLength(246) };
            groupHeaderGrid.ColumnDefinitions.Add(colLeft);
            groupHeaderGrid.ColumnDefinitions.Add(colTop);
            groupHeaderGrid.ColumnDefinitions.Add(colBottom);

            // Mặt Trên label
            var topLabel = new Border
            {
                Background = TopHeaderBg,
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(6, 4, 6, 4),
                Margin = new Thickness(1, 0, 1, 3)
            };
            topLabel.Child = new TextBlock
            {
                Text = "▲ Mặt trên",
                FontWeight = FontWeights.Bold, FontSize = 12,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            Grid.SetColumn(topLabel, 1);
            groupHeaderGrid.Children.Add(topLabel);

            // Mặt Dưới label
            var bottomLabel = new Border
            {
                Background = BottomHeaderBg,
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(6, 4, 6, 4),
                Margin = new Thickness(1, 0, 1, 3)
            };
            bottomLabel.Child = new TextBlock
            {
                Text = "▼ Mặt dưới",
                FontWeight = FontWeights.Bold, FontSize = 12,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            Grid.SetColumn(bottomLabel, 2);
            groupHeaderGrid.Children.Add(bottomLabel);

            Grid.SetRow(groupHeaderGrid, 1);
            mainGrid.Children.Add(groupHeaderGrid);

            // --- DATAGRID ---
            Specs = new ObservableCollection<PanelSpec>(_manager.GetAll());
            _dataGrid = new DataGrid
            {
                AutoGenerateColumns = false,
                CanUserAddRows = false,
                CanUserDeleteRows = false,
                ItemsSource = Specs,
                HeadersVisibility = DataGridHeadersVisibility.Column,
                AlternatingRowBackground = new SolidColorBrush(Color.FromRgb(248, 249, 250)),
                GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
                HorizontalGridLinesBrush = new SolidColorBrush(Color.FromRgb(222, 226, 230)),
                FontSize = 12
            };

            // --- IDENTITY ---
            AddTextColumn("Mã Spec", "Key", 100, null);
            AddTemplateComboColumn("Khổ tấm", "PanelWidth", 75, PanelSpec.PanelWidthOptions, null);

            // --- PANEL INFO ---
            AddTemplateComboColumn("Loại panel", "PanelType", 85, PanelSpec.PanelTypeOptions, null);
            AddTextColumn("Tỷ trọng", "Density", 55, null);
            AddTemplateComboColumn("Chiều dày", "Thickness", 70, PanelSpec.ThicknessOptions, null);
            AddTextColumn("Chống cháy", "FireRating", 62, null);
            AddCheckColumn("FM", "FmApproved", 33);

            // --- MẶT TRÊN (Blue) — columns 7..11 ---
            AddTextColumn("Màu sắc", "FacingColor", 55, TopHeaderBg);
            AddTextColumn("Vật liệu", "TopFacing", 55, TopHeaderBg);
            AddTextColumn("Độ mạ", "TopCoating", 55, TopHeaderBg);
            AddTextColumn("Dày tôn", "TopSteelThickness", 55, TopHeaderBg);
            AddTemplateComboColumn("Profile", "TopProfile", 78, PanelSpec.ProfileOptions, TopHeaderBg);

            // --- MẶT DƯỚI (Green) — columns 12..16 ---
            AddTextColumn("Màu sắc", "BottomFacingColor", 55, BottomHeaderBg);
            AddTextColumn("Vật liệu", "BottomFacing", 55, BottomHeaderBg);
            AddTextColumn("Độ mạ", "BottomCoating", 55, BottomHeaderBg);
            AddTextColumn("Dày tôn", "BottomSteelThickness", 55, BottomHeaderBg);
            AddTemplateComboColumn("Profile", "BottomProfile", 78, PanelSpec.ProfileOptions, BottomHeaderBg);

            // Sync group header widths when columns are resized
            _dataGrid.LayoutUpdated += (s, e) =>
            {
                try
                {
                    // Identity + Panel Info columns (0..6) — 7 cột sau khi bỏ Wall Code
                    double leftW = 0;
                    for (int i = 0; i < 7 && i < _dataGrid.Columns.Count; i++)
                        leftW += _dataGrid.Columns[i].ActualWidth;
                    colLeft.Width = new GridLength(leftW);

                    // Mặt Trên columns (7..11)
                    double topW = 0;
                    for (int i = 7; i < 12 && i < _dataGrid.Columns.Count; i++)
                        topW += _dataGrid.Columns[i].ActualWidth;
                    colTop.Width = new GridLength(topW);

                    // Mặt Dưới columns (12..16)
                    double bottomW = 0;
                    for (int i = 12; i < _dataGrid.Columns.Count; i++)
                        bottomW += _dataGrid.Columns[i].ActualWidth;
                    colBottom.Width = new GridLength(bottomW);
                }
                catch { /* ignore during initialization */ }
            };

            Grid.SetRow(_dataGrid, 2);
            mainGrid.Children.Add(_dataGrid);

            // --- FOOTER ---
            var footer = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 10, 0, 0)
            };
            Grid.SetRow(footer, 3);
            mainGrid.Children.Add(footer);

            var btnAdd = new Button
            {
                Content = "➕ Thêm spec",
                Width = 110, Height = 35,
                Margin = new Thickness(0, 0, 8, 0),
                Background = new SolidColorBrush(Color.FromRgb(52, 152, 219)),
                Foreground = Brushes.White,
                FontWeight = FontWeights.SemiBold,
                ToolTip = "Chọn 1 dòng → nhân bản + tăng số Key.\nKhông chọn → tạo dòng trống."
            };
            btnAdd.Click += (s, e) =>
            {
                var source = _dataGrid.SelectedItem as PanelSpec ?? (Specs.Count > 0 ? Specs[Specs.Count - 1] : null);
                var newSpec = CloneAndIncrement(source);
                Specs.Add(newSpec);
                _dataGrid.ScrollIntoView(newSpec);
                _dataGrid.SelectedItem = newSpec;
            };

            var btnBatchAdd = new Button
            {
                Content = "📋 Nhân bản ×N",
                Width = 115, Height = 35,
                Margin = new Thickness(0, 0, 8, 0),
                Background = new SolidColorBrush(Color.FromRgb(142, 68, 173)),
                Foreground = Brushes.White,
                FontWeight = FontWeights.SemiBold,
                ToolTip = "Chọn 1 dòng → tạo N bản sao với Key tăng dần"
            };
            btnBatchAdd.Click += (s, e) =>
            {
                var source = _dataGrid.SelectedItem as PanelSpec ?? (Specs.Count > 0 ? Specs[Specs.Count - 1] : null);

                // Hỏi số lượng
                var dlg = new Window
                {
                    Title = "Nhân bản spec",
                    Width = 320, Height = 200,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Owner = this,
                    ResizeMode = ResizeMode.NoResize
                };
                var sp = new StackPanel { Margin = new Thickness(16) };
                sp.Children.Add(new TextBlock
                {
                    Text = $"Nhân bản từ: {source?.Key ?? "Mới"}\nSố lượng bản sao:",
                    Margin = new Thickness(0, 0, 0, 8)
                });
                var txtCount = new TextBox { Text = "5", Width = 60, HorizontalAlignment = HorizontalAlignment.Left };
                sp.Children.Add(txtCount);
                var btnOk = new Button
                {
                    Content = "OK", Width = 60, Height = 28,
                    Margin = new Thickness(0, 12, 0, 0),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    IsDefault = true
                };
                btnOk.Click += (_, _) => { dlg.DialogResult = true; };
                sp.Children.Add(btnOk);
                dlg.Content = sp;

                if (dlg.ShowDialog() == true && int.TryParse(txtCount.Text, out int count) && count > 0 && count <= 50)
                {
                    PanelSpec lastClone = source;
                    for (int i = 0; i < count; i++)
                    {
                        lastClone = CloneAndIncrement(lastClone);
                        Specs.Add(lastClone);
                    }
                    _dataGrid.ScrollIntoView(Specs[Specs.Count - 1]);
                }
            };

            var btnDelete = new Button
            {
                Content = "🗑️ Xóa",
                Width = 80, Height = 35,
                Margin = new Thickness(0, 0, 20, 0),
                Background = new SolidColorBrush(Color.FromRgb(231, 76, 60)),
                Foreground = Brushes.White,
                FontWeight = FontWeights.SemiBold
            };
            btnDelete.Click += (s, e) =>
            {
                var toRemove = _dataGrid.SelectedItems.Cast<PanelSpec>().ToList();
                if (toRemove.Count == 0) return;
                if (MessageBox.Show($"Xóa {toRemove.Count} dòng?", "Xác nhận",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    foreach (var spec in toRemove)
                        Specs.Remove(spec);
                }
            };

            var btnSave = new Button
            {
                Content = "💾 Lưu & đóng",
                Width = 120, Height = 35,
                Margin = new Thickness(0, 0, 8, 0),
                Background = new SolidColorBrush(Color.FromRgb(44, 62, 80)),
                Foreground = Brushes.White,
                FontWeight = FontWeights.SemiBold
            };
            btnSave.Click += (s, e) =>
            {
                _manager.Save(new List<PanelSpec>(Specs));
                DialogResult = true;
            };

            var btnCancel = new Button { Content = "Hủy", Width = 80, Height = 35 };
            btnCancel.Click += (s, e) => { DialogResult = false; };

            footer.Children.Add(btnAdd);
            footer.Children.Add(btnBatchAdd);
            footer.Children.Add(btnDelete);
            footer.Children.Add(btnSave);
            footer.Children.Add(btnCancel);

            Content = mainGrid;
        }

        /// <summary>
        /// Tạo header style có màu nền nhóm + context menu "Áp dụng cho tất cả".
        /// </summary>
        private Style CreateHeaderStyle(string header, string binding, SolidColorBrush groupBg)
        {
            var style = new Style(typeof(DataGridColumnHeader));
            style.Setters.Add(new Setter(DataGridColumnHeader.ToolTipProperty,
                "Chuột phải → Áp dụng giá trị dòng đầu cho tất cả"));

            if (groupBg != null)
            {
                style.Setters.Add(new Setter(DataGridColumnHeader.BackgroundProperty, groupBg));
                style.Setters.Add(new Setter(DataGridColumnHeader.ForegroundProperty, Brushes.White));
                style.Setters.Add(new Setter(DataGridColumnHeader.FontWeightProperty, FontWeights.SemiBold));
            }

            var menu = new ContextMenu();
            var menuItem = new MenuItem { Header = $"📋 Áp dụng \"{header}\" dòng đầu cho tất cả" };
            menuItem.Click += (s, e) => ApplyFirstRowToAll(binding);
            menu.Items.Add(menuItem);

            style.Setters.Add(new Setter(FrameworkElement.ContextMenuProperty, menu));
            return style;
        }

        private void AddTextColumn(string header, string binding, double width, SolidColorBrush groupBg)
        {
            var col = new DataGridTextColumn
            {
                Header = header,
                Binding = new Binding(binding),
                Width = width
            };
            col.HeaderStyle = CreateHeaderStyle(header, binding, groupBg);
            _dataGrid.Columns.Add(col);
        }

        private void AddCheckColumn(string header, string binding, double width)
        {
            _dataGrid.Columns.Add(new DataGridCheckBoxColumn
            {
                Header = header,
                Binding = new Binding(binding),
                Width = width
            });
        }

        /// <summary>
        /// Safe ComboBox column using DataGridTemplateColumn.
        /// Display = TextBlock, Edit = ComboBox. No IEditableObject needed.
        /// </summary>
        private void AddTemplateComboColumn(string header, string binding, double width,
            object[] options, SolidColorBrush groupBg)
        {
            var col = new DataGridTemplateColumn
            {
                Header = header,
                Width = width
            };

            // Display template: TextBlock
            var displayFactory = new FrameworkElementFactory(typeof(TextBlock));
            displayFactory.SetBinding(TextBlock.TextProperty, new Binding(binding));
            displayFactory.SetValue(TextBlock.MarginProperty, new Thickness(4, 2, 4, 2));
            col.CellTemplate = new DataTemplate { VisualTree = displayFactory };

            // Edit template: ComboBox (IsEditable=true for custom input)
            var editFactory = new FrameworkElementFactory(typeof(ComboBox));
            editFactory.SetBinding(ComboBox.TextProperty, new Binding(binding) { UpdateSourceTrigger = UpdateSourceTrigger.LostFocus });
            editFactory.SetValue(ComboBox.IsEditableProperty, true);
            editFactory.SetValue(ComboBox.ItemsSourceProperty, options);
            editFactory.SetValue(ComboBox.IsDropDownOpenProperty, true); // auto-open
            col.CellEditingTemplate = new DataTemplate { VisualTree = editFactory };

            col.HeaderStyle = CreateHeaderStyle(header, binding, groupBg);
            _dataGrid.Columns.Add(col);
        }

        /// <summary>Overload for int[] options</summary>
        private void AddTemplateComboColumn(string header, string binding, double width,
            int[] options, SolidColorBrush groupBg)
        {
            AddTemplateComboColumn(header, binding, width,
                options.Select(o => (object)o.ToString()).ToArray(), groupBg);
        }


        /// <summary>
        /// Lấy giá trị cột ở dòng đầu, set cho tất cả dòng còn lại.
        /// </summary>
        private void ApplyFirstRowToAll(string propertyName)
        {
            if (Specs.Count == 0) return;

            var firstSpec = Specs[0];
            var prop = typeof(PanelSpec).GetProperty(propertyName);
            if (prop == null) return;

            var value = prop.GetValue(firstSpec);
            for (int i = 1; i < Specs.Count; i++)
            {
                prop.SetValue(Specs[i], value);
            }
            _dataGrid.Items.Refresh();

            MessageBox.Show($"Đã áp dụng \"{value}\" cho {Specs.Count - 1} dòng.", "Áp dụng tất cả",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        /// <summary>
        /// Clone một PanelSpec, copy toàn bộ properties, tự tăng số ở cuối Key.
        /// VD: Spec1 → Spec2,  NEW-9 → NEW-10
        /// </summary>
        private PanelSpec CloneAndIncrement(PanelSpec source)
        {
            if (source == null)
                return new PanelSpec { Key = "Spec1" };

            return new PanelSpec
            {
                Key                  = IncrementKey(source.Key),
                WallCodePrefix       = source.WallCodePrefix,
                Description          = source.Description,
                PanelWidth           = source.PanelWidth,
                PanelType            = source.PanelType,
                Density              = source.Density,
                Thickness            = source.Thickness,
                FireRating           = source.FireRating,
                FmApproved           = source.FmApproved,
                FacingColor          = source.FacingColor,
                TopFacing            = source.TopFacing,
                TopCoating           = source.TopCoating,
                TopSteelThickness    = source.TopSteelThickness,
                TopProfile           = source.TopProfile,
                BottomFacingColor    = source.BottomFacingColor,
                BottomFacing         = source.BottomFacing,
                BottomCoating        = source.BottomCoating,
                BottomSteelThickness = source.BottomSteelThickness,
                BottomProfile        = source.BottomProfile
            };
        }

        /// <summary>
        /// Tăng số cuối cùng trong chuỗi Key.
        /// "Spec1" → "Spec2", "NEW-10" → "NEW-11", "ABC" → "ABC1"
        /// </summary>
        private static string IncrementKey(string key)
        {
            if (string.IsNullOrEmpty(key)) return "Spec1";

            // Tìm số ở cuối chuỗi
            var match = System.Text.RegularExpressions.Regex.Match(key, @"(\d+)$");
            if (match.Success)
            {
                int num = int.Parse(match.Value) + 1;
                return key.Substring(0, match.Index) + num;
            }
            return key + "1";
        }
    }
}
