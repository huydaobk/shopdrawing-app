﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ShopDrawing.Plugin.Core;

namespace ShopDrawing.Plugin.UI
{
    public class ExternalTitleBlockMappingDialog : Window
    {
        private readonly Dictionary<string, string> _savedSelections;
        private readonly ComboBox _cbSource;
        private readonly StackPanel _mappingPanel;
        private Dictionary<string, ComboBox> _selectors = new(StringComparer.OrdinalIgnoreCase);

        public string SelectedSourceName { get; private set; } = "";
        public IReadOnlyList<LayoutTitleBlockAttributeMapping> AttributeMappings { get; private set; } = [];

        public ExternalTitleBlockMappingDialog(
            string dwgPath,
            IReadOnlyList<LayoutTitleBlockSource> sources,
            LayoutTitleBlockConfig? existingConfig = null)
        {
            _savedSelections = existingConfig?.AttributeMappings
                .ToDictionary(mapping => mapping.AttributeTag, mapping => mapping.PluginField, StringComparer.OrdinalIgnoreCase)
                ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            Title = "Ánh xạ title block ngoài";
            Width = 620;
            Height = 520;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = Brushes.WhiteSmoke;

            var root = new Grid { Margin = new Thickness(16) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var header = new TextBlock
            {
                Text = "Gán attribute trong DWG vào field của plugin",
                FontSize = 15,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(44, 62, 80)),
                Margin = new Thickness(0, 0, 0, 8)
            };
            Grid.SetRow(header, 0);
            root.Children.Add(header);

            var filePath = new TextBlock
            {
                Text = dwgPath,
                TextWrapping = TextWrapping.Wrap,
                Foreground = Brushes.DimGray,
                Margin = new Thickness(0, 0, 0, 10)
            };
            Grid.SetRow(filePath, 1);
            root.Children.Add(filePath);

            var sourceRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 0, 0, 10)
            };
            sourceRow.Children.Add(new TextBlock
            {
                Text = "Nguồn block:",
                Width = 90,
                VerticalAlignment = VerticalAlignment.Center,
                FontWeight = FontWeights.SemiBold
            });

            _cbSource = new ComboBox
            {
                Width = 260,
                DisplayMemberPath = nameof(LayoutTitleBlockSource.DisplayName),
                ItemsSource = sources.ToList()
            };
            _cbSource.SelectionChanged += (_, _) => RenderMappings();
            sourceRow.Children.Add(_cbSource);

            Grid.SetRow(sourceRow, 2);
            root.Children.Add(sourceRow);

            _mappingPanel = new StackPanel();
            var scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content = _mappingPanel
            };
            Grid.SetRow(scroll, 3);
            root.Children.Add(scroll);

            var footer = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 12, 0, 0)
            };

            var btnCancel = new Button
            {
                Content = "Hủy",
                Width = 90,
                Height = 32,
                Margin = new Thickness(0, 0, 8, 0)
            };
            btnCancel.Click += (_, _) => DialogResult = false;

            var btnSave = new Button
            {
                Content = "Lưu ánh xạ",
                Width = 120,
                Height = 32,
                IsDefault = true,
                Background = new SolidColorBrush(Color.FromRgb(33, 150, 243)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                FontWeight = FontWeights.SemiBold
            };
            btnSave.Click += (_, _) => SaveAndClose();

            footer.Children.Add(btnCancel);
            footer.Children.Add(btnSave);

            Grid.SetRow(footer, 4);
            root.Children.Add(footer);

            Content = root;
            UiText.NormalizeWindow(this);

            _cbSource.SelectedItem = _cbSource.Items
                .OfType<LayoutTitleBlockSource>()
                .FirstOrDefault(source =>
                    string.Equals(source.Name, existingConfig?.SourceName, StringComparison.OrdinalIgnoreCase))
                ?? _cbSource.Items.OfType<LayoutTitleBlockSource>().FirstOrDefault();
        }

        private void RenderMappings()
        {
            _mappingPanel.Children.Clear();
            _selectors = new Dictionary<string, ComboBox>(StringComparer.OrdinalIgnoreCase);

            if (_cbSource.SelectedItem is not LayoutTitleBlockSource source)
                return;

            foreach (string tag in source.AttributeTags)
            {
                var row = new Grid { Margin = new Thickness(0, 0, 0, 8) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(180) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                var tagLabel = new TextBlock
                {
                    Text = tag,
                    VerticalAlignment = VerticalAlignment.Center,
                    FontFamily = new FontFamily("Consolas"),
                    FontWeight = FontWeights.SemiBold
                };
                Grid.SetColumn(tagLabel, 0);
                row.Children.Add(tagLabel);

                var combo = new ComboBox
                {
                    ItemsSource = LayoutTitleBlockFields.Options,
                    DisplayMemberPath = nameof(LayoutTitleBlockFieldOption.Label),
                    SelectedValuePath = nameof(LayoutTitleBlockFieldOption.Value),
                    Margin = new Thickness(8, 0, 0, 0)
                };
                combo.SelectedValue = _savedSelections.TryGetValue(tag, out string? selectedField)
                    ? selectedField
                    : LayoutTitleBlockFields.None;

                Grid.SetColumn(combo, 1);
                row.Children.Add(combo);

                _selectors[tag] = combo;
                _mappingPanel.Children.Add(row);
            }
        }

        private void SaveAndClose()
        {
            if (_cbSource.SelectedItem is not LayoutTitleBlockSource source)
                return;

            var mappings = _selectors
                .Select(selector => new LayoutTitleBlockAttributeMapping(
                    selector.Key,
                    selector.Value.SelectedValue?.ToString() ?? LayoutTitleBlockFields.None))
                .Where(mapping => !string.IsNullOrWhiteSpace(mapping.PluginField))
                .ToList();

            if (mappings.Count == 0)
            {
                UiFeedback.ShowWarning("Cần gán ít nhất 1 attribute vào field của plugin.", "Ánh xạ chưa hợp lệ");
                return;
            }

            SelectedSourceName = source.Name;
            AttributeMappings = mappings;
            DialogResult = true;
        }
    }
}
