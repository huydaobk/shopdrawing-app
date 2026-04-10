﻿using System;

using System.ComponentModel;

using System.IO;

using System.Linq;

using System.Windows;

using System.Windows.Controls;

using System.Windows.Media;

using System.Windows.Threading;

using ShopDrawing.Plugin.Commands;

using ShopDrawing.Plugin.Core;

using ShopDrawing.Plugin.Models;



namespace ShopDrawing.Plugin.UI

{

    /// <summary>

    /// Palette control cho Tender dockable trong AutoCAD PaletteSet.

    /// Compact layout: project info + 2 nut mo dialog + footer + actions.

    /// </summary>

    public class TenderPaletteControl : UserControl

    {

        private TextBox _txtProjectName;

        private TextBox _txtCustomerName;

        private TextBlock _lblFooter;

        private readonly DispatcherUnhandledExceptionEventHandler _dispatcherUnhandledExceptionHandler;



        // Current project shared giua palette va dialogs

        private TenderProject? _currentProject;

        private readonly TenderProjectManager _projectManager = new();
        private readonly ProjectProfileManager _projectProfileManager = new();

        private TenderBomDialog? _bomDialog;



        public TenderPaletteControl()

        {

            _dispatcherUnhandledExceptionHandler = HandleDispatcherUnhandledException;

            var mainStack = new StackPanel { Margin = new Thickness(10) };



            // Header

            mainStack.Children.Add(SdPaletteStyles.CreateHeader("TENDER - CHÀO GIÁ"));



            // Project info

            var projectSection = new StackPanel();

            projectSection.Children.Add(SdPaletteStyles.CreateSectionHeader("THÔNG TIN DỰ ÁN"));



            // Du an

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



            // Khach hang

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



            // Action buttons

            var actionSection = new StackPanel();

            actionSection.Children.Add(SdPaletteStyles.CreateSectionHeader("QUẢN LÝ"));



            var btnSpec = SdPaletteStyles.CreateActionButton("Quản lý Spec", SdPaletteStyles.BtnDefaultBrush);

            btnSpec.ToolTip = "Khai báo Spec cho dự án Tender";

            btnSpec.Click += OnOpenSpecManager;

            actionSection.Children.Add(btnSpec);



            var btnBom = SdPaletteStyles.CreateActionButton("Quản lý Khối lượng", SdPaletteStyles.AccentBlueBrush);

            btnBom.ToolTip = "Nhập vách, lỗ mở và tính BOM";

            btnBom.Click += OnOpenBomDialog;

            actionSection.Children.Add(btnBom);



            mainStack.Children.Add(SdPaletteStyles.CreateSectionBorder(actionSection));



            // Footer summary

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



            // Bottom actions

            var bottomStack = new StackPanel();



            var btnExport = SdPaletteStyles.CreateActionButton("Xuất Excel", SdPaletteStyles.AccentGreenBrush);

            btnExport.Click += OnExportExcel;

            bottomStack.Children.Add(btnExport);



            var btnSave = SdPaletteStyles.CreateOutlineButton("Lưu dự án");

            btnSave.Click += OnSaveProject;

            bottomStack.Children.Add(btnSave);



            var btnLoad = SdPaletteStyles.CreateOutlineButton("Mở dự án");

            btnLoad.Click += OnLoadProject;

            bottomStack.Children.Add(btnLoad);



            mainStack.Children.Add(bottomStack);



            Content = SdPaletteStyles.WrapInScrollViewer(mainStack);

            Background = SdPaletteStyles.BgPrimaryBrush;
            ApplyProjectProfile(_projectProfileManager.LoadOrDefault(), overwriteFields: false);



            // Global exception handler to prevent AutoCAD crash

            Loaded += OnLoaded;

            Unloaded += OnUnloaded;

        }



        // Event handlers



        private void OnLoaded(object sender, RoutedEventArgs e)

        {

            Dispatcher.UnhandledException -= _dispatcherUnhandledExceptionHandler;

            Dispatcher.UnhandledException += _dispatcherUnhandledExceptionHandler;
            ProjectProfileManager.ProfileUpdated -= HandleProjectProfileUpdated;
            ProjectProfileManager.ProfileUpdated += HandleProjectProfileUpdated;
            ApplyProjectProfile(_projectProfileManager.LoadOrDefault(), overwriteFields: true);

        }



