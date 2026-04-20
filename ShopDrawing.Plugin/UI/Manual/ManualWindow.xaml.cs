using System.Windows;
using System.Windows.Documents;
using Autodesk.AutoCAD.ApplicationServices;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;

namespace ShopDrawing.Plugin.UI.Manual
{
    public partial class ManualWindow : Window
    {
        public ManualWindow()
        {
            InitializeComponent();
        }

        private void OnRunCommand(object sender, RoutedEventArgs e)
        {
            if (sender is Hyperlink link && link.CommandParameter is string cmd)
            {
                var doc = Application.DocumentManager.MdiActiveDocument;
                if (doc != null)
                {
                    this.Close();
                    // Send command to the active document allowing it to execute as if Typed
                    doc.SendStringToExecute(cmd, true, false, false);
                }
            }
        }
    }
}
