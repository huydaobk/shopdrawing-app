using System;
using System.Windows.Controls;
using System.Windows.Media;

namespace ShopDrawing.Plugin.UI
{
    internal static class UiStatus
    {
        public static void Apply(TextBlock target, string message)
        {
            var normalized = UiText.Normalize(message);
            target.Text = normalized;
            target.Foreground = ResolveBrush(normalized);
        }

        public static void ApplyInfo(TextBlock target, string message)
        {
            target.Text = UiText.Normalize(message);
            target.Foreground = SdPaletteStyles.TextSecondaryBrush;
        }

        public static void ApplySuccess(TextBlock target, string message)
        {
            target.Text = UiText.Normalize(message);
            target.Foreground = SdPaletteStyles.AccentGreenBrush;
        }

        public static void ApplyWarning(TextBlock target, string message)
        {
            target.Text = UiText.Normalize(message);
            target.Foreground = SdPaletteStyles.AccentOrangeBrush;
        }

        public static void ApplyError(TextBlock target, string message)
        {
            target.Text = UiText.Normalize(message);
            target.Foreground = SdPaletteStyles.AccentRedBrush;
        }

        public static Brush ResolveBrush(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return SdPaletteStyles.TextSecondaryBrush;
            }

            if (ContainsAny(
                message,
                "Cảnh báo",
                "Không",
                "Chọn ",
                "Hủy ",
                "Warning",
                "Canceled",
                "Cancelled"))
            {
                return SdPaletteStyles.AccentOrangeBrush;
            }

            if (ContainsAny(
                message,
                "Lỗi",
                "Error",
                "Failed",
                "Exception"))
            {
                return SdPaletteStyles.AccentRedBrush;
            }

            if (ContainsAny(
                message,
                "Đã ",
                "Thành công",
                "Success",
                "Created",
                "Updated",
                "Deleted"))
            {
                return SdPaletteStyles.AccentGreenBrush;
            }

            return SdPaletteStyles.TextPrimaryBrush;
        }

        private static bool ContainsAny(string text, params string[] patterns)
        {
            foreach (var pattern in patterns)
            {
                if (text.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
