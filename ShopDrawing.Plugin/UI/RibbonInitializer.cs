using System.Reflection;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Autodesk.Windows;

namespace ShopDrawing.Plugin.UI
{
    /// <summary>
    /// Tạo custom Ribbon Tab "Shop Drawing" với 3 panel nhóm theo chức năng:
    /// [Thiết Kế] | [Kích Thước] | [Xuất]
    /// </summary>
    public static class RibbonInitializer
    {
        private static bool _created = false;

        public static void CreateRibbon()
        {
            if (_created) return;

            var ribbon = ComponentManager.Ribbon;
            if (ribbon == null) return;

            // ── Tab: Shop Drawing ──
            var tab = new RibbonTab { Title = "Shop Drawing", Id = "SD_TAB" };
            ribbon.Tabs.Add(tab);

            // ══════════════════════════════════════════
            // PANEL 1 — Đấu thầu
            // ══════════════════════════════════════════
            var panel1Source = new RibbonPanelSource { Title = "Đấu thầu", Id = "SD_PANEL1" };
            tab.Panels.Add(new RibbonPanel { Source = panel1Source });

            panel1Source.Items.Add(CreateButton(
                "Tender",
                "Chuẩn bị báo giá & khối lượng Tender.",
                "SD_TENDER",
                "ShopDrawing.Plugin.Resources.Icons.icon_tender.png", // Dự phòng icon
                isLarge: true
            ));

            // ══════════════════════════════════════════
            // PANEL 2 — Thiết Kế
            // ══════════════════════════════════════════
            var panel2Source = new RibbonPanelSource { Title = "Thiết Kế", Id = "SD_PANEL2" };
            tab.Panels.Add(new RibbonPanel { Source = panel2Source });

            panel2Source.Items.Add(CreateButton(
                "Shopdrawing",
                "Mở Palette Shop Drawing\nTạo bản vẽ lắp đặt tấm panel tường.",
                "SD_PANEL",
                "ShopDrawing.Plugin.Resources.Icons.icon_shopdrawing.png",
                isLarge: true
            ));

            panel2Source.Items.Add(CreateButton(
                "Smart Dim",
                "Smart Dimension\nĐo kích thước tự động + thủ công.",
                "SD_SMART_DIM",
                "ShopDrawing.Plugin.Resources.Icons.icon_smartdim.png",
                isLarge: true
            ));

            panel2Source.Items.Add(CreateButton(
                "Layout",
                "Layout Manager\nPhân bổ bản vẽ vào Layout tabs.",
                "SD_LAYOUT",
                "ShopDrawing.Plugin.Resources.Icons.icon_layout.png",
                isLarge: true
            ));

            // ══════════════════════════════════════════
            // PANEL 3 — Xuất
            // ══════════════════════════════════════════
            var panel3Source = new RibbonPanelSource { Title = "Xuất", Id = "SD_PANEL3" };
            tab.Panels.Add(new RibbonPanel { Source = panel3Source });

            panel3Source.Items.Add(CreateButton(
                "Xuất PDF",
                "Xuất bản vẽ PDF\nXuất bộ bản vẽ cho trình duyệt & ban hành.",
                "SD_EXPORT",
                "ShopDrawing.Plugin.Resources.Icons.icon_export.png",
                isLarge: true
            ));

            tab.IsActive = true;
            _created = true;
        }

        /// <summary>
        /// Tạo RibbonButton.
        /// isLarge=true  → icon 32px + text dưới (Large button).
        /// isLarge=false → icon 16px + text ngang (Standard button).
        /// </summary>
        private static RibbonButton CreateButton(
            string text, string tooltip, string command,
            string resourceName, bool isLarge)
        {
            var btn = new RibbonButton
            {
                Text             = text,
                ShowText         = true,
                ShowImage        = true,
                ToolTip          = tooltip,
                CommandHandler   = new RibbonCommandHandler(),
                CommandParameter = command + "\n",
                Image            = LoadAndResize(resourceName, 16),
                LargeImage       = LoadAndResize(resourceName, 32)
            };

            if (isLarge)
            {
                btn.Size        = RibbonItemSize.Large;
                btn.Orientation = System.Windows.Controls.Orientation.Vertical;
            }
            else
            {
                btn.Size        = RibbonItemSize.Standard;
                btn.Orientation = System.Windows.Controls.Orientation.Horizontal;
            }

            return btn;
        }

        /// <summary>
        /// Load PNG từ EmbeddedResource rồi resize về kích thước cần thiết.
        /// AutoCAD Ribbon cần icon 16x16 (Standard) hoặc 32x32 (Large).
        /// </summary>
        private static BitmapSource? LoadAndResize(string resourceName, int size)
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                using var stream = assembly.GetManifestResourceStream(resourceName);
                if (stream == null) return null;

                var original = new BitmapImage();
                original.BeginInit();
                original.StreamSource = stream;
                original.CacheOption  = BitmapCacheOption.OnLoad;
                original.EndInit();
                original.Freeze();

                if (original.PixelWidth == size && original.PixelHeight == size)
                    return original;

                double scaleX = (double)size / original.PixelWidth;
                double scaleY = (double)size / original.PixelHeight;
                var transformed = new TransformedBitmap(original, new ScaleTransform(scaleX, scaleY));
                transformed.Freeze();
                return transformed;
            }
            catch { return null; }
        }
    }

    /// <summary>
    /// CommandHandler cho RibbonButton — gửi lệnh vào AutoCAD command line.
    /// </summary>
    public class RibbonCommandHandler : System.Windows.Input.ICommand
    {
        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter)
        {
            if (parameter is RibbonButton btn && btn.CommandParameter is string cmd)
            {
                var doc = Autodesk.AutoCAD.ApplicationServices.Application
                    .DocumentManager.MdiActiveDocument;
                if (doc != null)
                    doc.SendStringToExecute(cmd, true, false, false);
            }
        }
    }
}
