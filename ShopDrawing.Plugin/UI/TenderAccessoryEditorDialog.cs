﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using ShopDrawing.Plugin.Core;
using ShopDrawing.Plugin.Models;

namespace ShopDrawing.Plugin.UI
{
    public class TenderAccessoryEditorDialog : Window
    {
        private readonly ObservableCollection<TenderAccessoryRow> _rows;
        private readonly string[] _specOptions;
        private readonly string[] _categoryOptions;
        private readonly TenderAccessoryRules.RuleOption[] _ruleOptions;
        private DataGrid _grid = null!;

        public TenderAccessoryEditorDialog(IEnumerable<TenderAccessory> accessories, IEnumerable<string> specKeys)
        {
            Title = "Cấu hình phụ kiện đấu thầu";
            Width = 1460;
            Height = 760;
            MinWidth = 1320;
            MinHeight = 680;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = Brushes.WhiteSmoke;

            _specOptions = new[] { "Tất cả" }
                .Concat(specKeys.Where(key => !string.IsNullOrWhiteSpace(key)).Distinct().OrderBy(key => key))
                .ToArray();

            _categoryOptions = TenderAccessory.CategoryScopeOptions
                .Where(option => !TenderAccessoryRules.IsAllScope(option))
                .ToArray();
            _ruleOptions = TenderAccessoryRules.GetRuleOptions().ToArray();

            _rows = new ObservableCollection<TenderAccessoryRow>(
                AccessoryDataManager.NormalizeConfiguredAccessories(accessories)
                    .Select(TenderAccessoryRow.FromModel));

            if (_rows.Count == 0)
            {
                foreach (var item in AccessoryDataManager.GetDefaults())
                {
                    _rows.Add(TenderAccessoryRow.FromModel(item));
                }
            }

            SortRowsByApplication();
            ReindexRows();

            Content = BuildLayout();
            UiText.NormalizeWindow(this);
        }

        public List<TenderAccessory> GetAccessories()
        {
            return _rows
                .Select(row => row.ToModel())
                .Where(row => !string.IsNullOrWhiteSpace(row.Name))
                .OrderBy(row => row, Comparer<TenderAccessory>.Create(CompareAccessories))
                .ToList();
        }

        private UIElement BuildLayout()
        {
            var root = new Grid { Margin = new Thickness(12) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var header = new TextBlock
            {
                Text = "CẤU HÌNH PHỤ KIỆN ĐẤU THẦU",
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(44, 62, 80)),
                Margin = new Thickness(0, 0, 0, 6)
            };
            Grid.SetRow(header, 0);
            root.Children.Add(header);

            var info = new TextBlock
            {
                Text = "Danh mục này dùng để tính bảng phụ kiện cho giai đoạn đấu thầu. " +
                       "Khối lượng chốt = tự động x (1 + hao hụt %) + điều chỉnh.",
                Foreground = Brushes.DimGray,
                Margin = new Thickness(0, 0, 0, 8)
            };
            Grid.SetRow(info, 1);
            root.Children.Add(info);

            var main = new Grid();
            main.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            main.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            Grid.SetRow(main, 2);
            root.Children.Add(main);

            var toolbar = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 0, 0, 8)
            };
            toolbar.Children.Add(CreateButton("Thêm dòng", new SolidColorBrush(Color.FromRgb(39, 174, 96)), OnAddRow));
            toolbar.Children.Add(CreateButton("Xóa dòng", new SolidColorBrush(Color.FromRgb(231, 76, 60)), OnDeleteRows));
            toolbar.Children.Add(CreateButton("Khôi phục mặc định", new SolidColorBrush(Color.FromRgb(52, 152, 219)), OnResetDefaults));
            Grid.SetRow(toolbar, 0);
            main.Children.Add(toolbar);

