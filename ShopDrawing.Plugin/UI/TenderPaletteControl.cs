using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ShopDrawing.Plugin.Commands;
using ShopDrawing.Plugin.Core;
using ShopDrawing.Plugin.Models;

namespace ShopDrawing.Plugin.UI
{
    /// <summary>
    /// Palette control cho Tender — dockable trong AutoCAD PaletteSet.
    /// Compact layout: project info + 2 nút mở dialog + footer + actions.
    /// </summary>
    public class TenderPaletteControl : UserControl
    {
        private TextBox _txtProjectName;
        private TextBox _txtCustomerName;
        private TextBlock _lblFooter;

        // Current project — shared giữa palette và dialogs
        private TenderProject? _currentProject;
        private readonly TenderProjectManager _projectManager = new();

        public TenderPaletteControl()
        {
            var mainStack = new StackPanel { Margin = new Thickness(10) };

            // ═══ Header ═══
            mainStack.Children.Add(SdPaletteStyles.CreateHeader("TENDER - CHÀO GIÁ"));

            // ═══ Project Info ═══
            var projectSection = new StackPanel();
            projectSection.Children.Add(SdPaletteStyles.CreateSectionHeader("THÔNG TIN DỰ ÁN"));

            // Dự án
            var stackProject = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 5) };
            stackProject.Children.Add(MakeLabel("Dự án:", 80));
            _txtProjectName = new TextBox
            {
                Width = 140,
                VerticalContentAlignment = VerticalAlignment.Center,
                ToolTip = "Tên dự án chào giá"
            };
            stackProject.Children.Add(_txtProjectName);
            projectSection.Children.Add(stackProject);

