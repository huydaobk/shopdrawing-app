using ShopDrawing.Plugin.Core;
using ShopDrawing.Plugin.Runtime;
using ShopDrawing.Plugin.UI;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;

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

            var profile = _projectProfileManager.LoadOrDefault();
            var dialog = new ProjectInputDialog(profile);
            if (Application.ShowModalWindow(dialog) == true)
            {
                _projectProfileManager.Save(dialog.ProjectProfile);
                UiFeedback.ShowInfo("Đã lưu INPUT cho dự án hiện tại.", "ShopDrawing");
            }
        }
    }
}