        private void OnUnloaded(object sender, RoutedEventArgs e)

        {

            Dispatcher.UnhandledException -= _dispatcherUnhandledExceptionHandler;
            ProjectProfileManager.ProfileUpdated -= HandleProjectProfileUpdated;

        }



        private void HandleDispatcherUnhandledException(object? sender, DispatcherUnhandledExceptionEventArgs ex)

        {

            ex.Handled = true;

            try

            {

            UiFeedback.ShowWarning($"Lỗi: {ex.Exception.Message}", "Tender");

            }

            catch (System.Exception innerEx)

            {

                ShopDrawing.Plugin.Core.PluginLogger.Error("Suppressed exception in TenderPaletteControl.cs", innerEx);

            }

        }



        private void EnsureProject()

        {
            var profile = _projectProfileManager.LoadOrDefault();
            if (string.IsNullOrWhiteSpace(_txtProjectName.Text) && !string.IsNullOrWhiteSpace(profile.ProjectName))
            {
                _txtProjectName.Text = profile.ProjectName;
            }

            if (string.IsNullOrWhiteSpace(_txtCustomerName.Text) && !string.IsNullOrWhiteSpace(profile.CustomerName))
            {
                _txtCustomerName.Text = profile.CustomerName;
            }

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



        /// <summary>Lay ten file DWG hien tai.</summary>

        private static string GetDwgFileName()

        {

            try

            {

                var doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;

                return doc?.Name ?? "";

            }

            catch (System.Exception ex) { ShopDrawing.Plugin.Core.PluginLogger.Warn("Suppressed exception: " + ex.Message); 

                return "";

            }

        }



        /// <summary>Auto-save project theo DWG hien tai.</summary>

        private void AutoSaveProject()

        {

            if (_currentProject == null)

            {

                return;

            }



            try

            {

                string dwgName = GetDwgFileName();

                if (!string.IsNullOrEmpty(dwgName))

                {

                    _projectManager.AutoSave(_currentProject, dwgName);

                }

            }

            catch (System.Exception innerEx)

            {

                ShopDrawing.Plugin.Core.PluginLogger.Error("Suppressed exception in TenderPaletteControl.cs", innerEx);

            }

        }



        private void OnOpenSpecManager(object sender, RoutedEventArgs e)

        {

            try

            {

                EnsureProject();



                // Tao SpecConfigManager rieng cho Tender

                var tenderSpecManager = new SpecConfigManager(_currentProject!.Specs);

                var dialog = new SpecManagerDialog(tenderSpecManager);

                Autodesk.AutoCAD.ApplicationServices.Application.ShowModalWindow(dialog);



                // Cap nhat specs tu dialog vao project

                _currentProject.Specs = tenderSpecManager.GetAll();

                AutoSaveProject();

            }

            catch (Exception ex)

            {

            UiFeedback.ShowWarning($"Lỗi mở Spec Manager: {ex.Message}", "Lỗi");

            }

        }



        private void OnOpenBomDialog(object sender, RoutedEventArgs e)

        {

            try

            {

                EnsureProject();



                // Neu dialog da mo thi dua len truoc, khong mo lai

                if (_bomDialog != null && _bomDialog.IsLoaded)

                {

                    _bomDialog.Activate();

                    return;

                }



                _bomDialog = new TenderBomDialog(_currentProject!);

                _bomDialog.Topmost = false;

                _bomDialog.ShowInTaskbar = true;



                // Khi dialog dong thi sync data ve project

                _bomDialog.Closed += (s, args) =>

                {

                    AutoSaveProject();

                    UpdateFooter();

                    _bomDialog = null;

                };



                // Show modeless de khong block AutoCAD

                _bomDialog.Show();

            }

            catch (Exception ex)

            {

            UiFeedback.ShowWarning($"Lỗi mở BOM Dialog: {ex.Message}", "Lỗi");

            }

        }



        private void OnExportExcel(object sender, RoutedEventArgs e)

        {

            if (_currentProject == null || _currentProject.Walls.Count == 0)

            {

            UiFeedback.ShowInfo("Chưa có dữ liệu. Hãy nhập vách trước.");

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

                if (dlg.ShowDialog() != true)

                {

                    return;

                }



                var exporter = new TenderExcelExporter();

                exporter.Export(_currentProject, dlg.FileName);

            UiFeedback.ShowInfo($"Đã xuất Excel:\n{dlg.FileName}", "Thành công");

            }

            catch (Exception ex)

            {

            UiFeedback.ShowError($"Lỗi xuất Excel: {ex.Message}");

            }

        }



        private void OnSaveProject(object sender, RoutedEventArgs e)

        {

            if (_currentProject == null)

            {

            UiFeedback.ShowInfo("Chưa có dự án. Hãy nhập dữ liệu trước.");

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



                if (dlg.ShowDialog() != true)

                {

                    return;

                }



                string path = _projectManager.Save(_currentProject, dlg.FileName);

            UiFeedback.ShowInfo($"Đã lưu:\n{path}", "Thành công");

            }

            catch (Exception ex)

            {

            UiFeedback.ShowError($"Lỗi lưu: {ex.Message}");

            }

        }



        private void OnLoadProject(object sender, RoutedEventArgs e)

        {

            try

            {

                // Safe InitialDirectory: fallback to Desktop if folder doesn't exist

                string initDir;

                try

                {

                    initDir = Directory.Exists(_projectManager.ProjectsFolder)

                        ? _projectManager.ProjectsFolder

                        : Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

                }

                catch (System.Exception ex) { ShopDrawing.Plugin.Core.PluginLogger.Warn("Suppressed exception: " + ex.Message); 

                    initDir = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

                }



                var dlg = new Microsoft.Win32.OpenFileDialog

                {

                Title = "Mở dự án Tender",

                    Filter = "JSON Files (*.json)|*.json",

                    InitialDirectory = initDir

                };

                if (dlg.ShowDialog() != true)

                {

                    return;

                }



                var project = _projectManager.Load(dlg.FileName);

                if (project == null)

                {

            UiFeedback.ShowWarning("Không đọc được file.", "Lỗi");

                    return;

                }



                _currentProject = project;

                _txtProjectName.Text = project.ProjectName ?? "";

                _txtCustomerName.Text = project.CustomerName ?? "";

                UpdateFooter();

            UiFeedback.ShowInfo($"Đã mở: {project.ProjectName}\n{project.Walls.Count} vách", "Thành công");

            }

            catch (Exception ex)

            {

            UiFeedback.ShowError($"Lỗi mở: {ex.Message}");

            }

        }



        /// <summary>Cap nhat footer summary tu project data.</summary>

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



            _lblFooter.Text = $"| {wallCount} vách | {totalArea:F1} m² | Net: {netArea:F1} m² | ~{panelCount} tấm";

            _lblFooter.Foreground = SdPaletteStyles.AccentGreenBrush;

        }