            _grid = new DataGrid
            {
                AutoGenerateColumns = false,
                CanUserAddRows = false,
                CanUserDeleteRows = false,
                HeadersVisibility = DataGridHeadersVisibility.Column,
                AlternatingRowBackground = new SolidColorBrush(Color.FromRgb(248, 250, 255)),
                SelectionMode = DataGridSelectionMode.Extended,
                SelectionUnit = DataGridSelectionUnit.FullRow,
                FrozenColumnCount = 4,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                ItemsSource = _rows
            };
            _grid.CellEditEnding += OnGridCellEditEnding;
            _grid.Columns.Clear();
            _grid.Columns.Add(CreateTextColumn("STT", "Index", 45));
            _grid.Columns.Add(CreateComboColumn("Hạng mục", "CategoryScope", _categoryOptions, 95));
            _grid.Columns.Add(CreateComboColumn("Ứng dụng", "Application", TenderWall.ApplicationOptions, 105));
            _grid.Columns.Add(CreateComboColumn("Mã spec", "SpecKey", _specOptions, 110));
            _grid.Columns.Add(CreateTextColumn("Tên phụ kiện", "Name", 180));
            _grid.Columns.Add(CreateTextColumn("Vật liệu", "Material", 95));
            _grid.Columns.Add(CreateTextColumn("Vị trí", "Position", 130));
            _grid.Columns.Add(CreateComboColumn("Đơn vị", "Unit", TenderAccessory.UnitOptions, 75));
            _grid.Columns.Add(CreateRuleComboColumn("Mã quy tắc", "CalcRule", _ruleOptions, 240));
            _grid.Columns.Add(CreateReadOnlyColumn("Diễn giải", "RuleDescription", 220));
            _grid.Columns.Add(CreateTextColumn("Hệ số", "Factor", 65, "F2"));
            _grid.Columns.Add(CreateTextColumn("Hao hụt (%)", "WasteFactor", 80, "F1"));
            _grid.Columns.Add(CreateTextColumn("Điều chỉnh", "Adjustment", 80, "F2"));
            _grid.Columns.Add(CreateCheckColumn("Nhập tay", "IsManualOnly", 65));
            _grid.Columns.Add(CreateTextColumn("Ghi chú", "Note", 340));

            Grid.SetRow(_grid, 1);
            main.Children.Add(_grid);

            var footer = new DockPanel { Margin = new Thickness(0, 10, 0, 0) };
            var left = new TextBlock
            {
                Text = "Mỗi dòng phụ kiện nên được gán rõ theo Hạng mục và Ứng dụng để bóc tách đúng.",
                Foreground = Brushes.DimGray,
                VerticalAlignment = VerticalAlignment.Center
            };
            DockPanel.SetDock(left, Dock.Left);
            footer.Children.Add(left);

            var actions = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            var btnCancel = CreateButton("Hủy", new SolidColorBrush(Color.FromRgb(149, 165, 166)), (_, _) => { DialogResult = false; });
            btnCancel.Width = 90;
            actions.Children.Add(btnCancel);

            var btnSave = CreateButton("Lưu", new SolidColorBrush(Color.FromRgb(41, 128, 185)), (_, _) => { SaveAndClose(); });
            btnSave.Width = 90;
            actions.Children.Add(btnSave);
            DockPanel.SetDock(actions, Dock.Right);
            footer.Children.Add(actions);

