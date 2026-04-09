using System.Windows;

namespace ShopDrawing.Plugin.UI
{
    internal static class UiFeedback
    {
        public static void ShowInfo(string message, string caption = "Thông báo")
        {
            MessageBox.Show(UiText.Normalize(message), UiText.Normalize(caption), MessageBoxButton.OK, MessageBoxImage.Information);
        }

        public static void ShowWarning(string message, string caption = "Cảnh báo")
        {
            MessageBox.Show(UiText.Normalize(message), UiText.Normalize(caption), MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        public static void ShowError(string message, string caption = "Lỗi")
        {
            MessageBox.Show(UiText.Normalize(message), UiText.Normalize(caption), MessageBoxButton.OK, MessageBoxImage.Error);
        }

        public static MessageBoxResult AskYesNoCancel(string message, string caption = "Xác nhận")
        {
            return MessageBox.Show(UiText.Normalize(message), UiText.Normalize(caption), MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
        }

        public static MessageBoxResult AskYesNo(string message, string caption = "Xác nhận")
        {
            return MessageBox.Show(UiText.Normalize(message), UiText.Normalize(caption), MessageBoxButton.YesNo, MessageBoxImage.Question);
        }
    }
}
