using System;
using Autodesk.AutoCAD.ApplicationServices.Core;

namespace ShopDrawing.Plugin.Core
{
    internal static class SafeCadLock
    {
        /// <summary>
        /// Attempts to lock the active document. Returns null if
        /// the document is unavailable or already command-locked.
        /// Callers MUST check for null before proceeding with CAD operations.
        /// </summary>
        public static Autodesk.AutoCAD.ApplicationServices.DocumentLock? TryLock()
        {
            try
            {
                var doc = Application.DocumentManager.MdiActiveDocument;
                if (doc == null)
                {
                    PluginLogger.Warn("CAD document lock vetoed - No active document available.");
                    return null;
                }

                // If document is present and we're good to edit
                return doc.LockDocument();
            }
            catch (Autodesk.AutoCAD.Runtime.Exception ex) when (ex.ErrorStatus == Autodesk.AutoCAD.Runtime.ErrorStatus.Vetoed)
            {
                PluginLogger.Warn("CAD document lock vetoed - DocumentLockVeto exception.");
                return null;
            }
            catch (Exception ex)
            {
                PluginLogger.Error("Unexpected error while attempting to lock CAD document.", ex);
                return null;
            }
        }
    }
}
