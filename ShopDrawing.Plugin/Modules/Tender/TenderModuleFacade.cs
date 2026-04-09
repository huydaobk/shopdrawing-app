using Autodesk.AutoCAD.Windows;
using ShopDrawing.Plugin.UI;

namespace ShopDrawing.Plugin.Modules.Tender
{
    internal sealed class TenderModuleFacade
    {
        private PaletteSet? _paletteSet;

        public void TogglePalette()
        {
            if (_paletteSet == null || _paletteSet.IsDisposed)
            {
                _paletteSet = new PaletteSet("Tender - Chào Giá");
                _paletteSet.Style = PaletteSetStyles.ShowCloseButton
                    | PaletteSetStyles.ShowPropertiesMenu
                    | PaletteSetStyles.ShowAutoHideButton;
                _paletteSet.MinimumSize = new System.Drawing.Size(280, 450);
                _paletteSet.AddVisual("Tender", new TenderPaletteControl());
            }

            _paletteSet.Visible = !_paletteSet.Visible;
        }
    }
}
