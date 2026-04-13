using ShopDrawing.Plugin.Core;
using ShopDrawing.Plugin.Models;
using ShopDrawing.Plugin.Runtime;
using ShopDrawing.Plugin.UI;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using Path = System.IO.Path;
using File = System.IO.File;

namespace ShopDrawing.Plugin.Commands
{
    internal sealed class SystemCommandActions
    {
        private readonly ProjectInitializationService _projectInitializationService = new();
        private readonly ProjectProfileManager _projectProfileManager = new();

        public void InitializeProject()
        {
            _projectInitializationService.InitializeInteractive();
        }

        public void InputProjectInfo()
        {
            // INPUT la diem vao duy nhat: dam bao project root + data folder ton tai.
            _ = ProjectDataPathResolver.GetRuntimeRoot();
            ShopDrawingRuntimeServices.RefreshProjectScopedServices();

            var profile = ShouldUseBlankProfileForCurrentDrawing()
                ? new ProjectProfile()
                : _projectProfileManager.LoadOrDefault();

            var dialog = new ProjectInputDialog(profile);
            if (Application.ShowModalWindow(dialog) == true)
            {
                _projectProfileManager.Save(dialog.ProjectProfile);
                UiFeedback.ShowInfo("Đã lưu INPUT cho dự án hiện tại.", "ShopDrawing");
            }
        }

        private static bool ShouldUseBlankProfileForCurrentDrawing()
        {
            var document = Application.DocumentManager.MdiActiveDocument;
            if (document == null)
            {
                return true;
            }

            string drawingName = Path.GetFileName(document.Name ?? string.Empty);
            if (IsAutoCadTemporaryDrawingName(drawingName))
            {
                return true;
            }

            string? drawingPath = document.Database?.Filename;
            if (string.IsNullOrWhiteSpace(drawingPath) || !Path.IsPathRooted(drawingPath))
            {
                return true;
            }

            if (!File.Exists(drawingPath))
            {
                return true;
            }

            try
            {
                var context = ProjectDataPathResolver.ResolveFromDrawingPath(drawingPath, ensureExists: false);
                string markerPath = Path.Combine(context.RuntimeRoot, ProjectDataPathResolver.GetProjectMarkerFileName());
                return !File.Exists(markerPath);
            }
            catch
            {
                return true;
            }
        }

        private static bool IsAutoCadTemporaryDrawingName(string drawingName)
        {
            if (string.IsNullOrWhiteSpace(drawingName) ||
                !drawingName.EndsWith(".dwg", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string stem = Path.GetFileNameWithoutExtension(drawingName);
            if (!stem.StartsWith("Drawing", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string suffix = stem.Substring("Drawing".Length);
            if (suffix.Length == 0)
            {
                return false;
            }

            foreach (char c in suffix)
            {
                if (!char.IsDigit(c))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