            // Khách hàng
            var stackCustomer = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 5) };
            stackCustomer.Children.Add(MakeLabel("Khách hàng:", 80));
            _txtCustomerName = new TextBox
            {
                Width = 140,
                VerticalContentAlignment = VerticalAlignment.Center,
                ToolTip = "Tên khách hàng"
            };
            stackCustomer.Children.Add(_txtCustomerName);
            projectSection.Children.Add(stackCustomer);

            mainStack.Children.Add(SdPaletteStyles.CreateSectionBorder(projectSection));

            // ═══ Action Buttons ═══
            var actionSection = new StackPanel();
            actionSection.Children.Add(SdPaletteStyles.CreateSectionHeader("QUẢN LÝ"));

            var btnSpec = SdPaletteStyles.CreateActionButton("📋 Quản Lý Spec", SdPaletteStyles.BtnDefaultBrush);
            btnSpec.ToolTip = "Khai báo spec cho dự án (fork từ ShopDrawing)";
            btnSpec.Click += OnOpenSpecManager;
            actionSection.Children.Add(btnSpec);

            var btnBom = SdPaletteStyles.CreateActionButton("📊 Quản Lý Khối Lượng", SdPaletteStyles.AccentBlueBrush);
            btnBom.ToolTip = "Nhập vách, opening, tính BOM";
            btnBom.Click += OnOpenBomDialog;
            actionSection.Children.Add(btnBom);

            mainStack.Children.Add(SdPaletteStyles.CreateSectionBorder(actionSection));

            // ═══ Footer: Summary ═══
            var footerSection = new StackPanel();
            footerSection.Children.Add(SdPaletteStyles.CreateSectionHeader("TỔNG HỢP"));

            _lblFooter = new TextBlock
            {
                Text = "Chưa có dữ liệu",
                FontFamily = SdPaletteStyles.Font,
                FontSize = SdPaletteStyles.FontSizeNormal,
                Foreground = SdPaletteStyles.TextMutedBrush,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 4)
            };
            footerSection.Children.Add(_lblFooter);
            mainStack.Children.Add(SdPaletteStyles.CreateSectionBorder(footerSection));

            // ═══ Bottom Actions ═══
            var bottomStack = new StackPanel();

            var btnExport = SdPaletteStyles.CreateActionButton("📥 Xuất Excel", SdPaletteStyles.AccentGreenBrush);
            btnExport.Click += OnExportExcel;
            bottomStack.Children.Add(btnExport);

            var btnSave = SdPaletteStyles.CreateOutlineButton("💾 Lưu dự án");
            btnSave.Click += OnSaveProject;
            bottomStack.Children.Add(btnSave);

            var btnLoad = SdPaletteStyles.CreateOutlineButton("📂 Mở dự án");
            btnLoad.Click += OnLoadProject;
            bottomStack.Children.Add(btnLoad);

            mainStack.Children.Add(bottomStack);

            Content = SdPaletteStyles.WrapInScrollViewer(mainStack);
            Background = SdPaletteStyles.BgPrimaryBrush;

            // Global exception handler to prevent AutoCAD crash
            Dispatcher.UnhandledException += (s, ex) =>
            {
                ex.Handled = true;
                try
                {
                    MessageBox.Show($"Lỗi: {ex.Exception.Message}", "⚠️ Tender",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                catch { }
            };
        }

        // ═══ Event Handlers ═══

        private void EnsureProject()
        {
            if (_currentProject == null)
            {
                // Try auto-load from DWG-linked autosave
                string dwgName = GetDwgFileName();
                if (!string.IsNullOrEmpty(dwgName))
                {
                    var loaded = _projectManager.TryAutoLoad(dwgName);
                    if (loaded != null)
                    {
                        _currentProject = loaded;
                        _txtProjectName.Text = loaded.ProjectName;
                        _txtCustomerName.Text = loaded.CustomerName;
                        UpdateFooter();
                        return;
                    }
                }

                _currentProject = _projectManager.CreateNew(
                    _txtProjectName.Text.Trim(),
                    _txtCustomerName.Text.Trim());
            }
            else
            {
                _currentProject.ProjectName = _txtProjectName.Text.Trim();
                _currentProject.CustomerName = _txtCustomerName.Text.Trim();
            }
        }

        /// <summary>Lấy tên file DWG hiện tại</summary>
        private static string GetDwgFileName()
        {
            try
            {
                var doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
                return doc?.Name ?? "";
            }
            catch { return ""; }
        }

        /// <summary>Auto-save project theo DWG hiện tại</summary>
        private void AutoSaveProject()
        {
            if (_currentProject == null) return;
            try
            {
                string dwgName = GetDwgFileName();
                if (!string.IsNullOrEmpty(dwgName))
                    _projectManager.AutoSave(_currentProject, dwgName);
            }
            catch { }
        }

        private void OnOpenSpecManager(object sender, RoutedEventArgs e)
        {
            try
            {
                EnsureProject();
                // Tạo SpecConfigManager riêng cho Tender (fork pattern)
                var tenderSpecManager = new SpecConfigManager(_currentProject!.Specs);
                var dialog = new SpecManagerDialog(tenderSpecManager);
                Autodesk.AutoCAD.ApplicationServices.Application.ShowModalWindow(dialog);
                // Cập nhật specs từ dialog về project
                _currentProject.Specs = tenderSpecManager.GetAll();
                AutoSaveProject();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi mở Spec Manager: {ex.Message}", "Lỗi",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private TenderBomDialog? _bomDialog;

        private void OnOpenBomDialog(object sender, RoutedEventArgs e)
        {
            try
            {
                EnsureProject();

                // Nếu dialog đã mở → đưa lên top, không mở lại
                if (_bomDialog != null && _bomDialog.IsLoaded)
                {
                    _bomDialog.Activate();
                    return;
                }

                _bomDialog = new TenderBomDialog(_currentProject!);
                _bomDialog.Topmost = false;
                _bomDialog.ShowInTaskbar = true;

                // Khi dialog đóng → sync data về project
                _bomDialog.Closed += (s, args) =>
                {
                    AutoSaveProject();
                    UpdateFooter();
                    _bomDialog = null;
                };

                // Show modeless — không block AutoCAD
                _bomDialog.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi mở BOM Dialog: {ex.Message}", "Lỗi",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void OnExportExcel(object sender, RoutedEventArgs e)
        {
            if (_currentProject == null || _currentProject.Walls.Count == 0)
            {
                MessageBox.Show("Chưa có dữ liệu. Hãy nhập vách trước.", "Thông báo",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                var dlg = new Microsoft.Win32.SaveFileDialog
                {
                    Title = "Chọn nơi lưu file Excel",
                    Filter = "Excel Files (*.xlsx)|*.xlsx",
                    FileName = $"Tender_{SanitizeName(_currentProject.ProjectName)}_{DateTime.Now:yyyyMMdd}.xlsx",
                    InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
                };
                if (dlg.ShowDialog() != true) return;

                var exporter = new TenderExcelExporter();
                exporter.Export(_currentProject, dlg.FileName);
                MessageBox.Show($"Đã xuất Excel:\n{dlg.FileName}", "Thành công",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi xuất Excel: {ex.Message}", "Lỗi",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnSaveProject(object sender, RoutedEventArgs e)
        {
            if (_currentProject == null)
            {
                MessageBox.Show("Chưa có dự án. Hãy nhập dữ liệu trước.", "Thông báo",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                EnsureProject();

                string defaultName = $"Tender_{SanitizeName(_currentProject.ProjectName ?? "DuAn")}_{DateTime.Now:yyyyMMdd}.json";
                var dlg = new Microsoft.Win32.SaveFileDialog
                {
                    Title = "Lưu dự án Tender",
                    Filter = "JSON Files (*.json)|*.json",
                    FileName = defaultName,
                    InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
                };

                if (dlg.ShowDialog() != true) return;

                string path = _projectManager.Save(_currentProject, dlg.FileName);
                MessageBox.Show($"Đã lưu:\n{path}", "Thành công",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi lưu: {ex.Message}", "Lỗi",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnLoadProject(object sender, RoutedEventArgs e)
        {
            try
            {
                // Safe InitialDirectory — fallback to Desktop if folder doesn't exist
                string initDir;
                try
                {
                    initDir = Directory.Exists(_projectManager.ProjectsFolder)
                        ? _projectManager.ProjectsFolder
                        : Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                }
                catch { initDir = Environment.GetFolderPath(Environment.SpecialFolder.Desktop); }

                var dlg = new Microsoft.Win32.OpenFileDialog
                {
                    Title = "Mở dự án Tender",
                    Filter = "JSON Files (*.json)|*.json",
                    InitialDirectory = initDir
                };
                if (dlg.ShowDialog() != true) return;

                var project = _projectManager.Load(dlg.FileName);
                if (project == null)
                {
                    MessageBox.Show("Không đọc được file.", "Lỗi",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                _currentProject = project;
                _txtProjectName.Text = project.ProjectName ?? "";
                _txtCustomerName.Text = project.CustomerName ?? "";
                UpdateFooter();
                MessageBox.Show($"Đã mở: {project.ProjectName}\n{project.Walls.Count} vách",
                    "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi mở: {ex.Message}", "Lỗi",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>Cập nhật footer summary từ project data</summary>
        public void UpdateFooter()
        {
            if (_currentProject == null || _currentProject.Walls.Count == 0)
            {
                _lblFooter.Text = "Chưa có dữ liệu";
                _lblFooter.Foreground = SdPaletteStyles.TextMutedBrush;
                return;
            }

            int wallCount = _currentProject.Walls.Count;
            double totalArea = _currentProject.Walls.Sum(w => w.WallAreaM2);
            double netArea = _currentProject.Walls.Sum(w => w.NetAreaM2);
            int panelCount = _currentProject.Walls.Sum(w => w.EstimatedPanelCount);

            _lblFooter.Text = $"● {wallCount} vách | {totalArea:F1} m² | Net: {netArea:F1} m² | ~{panelCount} tấm";
            _lblFooter.Foreground = SdPaletteStyles.AccentGreenBrush;
        }

        private static TextBlock MakeLabel(string text, double width = 0)
        {
            var lbl = SdPaletteStyles.CreateLabel(text);
            lbl.VerticalAlignment = VerticalAlignment.Center;
            if (width > 0) lbl.Width = width;
            return lbl;
        }

        private static string SanitizeName(string name)
        {
            foreach (char c in System.IO.Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name.Trim().Replace(' ', '_');
        }
    }
}