            Grid.SetRow(footer, 3);
            root.Children.Add(footer);
            return root;
        }

        private void OnAddRow(object? sender, RoutedEventArgs e)
        {
            _rows.Add(new TenderAccessoryRow
            {
                Index = _rows.Count + 1,
                CategoryScope = AccessoryDataManager.DefaultCategoryScope,
                Application = AccessoryDataManager.DefaultApplication,
                SpecKey = "Tất cả",
                Unit = "md",
                CalcRule = AccessoryCalcRule.PER_WALL_LENGTH,
                Factor = 1,
                Note = string.Empty
            });

            SortRowsByApplication();
            ReindexRows();
        }

        private void OnDeleteRows(object? sender, RoutedEventArgs e)
        {
            var selected = _grid.SelectedItems.Cast<TenderAccessoryRow>().ToList();
            if (selected.Count == 0)
            {
                return;
            }

            foreach (var row in selected)
            {
                _rows.Remove(row);
            }

            SortRowsByApplication();
            ReindexRows();
        }

        private void OnResetDefaults(object? sender, RoutedEventArgs e)
        {
            var confirm = UiFeedback.AskYesNo("Khôi phục danh mục phụ kiện mặc định cho dự án này?", "Khôi phục mặc định");

            if (confirm != MessageBoxResult.Yes)
            {
                return;
            }

            _rows.Clear();
            foreach (var item in AccessoryDataManager.GetDefaults())
            {
                _rows.Add(TenderAccessoryRow.FromModel(item));
            }

            SortRowsByApplication();
            ReindexRows();
        }

        private void OnGridCellEditEnding(object? sender, DataGridCellEditEndingEventArgs e)
        {
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, new Action(() =>
            {
                SortRowsByApplication();
                foreach (var row in _rows)
                {
                    row.RefreshDescriptions();
                }

                ReindexRows();
                _grid.Items.Refresh();
            }));
        }

        private void SaveAndClose()
        {
            SortRowsByApplication();
            ReindexRows();
            DialogResult = true;
        }

        private void SortRowsByApplication()
        {
            var sorted = _rows.OrderBy(row => row, Comparer<TenderAccessoryRow>.Create(CompareRows)).ToList();
            _rows.Clear();
            foreach (var row in sorted)
            {
                _rows.Add(row);
            }
        }

        private void ReindexRows()
        {
            for (int i = 0; i < _rows.Count; i++)
            {
                _rows[i].Index = i + 1;
            }
        }

        private static int CompareRows(TenderAccessoryRow left, TenderAccessoryRow right)
        {
            int result = TenderAccessoryRules.CompareApplications(left.Application, right.Application);
            if (result != 0)
            {
                return result;
            }

            result = TenderAccessoryRules.CompareScopes(left.CategoryScope, right.CategoryScope);
            if (result != 0)
            {
                return result;
            }

            result = TenderAccessoryRules.CompareScopes(left.SpecKey, right.SpecKey);
            if (result != 0)
            {
                return result;
            }

            result = string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);
            if (result != 0)
            {
                return result;
            }

            return string.Compare(left.Position, right.Position, StringComparison.OrdinalIgnoreCase);
        }

        private static int CompareAccessories(TenderAccessory left, TenderAccessory right)
        {
            int result = TenderAccessoryRules.CompareApplications(left.Application, right.Application);
            if (result != 0)
            {
                return result;
            }

            result = TenderAccessoryRules.CompareScopes(left.CategoryScope, right.CategoryScope);
            if (result != 0)
            {
                return result;
            }

            result = TenderAccessoryRules.CompareScopes(left.SpecKey, right.SpecKey);
            if (result != 0)
            {
                return result;
            }

            result = string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);
            if (result != 0)
            {
                return result;
            }

            return string.Compare(left.Position, right.Position, StringComparison.OrdinalIgnoreCase);
        }

        private static Button CreateButton(string text, Brush background, RoutedEventHandler onClick)
        {
            var button = new Button
            {
                Content = text,
                Background = background,
                Foreground = Brushes.White,
                FontWeight = FontWeights.SemiBold,
                Height = 30,
                Padding = new Thickness(10, 2, 10, 2),
                Margin = new Thickness(0, 0, 6, 0),
                BorderThickness = new Thickness(0)
            };
            button.Click += onClick;
            return button;
        }

        private static DataGridTextColumn CreateTextColumn(string header, string binding, double width, string? format = null)
        {
            return new DataGridTextColumn
            {
                Header = header,
                Binding = new Binding(binding) { StringFormat = format, UpdateSourceTrigger = UpdateSourceTrigger.LostFocus },
                Width = new DataGridLength(width)
            };
        }

        private static DataGridTextColumn CreateReadOnlyColumn(string header, string binding, double width)
        {
            return new DataGridTextColumn
            {
                Header = header,
                Binding = new Binding(binding),
                Width = new DataGridLength(width),
                IsReadOnly = true
            };
        }

        private static DataGridComboBoxColumn CreateComboColumn(string header, string binding, IEnumerable<string> items, double width)
        {
            return new DataGridComboBoxColumn
            {
                Header = header,
                SelectedItemBinding = new Binding(binding) { UpdateSourceTrigger = UpdateSourceTrigger.LostFocus },
                ItemsSource = items.ToArray(),
                Width = new DataGridLength(width)
            };
        }

        private static DataGridComboBoxColumn CreateRuleComboColumn(string header, string binding, IEnumerable<TenderAccessoryRules.RuleOption> items, double width)
        {
            return new DataGridComboBoxColumn
            {
                Header = header,
                SelectedValueBinding = new Binding(binding) { UpdateSourceTrigger = UpdateSourceTrigger.LostFocus },
                SelectedValuePath = nameof(TenderAccessoryRules.RuleOption.Value),
                DisplayMemberPath = nameof(TenderAccessoryRules.RuleOption.Label),
                ItemsSource = items.ToArray(),
                Width = new DataGridLength(width)
            };
        }

        private static DataGridComboBoxColumn CreateEnumComboColumn(string header, string binding, Type enumType, double width)
        {
            return new DataGridComboBoxColumn
            {
                Header = header,
                SelectedItemBinding = new Binding(binding) { UpdateSourceTrigger = UpdateSourceTrigger.LostFocus },
                ItemsSource = Enum.GetValues(enumType),
                Width = new DataGridLength(width)
            };
        }

        private static DataGridCheckBoxColumn CreateCheckColumn(string header, string binding, double width)
        {
            return new DataGridCheckBoxColumn
            {
                Header = header,
                Binding = new Binding(binding) { UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged },
                Width = new DataGridLength(width)
            };
        }
    }

    public class TenderAccessoryRow : INotifyPropertyChanged
    {
        public int Index { get; set; }
        public string CategoryScope { get; set; } = "Tất cả";
        public string Application { get; set; } = "Tất cả";
        public string SpecKey { get; set; } = "Tất cả";
        public string Name { get; set; } = string.Empty;
        public string Material { get; set; } = string.Empty;
        public string Position { get; set; } = string.Empty;
        public string Unit { get; set; } = "md";
        public AccessoryCalcRule CalcRule { get; set; }
        public double Factor { get; set; } = 1.0;
        public double WasteFactor { get; set; }
        public double Adjustment { get; set; }
        public bool IsManualOnly { get; set; }
        public string Note { get; set; } = string.Empty;

        public string RuleDescription => TenderAccessoryRules.GetRuleLabel(CalcRule);

        public static TenderAccessoryRow FromModel(TenderAccessory accessory)
        {
            return new TenderAccessoryRow
            {
                CategoryScope = TenderAccessoryRules.NormalizeScope(accessory.CategoryScope),
                Application = TenderAccessoryRules.NormalizeScope(accessory.Application),
                SpecKey = TenderAccessoryRules.NormalizeScope(accessory.SpecKey),
                Name = accessory.Name,
                Material = accessory.Material,
                Position = accessory.Position,
                Unit = accessory.Unit,
                CalcRule = accessory.CalcRule,
                Factor = accessory.Factor,
                WasteFactor = accessory.WasteFactor,
                Adjustment = accessory.Adjustment,
                IsManualOnly = accessory.IsManualOnly,
                Note = accessory.Note
            };
        }

        public TenderAccessory ToModel()
        {
            return new TenderAccessory
            {
                CategoryScope = TenderAccessoryRules.NormalizeScope(CategoryScope),
                Application = TenderAccessoryRules.NormalizeScope(Application),
                SpecKey = TenderAccessoryRules.NormalizeScope(SpecKey),
                Name = Name.Trim(),
                Material = Material?.Trim() ?? string.Empty,
                Position = Position?.Trim() ?? string.Empty,
                Unit = string.IsNullOrWhiteSpace(Unit) ? "md" : Unit.Trim(),
                CalcRule = CalcRule,
                Factor = Factor,
                WasteFactor = WasteFactor,
                Adjustment = Adjustment,
                IsManualOnly = IsManualOnly,
                Note = Note?.Trim() ?? string.Empty
            };
        }

        public void RefreshDescriptions()
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RuleDescription)));
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
