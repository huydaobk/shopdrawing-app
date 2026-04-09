﻿using System;

using System.Collections.Generic;

using System.Linq;

using System.Windows;

using System.Windows.Controls;

using System.Windows.Media;

using Autodesk.AutoCAD.DatabaseServices;

using Microsoft.Win32;

using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;

using ShopDrawing.Plugin.Core;
using ShopDrawing.Plugin.Models;



namespace ShopDrawing.Plugin.UI

{

    /// <summary>

    /// Layout Manager Palette.

    /// - Chon kho giay va margin

    /// - Nhap ten du an

    /// - Built-in / external title block

    /// - Nhap ten tuong, pick vung va tao layout

    /// - Hien thi danh sach layout da tao

    /// </summary>

    public class LayoutManagerPaletteControl : UserControl

    {

        private readonly LayoutManagerEngine _engine = new();
        private readonly ProjectProfileManager _projectProfileManager = new();



        private ComboBox _cbPaper = null!;

        private TextBox _txtMarginL = null!;

        private TextBox _txtMarginO = null!;

        private TextBlock _lblScale = null!;

        private TextBox _txtProject = null!;

        private RadioButton _rbBuiltIn = null!;

        private RadioButton _rbExternal = null!;

        private TextBox _txtExternalPath = null!;

        private Button _btnChooseExternal = null!;

        private TextBlock _lblExternalSummary = null!;

        private ComboBox _cboTitle = null!;

        private TextBox _txtPlanTitle = null!;

        private TextBlock _lblPlanRegion = null!;

        private ListBox _lstLayouts = null!;

        private TextBlock _lblStatus = null!;



        private LayoutTitleBlockConfig _titleBlockConfig = new();

        private bool _isUpdatingTitleBlockUi;

        private Extents3d? _selectedPlanRegion;

        private static readonly object PendingRequestGate = new();

        private static readonly Dictionary<string, PendingLayoutRequest> PendingRequests = new(StringComparer.OrdinalIgnoreCase);



        public LayoutManagerPaletteControl()

        {

            var root = new StackPanel { Margin = new Thickness(10) };



            // Header

            root.Children.Add(SdPaletteStyles.CreateHeader("LAYOUT MANAGER"));



            // Paper and scale section

            var paperSection = new StackPanel();

            paperSection.Children.Add(SdPaletteStyles.CreateSectionHeader("KHO GIAY VA TY LE"));



            var rowPaper = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };

            _cbPaper = new ComboBox { Width = 70, Margin = new Thickness(0, 0, 8, 0) };

            foreach (var paper in new[] { "A3", "A2", "A1" })

            {

                _cbPaper.Items.Add(paper);

            }



            _cbPaper.SelectedIndex = 0;

            rowPaper.Children.Add(_cbPaper);

            rowPaper.Children.Add(new TextBlock

            {

                Text = "Ngang",

                VerticalAlignment = VerticalAlignment.Center,

                Foreground = SdPaletteStyles.TextMutedBrush,

                FontStyle = FontStyles.Italic,

                FontFamily = SdPaletteStyles.Font

            });

            paperSection.Children.Add(rowPaper);



            var rowMargin = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };

            rowMargin.Children.Add(MakeLabel("Margin:", 50));

            rowMargin.Children.Add(MakeLabel("Trai:"));

            _txtMarginL = new TextBox { Width = 35, Text = "25", Padding = new Thickness(4, 2, 4, 2), Margin = new Thickness(2, 0, 8, 0) };

            rowMargin.Children.Add(_txtMarginL);

            rowMargin.Children.Add(MakeLabel("Khac:"));

            _txtMarginO = new TextBox { Width = 35, Text = "5", Padding = new Thickness(4, 2, 4, 2), Margin = new Thickness(2, 0, 0, 0) };

            rowMargin.Children.Add(_txtMarginO);

            paperSection.Children.Add(rowMargin);



