using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Autodesk.AutoCAD.DatabaseServices;
using Microsoft.Win32;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;
using ShopDrawing.Plugin.Core;

namespace ShopDrawing.Plugin.UI
{
    /// <summary>
    /// Layout Manager Palette (PRD v5):
    /// - Chọn khổ giấy + margin
    /// - Nhập tên dự án
    /// - Built-in / external title block
    /// - Nhập tên tường, pick vùng và tạo layout
    /// - Hiển thị danh sách layout đã tạo
    /// </summary>
    public class LayoutManagerPaletteControl : UserControl
    {
        private readonly LayoutManagerEngine _engine = new();

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
        private ListBox _lstLayouts = null!;
        private TextBlock _lblStatus = null!;

        private LayoutTitleBlockConfig _titleBlockConfig = new();
        private bool _isUpdatingTitleBlockUi;

        public LayoutManagerPaletteControl()
        {
            var root = new StackPanel { Margin = new Thickness(10) };

            // ═══ Header ═══
            root.Children.Add(SdPaletteStyles.CreateHeader("LAYOUT MANAGER"));

            // ═══ Paper & Scale Section ═══
            var paperSection = new StackPanel();
            paperSection.Children.Add(SdPaletteStyles.CreateSectionHeader("KHỔ GIẤY & TỶ LỆ"));

            var rowPaper = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
            _cbPaper = new ComboBox { Width = 70, Margin = new Thickness(0, 0, 8, 0) };
            foreach (var paper in new[] { "A3", "A2", "A1" }) _cbPaper.Items.Add(paper);
            _cbPaper.SelectedIndex = 0;
            rowPaper.Children.Add(_cbPaper);
            rowPaper.Children.Add(new TextBlock
            {
                Text = "Landscape", VerticalAlignment = VerticalAlignment.Center,
                Foreground = SdPaletteStyles.TextMutedBrush, FontStyle = FontStyles.Italic,
                FontFamily = SdPaletteStyles.Font
            });
            paperSection.Children.Add(rowPaper);

            var rowMargin = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
            rowMargin.Children.Add(MakeLabel("Margin:", 50));
            rowMargin.Children.Add(MakeLabel("Trái:"));
            _txtMarginL = new TextBox { Width = 35, Text = "25", Padding = new Thickness(4, 2, 4, 2), Margin = new Thickness(2, 0, 8, 0) };
            rowMargin.Children.Add(_txtMarginL);
            rowMargin.Children.Add(MakeLabel("Khác:"));
            _txtMarginO = new TextBox { Width = 35, Text = "5", Padding = new Thickness(4, 2, 4, 2), Margin = new Thickness(2, 0, 0, 0) };
            rowMargin.Children.Add(_txtMarginO);
            paperSection.Children.Add(rowMargin);

            var rowScale = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 0) };
            rowScale.Children.Add(MakeLabel("Tỷ lệ:", 50));
            _lblScale = new TextBlock
            {
                Text = "...", VerticalAlignment = VerticalAlignment.Center,
                Foreground = SdPaletteStyles.AccentBlueBrush, FontWeight = FontWeights.Bold,
                FontFamily = SdPaletteStyles.Font
            };
            rowScale.Children.Add(_lblScale);
            var btnRefresh = SdPaletteStyles.CreateCompactButton("↻");
            btnRefresh.Width = 22; btnRefresh.Height = 22;
            btnRefresh.Margin = new Thickness(6, 0, 0, 0);
            btnRefresh.Click += (_, _) => RefreshScale();
            rowScale.Children.Add(btnRefresh);
            paperSection.Children.Add(rowScale);
            root.Children.Add(SdPaletteStyles.CreateSectionBorder(paperSection));

            // ═══ Project & Title Block Section ═══
            var projectSection = new StackPanel();
            projectSection.Children.Add(SdPaletteStyles.CreateSectionHeader("DỰ ÁN & TITLE BLOCK"));

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

            // ═══ Create Layout Section ═══
            var createSection = new StackPanel();
            createSection.Children.Add(SdPaletteStyles.CreateSectionHeader("TẠO LAYOUT"));

