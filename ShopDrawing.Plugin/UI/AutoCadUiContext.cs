using Autodesk.AutoCAD.ApplicationServices;
using System.Windows;

namespace ShopDrawing.Plugin.UI
{
    internal static class AutoCadUiContext
    {
        public static Document? GetActiveDocument()
        {
            return Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
        }

        public static bool HasActiveDocument()
        {
            return GetActiveDocument() != null;
        }

        public static bool TrySendCommand(
            string command,
            bool notifyWhenMissing = false,
            string missingDocumentMessage = "Chưa có bản vẽ đang mở.",
            string caption = "Thông báo")
        {
            var doc = GetActiveDocument();
            if (doc == null)
            {
                if (notifyWhenMissing)
                {
                    UiFeedback.ShowInfo(missingDocumentMessage, caption);
                }

                return false;
            }

            doc.SendStringToExecute(command, true, false, false);
            return true;
        }
    }
}
