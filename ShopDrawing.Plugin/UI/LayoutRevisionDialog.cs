﻿using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ShopDrawing.Plugin.UI
{
    public class LayoutRevisionDialog : Window
    {
        private readonly TextBox _txtContent;

        public string RevisionContent => _txtContent.Text.Trim();

        public LayoutRevisionDialog(int newCount, int updateCount, string initialContent = "")
        {
            Title = updateCount > 0 ? "Cập nhật revision" : "Tạo layout";
            Width = 460;
            Height = 300;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = Brushes.WhiteSmoke;

            var root = new Grid { Margin = new Thickness(18) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var title = new TextBlock
            {
                Text = "Nội dung revision cho đợt tạo layout này",
                FontSize = 15,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(44, 62, 80)),
                Margin = new Thickness(0, 0, 0, 8)
            };
            Grid.SetRow(title, 0);
            root.Children.Add(title);

            var summary = new TextBlock
            {
                Text = BuildSummary(newCount, updateCount),
                TextWrapping = TextWrapping.Wrap,
                Foreground = Brushes.DimGray,
                Margin = new Thickness(0, 0, 0, 10)
            };
            Grid.SetRow(summary, 1);
            root.Children.Add(summary);

            _txtContent = new TextBox
            {
                Text = initialContent,
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Padding = new Thickness(8),
                MinHeight = 130
            };
            Grid.SetRow(_txtContent, 2);
            root.Children.Add(_txtContent);

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

            var btnOk = new Button
            {
                Content = "Xác nhận",
                Width = 100,
                Height = 32,
                IsDefault = true,
                Background = new SolidColorBrush(Color.FromRgb(76, 175, 80)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                FontWeight = FontWeights.SemiBold
            };
            btnOk.Click += (_, _) => DialogResult = true;

            footer.Children.Add(btnCancel);
            footer.Children.Add(btnOk);

            Grid.SetRow(footer, 3);
            root.Children.Add(footer);

            Content = root;
            UiText.NormalizeWindow(this);
        }

        private static string BuildSummary(int newCount, int updateCount)
        {
            if (updateCount > 0 && newCount > 0)
                return $"Sẽ cập nhật revision cho {updateCount} layout đã tồn tại và tạo mới {newCount} layout. Nội dung này sẽ được đưa vào title block và DMBV.";

            if (updateCount > 0)
                return $"Sẽ cập nhật revision cho {updateCount} layout đã tồn tại. Nội dung này sẽ được đưa vào title block và DMBV.";

            return $"Sẽ tạo mới {newCount} layout với revision A. Nội dung này sẽ được đưa vào title block và DMBV.";
        }
    }
}
