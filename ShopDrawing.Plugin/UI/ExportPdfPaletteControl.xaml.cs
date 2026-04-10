using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using ShopDrawing.Plugin.Core;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace ShopDrawing.Plugin.UI
{
    public partial class ExportPdfPaletteControl : UserControl
    {
        private static ExportRequest? _pendingRequest;

        public ExportPdfPaletteControl()
        {
            InitializeComponent();
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
                var dwgPath = doc.Name;
                if (!string.IsNullOrEmpty(dwgPath) && !dwgPath.Contains("Drawing", StringComparison.OrdinalIgnoreCase) && File.Exists(dwgPath))
                {
                    txtOutputPath.Text = Path.GetDirectoryName(dwgPath) ?? string.Empty;
                    txtFileName.Text = Path.GetFileNameWithoutExtension(dwgPath) + ".pdf";
                    return;
                }
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
            SetStatus("Chuẩn bị xuất PDF...", StatusTone.InProgress);

            var outDir = txtOutputPath.Text?.Trim() ?? string.Empty;
            var fileName = txtFileName.Text?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(outDir))
            {
                SetStatus("Vui lòng chọn thư mục xuất PDF.", StatusTone.Error);
                btnExport.IsEnabled = true;
                return;
            }

            if (!EnsureOutputDirectory(outDir))
            {
                btnExport.IsEnabled = true;
                return;
            }

            if (string.IsNullOrWhiteSpace(fileName))
            {
                SetStatus("Vui lòng nhập tên file PDF.", StatusTone.Error);
                btnExport.IsEnabled = true;
                return;
            }

            if (!fileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            {
                fileName += ".pdf";
            }

            var fullPath = Path.Combine(outDir, fileName);
            fullPath = HandleFileConflict(fullPath);
            if (string.IsNullOrEmpty(fullPath))
            {
                SetStatus("Đã hủy xuất PDF.", StatusTone.Error);
                btnExport.IsEnabled = true;
                return;
            }

            var doc = AutoCadUiContext.GetActiveDocument();
            if (doc == null)
            {
                SetStatus("Không có bản vẽ đang mở để xuất PDF.", StatusTone.Error);
                btnExport.IsEnabled = true;
                return;
            }

            _pendingRequest = new ExportRequest
            {
                Options = new PdfExportOptions
                {
                    OutputFilePath = fullPath,
                    PlotterName = cmbPlotters.SelectedItem?.ToString() ?? "DWG To PDF.pc3",
                    PlotStyleName = cmbPlotStyles.SelectedItem?.ToString() ?? PlotStyleInstaller.DefaultPlotStyleName,
                    OpenAfterExport = chkOpenPdf.IsChecked == true
                },
                PaletteRef = new WeakReference<ExportPdfPaletteControl>(this)
            };

            if (!AutoCadUiContext.TrySendCommand("_SD_PLOT_API_WRAPPER "))
            {
                _pendingRequest = null;
                SetStatus("Không thể gửi lệnh xuất PDF vào AutoCAD.", StatusTone.Error);
                btnExport.IsEnabled = true;
            }
        }

        public void ReportProgress(string message, bool isDone, bool isSuccess, string savedPath, bool openAuto)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                SetStatus(message, isDone
                    ? (isSuccess ? StatusTone.Success : StatusTone.Error)
                    : StatusTone.InProgress);

                if (!isDone)
                {
                    return;
                }

                btnExport.IsEnabled = true;
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
                    catch (Exception ex)
                    {
                        PluginLogger.Error("Suppressed exception in ExportPdfPaletteControl.xaml.cs", ex);
                    }
                }
            }));
        }

        private bool EnsureOutputDirectory(string outDir)
        {
            if (Directory.Exists(outDir))
            {
                return true;
            }

            try
            {
                Directory.CreateDirectory(outDir);
                return true;
            }
            catch (Exception ex)
            {
                SetStatus($"Không thể tạo thư mục xuất PDF: {ex.Message}", StatusTone.Error);
                return false;
            }
        }

        private string HandleFileConflict(string path)
        {
            if (!File.Exists(path))
            {
                return path;
            }

            if (IsFileLocked(path))
            {
                UiFeedback.ShowError(
                    $"File '{Path.GetFileName(path)}' đang được mở bởi ứng dụng khác.\n\nVui lòng đóng file PDF và thử lại.");
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

            var dir = Path.GetDirectoryName(path) ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var name = Path.GetFileNameWithoutExtension(path);

            if (System.Text.RegularExpressions.Regex.IsMatch(name, @"_v\d+$"))
            {
                name = System.Text.RegularExpressions.Regex.Replace(name, @"_v\d+$", string.Empty);
            }

            var version = 2;
            string newPath;
            do
            {
                newPath = Path.Combine(dir, $"{name}_v{version}.pdf");
                version++;
            }
            while (File.Exists(newPath));

            return newPath;
        }

        private static bool IsFileLocked(string file)
        {
            try
            {
                using var stream = new FileInfo(file).Open(FileMode.Open, FileAccess.Read, FileShare.None);
                return false;
            }
            catch (IOException ex)
            {
                PluginLogger.Warn("Suppressed exception: " + ex.Message);
                return true;
            }
        }

        private void SetStatus(string message, StatusTone tone)
        {
            txtStatus.Text = UiText.Normalize(message);
            txtStatus.Foreground = new System.Windows.Media.SolidColorBrush(tone switch
            {
                StatusTone.Success => System.Windows.Media.Color.FromRgb(154, 205, 50),
                StatusTone.Error => System.Windows.Media.Color.FromRgb(220, 20, 60),
                _ => System.Windows.Media.Color.FromRgb(255, 165, 0)
            });
        }

        private enum StatusTone
        {
            InProgress,
            Success,
            Error
        }
    }
}
