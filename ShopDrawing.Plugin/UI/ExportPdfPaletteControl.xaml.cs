using System;
using System.IO;
using System.Linq;
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
        private static ExportRequest? _pendingRequest;

        public ExportPdfPaletteControl()
        {
            InitializeComponent();
            _engine = new PdfExportEngine();
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            UiText.NormalizeTree(this);
            LoadPlotSettings();
            SetDefaultOutputPaths();
        }

        private void LoadPlotSettings()
        {
            var plotters = PdfExportEngine.GetAvailablePlotters();
            cmbPlotters.ItemsSource = plotters;
            cmbPlotters.SelectedItem = plotters.FirstOrDefault(p => p.Equals("DWG To PDF.pc3", StringComparison.OrdinalIgnoreCase))
                ?? plotters.FirstOrDefault();

            var styles = PdfExportEngine.GetAvailablePlotStyles();
            cmbPlotStyles.ItemsSource = styles;
            cmbPlotStyles.SelectedItem = styles.FirstOrDefault(s => s.Equals(PlotStyleInstaller.DefaultPlotStyleName, StringComparison.OrdinalIgnoreCase))
                ?? styles.FirstOrDefault();
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
                    txtFileName.Text = "ShopDrawing_XuatPDF.pdf";
                }

                return;
            }

            txtOutputPath.Text = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            txtFileName.Text = "ShopDrawing_XuatPDF.pdf";
        }

        private void BtnBrowse_Click(object sender, RoutedEventArgs e)
        {
            var folderDialog = new OpenFolderDialog
            {
                Title = "Chọn thư mục lưu PDF",
                FolderName = txtOutputPath.Text
            };

            if (folderDialog.ShowDialog() == true)
            {
                txtOutputPath.Text = folderDialog.FolderName;
            }
        }

        public sealed class ExportRequest
        {
            public PdfExportOptions Options { get; set; } = new();
            public WeakReference<ExportPdfPaletteControl>? PaletteRef { get; init; }

            public ExportPdfPaletteControl? GetPalette()
            {
                if (PaletteRef != null && PaletteRef.TryGetTarget(out var palette))
                {
                    return palette;
                }

                return null;
            }
        }

        public static ExportRequest? TakePendingRequest()
        {
            var req = _pendingRequest;
            _pendingRequest = null;
            return req;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            if (_pendingRequest?.GetPalette() == this)
            {
                _pendingRequest = null;
            }
        }

        private void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            btnExport.IsEnabled = false;
            txtStatus.Text = "Chuẩn bị xuất PDF...";
            txtStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 165, 0));

            string outDir = txtOutputPath.Text;
            string fileName = txtFileName.Text;

            if (!fileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            {
                fileName += ".pdf";
            }

            string fullPath = Path.Combine(outDir, fileName);

            fullPath = HandleFileConflict(fullPath);
            if (string.IsNullOrEmpty(fullPath))
            {
                txtStatus.Text = "Đã hủy xuất PDF.";
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

            var doc = AutoCadUiContext.GetActiveDocument();
            if (doc == null)
            {
            txtStatus.Text = "Không có bản vẽ đang mở để xuất PDF.";
                btnExport.IsEnabled = true;
                txtStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(220, 20, 60));
                return;
            }

            _pendingRequest = new ExportRequest
            {
                Options = options,
                PaletteRef = new WeakReference<ExportPdfPaletteControl>(this)
            };

            AutoCadUiContext.TrySendCommand("_SD_PLOT_API_WRAPPER ");
        }

        public void ReportProgress(string message, bool isDone, string savedPath, bool openAuto)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                txtStatus.Text = UiText.Normalize(message);

                if (isDone)
                {
                    btnExport.IsEnabled = true;
                    txtStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(154, 205, 50));

                    if (openAuto && File.Exists(savedPath))
                    {
                        try
                        {
                            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                            {
                                FileName = savedPath,
                                UseShellExecute = true
                            });
                        }
                        catch (System.Exception ex)
            {
                ShopDrawing.Plugin.Core.PluginLogger.Error("Suppressed exception in ExportPdfPaletteControl.xaml.cs", ex);
            }
                    }
                }
                else
                {
                    txtStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 165, 0));
                }
            }));
        }

        private string HandleFileConflict(string path)
        {
            if (!File.Exists(path))
            {
                return path;
            }

            if (IsFileLocked(path))
            {
                    UiFeedback.ShowError($"File '{Path.GetFileName(path)}' đang được mở bởi ứng dụng khác.\n\nVui lòng đóng file PDF và thử lại.");
                return string.Empty;
            }

            var result = UiFeedback.AskYesNoCancel(
                $"File '{Path.GetFileName(path)}' đã tồn tại.\n\nNhấn 'Yes' để ghi đè.\nNhấn 'No' để tự thêm phiên bản (_v2...).",
                "Trùng file");

            if (result == MessageBoxResult.Cancel)
            {
                return string.Empty;
            }

            if (result == MessageBoxResult.Yes)
            {
                return path;
            }

            string dir = Path.GetDirectoryName(path) ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
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
            }
            while (File.Exists(newPath));

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
            catch (IOException ex) { ShopDrawing.Plugin.Core.PluginLogger.Warn("Suppressed exception: " + ex.Message); 
                return true;
            }

            return false;
        }
    }
}
