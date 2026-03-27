using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Autodesk.AutoCAD.ApplicationServices;
using Microsoft.Win32;
using ShopDrawing.Plugin.Core;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace ShopDrawing.Plugin.UI
{
    public partial class ExportPdfPaletteControl : UserControl
    {
        private PdfExportEngine _engine;

        public ExportPdfPaletteControl()
        {
            InitializeComponent();
            _engine = new PdfExportEngine();
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            LoadPlotSettings();
            SetDefaultOutputPaths();
        }

        private void LoadPlotSettings()
        {
            var plotters = PdfExportEngine.GetAvailablePlotters();
            cmbPlotters.ItemsSource = plotters;
            cmbPlotters.SelectedItem = plotters.FirstOrDefault(p => p.Equals("DWG To PDF.pc3", StringComparison.OrdinalIgnoreCase)) ?? plotters.FirstOrDefault();

            var styles = PdfExportEngine.GetAvailablePlotStyles();
            cmbPlotStyles.ItemsSource = styles;
            cmbPlotStyles.SelectedItem = styles.FirstOrDefault(s => s.Equals(PlotStyleInstaller.DefaultPlotStyleName, StringComparison.OrdinalIgnoreCase)) ?? styles.FirstOrDefault();
        }

        private void SetDefaultOutputPaths()
        {
            var doc = AcadApp.DocumentManager.MdiActiveDocument;
            if (doc != null)
            {
                string dwgPath = doc.Name;
                if (!string.IsNullOrEmpty(dwgPath) && !dwgPath.Contains("Drawing") && File.Exists(dwgPath))
                {
                    txtOutputPath.Text = Path.GetDirectoryName(dwgPath);
                    txtFileName.Text = Path.GetFileNameWithoutExtension(dwgPath) + ".pdf";
                }
                else
                {
                    txtOutputPath.Text = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                    txtFileName.Text = "ShopDrawingExport.pdf";
                }
            }
        }

        private void BtnBrowse_Click(object sender, RoutedEventArgs e)
        {
            var folderDialog = new OpenFolderDialog
            {
                Title = "Chon thu muc luu PDF",
                FolderName = txtOutputPath.Text
            };

            if (folderDialog.ShowDialog() == true)
            {
                txtOutputPath.Text = folderDialog.FolderName;
            }
        }

        public class ExportRequest
        {
            public PdfExportOptions Options { get; set; } = new();
            public ExportPdfPaletteControl Palette { get; set; } = null!;
        }

        private static ExportRequest? _pendingRequest;

        public static ExportRequest? TakePendingRequest()
        {
            var req = _pendingRequest;
            _pendingRequest = null;
            return req;
        }

        private void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            btnExport.IsEnabled = false;
            txtStatus.Text = "Chuáº©n bá»‹ xuáº¥t PDF...";
            txtStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 165, 0));

            string outDir = txtOutputPath.Text;
            string fileName = txtFileName.Text;

            if (!fileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                fileName += ".pdf";

            string fullPath = Path.Combine(outDir, fileName);

            fullPath = HandleFileConflict(fullPath);
            if (string.IsNullOrEmpty(fullPath))
            {
                txtStatus.Text = "ÄÃ£ huá»· xuáº¥t PDF.";
                btnExport.IsEnabled = true;
                return;
            }

            var options = new PdfExportOptions
            {
                OutputFilePath = fullPath,
                PlotterName = cmbPlotters.SelectedItem?.ToString() ?? "DWG To PDF.pc3",
                PlotStyleName = cmbPlotStyles.SelectedItem?.ToString() ?? PlotStyleInstaller.DefaultPlotStyleName,
                OpenAfterExport = chkOpenPdf.IsChecked == true
            };

            _pendingRequest = new ExportRequest { Options = options, Palette = this };

            var doc = AcadApp.DocumentManager.MdiActiveDocument;
            doc.SendStringToExecute("_SD_PLOT_API_WRAPPER ", true, false, false);
        }

        public void ReportProgress(string message, bool isDone, string savedPath, bool openAuto)
        {
            Dispatcher.Invoke(() =>
            {
                txtStatus.Text = message;

                if (isDone)
                {
                    btnExport.IsEnabled = true;
                    txtStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(154, 205, 50));

                    if (openAuto && File.Exists(savedPath))
                    {
                        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = savedPath, UseShellExecute = true }); } catch { }
                    }
                }
                else
                {
                    txtStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 165, 0));
                }
            });
        }

        private string HandleFileConflict(string path)
        {
            if (!File.Exists(path)) return path;

            if (IsFileLocked(path))
            {
                MessageBox.Show($"File '{Path.GetFileName(path)}' Ä‘ang Ä‘Æ°á»£c má»Ÿ bá»Ÿi á»©ng dá»¥ng khÃ¡c.\n\nVui lÃ²ng Ä‘Ã³ng file PDF vÃ  thá»­ láº¡i.", "Lá»—i", MessageBoxButton.OK, MessageBoxImage.Error);
                return string.Empty;
            }

            var result = MessageBox.Show($"File '{Path.GetFileName(path)}' Ä‘Ã£ tá»“n táº¡i.\n\nNháº¥n 'Yes' Ä‘á»ƒ GHI ÄÃˆ.\nNháº¥n 'No' Ä‘á»ƒ Tá»° THÃŠM VERSION (_v2...).", "TrÃ¹ng file", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);

            if (result == MessageBoxResult.Cancel)
                return string.Empty;

            if (result == MessageBoxResult.Yes)
                return path;

            string dir = Path.GetDirectoryName(path);
            string name = Path.GetFileNameWithoutExtension(path);

            if (System.Text.RegularExpressions.Regex.IsMatch(name, @"_v\d+$"))
            {
                name = System.Text.RegularExpressions.Regex.Replace(name, @"_v\d+$", "");
            }

            int version = 2;
            string newPath;
            do
            {
                newPath = Path.Combine(dir, $"{name}_v{version}.pdf");
                version++;
            } while (File.Exists(newPath));

            return newPath;
        }

        private bool IsFileLocked(string file)
        {
            try
            {
                using (FileStream stream = new FileInfo(file).Open(FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    stream.Close();
                }
            }
            catch (IOException)
            {
                return true;
            }

            return false;
        }
    }
}