        private static TextBlock MakeLabel(string text, double width = 0)

        {

            var lbl = SdPaletteStyles.CreateLabel(text);

            lbl.VerticalAlignment = VerticalAlignment.Center;

            if (width > 0)

            {

                lbl.Width = width;

            }



            return lbl;

        }



        private void HandleProjectProfileUpdated(ProjectProfile profile)

        {

            Dispatcher.BeginInvoke(new Action(() =>

            {

                ApplyProjectProfile(profile, overwriteFields: true);

            }));

        }



        private void ApplyProjectProfile(ProjectProfile profile, bool overwriteFields)

        {

            if (overwriteFields || string.IsNullOrWhiteSpace(_txtProjectName.Text))

            {

                _txtProjectName.Text = profile.ProjectName ?? string.Empty;

            }

            if (overwriteFields || string.IsNullOrWhiteSpace(_txtCustomerName.Text))

            {

                _txtCustomerName.Text = profile.CustomerName ?? string.Empty;

            }

            if (_currentProject != null)

            {

                _currentProject.ProjectName = _txtProjectName.Text.Trim();

                _currentProject.CustomerName = _txtCustomerName.Text.Trim();

            }

        }



        private static string SanitizeName(string name)

        {

            foreach (char c in Path.GetInvalidFileNameChars())

            {

                name = name.Replace(c, '_');

            }



            return name.Trim().Replace(' ', '_');

        }

    }

}

