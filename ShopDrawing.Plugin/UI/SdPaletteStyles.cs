using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace ShopDrawing.Plugin.UI
{
    /// <summary>
    /// Shared visual constants & factory methods for unified palette UI.
    /// All palettes use: dark theme, Segoe UI, #007ACC accent.
    /// Matches AutoCAD 2026 dark workspace.
    /// </summary>
    internal static class SdPaletteStyles
    {
        // ═══ Colors ═══
        public static readonly Color BgPrimary     = Color.FromRgb(45, 45, 48);    // #2D2D30
        public static readonly Color BgSection     = Color.FromRgb(37, 37, 38);    // #252526
        public static readonly Color BorderColor   = Color.FromRgb(62, 62, 66);    // #3E3E42
        public static readonly Color AccentBlue    = Color.FromRgb(0, 122, 204);   // #007ACC
        public static readonly Color AccentGreen   = Color.FromRgb(76, 175, 80);   // #4CAF50
        public static readonly Color AccentRed     = Color.FromRgb(229, 57, 53);   // #E53935
        public static readonly Color AccentOrange  = Color.FromRgb(255, 152, 0);   // #FF9800
        public static readonly Color TextPrimary   = Color.FromRgb(224, 224, 224); // #E0E0E0
        public static readonly Color TextSecondary = Color.FromRgb(153, 153, 153); // #999999
        public static readonly Color TextMuted     = Color.FromRgb(120, 120, 120); // #787878
        public static readonly Color BtnDefault    = Color.FromRgb(63, 63, 70);    // #3F3F46
        public static readonly Color BtnHover      = Color.FromRgb(28, 151, 234);  // #1C97EA

        // ═══ Brushes (cached) ═══
        public static readonly Brush BgPrimaryBrush    = Freeze(new SolidColorBrush(BgPrimary));
        public static readonly Brush BgSectionBrush    = Freeze(new SolidColorBrush(BgSection));
        public static readonly Brush BorderBrush       = Freeze(new SolidColorBrush(BorderColor));
        public static readonly Brush AccentBlueBrush   = Freeze(new SolidColorBrush(AccentBlue));
        public static readonly Brush AccentGreenBrush  = Freeze(new SolidColorBrush(AccentGreen));
        public static readonly Brush AccentRedBrush    = Freeze(new SolidColorBrush(AccentRed));
        public static readonly Brush AccentOrangeBrush = Freeze(new SolidColorBrush(AccentOrange));
        public static readonly Brush TextPrimaryBrush  = Freeze(new SolidColorBrush(TextPrimary));
        public static readonly Brush TextSecondaryBrush= Freeze(new SolidColorBrush(TextSecondary));
        public static readonly Brush TextMutedBrush    = Freeze(new SolidColorBrush(TextMuted));
        public static readonly Brush BtnDefaultBrush   = Freeze(new SolidColorBrush(BtnDefault));
        public static readonly Brush TransparentBrush  = Brushes.Transparent;

        // ═══ Font ═══
        public static readonly FontFamily Font = new("Segoe UI");
        public const double FontSizeNormal = 12;
        public const double FontSizeSmall  = 11;
        public const double FontSizeHeader = 14;

        // ═══ Factory: Palette Header ═══
        public static Label CreateHeader(string title)
        {
            return new Label
            {
                Content    = title,
                FontWeight = FontWeights.Bold,
                FontSize   = FontSizeHeader,
                FontFamily = Font,
                Foreground = AccentBlueBrush,
                Padding    = new Thickness(0),
                Margin     = new Thickness(0, 0, 0, 10)
            };
        }

        // ═══ Factory: Section Header ═══
        public static TextBlock CreateSectionHeader(string title)
        {
            return new TextBlock
            {
                Text       = title,
                FontWeight = FontWeights.SemiBold,
                FontSize   = FontSizeSmall,
                FontFamily = Font,
                Foreground = TextSecondaryBrush,
                Margin     = new Thickness(0, 0, 0, 4)
            };
        }

        // ═══ Factory: Section Border ═══
        public static Border CreateSectionBorder(UIElement content)
        {
            return new Border
            {
                BorderBrush     = BorderBrush,
                BorderThickness = new Thickness(1),
                CornerRadius    = new CornerRadius(4),
                Background      = BgSectionBrush,
                Padding         = new Thickness(10),
                Margin          = new Thickness(0, 0, 0, 8),
                Child           = content
            };
        }

        // ═══ Factory: Label ═══
        public static TextBlock CreateLabel(string text)
        {
            return new TextBlock
            {
                Text       = text,
                FontFamily = Font,
                FontSize   = FontSizeNormal,
                Foreground = TextPrimaryBrush,
                Margin     = new Thickness(0, 0, 0, 2)
            };
        }

        // ═══ Factory: Primary Action Button ═══
        public static Button CreateActionButton(string text, Brush? bg = null, Brush? fg = null)
        {
            return new Button
            {
                Content         = text,
                Background      = bg ?? AccentBlueBrush,
                Foreground      = fg ?? Brushes.White,
                FontWeight      = FontWeights.SemiBold,
                FontSize        = FontSizeNormal,
                FontFamily      = Font,
                Height          = 32,
                Padding         = new Thickness(12, 4, 12, 4),
                Margin          = new Thickness(0, 4, 0, 4),
                BorderThickness = new Thickness(0),
                Cursor          = System.Windows.Input.Cursors.Hand
            };
        }

        // ═══ Factory: Secondary Button (outlined) ═══
        public static Button CreateOutlineButton(string text)
        {
            return new Button
            {
                Content         = text,
                Background      = TransparentBrush,
                Foreground      = TextPrimaryBrush,
                FontSize        = FontSizeNormal,
                FontFamily      = Font,
                Height          = 28,
                Padding         = new Thickness(8, 3, 8, 3),
                Margin          = new Thickness(0, 2, 0, 2),
                BorderBrush     = BorderBrush,
                BorderThickness = new Thickness(1),
                Cursor          = System.Windows.Input.Cursors.Hand
            };
        }

        // ═══ Factory: Compact Button ═══
        public static Button CreateCompactButton(string text, Brush? bg = null, Brush? fg = null)
        {
            return new Button
            {
                Content         = text,
                Background      = bg ?? BtnDefaultBrush,
                Foreground      = fg ?? Brushes.White,
                FontSize        = FontSizeNormal,
                FontFamily      = Font,
                Height          = 26,
                Padding         = new Thickness(8, 2, 8, 2),
                Margin          = new Thickness(0, 2, 0, 2),
                BorderThickness = new Thickness(0),
                Cursor          = System.Windows.Input.Cursors.Hand
            };
        }

        // ═══ Factory: Status TextBlock ═══
        public static TextBlock CreateStatusText()
        {
            return new TextBlock
            {
                Text         = "",
                TextWrapping = TextWrapping.Wrap,
                FontFamily   = Font,
                FontSize     = FontSizeSmall,
                Foreground   = TextSecondaryBrush,
                Margin       = new Thickness(0, 6, 0, 0)
            };
        }

        // ═══ Factory: Separator ═══
        public static Separator CreateSeparator()
        {
            return new Separator
            {
                Background = BorderBrush,
                Margin     = new Thickness(0, 6, 0, 6)
            };
        }

        // ═══ Apply global dark theme to palette root ═══
        public static ScrollViewer WrapInScrollViewer(StackPanel content)
        {
            return new ScrollViewer
            {
                VerticalScrollBarVisibility   = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Content = content
            };
        }

        // ═══ Factory: Dark DataGrid Style ═══
        /// <summary>
        /// Apply dark theme to a DataGrid — background, cell, row, header, selection styles.
        /// Call after setting columns and before displaying.
        /// </summary>
        public static void ApplyDarkTheme(DataGrid grid)
        {
            grid.Background = BgSectionBrush;
            grid.Foreground = TextPrimaryBrush;
            grid.FontFamily = Font;
            grid.FontSize = FontSizeSmall;
            grid.BorderBrush = BorderBrush;
            grid.BorderThickness = new Thickness(1);
            grid.GridLinesVisibility = DataGridGridLinesVisibility.Horizontal;
            grid.HorizontalGridLinesBrush = BorderBrush;
            grid.VerticalGridLinesBrush = BorderBrush;
            grid.RowHeaderWidth = 0;
            grid.HeadersVisibility = DataGridHeadersVisibility.Column;

            // Row style
            var rowStyle = new Style(typeof(DataGridRow));
            rowStyle.Setters.Add(new Setter(DataGridRow.BackgroundProperty, BgSectionBrush));
            rowStyle.Setters.Add(new Setter(DataGridRow.ForegroundProperty, TextPrimaryBrush));
            rowStyle.Setters.Add(new Setter(DataGridRow.BorderBrushProperty, BorderBrush));
            rowStyle.Setters.Add(new Setter(DataGridRow.BorderThicknessProperty, new Thickness(0, 0, 0, 1)));
            // Hover trigger
            var hoverTrigger = new Trigger { Property = DataGridRow.IsMouseOverProperty, Value = true };
            hoverTrigger.Setters.Add(new Setter(DataGridRow.BackgroundProperty,
                Freeze(new SolidColorBrush(Color.FromRgb(51, 51, 55))))); // #333337
            rowStyle.Triggers.Add(hoverTrigger);
            // Selected trigger
            var selTrigger = new Trigger { Property = DataGridRow.IsSelectedProperty, Value = true };
            selTrigger.Setters.Add(new Setter(DataGridRow.BackgroundProperty, AccentBlueBrush));
            selTrigger.Setters.Add(new Setter(DataGridRow.ForegroundProperty, Brushes.White));
            rowStyle.Triggers.Add(selTrigger);
            grid.RowStyle = rowStyle;

            // Cell style
            var cellStyle = new Style(typeof(DataGridCell));
            cellStyle.Setters.Add(new Setter(DataGridCell.ForegroundProperty, TextPrimaryBrush));
            cellStyle.Setters.Add(new Setter(DataGridCell.BorderThicknessProperty, new Thickness(0)));
            cellStyle.Setters.Add(new Setter(DataGridCell.PaddingProperty, new Thickness(4, 2, 4, 2)));
            // Cell selected trigger — override blue background with transparent so row bg shows
            var cellSelTrigger = new Trigger { Property = DataGridCell.IsSelectedProperty, Value = true };
            cellSelTrigger.Setters.Add(new Setter(DataGridCell.BackgroundProperty, Brushes.Transparent));
            cellSelTrigger.Setters.Add(new Setter(DataGridCell.ForegroundProperty, Brushes.White));
            cellStyle.Triggers.Add(cellSelTrigger);
            grid.CellStyle = cellStyle;

            // Column header style
            var headerStyle = new Style(typeof(DataGridColumnHeader));
            headerStyle.Setters.Add(new Setter(DataGridColumnHeader.BackgroundProperty, BtnDefaultBrush)); // #3F3F46
            headerStyle.Setters.Add(new Setter(DataGridColumnHeader.ForegroundProperty, TextPrimaryBrush));
            headerStyle.Setters.Add(new Setter(DataGridColumnHeader.FontFamilyProperty, Font));
            headerStyle.Setters.Add(new Setter(DataGridColumnHeader.FontSizeProperty, FontSizeSmall));
            headerStyle.Setters.Add(new Setter(DataGridColumnHeader.FontWeightProperty, FontWeights.SemiBold));
            headerStyle.Setters.Add(new Setter(DataGridColumnHeader.PaddingProperty, new Thickness(6, 4, 6, 4)));
            headerStyle.Setters.Add(new Setter(DataGridColumnHeader.BorderBrushProperty, BorderBrush));
            headerStyle.Setters.Add(new Setter(DataGridColumnHeader.BorderThicknessProperty, new Thickness(0, 0, 1, 1)));
            headerStyle.Setters.Add(new Setter(DataGridColumnHeader.HorizontalContentAlignmentProperty, HorizontalAlignment.Left));
            grid.ColumnHeaderStyle = headerStyle;

            // Alternating rows
            grid.AlternatingRowBackground = Freeze(new SolidColorBrush(Color.FromRgb(40, 40, 43))); // #28282B
        }

        // ═══ Factory: Dark TabControl Style ═══
        /// <summary>
        /// Apply dark theme to a TabControl — tab header background, selected tab accent.
        /// </summary>
        public static void ApplyDarkTheme(TabControl tabs)
        {
            tabs.Background = BgPrimaryBrush;
            tabs.Foreground = TextPrimaryBrush;
            tabs.FontFamily = Font;
            tabs.FontSize = FontSizeNormal;
            tabs.BorderBrush = BorderBrush;
            tabs.BorderThickness = new Thickness(0, 0, 0, 1);
        }

        /// <summary>Create a dark-themed TabItem with accent highlight when selected</summary>
        public static TabItem CreateDarkTabItem(string header, UIElement content)
        {
            var tab = new TabItem
            {
                Content = content,
                FontFamily = Font,
                FontSize = FontSizeNormal,
            };

            // Header with custom styling
            var headerBlock = new TextBlock
            {
                Text = header,
                FontFamily = Font,
                FontSize = FontSizeNormal,
                Padding = new Thickness(10, 6, 10, 6)
            };
            tab.Header = headerBlock;

            // Style the tab
            var tabStyle = new Style(typeof(TabItem));
            tabStyle.Setters.Add(new Setter(TabItem.BackgroundProperty, BgSectionBrush));
            tabStyle.Setters.Add(new Setter(TabItem.ForegroundProperty, TextSecondaryBrush));
            tabStyle.Setters.Add(new Setter(TabItem.BorderBrushProperty, BorderBrush));
            tabStyle.Setters.Add(new Setter(TabItem.BorderThicknessProperty, new Thickness(1, 1, 1, 0)));
            tabStyle.Setters.Add(new Setter(TabItem.MarginProperty, new Thickness(0, 0, 2, 0)));
            tabStyle.Setters.Add(new Setter(TabItem.PaddingProperty, new Thickness(0)));

            var selTrigger = new Trigger { Property = TabItem.IsSelectedProperty, Value = true };
            selTrigger.Setters.Add(new Setter(TabItem.BackgroundProperty, BgPrimaryBrush));
            selTrigger.Setters.Add(new Setter(TabItem.ForegroundProperty, TextPrimaryBrush));
            selTrigger.Setters.Add(new Setter(TabItem.BorderBrushProperty, AccentBlueBrush));
            tabStyle.Triggers.Add(selTrigger);

            tab.Style = tabStyle;
            return tab;
        }

        private static Brush Freeze(SolidColorBrush brush)
        {
            brush.Freeze();
            return brush;
        }
    }
}