            var rowScale = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 0) };

            rowScale.Children.Add(MakeLabel("Tỷ lệ:", 50));

            _lblScale = new TextBlock

            {

                Text = "...",

                VerticalAlignment = VerticalAlignment.Center,

                Foreground = SdPaletteStyles.AccentBlueBrush,

                FontWeight = FontWeights.Bold,

                FontFamily = SdPaletteStyles.Font

            };

            rowScale.Children.Add(_lblScale);

            var btnRefresh = SdPaletteStyles.CreateCompactButton("Làm mới");

            btnRefresh.Width = 60;

            btnRefresh.Height = 22;

            btnRefresh.Margin = new Thickness(6, 0, 0, 0);

            btnRefresh.Click += (_, _) => RefreshScale();

            rowScale.Children.Add(btnRefresh);

            paperSection.Children.Add(rowScale);

            root.Children.Add(SdPaletteStyles.CreateSectionBorder(paperSection));



            // Project and title block section

            var projectSection = new StackPanel();

            projectSection.Children.Add(SdPaletteStyles.CreateSectionHeader("DU AN VA TITLE BLOCK"));



            projectSection.Children.Add(MakeLabel("Tên dự án:"));

            _txtProject = new TextBox

            {

                Padding = new Thickness(6, 4, 6, 4),

                Margin = new Thickness(0, 2, 0, 8),

                ToolTip = "Hiển thị trong khung tên và danh mục bản vẽ"

            };

            projectSection.Children.Add(_txtProject);



            projectSection.Children.Add(MakeLabel("Title Block:"));

            _rbBuiltIn = new RadioButton

            {

                Content = "Mặc định (TCVN)",

                Foreground = SdPaletteStyles.TextPrimaryBrush,

                FontFamily = SdPaletteStyles.Font,

                Margin = new Thickness(0, 2, 0, 4)

            };

            _rbBuiltIn.Checked += (_, _) => HandleBuiltInChecked();

            projectSection.Children.Add(_rbBuiltIn);



            var rowExternal = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };

            _rbExternal = new RadioButton

            {

                Content = "Khách hàng",

                Foreground = SdPaletteStyles.TextPrimaryBrush,

                FontFamily = SdPaletteStyles.Font,

                VerticalAlignment = VerticalAlignment.Center

            };

            _rbExternal.Checked += (_, _) => HandleExternalChecked();

            rowExternal.Children.Add(_rbExternal);



            _btnChooseExternal = SdPaletteStyles.CreateCompactButton("Chọn .DWG...", SdPaletteStyles.AccentBlueBrush);

            _btnChooseExternal.Margin = new Thickness(8, 0, 0, 0);

            _btnChooseExternal.Click += (_, _) => ChooseExternalTitleBlock();

            rowExternal.Children.Add(_btnChooseExternal);

            projectSection.Children.Add(rowExternal);



            _txtExternalPath = new TextBox

            {

                IsReadOnly = true,

                Margin = new Thickness(0, 0, 0, 4),

                Padding = new Thickness(6, 4, 6, 4)

            };

            projectSection.Children.Add(_txtExternalPath);



            _lblExternalSummary = new TextBlock

            {

                TextWrapping = TextWrapping.Wrap,

                Foreground = SdPaletteStyles.TextMutedBrush,

                FontSize = 10,

                FontFamily = SdPaletteStyles.Font,

                Margin = new Thickness(0, 0, 0, 6)

            };

            projectSection.Children.Add(_lblExternalSummary);

            root.Children.Add(SdPaletteStyles.CreateSectionBorder(projectSection));



            // Create layout section

            var createSection = new StackPanel();

            createSection.Children.Add(SdPaletteStyles.CreateSectionHeader("TAO LAYOUT"));



            createSection.Children.Add(MakeLabel("Tên bản vẽ (View Title):"));

            var rowTitle = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 6) };

            rowTitle.Children.Add(new TextBlock

            {

                Text = "MẶT ĐỨNG VÁCH -",

                VerticalAlignment = VerticalAlignment.Center,

                FontSize = 10,

                FontFamily = SdPaletteStyles.Font,

                Foreground = SdPaletteStyles.TextMutedBrush,

                Margin = new Thickness(0, 0, 4, 0)

            });

            _cboTitle = new ComboBox

            {

                Width = 110,

                IsEditable = true,

                Text = "",

                VerticalContentAlignment = VerticalAlignment.Center,

                ToolTip = "Tự nhận từ vùng chọn, hoặc nhập thủ công (VD: W1, TRỤC A...)"

            };

            rowTitle.Children.Add(_cboTitle);

            createSection.Children.Add(rowTitle);



            createSection.Children.Add(MakeLabel("Mặt bằng kèm theo (tuỳ chọn):"));

            var rowPlanTitle = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 4) };

            rowPlanTitle.Children.Add(new TextBlock

            {

                Text = "MẶT BẰNG -",

                VerticalAlignment = VerticalAlignment.Center,

                FontSize = 10,

                FontFamily = SdPaletteStyles.Font,

                Foreground = SdPaletteStyles.TextMutedBrush,

                Margin = new Thickness(0, 0, 4, 0)

            });

            _txtPlanTitle = new TextBox

            {

                Width = 110,

                Padding = new Thickness(4, 2, 4, 2),

                ToolTip = "Để trống sẽ dùng cùng mã với mặt đứng"

            };

            rowPlanTitle.Children.Add(_txtPlanTitle);

            createSection.Children.Add(rowPlanTitle);

            var rowPlanActions = new Grid { Margin = new Thickness(0, 0, 0, 6) };

            rowPlanActions.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            rowPlanActions.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });

            var btnPickPlan = SdPaletteStyles.CreateCompactButton("Chọn mặt bằng", SdPaletteStyles.AccentBlueBrush);

            btnPickPlan.Width = 100;

            btnPickPlan.Click += (_, _) => PickPlanRegion();

            Grid.SetColumn(btnPickPlan, 0);

            rowPlanActions.Children.Add(btnPickPlan);

            var btnClearPlan = SdPaletteStyles.CreateCompactButton("Bỏ", SdPaletteStyles.AccentRedBrush);

            btnClearPlan.Width = 44;

            btnClearPlan.Margin = new Thickness(6, 0, 0, 0);

            btnClearPlan.Click += (_, _) => ClearPlanRegion();

            Grid.SetColumn(btnClearPlan, 1);

            rowPlanActions.Children.Add(btnClearPlan);

            createSection.Children.Add(rowPlanActions);

            _lblPlanRegion = new TextBlock

            {

                Text = "Chưa chọn mặt bằng.",

                TextWrapping = TextWrapping.Wrap,

                Foreground = SdPaletteStyles.TextMutedBrush,

                FontSize = 10,

                FontFamily = SdPaletteStyles.Font,

                Margin = new Thickness(0, 0, 0, 6)

            };

            createSection.Children.Add(_lblPlanRegion);

            var btnPick = SdPaletteStyles.CreateActionButton("Chọn vùng và tạo layout", SdPaletteStyles.AccentGreenBrush);

            btnPick.Click += (_, _) => PickAndCreate();

            createSection.Children.Add(btnPick);

            root.Children.Add(SdPaletteStyles.CreateSectionBorder(createSection));



            // Layout list section

            var listSection = new StackPanel();

            listSection.Children.Add(SdPaletteStyles.CreateSectionHeader("LAYOUT ĐÃ TẠO"));

            _lstLayouts = new ListBox

            {

                Height = 100,

                Margin = new Thickness(0, 4, 0, 6),

                FontFamily = new FontFamily("Consolas"),

                FontSize = 10,

                Background = SdPaletteStyles.BgSectionBrush,

                Foreground = SdPaletteStyles.TextPrimaryBrush,

                BorderBrush = SdPaletteStyles.BorderBrush,

                BorderThickness = new Thickness(1)

            };

            listSection.Children.Add(_lstLayouts);



            var rowActions = new Grid();

            rowActions.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            rowActions.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });



            var btnDelete = SdPaletteStyles.CreateActionButton("Xóa", SdPaletteStyles.AccentRedBrush);

            Grid.SetColumn(btnDelete, 0);

            btnDelete.Margin = new Thickness(0, 2, 4, 2);

            btnDelete.Click += (_, _) => DeleteSelected();

            rowActions.Children.Add(btnDelete);



            var btnSync = SdPaletteStyles.CreateActionButton("Sync DMBV", SdPaletteStyles.AccentBlueBrush);

            Grid.SetColumn(btnSync, 1);

            btnSync.Margin = new Thickness(4, 2, 0, 2);

            btnSync.Click += (_, _) => SyncDMBD();

            rowActions.Children.Add(btnSync);

            listSection.Children.Add(rowActions);

            root.Children.Add(SdPaletteStyles.CreateSectionBorder(listSection));



            // Status and footer

            _lblStatus = SdPaletteStyles.CreateStatusText();

            _lblStatus.FontWeight = FontWeights.SemiBold;

            root.Children.Add(_lblStatus);



            root.Children.Add(new TextBlock

            {

                Text = "Tường dài sẽ tự động phân trang (PHẦN 1/N)\nChồng lắp 1000mm giữa các trang.",

                Foreground = SdPaletteStyles.TextMutedBrush,

                FontSize = 10,

                FontFamily = SdPaletteStyles.Font,

                TextWrapping = TextWrapping.Wrap,

                Margin = new Thickness(0, 4, 0, 0)

            });



            Content = SdPaletteStyles.WrapInScrollViewer(root);

            Background = SdPaletteStyles.BgPrimaryBrush;
            Loaded += OnLoaded;

            Unloaded += OnUnloaded;



            RefreshScale();

            LoadPersistedTitleBlockConfig();

            ApplyProjectProfile(_projectProfileManager.LoadOrDefault(), overwriteProjectName: false);
            LoadProjectNameFromDocument();

            RefreshLayoutList();

            ScanWallCodesForTitle();

        }



        private void OnUnloaded(object sender, RoutedEventArgs e)

        {
            ProjectProfileManager.ProfileUpdated -= HandleProjectProfileUpdated;
            lock (PendingRequestGate)
            {
                foreach (string key in PendingRequests
                    .Where(entry => entry.Value.GetSource() == this)
                    .Select(entry => entry.Key)
                    .ToList())
                {
                    PendingRequests.Remove(key);
                }
            }

        }



        private void OnLoaded(object sender, RoutedEventArgs e)

        {
            ProjectProfileManager.ProfileUpdated -= HandleProjectProfileUpdated;
            ProjectProfileManager.ProfileUpdated += HandleProjectProfileUpdated;
            ApplyProjectProfile(_projectProfileManager.LoadOrDefault(), overwriteProjectName: true);
        }



        private void RefreshScale()

        {

            double denominator = LayoutManagerEngine.GetCurrentScaleDenominator();

            _lblScale.Text = $"Tỷ lệ 1:{(int)denominator}";

        }



        private void ScanWallCodesForTitle()

        {

            try

            {

                var codes = SmartDimEngine.ScanWallCodes();

                string prev = _cboTitle.Text;

                _cboTitle.Items.Clear();

                foreach (var c in codes)

                {

                    _cboTitle.Items.Add(c);

                }



                if (codes.Count > 0)

                {

                    if (codes.Contains(prev))

                    {

                        _cboTitle.Text = prev;

                    }

                    else

                    {

                        _cboTitle.Text = string.Empty;

                    }

                }

            }

            catch (System.Exception ex)

            {

                ShopDrawing.Plugin.Core.PluginLogger.Error("Suppressed exception in LayoutManagerPaletteControl.cs", ex);

            }

        }



        private void LoadProjectNameFromDocument()

        {

            if (!string.IsNullOrWhiteSpace(_txtProject.Text))

            {

                return;

            }



            var doc = AcadApp.DocumentManager.MdiActiveDocument;

            if (doc == null)

            {

                return;

            }



            _txtProject.Text = DrawingListManager.GetDocumentProjectName(doc);

        }



        private void HandleProjectProfileUpdated(ProjectProfile profile)

        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                ApplyProjectProfile(profile, overwriteProjectName: true);
            }));
        }



        private void ApplyProjectProfile(ProjectProfile profile, bool overwriteProjectName)

        {
            if (overwriteProjectName || string.IsNullOrWhiteSpace(_txtProject.Text))
            {
                _txtProject.Text = profile.ProjectName ?? string.Empty;
            }
        }



        private void ApplyMargins()

        {

            if (double.TryParse(_txtMarginL.Text, out double marginLeft))

            {

                _engine.MarginLeft = marginLeft;

            }



            if (double.TryParse(_txtMarginO.Text, out double marginOther))

            {

                _engine.MarginRight = marginOther;

                _engine.MarginTop = marginOther;

                _engine.MarginBot = marginOther;

            }

        }



        public void SetStatusPublic(string message) => SetStatus(message, false);



        public void RefreshLayoutListPublic() => RefreshLayoutList();



        internal static void EnqueuePendingRequest(LayoutCreationRequest request, LayoutManagerPaletteControl source, string documentKey)

        {
            lock (PendingRequestGate)
            {
                PendingRequests[documentKey] = new PendingLayoutRequest(request, source);
            }
        }



        internal static PendingLayoutRequest? TakePendingRequest(string documentKey)

        {
            lock (PendingRequestGate)
            {
                if (!PendingRequests.TryGetValue(documentKey, out var pending))
                {
                    return null;
                }

                PendingRequests.Remove(documentKey);
                return pending;
            }
        }



        private void PickPlanRegion()

        {

            try

            {

                ApplyMargins();

                SetStatus("Đang chờ chọn vùng mặt bằng...", false);



                var region = _engine.PickRegion();

                if (region == null)

                {

                    SetStatus("Đã hủy chọn mặt bằng.", true);

                    return;

                }



                _selectedPlanRegion = region.Value;

                string resolvedTitle = ResolveTitleFromRegion(

                    region.Value,

                    _txtPlanTitle.Text.Trim().ToUpperInvariant(),

                    autoTitle => Dispatcher.BeginInvoke(new Action(() => _txtPlanTitle.Text = autoTitle)));



                if (string.IsNullOrWhiteSpace(_txtPlanTitle.Text))

                {

                    _txtPlanTitle.Text = resolvedTitle;

                }



                _lblPlanRegion.Text = $"Đã chọn mặt bằng: {DescribeRegion(region.Value)}";

                SetStatus("Đã nhận vùng mặt bằng. Bấm tạo layout để ghép vào bản vẽ.", false);

            }

            catch (Exception ex)

            {

                SetStatus(ex.Message, true);

            }

        }



        private void ClearPlanRegion()

        {

            _selectedPlanRegion = null;

            _txtPlanTitle.Text = string.Empty;

            _lblPlanRegion.Text = "Chưa chọn mặt bằng.";

            SetStatus("Đã bỏ mặt bằng phụ.", false);

        }



        private string ResolveTitleFromRegion(Extents3d region, string currentTitle, Action<string>? applyAutoTitle)

        {

            string title = currentTitle.Trim().ToUpperInvariant();

            var detectedCodes = SmartDimEngine.ScanWallCodesInRegion(region);

            if (detectedCodes.Count == 0)

                return title;



            string autoTitle = string.Join(", ", detectedCodes);

            if (string.IsNullOrWhiteSpace(title) || title == "W1")

            {

                applyAutoTitle?.Invoke(autoTitle);

                return autoTitle;

            }



            if (!title.Equals(autoTitle, StringComparison.OrdinalIgnoreCase))

            {

                var result = UiFeedback.AskYesNo(

                    $"Phát hiện mã tường trong vùng chọn: {autoTitle}\nBạn đang nhập: {title}\n\nDùng mã tự động ({autoTitle})?\n\n- Yes: Dùng '{autoTitle}'\n- No: Giữ '{title}'",

                    "Tự nhận mã tường");



                if (result == MessageBoxResult.Yes)

                {

                    applyAutoTitle?.Invoke(autoTitle);

                    return autoTitle;

                }

            }



            return title;

        }



        private static string DescribeRegion(Extents3d region)

        {

            double width = region.MaxPoint.X - region.MinPoint.X;

            double height = region.MaxPoint.Y - region.MinPoint.Y;

            return $"{width:F0} x {height:F0} mm";

        }



        private void PickAndCreate()

        {

            try

            {

                ApplyMargins();

                SetStatus("Đang chờ chọn vùng...", false);



                var region = _engine.PickRegion();

                if (region == null)

                {

                    SetStatus("Đã hủy.", true);

                    return;

                }



                string title = ResolveTitleFromRegion(

                    region.Value,

                    _cboTitle.Text.Trim().ToUpperInvariant(),

                    autoTitle => Dispatcher.BeginInvoke(new Action(() => _cboTitle.Text = autoTitle)));



                if (string.IsNullOrWhiteSpace(title))

                {

                    SetStatus("Không phát hiện mã tường. Nhập thủ công hoặc chọn vùng có tag.", true);

                    return;

                }



                string planTitle = _txtPlanTitle.Text.Trim().ToUpperInvariant();

                if (_selectedPlanRegion != null && string.IsNullOrWhiteSpace(planTitle))

                {

                    planTitle = title;

                }



                var request = new LayoutCreationRequest

                {

                    UserTitle = title,

                    Region = region.Value,

                    PaperSize = _cbPaper.SelectedItem?.ToString() ?? "A3",

                    ProjectName = _txtProject.Text.Trim(),

                    MarginLeft = _engine.MarginLeft,

                    MarginRight = _engine.MarginRight,

                    MarginTop = _engine.MarginTop,

                    MarginBot = _engine.MarginBot,

                    TitleBlockConfig = BuildRequestTitleBlockConfig(),

                    SecondaryViews = _selectedPlanRegion == null

                        ? Array.Empty<LayoutViewRequest>()

                        : new[]

                        {

                            new LayoutViewRequest

                            {

                                Kind = LayoutViewKind.Plan,

                                UserTitle = planTitle,

                                Region = _selectedPlanRegion.Value

                            }

                        }

                };



                var doc = AutoCadUiContext.GetActiveDocument();

                if (doc == null)

                {

                    SetStatus("Chưa có bản vẽ đang mở.", true);

                    return;

                }



                EnqueuePendingRequest(request, this, doc.Name);

                SetStatus(

                    _selectedPlanRegion == null

                        ? $"Đang tạo layout '{title}'..."

                        : $"Đang tạo layout '{title}' và ghép thêm mặt bằng...",

                    false);



                AutoCadUiContext.TrySendCommand("_SD_LAYOUT_CREATE\n");

            }

            catch (Exception ex)

            {

                SetStatus(ex.Message, true);

                AcadApp.DocumentManager.MdiActiveDocument?.Editor?.WriteMessage($"\n[SD] PickAndCreate error: {ex.Message}");

            }

        }



        private void DeleteSelected()

        {

            if (_lstLayouts.SelectedItem is not string layoutName)

            {

                return;

            }



            try

            {

                var doc = AcadApp.DocumentManager.MdiActiveDocument;

                if (doc == null)

                {

                    return;

                }



                using (var cadLock = SafeCadLock.TryLock())

                {

                    if (cadLock == null)

                    {

                        SetStatus("Bản vẽ đang bận, không thể xóa layout.", true);

                        return;

                    }



                    using (var tr = doc.Database.TransactionManager.StartTransaction())

                    {

                        var layoutDict = (DBDictionary)tr.GetObject(

                            doc.Database.LayoutDictionaryId,

                            OpenMode.ForWrite);

                        if (layoutDict.Contains(layoutName))

                        {

                            LayoutManager.Current.DeleteLayout(layoutName);

                        }



                        tr.Commit();

                    }



                    DrawingListManager.DeleteLayoutMeta(doc, layoutName);

                    DrawingListManager.Sync(doc);

                }



                RefreshLayoutList();

                SetStatus($"Đã xóa: {layoutName}", false);

            }

            catch (Exception ex)

            {

                SetStatus(ex.Message, true);

            }

        }



        private void SyncDMBD()

        {

            try

            {

                var doc = AcadApp.DocumentManager.MdiActiveDocument;

                if (doc == null)

                {

                    return;

                }



                using (var cadLock = SafeCadLock.TryLock())

                {

                    if (cadLock == null)

                    {

                        SetStatus("Bản vẽ đang bận, không thể đồng bộ.", true);

                        return;

                    }



                    DrawingListManager.Sync(doc);

                }



                SetStatus("Danh mục bản vẽ đã cập nhật.", false);

            }

            catch (Exception ex)

            {

                SetStatus(ex.Message, true);

            }

        }



        private void RefreshLayoutList()

        {

            _lstLayouts.Items.Clear();



            try

            {

                var doc = AcadApp.DocumentManager.MdiActiveDocument;

                if (doc == null)

                {

                    return;

                }



                LoadProjectNameFromDocument();

                foreach (string name in DrawingListManager.GetOrderedLayoutNames(doc))

                {

                    _lstLayouts.Items.Add(name);

                }

            }

            catch (System.Exception ex)

            {

                ShopDrawing.Plugin.Core.PluginLogger.Error("Suppressed exception in LayoutManagerPaletteControl.cs", ex);

            }

        }



        private void HandleBuiltInChecked()

        {

            if (_isUpdatingTitleBlockUi)

            {

                return;

            }



            _titleBlockConfig = _titleBlockConfig with { Mode = LayoutTitleBlockMode.BuiltIn };

            PersistTitleBlockConfig();

            UpdateTitleBlockUi();

        }



        private void HandleExternalChecked()

        {

            if (_isUpdatingTitleBlockUi)

            {

                return;

            }



            if (!_titleBlockConfig.IsConfiguredForExternal)

            {

                ChooseExternalTitleBlock();

                return;

            }



            _titleBlockConfig = _titleBlockConfig with { Mode = LayoutTitleBlockMode.External };

            PersistTitleBlockConfig();

            UpdateTitleBlockUi();

        }



        private void LoadPersistedTitleBlockConfig()

        {

            var doc = AcadApp.DocumentManager.MdiActiveDocument;

            _titleBlockConfig = doc == null

                ? new LayoutTitleBlockConfig()

                : LayoutTitleBlockConfigStore.Load(doc);



            UpdateTitleBlockUi();

        }



        private void PersistTitleBlockConfig()

        {

            var doc = AcadApp.DocumentManager.MdiActiveDocument;

            if (doc == null)

            {

                return;

            }



            LayoutTitleBlockConfigStore.Save(doc, _titleBlockConfig);

        }



        private void UpdateTitleBlockUi()

        {

            _isUpdatingTitleBlockUi = true;

            _rbBuiltIn.IsChecked = _titleBlockConfig.Mode == LayoutTitleBlockMode.BuiltIn;

            _rbExternal.IsChecked = _titleBlockConfig.Mode == LayoutTitleBlockMode.External;

            _isUpdatingTitleBlockUi = false;



            _txtExternalPath.Text = _titleBlockConfig.DwgPath;

            _btnChooseExternal.Content = _titleBlockConfig.IsConfiguredForExternal

                ? "Chọn / ánh xạ lại..."

                : "Chọn .DWG...";

            _lblExternalSummary.Text = BuildExternalSummary(_titleBlockConfig);

            _lblExternalSummary.Foreground = _titleBlockConfig.Mode == LayoutTitleBlockMode.External

                ? SdPaletteStyles.TextSecondaryBrush

                : SdPaletteStyles.TextMutedBrush;

        }



        private static string BuildExternalSummary(LayoutTitleBlockConfig config)

        {

            if (string.IsNullOrWhiteSpace(config.DwgPath))

            {

                return "Chưa cấu hình title block ngoài.";

            }



            if (!config.IsConfiguredForExternal)

            {

                return "Đã chọn file nhưng phần ánh xạ chưa hoàn tất.";

            }



            string mappingSummary = string.Join(

                ", ",

                config.AttributeMappings

                    .Take(3)

                    .Select(mapping =>

                        $"{mapping.AttributeTag} -> {LayoutTitleBlockFields.GetLabel(mapping.PluginField)}"));



            if (config.AttributeMappings.Count > 3)

            {

                mappingSummary += ", ...";

            }



            return $"Nguồn: {config.DisplaySourceName} | {mappingSummary}";

        }



        private void ChooseExternalTitleBlock()

        {

            var previousConfig = _titleBlockConfig;



            try

            {

                var dialog = new OpenFileDialog

                {

                    Filter = "AutoCAD Drawing (*.dwg)|*.dwg",

                    CheckFileExists = true,

                    CheckPathExists = true

                };



                if (dialog.ShowDialog() != true)

                {

                    if (!previousConfig.IsConfiguredForExternal)

                    {

                        _titleBlockConfig = previousConfig with { Mode = LayoutTitleBlockMode.BuiltIn };

                        UpdateTitleBlockUi();

                    }



                    return;

                }



                var sources = LayoutTitleBlockConfigStore.DiscoverSources(dialog.FileName);

                if (sources.Count == 0)

                {

                    SetStatus("File này không có attribute để ánh xạ. Sẽ dùng mẫu tích hợp.", true);

                    _titleBlockConfig = previousConfig;

                    UpdateTitleBlockUi();

                    return;

                }



                var seedConfig = string.Equals(previousConfig.DwgPath, dialog.FileName, StringComparison.OrdinalIgnoreCase)

                    ? previousConfig

                    : previousConfig with

                    {

                        DwgPath = dialog.FileName,

                        SourceName = string.Empty,

                        AttributeMappings = []

                    };



                var mappingDialog = new ExternalTitleBlockMappingDialog(dialog.FileName, sources, seedConfig);

                if (AcadApp.ShowModalWindow(mappingDialog) != true)

                {

                    _titleBlockConfig = previousConfig;

                    UpdateTitleBlockUi();

                    return;

                }



                var selectedSource = sources.FirstOrDefault(s =>

                    string.Equals(s.Name, mappingDialog.SelectedSourceName, StringComparison.OrdinalIgnoreCase));



                _titleBlockConfig = new LayoutTitleBlockConfig

                {

                    Mode = LayoutTitleBlockMode.External,

                    DwgPath = dialog.FileName,

                    SourceName = mappingDialog.SelectedSourceName,

                    AttributeMappings = mappingDialog.AttributeMappings.ToList(),

                    BlockWidth = selectedSource?.Width ?? 0,

                    BlockHeight = selectedSource?.Height ?? 0,

                    StripPosition = selectedSource?.StripPosition ?? TitleBlockStripPosition.Bottom,

                    StripSize = selectedSource?.StripSize ?? 0

                };



                PersistTitleBlockConfig();

                UpdateTitleBlockUi();

                SetStatus("Đã lưu ánh xạ title block ngoài.", false);

            }

            catch (Exception ex)

            {

                _titleBlockConfig = previousConfig;

                UpdateTitleBlockUi();

                SetStatus(ex.Message, true);

            }

        }



        private LayoutTitleBlockConfig BuildRequestTitleBlockConfig()

        {

            var mode = _rbExternal.IsChecked == true

                ? LayoutTitleBlockMode.External

                : LayoutTitleBlockMode.BuiltIn;



            var requestConfig = _titleBlockConfig with { Mode = mode };

            if (requestConfig.Mode == LayoutTitleBlockMode.External && !requestConfig.IsConfiguredForExternal)

            {

                SetStatus("Title block ngoài chưa hợp lệ, plugin sẽ dùng mẫu tích hợp.", true);

                requestConfig = requestConfig with { Mode = LayoutTitleBlockMode.BuiltIn };

            }



            return requestConfig;

        }



        private void SetStatus(string message, bool isError)

        {

            if (isError)

            {

                UiStatus.ApplyWarning(_lblStatus, message);

                return;

            }



            UiStatus.ApplySuccess(_lblStatus, message);

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



        internal sealed class PendingLayoutRequest

        {

            private readonly WeakReference<LayoutManagerPaletteControl> _source;



            public PendingLayoutRequest(LayoutCreationRequest request, LayoutManagerPaletteControl source)

            {

                Request = request;

                _source = new WeakReference<LayoutManagerPaletteControl>(source);

            }



            public LayoutCreationRequest Request { get; }



            public LayoutManagerPaletteControl? GetSource()

            {

                return _source.TryGetTarget(out var source) ? source : null;

            }

        }

    }

}

