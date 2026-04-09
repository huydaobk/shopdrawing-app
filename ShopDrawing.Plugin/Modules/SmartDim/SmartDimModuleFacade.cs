using Autodesk.AutoCAD.Windows;
using ShopDrawing.Plugin.UI;

namespace ShopDrawing.Plugin.Modules.SmartDim
{
    internal sealed class SmartDimModuleFacade
    {
        private PaletteSet? _paletteSet;
        private readonly UpdatePluginTextService _updatePluginTextService = new();

        public void TogglePalette()
        {
            if (_paletteSet == null || _paletteSet.IsDisposed)
            {
                _paletteSet = new PaletteSet("Smart Dimension");
                _paletteSet.Style = PaletteSetStyles.ShowCloseButton
                    | PaletteSetStyles.ShowPropertiesMenu
                    | PaletteSetStyles.ShowAutoHideButton;
                _paletteSet.MinimumSize = new System.Drawing.Size(260, 380);
                _paletteSet.AddVisual("Smart Dim", new SmartDimPaletteControl());
            }

            _paletteSet.Visible = !_paletteSet.Visible;
        }

        public void UpdateAllPluginText(double defaultTextHeightMm)
        {
            _updatePluginTextService.Run(defaultTextHeightMm);
        }
    }
}
