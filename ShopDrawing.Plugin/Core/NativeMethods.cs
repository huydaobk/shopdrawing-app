using System;
using System.Runtime.InteropServices;
using Autodesk.AutoCAD.DatabaseServices;

namespace ShopDrawing.Plugin.Core
{
    internal static class NativeMethods
    {
        // For AutoCAD 2024/2025/2026, the entry point for acedSetCurrentVPort may vary.
        // We decouple it into its own static class to prevent early TypeLoadExceptions.
        [DllImport("acad.exe", CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "?acedSetCurrentVPort@@YA?AW4ErrorStatus@Acad@@PBVAcDbViewport@@@Z")]
        private static extern int acedSetCurrentVPort(IntPtr viewport);

        /// <summary>
        /// Attempts to activate the viewport using P/Invoke as a fallback.
        /// </summary>
        public static bool TrySetCurrentVPortNative(Viewport viewport)
        {
            try
            {
                int result = acedSetCurrentVPort(viewport.UnmanagedObject);
                return result == 0; // Acad::eOk is usually 0
            }
            catch (EntryPointNotFoundException)
            {
                PluginLogger.Error("P/Invoke entry point for acedSetCurrentVPort not found. AutoCAD version mismatch.");
                return false;
            }
            catch (DllNotFoundException)
            {
                PluginLogger.Error("acad.exe not found for P/Invoke context.");
                return false;
            }
            catch (Exception ex)
            {
                PluginLogger.Error("Unexpected error during native viewport activation.", ex);
                return false;
            }
        }
    }
}
