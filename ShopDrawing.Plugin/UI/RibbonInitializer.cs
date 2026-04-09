using System;
using System.Reflection;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Autodesk.Windows;

namespace ShopDrawing.Plugin.UI
{
    /// <summary>
    /// Tạo custom Ribbon Tab "Shop Drawing" và tách panel theo từng chức năng.
    /// </summary>
    public static class RibbonInitializer
    {
        private static bool _created;

        public static void CreateRibbon()
        {
            if (_created)
            {
                return;
            }

            var ribbon = ComponentManager.Ribbon;
            if (ribbon == null)
            {
                return;
            }

            var tab = new RibbonTab
            {
                Title = UiText.Normalize("Shop Drawing"),
                Id = "SD_TAB"
            };
            ribbon.Tabs.Add(tab);

            AddFeaturePanel(
                tab,
                panelId: "SD_PANEL_TENDER",
                panelTitle: "Tender",
                buttonText: "Tender",
                tooltip: "Chuẩn bị báo giá và khối lượng Tender.",
                command: "SD_TENDER",
                resourceName: "ShopDrawing.Plugin.Resources.Icons.icon_tender.png");

            AddFeaturePanel(
                tab,
                panelId: "SD_PANEL_SHOPDRAWING",
                panelTitle: "Shopdrawing",
                buttonText: "Shopdrawing",
                tooltip: "Mở palette Shopdrawing để tạo bản vẽ lắp đặt tấm panel tường.",
                command: "SD_PANEL",
                resourceName: "ShopDrawing.Plugin.Resources.Icons.icon_shopdrawing.png");

            AddFeaturePanel(
                tab,
                panelId: "SD_PANEL_SMARTDIM",
                panelTitle: "SmartDim",
                buttonText: "SmartDim",
                tooltip: "Đo kích thước tự động và thủ công.",
                command: "SD_SMART_DIM",
                resourceName: "ShopDrawing.Plugin.Resources.Icons.icon_smartdim.png");

            AddFeaturePanel(
                tab,
                panelId: "SD_PANEL_LAYOUT",
                panelTitle: "Layout",
                buttonText: "Layout",
                tooltip: "Phân bổ bản vẽ vào Layout tabs.",
                command: "SD_LAYOUT",
                resourceName: "ShopDrawing.Plugin.Resources.Icons.icon_layout.png");

            AddFeaturePanel(
                tab,
                panelId: "SD_PANEL_EXPORTPDF",
                panelTitle: "ExportPDF",
                buttonText: "ExportPDF",
                tooltip: "Xuất bộ bản vẽ PDF cho trình duyệt và ban hành.",
                command: "SD_EXPORT",
                resourceName: "ShopDrawing.Plugin.Resources.Icons.icon_export.png");

            AddSystemButtons(tab);

            tab.IsActive = true;
            _created = true;
        }

        private static void AddFeaturePanel(
            RibbonTab tab,
            string panelId,
            string panelTitle,
            string buttonText,
            string tooltip,
            string command,
            string resourceName)
        {
            var panelSource = new RibbonPanelSource
            {
                Title = UiText.Normalize(panelTitle),
                Id = panelId
            };

            panelSource.Items.Add(CreateButton(
                buttonText,
                tooltip,
                command,
                resourceName,
                isLarge: true));

            tab.Panels.Add(new RibbonPanel { Source = panelSource });
        }

        private static void AddSystemButtons(RibbonTab tab)
        {
            var panelSource = new RibbonPanelSource
            {
                Title = UiText.Normalize("System"),
                Id = "SD_PANEL_SYSTEM_TOOLS"
            };

            panelSource.Items.Add(CreateButton(
                "Init Project",
                "Khoi tao root du an va thu muc ShopDrawingData.",
                "SD_INIT_PROJECT",
                "ShopDrawing.Plugin.Resources.Icons.icon_shopdrawing.png",
                isLarge: true));

            panelSource.Items.Add(CreateButton(
                "Update",
                "Kiem tra ban cap nhat plugin.",
                "SD_CHECK_UPDATE",
                "ShopDrawing.Plugin.Resources.Icons.icon_export.png",
                isLarge: true));

            tab.Panels.Add(new RibbonPanel { Source = panelSource });
        }

        /// <summary>
        /// Tạo RibbonButton.
        /// isLarge=true: icon 32px + text dưới.
        /// isLarge=false: icon 16px + text ngang.
        /// </summary>
        private static RibbonButton CreateButton(
            string text,
            string tooltip,
            string command,
            string resourceName,
            bool isLarge)
        {
            var btn = new RibbonButton
            {
                Text = UiText.Normalize(text),
                ShowText = false,
                ShowImage = true,
                ToolTip = UiText.Normalize(tooltip),
                CommandHandler = new RibbonCommandHandler(),
                CommandParameter = command + "\n",
                Image = LoadAndResize(resourceName, 16),
                LargeImage = LoadAndResize(resourceName, 32)
            };

            if (isLarge)
            {
                btn.Size = RibbonItemSize.Large;
                btn.Orientation = System.Windows.Controls.Orientation.Vertical;
            }
            else
            {
                btn.Size = RibbonItemSize.Standard;
                btn.Orientation = System.Windows.Controls.Orientation.Horizontal;
            }

            return btn;
        }

        /// <summary>
        /// Load PNG từ EmbeddedResource rồi resize về kích thước cần thiết.
        /// AutoCAD Ribbon cần icon 16x16 hoặc 32x32.
        /// </summary>
        private static BitmapSource? LoadAndResize(string resourceName, int size)
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                using var stream = assembly.GetManifestResourceStream(resourceName);
                if (stream == null)
                {
                    return null;
                }

                var original = new BitmapImage();
                original.BeginInit();
                original.StreamSource = stream;
                original.CacheOption = BitmapCacheOption.OnLoad;
                original.EndInit();
                original.Freeze();

                if (original.PixelWidth == size && original.PixelHeight == size)
                {
                    return original;
                }

                double scaleX = (double)size / original.PixelWidth;
                double scaleY = (double)size / original.PixelHeight;
                var transformed = new TransformedBitmap(original, new ScaleTransform(scaleX, scaleY));
                transformed.Freeze();
                return transformed;
            }
            catch (Exception ex)
            {
                ShopDrawing.Plugin.Core.PluginLogger.Warn("Suppressed exception: " + ex.Message);
                return null;
            }
        }
    }

    /// <summary>
    /// CommandHandler cho RibbonButton gửi lệnh vào AutoCAD command line.
    /// </summary>
    public class RibbonCommandHandler : System.Windows.Input.ICommand
    {
        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter)
        {
            if (parameter is RibbonButton btn && btn.CommandParameter is string cmd)
            {
                AutoCadUiContext.TrySendCommand(cmd);
            }
        }
    }
}

