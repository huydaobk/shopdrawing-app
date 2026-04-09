﻿using System.Collections.Generic;

using System.Collections.ObjectModel;

using System.Linq;

using System.Windows;

using System.Windows.Controls;

using System.Windows.Controls.Primitives;

using System.Windows.Data;

using System.Windows.Media;

using ShopDrawing.Plugin.Core;

using ShopDrawing.Plugin.Models;



namespace ShopDrawing.Plugin.UI

{

    public class SpecManagerDialog : Window

    {

        private readonly SpecConfigManager _manager;

        private readonly DataGrid _dataGrid;

        public ObservableCollection<PanelSpec> Specs { get; set; }



        private static readonly SolidColorBrush TopHeaderBg = new SolidColorBrush(Color.FromRgb(41, 128, 185));

        private static readonly SolidColorBrush BottomHeaderBg = new SolidColorBrush(Color.FromRgb(39, 174, 96));



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

            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });



            var header = new TextBlock

            {

                Text = "Bảng quản lý specs",

                FontSize = 16,

                FontWeight = FontWeights.Bold,

                Foreground = new SolidColorBrush(Color.FromRgb(44, 62, 80)),

                Margin = new Thickness(0, 0, 0, 10)

            };

            Grid.SetRow(header, 0);

            mainGrid.Children.Add(header);



            var groupHeaderGrid = new Grid();

            var colLeft = new ColumnDefinition { Width = new GridLength(500) };

            var colTop = new ColumnDefinition { Width = new GridLength(301) };

            var colBottom = new ColumnDefinition { Width = new GridLength(246) };

            groupHeaderGrid.ColumnDefinitions.Add(colLeft);

            groupHeaderGrid.ColumnDefinitions.Add(colTop);

            groupHeaderGrid.ColumnDefinitions.Add(colBottom);



            var topLabel = new Border

            {

                Background = TopHeaderBg,

                CornerRadius = new CornerRadius(4),

                Padding = new Thickness(6, 4, 6, 4),

                Margin = new Thickness(1, 0, 1, 3)

            };

            topLabel.Child = new TextBlock

            {

                Text = "Mặt trên",

                FontWeight = FontWeights.Bold,

                FontSize = 12,

                Foreground = Brushes.White,

                HorizontalAlignment = HorizontalAlignment.Center

            };

            Grid.SetColumn(topLabel, 1);

            groupHeaderGrid.Children.Add(topLabel);



            var bottomLabel = new Border

            {

                Background = BottomHeaderBg,

                CornerRadius = new CornerRadius(4),

                Padding = new Thickness(6, 4, 6, 4),

                Margin = new Thickness(1, 0, 1, 3)

            };

            bottomLabel.Child = new TextBlock

            {

                Text = "Mặt dưới",

                FontWeight = FontWeights.Bold,

                FontSize = 12,

                Foreground = Brushes.White,

                HorizontalAlignment = HorizontalAlignment.Center

            };

            Grid.SetColumn(bottomLabel, 2);

            groupHeaderGrid.Children.Add(bottomLabel);



            Grid.SetRow(groupHeaderGrid, 1);

            mainGrid.Children.Add(groupHeaderGrid);



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



            AddTextColumn("Mã spec", "Key", 100, null);

            AddTemplateComboColumn("Khổ tấm", "PanelWidth", 75, PanelSpec.PanelWidthOptions, null);



            AddTemplateComboColumn("Loại panel", "PanelType", 85, PanelSpec.PanelTypeOptions, null);

            AddTextColumn("Tỷ trọng", "Density", 55, null);

            AddTemplateComboColumn("Chiều dày", "Thickness", 70, PanelSpec.ThicknessOptions, null);

            AddTextColumn("Chống cháy", "FireRating", 62, null);

            AddCheckColumn("FM", "FmApproved", 33);



            AddTextColumn("Màu sắc", "FacingColor", 55, TopHeaderBg);

            AddTextColumn("Vật liệu", "TopFacing", 55, TopHeaderBg);

            AddTextColumn("Độ mạ", "TopCoating", 55, TopHeaderBg);

            AddTextColumn("Dày tôn", "TopSteelThickness", 55, TopHeaderBg);

            AddTemplateComboColumn("Profile", "TopProfile", 78, PanelSpec.ProfileOptions, TopHeaderBg);



            AddTextColumn("Màu sắc", "BottomFacingColor", 55, BottomHeaderBg);

            AddTextColumn("Vật liệu", "BottomFacing", 55, BottomHeaderBg);

            AddTextColumn("Độ mạ", "BottomCoating", 55, BottomHeaderBg);

            AddTextColumn("Dày tôn", "BottomSteelThickness", 55, BottomHeaderBg);

            AddTemplateComboColumn("Profile", "BottomProfile", 78, PanelSpec.ProfileOptions, BottomHeaderBg);



            _dataGrid.LayoutUpdated += (_, _) =>

            {

                try

                {

                    double leftW = 0;

                    for (int i = 0; i < 7 && i < _dataGrid.Columns.Count; i++)

                    {

                        leftW += _dataGrid.Columns[i].ActualWidth;

                    }



                    colLeft.Width = new GridLength(leftW);



                    double topW = 0;

                    for (int i = 7; i < 12 && i < _dataGrid.Columns.Count; i++)

                    {

                        topW += _dataGrid.Columns[i].ActualWidth;

                    }



                    colTop.Width = new GridLength(topW);



                    double bottomW = 0;

                    for (int i = 12; i < _dataGrid.Columns.Count; i++)

                    {

                        bottomW += _dataGrid.Columns[i].ActualWidth;

                    }



                    colBottom.Width = new GridLength(bottomW);

                }

                catch (System.Exception ex)

            {

                ShopDrawing.Plugin.Core.PluginLogger.Error("Suppressed exception in SpecManagerDialog.cs", ex);

            }

            };



            Grid.SetRow(_dataGrid, 2);

            mainGrid.Children.Add(_dataGrid);



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

                Content = "Thêm spec",

                Width = 110,

                Height = 35,

                Margin = new Thickness(0, 0, 8, 0),

                Background = new SolidColorBrush(Color.FromRgb(52, 152, 219)),

                Foreground = Brushes.White,

                FontWeight = FontWeights.SemiBold,

                ToolTip = "Chọn 1 dòng để nhân bản và tăng số Key. Không chọn thì tạo dòng trống."

            };

            btnAdd.Click += (_, _) =>

            {

                PanelSpec? source = _dataGrid.SelectedItem as PanelSpec ?? (Specs.Count > 0 ? Specs[Specs.Count - 1] : null);

                var newSpec = CloneAndIncrement(source);

                Specs.Add(newSpec);

                _dataGrid.ScrollIntoView(newSpec);

                _dataGrid.SelectedItem = newSpec;

            };



            var btnBatchAdd = new Button

            {

                Content = "Nhân bản xN",

                Width = 115,

                Height = 35,

                Margin = new Thickness(0, 0, 8, 0),

                Background = new SolidColorBrush(Color.FromRgb(142, 68, 173)),

                Foreground = Brushes.White,

                FontWeight = FontWeights.SemiBold,

                ToolTip = "Chọn 1 dòng để tạo N bản sao với Key tăng dần"

            };

            btnBatchAdd.Click += (_, _) =>

            {

                PanelSpec? source = _dataGrid.SelectedItem as PanelSpec ?? (Specs.Count > 0 ? Specs[Specs.Count - 1] : null);



                var dlg = new Window

                {

                Title = "Nhân bản spec",

                    Width = 320,

                    Height = 200,

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

                    Content = "OK",

                    Width = 60,

                    Height = 28,

                    Margin = new Thickness(0, 12, 0, 0),

                    HorizontalAlignment = HorizontalAlignment.Left,

                    IsDefault = true

                };

                btnOk.Click += (_, _) => { dlg.DialogResult = true; };

                sp.Children.Add(btnOk);

                dlg.Content = sp;



                if (dlg.ShowDialog() == true && int.TryParse(txtCount.Text, out int count) && count > 0 && count <= 50)

                {

                    PanelSpec lastClone = CloneAndIncrement(source);

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

                Content = "Xóa",

                Width = 80,

                Height = 35,

                Margin = new Thickness(0, 0, 20, 0),

                Background = new SolidColorBrush(Color.FromRgb(231, 76, 60)),

                Foreground = Brushes.White,

                FontWeight = FontWeights.SemiBold

            };

            btnDelete.Click += (_, _) =>

            {

                var toRemove = _dataGrid.SelectedItems.Cast<PanelSpec>().ToList();

                if (toRemove.Count == 0)

                {

                    return;

                }



            if (UiFeedback.AskYesNo($"Xóa {toRemove.Count} dòng?", "Xác nhận") == MessageBoxResult.Yes)

                {

                    foreach (var spec in toRemove)

                    {

                        Specs.Remove(spec);

                    }

                }

            };



            var btnSave = new Button

            {

                Content = "Lưu và đóng",

                Width = 120,

                Height = 35,

                Margin = new Thickness(0, 0, 8, 0),

                Background = new SolidColorBrush(Color.FromRgb(44, 62, 80)),

                Foreground = Brushes.White,

                FontWeight = FontWeights.SemiBold

            };

            btnSave.Click += (_, _) =>

            {

                _manager.Save(new List<PanelSpec>(Specs));

                DialogResult = true;

            };



            var btnCancel = new Button { Content = "Hủy", Width = 80, Height = 35 };

            btnCancel.Click += (_, _) => { DialogResult = false; };



            footer.Children.Add(btnAdd);

            footer.Children.Add(btnBatchAdd);

            footer.Children.Add(btnDelete);

            footer.Children.Add(btnSave);

            footer.Children.Add(btnCancel);



            Content = mainGrid;

            UiText.NormalizeWindow(this);

        }



        private Style CreateHeaderStyle(string header, string binding, SolidColorBrush? groupBg)

        {

            var style = new Style(typeof(DataGridColumnHeader));

            style.Setters.Add(new Setter(DataGridColumnHeader.ToolTipProperty,

                        "Chuột phải để áp dụng giá trị dòng đầu cho tất cả"));



            if (groupBg != null)

            {

                style.Setters.Add(new Setter(DataGridColumnHeader.BackgroundProperty, groupBg));

                style.Setters.Add(new Setter(DataGridColumnHeader.ForegroundProperty, Brushes.White));

                style.Setters.Add(new Setter(DataGridColumnHeader.FontWeightProperty, FontWeights.SemiBold));

            }



            var menu = new ContextMenu();

                var menuItem = new MenuItem { Header = $"Áp dụng \"{header}\" dòng đầu cho tất cả" };

            menuItem.Click += (_, _) => ApplyFirstRowToAll(binding);

            menu.Items.Add(menuItem);



            style.Setters.Add(new Setter(FrameworkElement.ContextMenuProperty, menu));

            return style;

        }



        private void AddTextColumn(string header, string binding, double width, SolidColorBrush? groupBg)

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



        private void AddTemplateComboColumn(string header, string binding, double width, object[] options, SolidColorBrush? groupBg)

        {

            var col = new DataGridTemplateColumn

            {

                Header = header,

                Width = width

            };



            var displayFactory = new FrameworkElementFactory(typeof(TextBlock));

            displayFactory.SetBinding(TextBlock.TextProperty, new Binding(binding));

            displayFactory.SetValue(TextBlock.MarginProperty, new Thickness(4, 2, 4, 2));

            col.CellTemplate = new DataTemplate { VisualTree = displayFactory };



            var editFactory = new FrameworkElementFactory(typeof(ComboBox));

            editFactory.SetBinding(ComboBox.TextProperty, new Binding(binding) { UpdateSourceTrigger = UpdateSourceTrigger.LostFocus });

            editFactory.SetValue(ComboBox.IsEditableProperty, true);

            editFactory.SetValue(ComboBox.ItemsSourceProperty, options);

            editFactory.SetValue(ComboBox.IsDropDownOpenProperty, true);

            col.CellEditingTemplate = new DataTemplate { VisualTree = editFactory };



            col.HeaderStyle = CreateHeaderStyle(header, binding, groupBg);

            _dataGrid.Columns.Add(col);

        }



        private void AddTemplateComboColumn(string header, string binding, double width, int[] options, SolidColorBrush? groupBg)

        {

            AddTemplateComboColumn(header, binding, width, options.Select(o => (object)o.ToString()).ToArray(), groupBg);

        }



        private void ApplyFirstRowToAll(string propertyName)

        {

            if (Specs.Count == 0)

            {

                return;

            }



            var firstSpec = Specs[0];

            var prop = typeof(PanelSpec).GetProperty(propertyName);

            if (prop == null)

            {

                return;

            }



            var value = prop.GetValue(firstSpec);

            for (int i = 1; i < Specs.Count; i++)

            {

                prop.SetValue(Specs[i], value);

            }



            _dataGrid.Items.Refresh();

            UiFeedback.ShowInfo($"Đã áp dụng \"{value}\" cho {Specs.Count - 1} dòng.", "Áp dụng tất cả");

        }



        private PanelSpec CloneAndIncrement(PanelSpec? source)

        {

            if (source == null)

            {

                return new PanelSpec { Key = "Spec1" };

            }



            return new PanelSpec

            {

                Key = IncrementKey(source.Key),

                WallCodePrefix = source.WallCodePrefix,

                Description = source.Description,

                PanelWidth = source.PanelWidth,

                PanelType = source.PanelType,

                Density = source.Density,

                Thickness = source.Thickness,

                FireRating = source.FireRating,

                FmApproved = source.FmApproved,

                FacingColor = source.FacingColor,

                TopFacing = source.TopFacing,

                TopCoating = source.TopCoating,

                TopSteelThickness = source.TopSteelThickness,

                TopProfile = source.TopProfile,

                BottomFacingColor = source.BottomFacingColor,

                BottomFacing = source.BottomFacing,

                BottomCoating = source.BottomCoating,

                BottomSteelThickness = source.BottomSteelThickness,

                BottomProfile = source.BottomProfile

            };

        }



        private static string IncrementKey(string key)

        {

            if (string.IsNullOrEmpty(key))

            {

                return "Spec1";

            }



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