            createSection.Children.Add(MakeLabel("Tên bản vẽ (View Title):"));
            var rowTitle = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 6) };
            rowTitle.Children.Add(new TextBlock
            {
                Text = "MẶT ĐỨNG VÁCH -",
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 10, FontFamily = SdPaletteStyles.Font,
                Foreground = SdPaletteStyles.TextMutedBrush,
                Margin = new Thickness(0, 0, 4, 0)
            });
            _cboTitle = new ComboBox
            {
                Width = 110, IsEditable = true, Text = "",
                VerticalContentAlignment = VerticalAlignment.Center,
                ToolTip = "Auto-detect từ vùng chọn, hoặc nhập thủ công (VD: W1, TRỤC A...)"
            };
            rowTitle.Children.Add(_cboTitle);
            createSection.Children.Add(rowTitle);

            var btnPick = SdPaletteStyles.CreateActionButton("📐  Chọn vùng & Tạo Layout", SdPaletteStyles.AccentGreenBrush);
            btnPick.Click += (_, _) => PickAndCreate();
            createSection.Children.Add(btnPick);
            root.Children.Add(SdPaletteStyles.CreateSectionBorder(createSection));

            // ═══ Layout List Section ═══
            var listSection = new StackPanel();
            listSection.Children.Add(SdPaletteStyles.CreateSectionHeader("LAYOUTS ĐÃ TẠO"));
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

            var btnDelete = SdPaletteStyles.CreateActionButton("🗑 Xóa", SdPaletteStyles.AccentRedBrush);
            Grid.SetColumn(btnDelete, 0); btnDelete.Margin = new Thickness(0, 2, 4, 2);
            btnDelete.Click += (_, _) => DeleteSelected();
            rowActions.Children.Add(btnDelete);

            var btnSync = SdPaletteStyles.CreateActionButton("↻ Sync DMBV", SdPaletteStyles.AccentBlueBrush);
            Grid.SetColumn(btnSync, 1); btnSync.Margin = new Thickness(4, 2, 0, 2);
            btnSync.Click += (_, _) => SyncDMBD();
            rowActions.Children.Add(btnSync);
            listSection.Children.Add(rowActions);
            root.Children.Add(SdPaletteStyles.CreateSectionBorder(listSection));

            // ═══ Status & Footer ═══
            _lblStatus = SdPaletteStyles.CreateStatusText();
            _lblStatus.FontWeight = FontWeights.SemiBold;
            root.Children.Add(_lblStatus);

            root.Children.Add(new TextBlock
            {
                Text = "Tường dài → tự động phân trang (PHẦN 1/N)\nOverlap 1000mm giữa các trang.",
                Foreground = SdPaletteStyles.TextMutedBrush,
                FontSize = 10, FontFamily = SdPaletteStyles.Font,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 4, 0, 0)
            });

            Content = SdPaletteStyles.WrapInScrollViewer(root);
            Background = SdPaletteStyles.BgPrimaryBrush;

            RefreshScale();
            LoadPersistedTitleBlockConfig();
            LoadProjectNameFromDocument();
            RefreshLayoutList();
            ScanWallCodesForTitle();
        }

        private void RefreshScale()
        {
            double denominator = LayoutManagerEngine.GetCurrentScaleDenominator();
            _lblScale.Text = $"TỶ LỆ 1:{(int)denominator}";
        }

        private void ScanWallCodesForTitle()
        {
            try
            {
                var codes = SmartDimEngine.ScanWallCodes();
                string prev = _cboTitle.Text;
                _cboTitle.Items.Clear();
                foreach (var c in codes) _cboTitle.Items.Add(c);

                if (codes.Count > 0)
                {
                    if (codes.Contains(prev))
                        _cboTitle.Text = prev;
                    else
                        _cboTitle.Text = ""; // Let auto-detect fill it
                }
            }
            catch { /* ignore scan failures */ }
        }

        private void LoadProjectNameFromDocument()
        {
            if (!string.IsNullOrWhiteSpace(_txtProject.Text))
                return;

            var doc = AcadApp.DocumentManager.MdiActiveDocument;
            if (doc == null)
                return;

            _txtProject.Text = DrawingListManager.GetDocumentProjectName(doc);
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

        internal static PendingLayoutRequest? PendingRequest { get; private set; }

        public void SetStatusPublic(string message) => SetStatus(message, false);

        public void RefreshLayoutListPublic() => RefreshLayoutList();

        internal static void EnqueuePendingRequest(LayoutCreationRequest request, LayoutManagerPaletteControl source)
        {
            PendingRequest = new PendingLayoutRequest(request, source);
        }

        internal static PendingLayoutRequest? TakePendingRequest()
        {
            var pending = PendingRequest;
            PendingRequest = null;
            return pending;
        }

        private void PickAndCreate()
        {
            try
            {
                ApplyMargins();
                SetStatus("⏳ Đang chờ chọn vùng...", false);

                var region = _engine.PickRegion();
                if (region == null)
                {
                    SetStatus("⚠️ Đã huỷ.", true);
                    return;
                }

                // Auto-detect wall code from selected region
                string title = _cboTitle.Text.Trim().ToUpperInvariant();
                var detectedCodes = SmartDimEngine.ScanWallCodesInRegion(region.Value);
                if (detectedCodes.Count > 0)
                {
                    string autoTitle = string.Join(", ", detectedCodes);
                    if (string.IsNullOrWhiteSpace(title) || title == "W1")
                    {
                        // Auto-fill with detected codes
                        title = autoTitle;
                        Dispatcher.Invoke(() => _cboTitle.Text = autoTitle);
                    }
                    else if (!title.Equals(autoTitle, StringComparison.OrdinalIgnoreCase))
                    {
                        // User typed something different — ask
                        var result = System.Windows.MessageBox.Show(
                            $"Phát hiện mã tường trong vùng chọn: {autoTitle}\nBạn đang nhập: {title}\n\nDùng mã tự động ({autoTitle})?\n\n- Yes: Dùng '{autoTitle}'\n- No: Giữ '{title}'",
                            "Auto-detect mã tường",
                            System.Windows.MessageBoxButton.YesNo,
                            System.Windows.MessageBoxImage.Question);

                        if (result == System.Windows.MessageBoxResult.Yes)
                        {
                            title = autoTitle;
                            Dispatcher.Invoke(() => _cboTitle.Text = autoTitle);
                        }
                    }
                }

                if (string.IsNullOrWhiteSpace(title))
                {
                    SetStatus("⚠️ Không phát hiện mã tường. Nhập thủ công hoặc chọn vùng có tag.", true);
                    return;
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
                    TitleBlockConfig = BuildRequestTitleBlockConfig()
                };

                EnqueuePendingRequest(request, this);
                SetStatus($"⏳ Đang tạo layout '{title}'...", false);

                var doc = AcadApp.DocumentManager.MdiActiveDocument;
                doc?.SendStringToExecute("_SD_LAYOUT_CREATE\n", true, false, false);
            }
            catch (Exception ex)
            {
                SetStatus($"❌ {ex.Message}", true);
                AcadApp.DocumentManager.MdiActiveDocument?.Editor
                    ?.WriteMessage($"\n[SD] PickAndCreate error: {ex.Message}");
            }
        }

        private void DeleteSelected()
        {
            if (_lstLayouts.SelectedItem is not string layoutName)
                return;

            try
            {
                var doc = AcadApp.DocumentManager.MdiActiveDocument;
                if (doc == null)
                    return;

                using (doc.LockDocument())
                {
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
                SetStatus($"✅ Đã xóa: {layoutName}", false);
            }
            catch (Exception ex)
            {
                SetStatus($"❌ {ex.Message}", true);
            }
        }

        private void SyncDMBD()
        {
            try
            {
                var doc = AcadApp.DocumentManager.MdiActiveDocument;
                if (doc == null)
                    return;

                using (doc.LockDocument())
                {
                    DrawingListManager.Sync(doc);
                }

                SetStatus("✅ Danh mục bản vẽ đã cập nhật.", false);
            }
            catch (Exception ex)
            {
                SetStatus($"❌ {ex.Message}", true);
            }
        }

        private void RefreshLayoutList()
        {
            _lstLayouts.Items.Clear();

            try
            {
                var doc = AcadApp.DocumentManager.MdiActiveDocument;
                if (doc == null)
                    return;

                LoadProjectNameFromDocument();
                foreach (string name in DrawingListManager.GetOrderedLayoutNames(doc))
                {
                    _lstLayouts.Items.Add(name);
                }
            }
            catch
            {
            }
        }

        private void HandleBuiltInChecked()
        {
            if (_isUpdatingTitleBlockUi)
                return;

            _titleBlockConfig = _titleBlockConfig with { Mode = LayoutTitleBlockMode.BuiltIn };
            PersistTitleBlockConfig();
            UpdateTitleBlockUi();
        }

        private void HandleExternalChecked()
        {
            if (_isUpdatingTitleBlockUi)
                return;

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
                return;

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
                ? "Chọn / map lại..."
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
                return "Chưa cấu hình title block external.";
            }

            if (!config.IsConfiguredForExternal)
            {
                return "Đã chọn file nhưng mapping chưa hoàn tất.";
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
                    SetStatus("⚠️ File này không có attribute để map. Sẽ dùng built-in.", true);
                    _titleBlockConfig = previousConfig;
                    UpdateTitleBlockUi();
                    return;
                }

                var seedConfig = string.Equals(previousConfig.DwgPath, dialog.FileName, StringComparison.OrdinalIgnoreCase)
                    ? previousConfig
                    : previousConfig with
                    {
                        DwgPath = dialog.FileName,
                        SourceName = "",
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
                SetStatus("✅ Đã lưu mapping title block external.", false);
            }
            catch (Exception ex)
            {
                _titleBlockConfig = previousConfig;
                UpdateTitleBlockUi();
                SetStatus($"❌ {ex.Message}", true);
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
                SetStatus("⚠️ Title block external chưa hợp lệ, plugin sẽ dùng built-in.", true);
                requestConfig = requestConfig with { Mode = LayoutTitleBlockMode.BuiltIn };
            }

            return requestConfig;
        }

        private void SetStatus(string message, bool isError)
        {
            _lblStatus.Text = message;
            _lblStatus.Foreground = isError ? SdPaletteStyles.AccentOrangeBrush : SdPaletteStyles.AccentGreenBrush;
        }

        private static TextBlock MakeLabel(string text, double width = 0)
        {
            var lbl = SdPaletteStyles.CreateLabel(text);
            lbl.VerticalAlignment = VerticalAlignment.Center;
            if (width > 0) lbl.Width = width;
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
