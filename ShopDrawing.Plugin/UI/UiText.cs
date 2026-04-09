using System;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace ShopDrawing.Plugin.UI
{
    internal static class UiText
    {
        public static string Normalize(string? text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return text ?? string.Empty;
            }

            var current = text;
            for (var i = 0; i < 3; i++)
            {
                if (!LooksMojibake(current))
                {
                    break;
                }

                var repaired = TryRepairUtf8(current);
                if (repaired == current)
                {
                    break;
                }

                current = repaired;
            }

            return current;
        }

        public static void NormalizeTree(DependencyObject? root)
        {
            if (root == null)
            {
                return;
            }

            NormalizeNode(root);

            foreach (var child in LogicalTreeHelper.GetChildren(root))
            {
                if (child is DependencyObject dependencyObject)
                {
                    NormalizeTree(dependencyObject);
                }
            }
        }

        public static void NormalizeWindow(Window window)
        {
            window.Title = Normalize(window.Title);
            NormalizeTree(window);
        }

        private static void NormalizeNode(DependencyObject node)
        {
            switch (node)
            {
                case FrameworkElement element when element.ToolTip is string toolTip:
                    element.ToolTip = Normalize(toolTip);
                    break;
            }

            switch (node)
            {
                case Window window:
                    window.Title = Normalize(window.Title);
                    break;
                case TextBlock textBlock:
                    textBlock.Text = Normalize(textBlock.Text);
                    break;
                case HeaderedContentControl headeredContentControl when headeredContentControl.Header is string header:
                    headeredContentControl.Header = Normalize(header);
                    break;
                case HeaderedItemsControl headeredItemsControl when headeredItemsControl.Header is string header:
                    headeredItemsControl.Header = Normalize(header);
                    break;
                case ContentControl contentControl when contentControl.Content is string content:
                    contentControl.Content = Normalize(content);
                    break;
                case TextBox textBox when textBox.ToolTip is string textBoxToolTip:
                    textBox.ToolTip = Normalize(textBoxToolTip);
                    break;
                case DataGrid dataGrid:
                    NormalizeDataGridColumns(dataGrid);
                    break;
                case Selector selector:
                    NormalizeSelectorItems(selector);
                    break;
            }
        }

        private static void NormalizeSelectorItems(Selector selector)
        {
            if (selector.ItemsSource != null)
            {
                return;
            }

            for (var i = 0; i < selector.Items.Count; i++)
            {
                if (selector.Items[i] is string item)
                {
                    selector.Items[i] = Normalize(item);
                }
            }
        }

        private static void NormalizeDataGridColumns(DataGrid dataGrid)
        {
            foreach (var column in dataGrid.Columns)
            {
                if (column.Header is string header)
                {
                    column.Header = Normalize(header);
                }
            }
        }

        private static bool LooksMojibake(string text)
        {
            return text.Contains('Ã') ||
                   text.Contains('Â') ||
                   text.Contains('Æ') ||
                   text.Contains('Ä') ||
                   text.Contains('ï') ||
                   text.Contains("áº", StringComparison.Ordinal) ||
                   text.Contains("á»", StringComparison.Ordinal) ||
                   text.Contains("Ä‘", StringComparison.Ordinal) ||
                   text.Contains("Ä", StringComparison.Ordinal) ||
                   text.Contains("Æ°", StringComparison.Ordinal) ||
                   text.Contains("Æ¡", StringComparison.Ordinal) ||
                   text.Contains("Ã¡Âº", StringComparison.Ordinal) ||
                   text.Contains("Ã¡Â»", StringComparison.Ordinal) ||
                   text.Contains("Ã„â€˜", StringComparison.Ordinal) ||
                   text.Contains("Ã„Â", StringComparison.Ordinal) ||
                   text.Contains("Ã†Â°", StringComparison.Ordinal) ||
                   text.Contains('�');
        }

        private static string TryRepairUtf8(string text)
        {
            try
            {
                var bytes = Encoding.GetEncoding(1252).GetBytes(text);
                return Encoding.UTF8.GetString(bytes);
            }
            catch (ArgumentException ex)
            {
                ShopDrawing.Plugin.Core.PluginLogger.Warn("Suppressed exception: " + ex.Message);
                return text;
            }
        }

    }
}
