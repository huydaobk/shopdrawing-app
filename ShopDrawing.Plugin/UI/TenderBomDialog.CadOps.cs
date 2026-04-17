using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ShopDrawing.Plugin.Core;
using ShopDrawing.Plugin.Models;

namespace ShopDrawing.Plugin.UI
{
    public partial class TenderBomDialog
    {
        private void BeginCadInteraction()
        {
            _suspendCadOperations = true;
            _isEditingCell = false;
            _cadPreviewTimer.Stop();
            _pendingPreviewRow = null;
            _lastCadPreviewKey = null;

            try { _wallGrid?.CommitEdit(DataGridEditingUnit.Cell, true); } catch (System.Exception ex)
            {
                ShopDrawing.Plugin.Core.PluginLogger.Error("Suppressed exception in TenderBomDialog.cs", ex);
            }
            try { _wallGrid?.CommitEdit(DataGridEditingUnit.Row, true); } catch (System.Exception ex)
            {
                ShopDrawing.Plugin.Core.PluginLogger.Error("Suppressed exception in TenderBomDialog.cs", ex);
            }
            try { _openingGrid?.CommitEdit(DataGridEditingUnit.Cell, true); } catch (System.Exception ex)
            {
                ShopDrawing.Plugin.Core.PluginLogger.Error("Suppressed exception in TenderBomDialog.cs", ex);
            }
            try { _openingGrid?.CommitEdit(DataGridEditingUnit.Row, true); } catch (System.Exception ex)
            {
                ShopDrawing.Plugin.Core.PluginLogger.Error("Suppressed exception in TenderBomDialog.cs", ex);
            }

            IsEnabled = false;
            Opacity = 0.92;
        }

        private void EndCadInteraction()
        {
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, new Action(() =>
            {
                Opacity = 1.0;
                IsEnabled = true;
                _suspendCadOperations = false;

                if (_wallGrid?.SelectedItem is TenderWallRow selectedRow)
                    RequestCadPreview(selectedRow);
            }));
        }

        private void OnPreviewCad(object sender, RoutedEventArgs e)
        {
            if (_wallGrid.SelectedItem is not TenderWallRow row)
            {
                SetStatus("Ch\u1ecdn v\u00e1ch ho\u1eb7c tr\u1ea7n c\u1ea7n preview CAD.");
                return;
            }

            RequestCadPreview(row, true);
        }
        private enum TenderPopupGeometryMode
        {
            None,
            WallLineChain,
            WallPolygon,
            CeilingPolygon
        }
        private sealed class PopupSegmentRow : INotifyPropertyChanged
        {
            private double _lengthMm;
            private double _heightMm;
            public string? CadHandle { get; set; }
            public bool IsDraftCadHandle { get; set; }
            public Autodesk.AutoCAD.Geometry.Point3d? StartPoint { get; set; }
            public Autodesk.AutoCAD.Geometry.Point3d? EndPoint { get; set; }
            public double LengthMm
            {
                get => _lengthMm;
                set
                {
                    if (System.Math.Abs(_lengthMm - value) < 0.01)
                        return;
                    _lengthMm = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LengthMm)));
                }
            }
            public double HeightMm
            {
                get => _heightMm;
                set
                {
                    if (System.Math.Abs(_heightMm - value) < 0.01)
                        return;
                    _heightMm = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HeightMm)));
                }
            }
            public event PropertyChangedEventHandler? PropertyChanged;
        }
        private sealed class DraftGeometrySession
        {
            public TenderPopupGeometryMode Mode { get; set; }
            public ReferenceGeometry? ReferenceGeometry { get; set; }
            public List<TenderHeightSegment> Segments { get; set; } = new();
            public List<TenderHeightSegment> HeightSegmentsDraft { get; set; } = new();
            public double RepresentativeHeightMm { get; set; }
            public string LayoutDirection { get; set; } = string.Empty;
            public List<TenderOpening> Openings { get; set; } = new();
            public List<TenderOpening> OpeningsDraft { get; set; } = new();
            public List<double[]>? PolygonVertices { get; set; }
            public List<string> DraftCadHandles { get; set; } = new();
            public List<string> CadDraftEntityIds { get; set; } = new();
            public int PanelWidthMm { get; set; }
            public string? AppliedGroupId { get; set; }
            public string SuspensionLayoutDirection { get; set; } = string.Empty;
            public bool ColdStorageDivideFromMaxSide { get; set; }
        }
        private sealed class ReferenceGeometry
        {
            public List<double[]> BoundaryVertices { get; set; } = new();
            public List<double[]> DevelopedChainVertices { get; set; } = new();
            public List<double[]> ReferenceChain { get; set; } = new();
            public List<double[]> OppositeChain { get; set; } = new();
            public double[] Origin { get; set; } = new[] { 0.0, 0.0 };
            public double[] UAxis { get; set; } = new[] { 1.0, 0.0 };
            public double[] VAxis { get; set; } = new[] { 0.0, 1.0 };
            public double ReferenceLengthMm { get; set; }
            public double ReferenceHeightMm { get; set; }
            public bool IsRectangularLike { get; set; }
            public TenderPopupGeometryMode GeometryMode { get; set; } = TenderPopupGeometryMode.None;
        }
        private static bool IsCeilingCategory(string? category)
        {
            return string.Equals(UiText.Normalize(category), "Tr\u1ea7n", System.StringComparison.OrdinalIgnoreCase);
        }
        private static TenderPopupGeometryMode ResolvePopupMode(TenderWallRow row)
        {
            if (row == null)
                return TenderPopupGeometryMode.None;
            if (row.PolygonVertices != null && row.PolygonVertices.Count >= 3)
                return IsCeilingCategory(row.Category) ? TenderPopupGeometryMode.CeilingPolygon : TenderPopupGeometryMode.WallPolygon;
            return TenderPopupGeometryMode.WallLineChain;
        }
        private bool TryResolveLineEndpointsByHandle(
            string? cadHandle,
            out Autodesk.AutoCAD.Geometry.Point3d startPoint,
            out Autodesk.AutoCAD.Geometry.Point3d endPoint)
        {
            startPoint = default;
            endPoint = default;
            if (string.IsNullOrWhiteSpace(cadHandle))
                return false;
            var doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            if (doc == null)
                return false;
            try
            {
                using (doc.LockDocument())
                using (var tr = doc.Database.TransactionManager.StartTransaction())
                {
                    if (!long.TryParse(cadHandle, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out var rawHandle))
                    {
                        tr.Commit();
                        return false;
                    }
                    var handle = new Autodesk.AutoCAD.DatabaseServices.Handle(rawHandle);
                    if (!doc.Database.TryGetObjectId(handle, out var objId))
                    {
                        tr.Commit();
                        return false;
                    }
                    if (tr.GetObject(objId, Autodesk.AutoCAD.DatabaseServices.OpenMode.ForRead, false) is not Autodesk.AutoCAD.DatabaseServices.Line line)
                    {
                        tr.Commit();
                        return false;
                    }
                    startPoint = line.StartPoint;
                    endPoint = line.EndPoint;
                    tr.Commit();
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

private void RepickWallFromCad(TenderWallRow targetRow, bool pickArea)
        {
            _ = pickArea;
            BeginCadInteraction();
            try
            {
                var doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
                if (doc == null) return;
                var draftRow = targetRow.Clone();
                if (!TryPromptTenderGeometryPopup(draftRow, isRepick: true, out var popupResult))
                {
                    PluginLogger.Warn($"TenderRepick.PopupCancelled | row={targetRow.Name}");
                    return;
                }
                // Đồng bộ dữ liệu popup vào bảng bất kể CAD có dựng thành công hay không.
                ApplyPopupResultToRow(targetRow, popupResult);
                bool cadApplied = TryApplyTenderPopupResult(targetRow, popupResult, isRepick: true);
                if (!cadApplied)
                {
                    CleanupDraftCadHandles(popupResult);
                }
                Dispatcher.Invoke(new Action(() =>
                {
                    SyncWallRowSpecData(targetRow);
                    targetRow.Refresh();
                    if (_wallGrid?.SelectedItem is TenderWallRow selectedRow && ReferenceEquals(selectedRow, targetRow))
                        LoadOpeningsForWall(targetRow);
                    SafeRefreshWallGrid();
                    RefreshWallGridViewAfterPopupApply();
                    RefreshFooter();
                    RefreshPanelBreakdown(targetRow);
                        RefreshBomSummary(allowDeferredRetry: false, forceWhenPendingEdits: true);
                    RefreshBomSummary(allowDeferredRetry: false, forceWhenPendingEdits: true);
                    _project.Walls = GetWallModels();
                    ProjectStateChanged?.Invoke(this, EventArgs.Empty);
                    _lastCadPreviewKey = null;
                    if (!cadApplied)
                    {
                        SetStatus($"Đã giữ dữ liệu {targetRow.Name}; không cập nhật CAD do hủy/lỗi vẽ.");
                    }
                    else
                    {
                        SetStatus($"\u0110\u00e3 c\u1eadp nh\u1eadt {targetRow.Name}");
                    }
                }));
                if (!cadApplied) return;
            }
            catch (Exception ex)
            {
                Dispatcher.BeginInvoke(new Action(() => SetStatus($"L\u1ed7i pick l\u1ea1i: {ex.Message}")));
            }
            finally
            {
                EndCadInteraction();
            }
        }
        private void PickFromCad(bool pickArea)
        {
            _ = pickArea;
            BeginCadInteraction();
            try
            {
                var doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
                if (doc == null) return;
                var pickTemplate = BuildPickTemplateRow();
                var popupDraftRow = pickTemplate.Clone();
                popupDraftRow.Index = _wallRows.Count + 1;
                popupDraftRow.Name = $"{TenderWall.GetCategoryPrefix(pickTemplate.Category)}-{_wallRows.Count + 1}";
                if (popupDraftRow.Height <= 0)
                    popupDraftRow.Height = 3000;
                if (!TryPromptTenderGeometryPopup(popupDraftRow, isRepick: false, out var popupResult))
                {
                    PluginLogger.Warn("TenderPick.PopupCancelled");
                    return;
                }
                // Đồng bộ dữ liệu popup vào bảng trước khi dựng CAD.
                // Nếu user hủy bước chọn điểm đặt mặt đứng, khối lượng vẫn không bị mất.
                ApplyPopupResultToRow(popupDraftRow, popupResult);
                Dispatcher.Invoke(new Action(() =>
                {
                    SyncWallRowSpecData(popupDraftRow);
                    popupDraftRow.Refresh();
                    _wallRows.Add(popupDraftRow);
                    ReindexWalls();
                    if (_wallGrid != null)
                    {
                        _wallGrid.SelectedItem = popupDraftRow;
                        Dispatcher.BeginInvoke(new Action(() => _wallGrid.ScrollIntoView(popupDraftRow)), System.Windows.Threading.DispatcherPriority.Background);
                    }
                    SafeRefreshWallGrid();
                    RefreshWallGridViewAfterPopupApply();
                    LoadOpeningsForWall(popupDraftRow);
                    RefreshFooter();
                    RefreshPanelBreakdown(popupDraftRow);
                    RefreshBomSummary(allowDeferredRetry: false, forceWhenPendingEdits: true);
                    _project.Walls = GetWallModels();
                    ProjectStateChanged?.Invoke(this, EventArgs.Empty);
                    _lastCadPreviewKey = null;
                    SetStatus($"\u0110\u00e3 th\u00eam d\u1eef li\u1ec7u {popupDraftRow.Name}. Ch\u1ecdn \u0111i\u1ec3m \u0111\u1eb7t \u0111\u1ec3 d\u1ef1ng CAD.");
                }));
                if (!TryApplyTenderPopupResult(popupDraftRow, popupResult, isRepick: false))
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        popupDraftRow.Refresh();
                        SafeRefreshWallGrid();
                        RefreshWallGridViewAfterPopupApply();
                        LoadOpeningsForWall(popupDraftRow);
                        RefreshFooter();
                        RefreshPanelBreakdown(popupDraftRow);
                        RefreshBomSummary(allowDeferredRetry: false, forceWhenPendingEdits: true);
                        _project.Walls = GetWallModels();
                        ProjectStateChanged?.Invoke(this, EventArgs.Empty);
                        SetStatus($"\u0110\u00e3 gi\u1eef d\u1eef li\u1ec7u {popupDraftRow.Name}; ch\u01b0a d\u1ef1ng CAD do h\u1ee7y/ch\u01b0a ch\u1ecdn \u0111i\u1ec3m \u0111\u1eb7t.");
                    }));
                    return;
                }
                Dispatcher.Invoke(new Action(() =>
                {
                    SyncWallRowSpecData(popupDraftRow);
                    popupDraftRow.Refresh();
                    SafeRefreshWallGrid();
                    RefreshWallGridViewAfterPopupApply();
                    LoadOpeningsForWall(popupDraftRow);
                    RefreshFooter();
                    RefreshPanelBreakdown(popupDraftRow);
                    RefreshBomSummary(allowDeferredRetry: false, forceWhenPendingEdits: true);
                    _project.Walls = GetWallModels();
                    ProjectStateChanged?.Invoke(this, EventArgs.Empty);
                    _lastCadPreviewKey = null;
                    SetStatus($"\u0110\u00e3 th\u00eam {popupDraftRow.Name} v\u00e0 d\u1ef1ng CAD.");
                }));
            }
            catch (Exception ex)
            {
                Dispatcher.BeginInvoke(new Action(() => SetStatus($"L\u1ed7i pick: {ex.Message}")));
            }
            finally
            {
                EndCadInteraction();
            }
        }
        private static bool IsRectangleByVertices(List<double[]> v)
        {
            if (v.Count != 4) return false;
            const double tolerance = 0.05;
            for (int i = 0; i < 4; i++)
            {
                // Vector cÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â»ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â§a 2 cÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚ÂºÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¡nh liÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Âªn tiÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚ÂºÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¿p
                var a = v[i];
                var b = v[(i + 1) % 4];
                var c = v[(i + 2) % 4];
                double ax = b[0] - a[0], ay = b[1] - a[1];
                double bx = c[0] - b[0], by = c[1] - b[1];
                // Dot product: 0 = vuÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â´ng gÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â³c
                double dot = ax * bx + ay * by;
                double lenA = Math.Sqrt(ax * ax + ay * ay);
                double lenB = Math.Sqrt(bx * bx + by * by);
                if (lenA < 1 || lenB < 1) return false;
                if (Math.Abs(dot) / (lenA * lenB) > tolerance) return false;
            }
            return true;
        }

        private bool TryPromptWallHeightInput(double defaultHeightMm, out double heightMm)
        {
            double resultHeight = Math.Round(defaultHeightMm > 0 ? defaultHeightMm : 3000.0);
            heightMm = resultHeight;
            bool confirmed = false;

            Dispatcher.Invoke(() =>
            {
                var dlg = new Window
                {
                    Title = "Nh\u1eadp chi\u1ec1u cao",
                    Width = 420,
                    Height = 210,
                    MinWidth = 420,
                    MinHeight = 210,
                    WindowStartupLocation = WindowStartupLocation.CenterScreen,
                    ResizeMode = ResizeMode.NoResize,
                    Background = new SolidColorBrush(Color.FromRgb(250, 250, 252)),
                    Owner = this
                };

                var root = new StackPanel { Margin = new Thickness(18, 16, 18, 16) };
                root.Children.Add(new TextBlock
                {
                    Text = "\u0110\u00e3 pick chi\u1ec1u d\u00e0i. Nh\u1eadp chi\u1ec1u cao \u0111\u1ec3 ho\u00e0n t\u1ea5t d\u00f2ng kh\u1ed1i l\u01b0\u1ee3ng:",
                    TextWrapping = TextWrapping.Wrap,
                    FontSize = 14,
                    FontWeight = FontWeights.SemiBold,
                    Margin = new Thickness(0, 0, 0, 12)
                });

                root.Children.Add(new TextBlock
                {
                    Text = "Chi\u1ec1u cao (mm)",
                    Margin = new Thickness(0, 0, 0, 6),
                    Foreground = FgDark
                });

                var txtHeight = new TextBox
                {
                    Text = resultHeight.ToString("F0"),
                    Height = 30,
                    FontSize = 14,
                    Padding = new Thickness(8, 4, 8, 4)
                };
                root.Children.Add(txtHeight);

                var hint = new TextBlock
                {
                    Text = "V\u00ed d\u1ee5: 3000",
                    Margin = new Thickness(0, 6, 0, 0),
                    Foreground = FgGray,
                    FontSize = 12
                };
                root.Children.Add(hint);

                var buttonBar = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Margin = new Thickness(0, 16, 0, 0)
                };

                void ConfirmAndClose()
                {
                    if (!double.TryParse(txtHeight.Text, out var parsed) || parsed <= 0)
                    {
                        txtHeight.Background = new SolidColorBrush(Color.FromRgb(255, 235, 238));
                        txtHeight.Focus();
                        txtHeight.SelectAll();
                        return;
                    }

                    resultHeight = Math.Round(parsed);
                    confirmed = true;
                    dlg.Close();
                }

                var btnOk = Btn("X\u00e1c nh\u1eadn", AccentGreen, Brushes.White, (s, e) => ConfirmAndClose(), 110);
                var btnCancel = Btn("H\u1ee7y", BtnGray, Brushes.White, (s, e) =>
                {
                    confirmed = false;
                    dlg.Close();
                }, 110);

                buttonBar.Children.Add(btnCancel);
                buttonBar.Children.Add(btnOk);
                root.Children.Add(buttonBar);
                dlg.Content = root;
                dlg.Loaded += (_, _) =>
                {
                    txtHeight.Focus();
                    txtHeight.SelectAll();
                };
                txtHeight.KeyDown += (s, e) =>
                {
                    if (e.Key == System.Windows.Input.Key.Enter)
                    {
                        ConfirmAndClose();
                        e.Handled = true;
                    }
                };
                dlg.ShowDialog();
            });

            heightMm = resultHeight;
            return confirmed;
        }

        private sealed class HeightSegmentInputRow : INotifyPropertyChanged
        {
            private double _lengthMm;
            private double _heightMm;
            public string? CadHandle { get; set; }
            public double LengthMm
            {
                get => _lengthMm;
                set
                {
                    if (Math.Abs(_lengthMm - value) < 0.01) return;
                    _lengthMm = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LengthMm)));
                }
            }
            public double HeightMm
            {
                get => _heightMm;
                set
                {
                    if (Math.Abs(_heightMm - value) < 0.01) return;
                    _heightMm = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HeightMm)));
                }
            }
            public event PropertyChangedEventHandler? PropertyChanged;
        }
        private static bool BuildNormalizedSegments(
            IEnumerable<HeightSegmentInputRow> rows,
            double totalLengthMm,
            double defaultHeightMm,
            out List<TenderHeightSegment> normalized,
            out string note,
            bool autoFillMissing)
        {
            normalized = rows
                .Where(r => r != null && r.LengthMm > 0 && r.HeightMm > 0)
                .Select(r => new TenderHeightSegment
                {
                    LengthMm = Math.Round(r.LengthMm),
                    HeightMm = Math.Round(r.HeightMm),
                    CadHandle = string.IsNullOrWhiteSpace(r.CadHandle) ? null : r.CadHandle
                })
                .ToList();
            if (normalized.Count == 0)
            {
                normalized.Add(new TenderHeightSegment
                {
                    LengthMm = totalLengthMm,
                    HeightMm = defaultHeightMm,
                    CadHandle = null
                });
                note = "\u0110ang d\u00f9ng 1 nh\u1ecbp m\u1eb7c \u0111\u1ecbnh to\u00e0n tuy\u1ebfn.";
                return true;
            }
            note = string.Empty;
            return true;
        }
        private static string DescribeSegments(IEnumerable<TenderHeightSegment>? segments)
        {
            var list = (segments ?? Enumerable.Empty<TenderHeightSegment>())
                .Where(s => s != null)
                .ToList();
            if (list.Count == 0)
                return "count=0,totalL=0,avgH=0";
            double totalLength = list.Sum(s => Math.Max(0, s.LengthMm));
            double avgHeight = totalLength > 0
                ? list.Sum(s => Math.Max(0, s.LengthMm) * Math.Max(0, s.HeightMm)) / totalLength
                : list.Average(s => Math.Max(0, s.HeightMm));
            return $"count={list.Count},totalL={totalLength:F0},avgH={avgHeight:F0}";
        }
        private static string DescribeOpenings(IEnumerable<TenderOpening>? openings)
        {
            var list = (openings ?? Enumerable.Empty<TenderOpening>())
                .Where(o => o != null)
                .ToList();
            if (list.Count == 0)
                return "count=0,qty=0,area=0";
            int qty = list.Sum(o => Math.Max(0, o.Quantity));
            double area = list.Sum(o => Math.Max(0, o.TotalAreaM2));
            return $"count={list.Count},qty={qty},area={area:F2}";
        }
        private TenderOpening ToTenderOpeningFromRow(TenderOpeningRow row)
        {
            var opening = new TenderOpening
            {
                Type = string.IsNullOrWhiteSpace(row.Type) ? TenderOpening.ResolveTypeByBottomElevation(row.BottomElevationMm) : row.Type,
                Width = Math.Max(1, Math.Round(row.Width)),
                Height = Math.Max(1, Math.Round(row.Height)),
                BottomElevationMm = Math.Max(0, Math.Round(row.BottomElevationMm)),
                StationStartMm = row.StationStartMm,
                StationEndMm = row.StationEndMm,
                CenterStationMm = row.CenterStationMm,
                ResolvedChainRatioStart = row.ResolvedChainRatioStart,
                ResolvedChainRatioEnd = row.ResolvedChainRatioEnd,
                Quantity = Math.Max(1, row.Quantity),
                OpeningPolygon = row.OpeningPolygon?.Select(p => p.ToArray()).ToList()
            };
            if (opening.StationStartMm >= 0 && opening.StationEndMm >= opening.StationStartMm)
                opening.CenterStationMm = (opening.StationStartMm + opening.StationEndMm) * 0.5;
            return opening;
        }
        private bool TryPickClosedPolygonVertices(
            out List<double[]> vertices,
            out string cadHandle,
            out double approximateHeightMm)
        {
            vertices = new List<double[]>();
            cadHandle = string.Empty;
            approximateHeightMm = 0;
            var doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            if (doc == null)
                return false;
            var ed = doc.Editor;
            
            var ppo = new Autodesk.AutoCAD.EditorInput.PromptPointOptions("\nPick 1 điểm bên trong vùng kín HOẶC [Chon polyline/Ve chu nhat]:", "Chon Ve");
            ppo.AllowNone = true;
            
            var res = ed.GetPoint(ppo);
            if (res.Status == Autodesk.AutoCAD.EditorInput.PromptStatus.Cancel)
                return false;

            using (doc.LockDocument())
            using (var tr = doc.Database.TransactionManager.StartTransaction())
            {
                Autodesk.AutoCAD.DatabaseServices.Polyline? targetPoly = null;

                if (res.Status == Autodesk.AutoCAD.EditorInput.PromptStatus.OK)
                {
                    try
                    {
                        var objs = ed.TraceBoundary(res.Value, false);
                        if (objs != null && objs.Count > 0)
                        {
                            foreach (Autodesk.AutoCAD.DatabaseServices.DBObject obj in objs)
                            {
                                if (obj is Autodesk.AutoCAD.DatabaseServices.Polyline p && p.Closed)
                                {
                                    targetPoly = p;
                                    break;
                                }
                            }
                            foreach (Autodesk.AutoCAD.DatabaseServices.DBObject obj in objs)
                            {
                                if (obj != targetPoly) obj.Dispose();
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        ed.WriteMessage($"\nLỗi truy tìm vùng: {ex.Message}");
                    }
                }
                else if (res.Status == Autodesk.AutoCAD.EditorInput.PromptStatus.Keyword)
                {
                    if (res.StringResult == "Chon")
                    {
                        var opt = new Autodesk.AutoCAD.EditorInput.PromptEntityOptions("\nChọn polyline kín:");
                        opt.SetRejectMessage("\nPhải là polyline kín.");
                        opt.AddAllowedClass(typeof(Autodesk.AutoCAD.DatabaseServices.Polyline), true);
                        var entRes = ed.GetEntity(opt);
                        if (entRes.Status == Autodesk.AutoCAD.EditorInput.PromptStatus.OK)
                        {
                            targetPoly = tr.GetObject(entRes.ObjectId, Autodesk.AutoCAD.DatabaseServices.OpenMode.ForRead, false) as Autodesk.AutoCAD.DatabaseServices.Polyline;
                        }
                    }
                    else if (res.StringResult == "Ve")
                    {
                        var p1Opt = new Autodesk.AutoCAD.EditorInput.PromptPointOptions("\nChọn góc thứ nhất của hình chữ nhật: ");
                        var p1Res = ed.GetPoint(p1Opt);
                        if (p1Res.Status == Autodesk.AutoCAD.EditorInput.PromptStatus.OK)
                        {
                            var p2Opt = new Autodesk.AutoCAD.EditorInput.PromptCornerOptions("\nChọn góc đối diện: ", p1Res.Value);
                            var p2Res = ed.GetCorner(p2Opt);
                            if (p2Res.Status == Autodesk.AutoCAD.EditorInput.PromptStatus.OK)
                            {
                                var pt1 = p1Res.Value;
                                var pt2 = p2Res.Value;
                                targetPoly = new Autodesk.AutoCAD.DatabaseServices.Polyline();
                                targetPoly.AddVertexAt(0, new Autodesk.AutoCAD.Geometry.Point2d(pt1.X, pt1.Y), 0, 0, 0);
                                targetPoly.AddVertexAt(1, new Autodesk.AutoCAD.Geometry.Point2d(pt2.X, pt1.Y), 0, 0, 0);
                                targetPoly.AddVertexAt(2, new Autodesk.AutoCAD.Geometry.Point2d(pt2.X, pt2.Y), 0, 0, 0);
                                targetPoly.AddVertexAt(3, new Autodesk.AutoCAD.Geometry.Point2d(pt1.X, pt2.Y), 0, 0, 0);
                                targetPoly.Closed = true;
                            }
                        }
                    }
                }
                else if (res.Status == Autodesk.AutoCAD.EditorInput.PromptStatus.None)
                {
                    // Default to Chon on Enter
                    var opt = new Autodesk.AutoCAD.EditorInput.PromptEntityOptions("\nChọn polyline kín:");
                    opt.SetRejectMessage("\nPhải là polyline kín.");
                    opt.AddAllowedClass(typeof(Autodesk.AutoCAD.DatabaseServices.Polyline), true);
                    var entRes = ed.GetEntity(opt);
                    if (entRes.Status == Autodesk.AutoCAD.EditorInput.PromptStatus.OK)
                    {
                        targetPoly = tr.GetObject(entRes.ObjectId, Autodesk.AutoCAD.DatabaseServices.OpenMode.ForRead, false) as Autodesk.AutoCAD.DatabaseServices.Polyline;
                    }
                }

                if (targetPoly == null || !targetPoly.Closed)
                {
                    tr.Commit();
                    return false;
                }

                if (targetPoly.ObjectId.IsNull)
                {
                    var btr = (Autodesk.AutoCAD.DatabaseServices.BlockTableRecord)tr.GetObject(doc.Database.CurrentSpaceId, Autodesk.AutoCAD.DatabaseServices.OpenMode.ForWrite);
                    targetPoly.ColorIndex = 3;
                    btr.AppendEntity(targetPoly);
                    tr.AddNewlyCreatedDBObject(targetPoly, true);
                }

                vertices = GetPolylineVertices(targetPoly);
                cadHandle = targetPoly.Handle.ToString();
                approximateHeightMm = Math.Max(0, targetPoly.GeometricExtents.MaxPoint.Y - targetPoly.GeometricExtents.MinPoint.Y);
                tr.Commit();
                return vertices.Count >= 3;
            }
        }
        private bool TryBuildWallReferenceGeometry(List<double[]> polygonVertices, out ReferenceGeometry geometry)
        {
            geometry = new ReferenceGeometry();
            if (TryBuildRectangularWallReferenceGeometry(polygonVertices, out geometry))
                return true;
            if (!TryResolvePolygonDevelopedGeometry(polygonVertices, out var referenceChain, out var oppositeChain, out var chainLength)
                || referenceChain.Count < 2
                || oppositeChain.Count < 2
                || chainLength <= 1.0)
            {
                return false;
            }
            var ratioSet = new HashSet<double> { 0.0, 1.0 };
            foreach (double ratio in BuildChainRatios(referenceChain))
                ratioSet.Add(ratio);
            foreach (double ratio in BuildChainRatios(oppositeChain))
                ratioSet.Add(ratio);
            var ratios = ratioSet.OrderBy(x => x).ToList();
            var bottom = new List<double[]>();
            var top = new List<double[]>();
            double maxHeight = 0;
            foreach (double ratio in ratios)
            {
                var refPoint = GetPointAlongPolyline(referenceChain, ratio);
                var oppPoint = GetPointAlongPolyline(oppositeChain, ratio);
                if (refPoint == null || oppPoint == null)
                    continue;
                double station = ratio * chainLength;
                double height = Math.Sqrt(
                    Math.Pow(oppPoint[0] - refPoint[0], 2) +
                    Math.Pow(oppPoint[1] - refPoint[1], 2));
                maxHeight = Math.Max(maxHeight, height);
                bottom.Add(new[] { station, 0.0 });
                top.Add(new[] { station, height });
            }
            if (bottom.Count < 2 || top.Count < 2)
                return false;
            geometry.ReferenceChain = bottom.Select(v => v.ToArray()).ToList();
            geometry.OppositeChain = top.Select(v => v.ToArray()).ToList();
            geometry.DevelopedChainVertices = referenceChain.Select(v => v.ToArray()).ToList();
            geometry.ReferenceLengthMm = chainLength;
            geometry.ReferenceHeightMm = maxHeight;
            geometry.BoundaryVertices = bottom.Concat(top.AsEnumerable().Reverse()).Select(v => v.ToArray()).ToList();
            geometry.Origin = referenceChain[0].ToArray();
            double ux = 1.0;
            double uy = 0.0;
            for (int i = 0; i + 1 < referenceChain.Count; i++)
            {
                double dx = referenceChain[i + 1][0] - referenceChain[i][0];
                double dy = referenceChain[i + 1][1] - referenceChain[i][1];
                double len = Math.Sqrt(dx * dx + dy * dy);
                if (len > 1e-6)
                {
                    ux = dx / len;
                    uy = dy / len;
                    break;
                }
            }
            var refAtStart = GetPointAlongPolyline(referenceChain, 0) ?? referenceChain[0];
            var oppAtStart = GetPointAlongPolyline(oppositeChain, 0) ?? oppositeChain[0];
            double vx = oppAtStart[0] - refAtStart[0];
            double vy = oppAtStart[1] - refAtStart[1];
            double vLen = Math.Sqrt(vx * vx + vy * vy);
            if (vLen > 1e-6)
            {
                vx /= vLen;
                vy /= vLen;
            }
            else
            {
                vx = -uy;
                vy = ux;
            }
            geometry.UAxis = new[] { ux, uy };
            geometry.VAxis = new[] { vx, vy };
            geometry.IsRectangularLike = !HasNonOrthogonalEdges(polygonVertices);
            geometry.GeometryMode = TenderPopupGeometryMode.WallPolygon;
            return true;
        }
        private static bool TryBuildRectangularWallReferenceGeometry(
            IReadOnlyList<double[]> polygonVertices,
            out ReferenceGeometry geometry)
        {
            geometry = new ReferenceGeometry();
            var polygon = (polygonVertices ?? Array.Empty<double[]>())
                .Where(v => v != null && v.Length >= 2)
                .Select(v => v.ToArray())
                .ToList();
            if (polygon.Count < 4 || HasNonOrthogonalEdges(polygon))
                return false;
            double minX = polygon.Min(v => v[0]);
            double maxX = polygon.Max(v => v[0]);
            double minY = polygon.Min(v => v[1]);
            double maxY = polygon.Max(v => v[1]);
            double spanX = maxX - minX;
            double spanY = maxY - minY;
            if (spanX <= 1.0 || spanY <= 1.0)
                return false;
            static bool Near(double a, double b) => Math.Abs(a - b) <= 1.0;
            // Chỉ nhận diện rectangle thật. Biên dạng bậc/L-shape vẫn đi qua nhánh khai triển chain cũ.
            bool allOnBoundingRectangle = polygon.All(v =>
                Near(v[0], minX) || Near(v[0], maxX) || Near(v[1], minY) || Near(v[1], maxY));
            if (!allOnBoundingRectangle)
                return false;
            double area = Math.Abs(ComputePolygonArea(polygon));
            double boundingArea = spanX * spanY;
            if (boundingArea <= 1.0 || area < boundingArea * 0.98)
                return false;
            bool stationAlongX = spanX >= spanY;
            double length = stationAlongX ? spanX : spanY;
            double height = stationAlongX ? spanY : spanX;
            geometry.ReferenceChain = new List<double[]>
            {
                new[] { 0.0, 0.0 },
                new[] { length, 0.0 }
            };
            geometry.OppositeChain = new List<double[]>
            {
                new[] { 0.0, height },
                new[] { length, height }
            };
            geometry.DevelopedChainVertices = geometry.ReferenceChain.Select(v => v.ToArray()).ToList();
            geometry.BoundaryVertices = new List<double[]>
            {
                new[] { 0.0, 0.0 },
                new[] { length, 0.0 },
                new[] { length, height },
                new[] { 0.0, height }
            };
            geometry.ReferenceLengthMm = length;
            geometry.ReferenceHeightMm = height;
            geometry.Origin = stationAlongX ? new[] { minX, minY } : new[] { minX, minY };
            geometry.UAxis = stationAlongX ? new[] { 1.0, 0.0 } : new[] { 0.0, 1.0 };
            geometry.VAxis = stationAlongX ? new[] { 0.0, 1.0 } : new[] { 1.0, 0.0 };
            geometry.IsRectangularLike = true;
            geometry.GeometryMode = TenderPopupGeometryMode.WallPolygon;
            return true;
        }
        private static double ComputePolygonArea(IReadOnlyList<double[]> polygon)
        {
            if (polygon == null || polygon.Count < 3)
                return 0;
            double area2 = 0;
            for (int i = 0; i < polygon.Count; i++)
            {
                var a = polygon[i];
                var b = polygon[(i + 1) % polygon.Count];
                area2 += a[0] * b[1] - b[0] * a[1];
            }
            return area2 * 0.5;
        }
        private static IEnumerable<double> BuildChainRatios(IReadOnlyList<double[]> vertices)
        {
            if (vertices == null || vertices.Count < 2)
                yield break;
            double total = GetPolylineLength(vertices.ToList());
            if (total <= 1.0)
                yield break;
            double walked = 0;
            yield return 0.0;
            for (int i = 0; i + 1 < vertices.Count; i++)
            {
                double dx = vertices[i + 1][0] - vertices[i][0];
                double dy = vertices[i + 1][1] - vertices[i][1];
                walked += Math.Sqrt(dx * dx + dy * dy);
                yield return Math.Max(0, Math.Min(1, walked / total));
            }
        }
        private void DrawPreviewScanSegments(
            Canvas canvas,
            List<double[]> vertices,
            double pos,
            bool horizontalLine,
            Brush brush,
            double thickness,
            Func<double[], Point> map)
        {
            foreach (var segment in GetScanSegments(vertices, pos, horizontalLine))
            {
                var p1 = horizontalLine ? map(new[] { segment.Start, pos }) : map(new[] { pos, segment.Start });
                var p2 = horizontalLine ? map(new[] { segment.End, pos }) : map(new[] { pos, segment.End });
                canvas.Children.Add(new System.Windows.Shapes.Line
                {
                    X1 = p1.X,
                    Y1 = p1.Y,
                    X2 = p2.X,
                    Y2 = p2.Y,
                    Stroke = brush,
                    StrokeThickness = thickness
                });
            }
        }
        private void DrawLocalPolygonPreview(
            Canvas canvas,
            List<double[]> vertices,
            int panelWidthMm,
            bool horizontalLayout,
            IReadOnlyList<TenderOpening>? openings,
            double referenceLengthMm,
            bool drawOpeningByStation,
            TenderWallRow row)
        {
            canvas.Children.Clear();
            if (vertices == null || vertices.Count < 3)
                return;
            double minX = vertices.Min(v => v[0]);
            double maxX = vertices.Max(v => v[0]);
            double minY = vertices.Min(v => v[1]);
            double maxY = vertices.Max(v => v[1]);
            double width = Math.Max(1, maxX - minX);
            double height = Math.Max(1, maxY - minY);
            double margin = 36;
            double plotW = Math.Max(80, canvas.Width - margin * 2);
            double plotH = Math.Max(80, canvas.Height - margin * 2);
            double scale = Math.Min(plotW / width, plotH / height);
            Point Map(double[] v) => new(
                margin + (v[0] - minX) * scale,
                margin + plotH - (v[1] - minY) * scale);

            double drawnWidth = width * scale;
            double drawnHeight = height * scale;
            double bX1 = margin;
            double bX2 = margin + drawnWidth;
            double bY = margin + plotH;
            canvas.Children.Add(new System.Windows.Shapes.Line { X1 = bX1, Y1 = bY + 20, X2 = bX2, Y2 = bY + 20, Stroke = Brushes.DimGray, StrokeThickness = 1 });
            canvas.Children.Add(new System.Windows.Shapes.Line { X1 = bX1, Y1 = bY, X2 = bX1, Y2 = bY + 24, Stroke = Brushes.DimGray, StrokeThickness = 1 });
            canvas.Children.Add(new System.Windows.Shapes.Line { X1 = bX2, Y1 = bY, X2 = bX2, Y2 = bY + 24, Stroke = Brushes.DimGray, StrokeThickness = 1 });
            var textW = new TextBlock
            {
                Text = $"{Math.Round(width)}",
                FontSize = 11,
                Foreground = Brushes.DarkSlateGray,
                Background = new SolidColorBrush(Color.FromArgb(180, 255, 255, 255))
            };
            textW.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(textW, bX1 + (bX2 - bX1) / 2 - textW.DesiredSize.Width / 2);
            Canvas.SetTop(textW, bY + 20 - 16);
            canvas.Children.Add(textW);

            double lX = margin;
            double lY1 = margin + plotH;
            double lY2 = margin + plotH - drawnHeight;
            canvas.Children.Add(new System.Windows.Shapes.Line { X1 = lX - 20, Y1 = lY1, X2 = lX - 20, Y2 = lY2, Stroke = Brushes.DimGray, StrokeThickness = 1 });
            canvas.Children.Add(new System.Windows.Shapes.Line { X1 = lX - 24, Y1 = lY1, X2 = lX, Y2 = lY1, Stroke = Brushes.DimGray, StrokeThickness = 1 });
            canvas.Children.Add(new System.Windows.Shapes.Line { X1 = lX - 24, Y1 = lY2, X2 = lX, Y2 = lY2, Stroke = Brushes.DimGray, StrokeThickness = 1 });
            var textH = new TextBlock
            {
                Text = $"{Math.Round(height)}",
                FontSize = 11,
                Foreground = Brushes.DarkSlateGray,
                Background = new SolidColorBrush(Color.FromArgb(180, 255, 255, 255))
            };
            textH.RenderTransform = new RotateTransform(-90);
            textH.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(textH, lX - 20 - 16);
            Canvas.SetTop(textH, lY2 + (lY1 - lY2) / 2 + textH.DesiredSize.Width / 2);
            canvas.Children.Add(textH);
            for (int i = 0; i < vertices.Count; i++)
            {
                var p1 = Map(vertices[i]);
                var p2 = Map(vertices[(i + 1) % vertices.Count]);
                canvas.Children.Add(new System.Windows.Shapes.Line
                {
                    X1 = p1.X,
                    Y1 = p1.Y,
                    X2 = p2.X,
                    Y2 = p2.Y,
                    Stroke = Brushes.Black,
                    StrokeThickness = 1.5
                });
            }
            if (panelWidthMm > 0)
            {
                double minAxis = horizontalLayout ? minY : minX;
                double maxAxis = horizontalLayout ? maxY : maxX;
                for (double pos = minAxis + panelWidthMm; pos < maxAxis - 1.0; pos += panelWidthMm)
                {
                    DrawPreviewScanSegments(canvas, vertices, pos, horizontalLayout, Brushes.DarkSlateGray, 1.0, Map);
                    
                    double center = pos - panelWidthMm / 2.0;
                    var pText = new TextBlock { Text = $"{panelWidthMm}", FontSize = 9, Foreground = Brushes.Gray };
                    pText.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                    int panelIndex = (int)(pos / panelWidthMm);
                    if (horizontalLayout)
                    {
                        var pt = Map(new[] { minX, center });
                        Canvas.SetLeft(pText, pt.X - (panelIndex % 2 == 0 ? 30 : 4) - pText.DesiredSize.Width);
                        Canvas.SetTop(pText, pt.Y - pText.DesiredSize.Height / 2);
                    }
                    else
                    {
                        var pt = Map(new[] { center, minY });
                        Canvas.SetLeft(pText, pt.X - pText.DesiredSize.Width / 2);
                        Canvas.SetTop(pText, pt.Y + (panelIndex % 2 == 0 ? 12 : 2));
                    }
                    canvas.Children.Add(pText);
                }
            }
            if (openings != null)
            {
                double lengthRef = Math.Max(1.0, row.Length);
                double spanX = Math.Max(1.0, maxX - minX);
                double spanY = Math.Max(1.0, maxY - minY);

                foreach (var opening in openings.Where(o => o != null))
                {
                    if (opening.OpeningPolygon != null && opening.OpeningPolygon.Count >= 3)
                    {
                        var points = opening.OpeningPolygon.Select(Map).ToList();
                        for (int i = 0; i < points.Count; i++)
                        {
                            var pt1 = points[i];
                            var pt2 = points[(i + 1) % points.Count];
                            canvas.Children.Add(new System.Windows.Shapes.Line
                            {
                                X1 = pt1.X,
                                Y1 = pt1.Y,
                                X2 = pt2.X,
                                Y2 = pt2.Y,
                                Stroke = Brushes.Firebrick,
                                StrokeThickness = 1.5
                            });
                        }
                        
                        // Optional bounding box text (can omit for clean look, or place in center)
                        var bounds = points;
                        double center_X = bounds.Average(p => p.X);
                        double center_Y = bounds.Average(p => p.Y);
                        var oText = new TextBlock
                        {
                            Text = $"W{opening.Width:F0}xH{opening.Height:F0}",
                            FontSize = 9,
                            Foreground = Brushes.Firebrick
                        };
                        oText.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                        Canvas.SetLeft(oText, center_X - oText.DesiredSize.Width / 2);
                        Canvas.SetTop(oText, center_Y - oText.DesiredSize.Height / 2);
                        canvas.Children.Add(oText);
                    }
                    else if (drawOpeningByStation && opening.Width > 0 && opening.Height > 0)
                    {
                        double left = opening.StationStartMm >= 0 ? opening.StationStartMm : opening.CenterStationMm;
                        if (left < 0) continue;
                        double right = opening.StationEndMm >= left ? opening.StationEndMm : left + opening.Width;
                        
                        double axisLeft = (left / lengthRef) * spanX;
                        double axisRight = (right / lengthRef) * spanX;
                        double bottom = Math.Max(0, opening.BottomElevationMm);

                        Point p1 = Map(new[] { minX + axisLeft, minY + bottom });
                        Point p2 = Map(new[] { minX + axisRight, Math.Min(maxY, minY + bottom + opening.Height) });

                        var rect = new System.Windows.Shapes.Rectangle
                        {
                            Width = Math.Max(2, Math.Abs(p2.X - p1.X)),
                            Height = Math.Max(2, Math.Abs(p2.Y - p1.Y)),
                            Stroke = Brushes.Firebrick,
                            StrokeThickness = 1.5
                        };
                        Canvas.SetLeft(rect, Math.Min(p1.X, p2.X));
                        Canvas.SetTop(rect, Math.Min(p1.Y, p2.Y));
                        canvas.Children.Add(rect);

                        var oText = new TextBlock
                        {
                            Text = $"W{opening.Width:F0}xH{opening.Height:F0}",
                            FontSize = 9,
                            Foreground = Brushes.Firebrick
                        };
                        oText.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                        Canvas.SetLeft(oText, Math.Min(p1.X, p2.X) + Math.Abs(p2.X - p1.X) / 2 - oText.DesiredSize.Width / 2);
                        Canvas.SetTop(oText, Math.Min(p1.Y, p2.Y) + Math.Abs(p2.Y - p1.Y) / 2 - oText.DesiredSize.Height / 2);
                        canvas.Children.Add(oText);
                    }
                }
            }

            if (IsSuspendedCeilingRow(row) && !drawOpeningByStation)
            {
                var preview = TenderBomCalculator.GetColdStorageCeilingPreviewData(row.ToModel());
                if (preview.HasValue)
                {
                    bool runAlongX = IsColdStorageRunAlongX(row);
                    var tPositions = BuildSuspensionLinePositions(vertices, runAlongX, row.ColdStorageDivideFromMaxSide, preview.Value.TSpacingMm, preview.Value.TSpacingMm, preview.Value.TLineCount);
                    var mushroomPositions = BuildSuspensionLinePositions(vertices, runAlongX, row.ColdStorageDivideFromMaxSide, preview.Value.TSpacingMm, preview.Value.MushroomOffsetMm, preview.Value.MushroomLineCount);
                    foreach (var pos in tPositions)
                        DrawPreviewScanSegments(canvas, vertices, pos, runAlongX, Brushes.SteelBlue, 1.5, Map);
                    foreach (var pos in mushroomPositions)
                        DrawPreviewScanSegments(canvas, vertices, pos, runAlongX, Brushes.Goldenrod, 1.2, Map);
                }
            }
        }
        private bool TryProjectOpeningPolygon(
            List<double[]> wallPolygon,
            List<double[]> openingPolygon,
            double referenceLengthMm,
            out TenderOpeningRow opening)
        {
            opening = new TenderOpeningRow();
            if (!TryResolvePolygonDevelopedGeometry(wallPolygon, out var referenceChain, out var oppositeChain, out var chainLength)
                || chainLength <= 1.0)
            {
                return false;
            }
            var stations = new List<double>();
            var bottoms = new List<double>();
            foreach (var vertex in openingPolygon)
            {
                var point = new Autodesk.AutoCAD.Geometry.Point3d(vertex[0], vertex[1], 0);
                if (!TryProjectPointToPolylineChain(point, referenceChain, out var stationAlongChain, out _))
                    return false;
                double ratio = Math.Max(0, Math.Min(1, stationAlongChain / chainLength));
                var refPoint = GetPointAlongPolyline(referenceChain, ratio);
                var oppPoint = GetPointAlongPolyline(oppositeChain, ratio);
                if (refPoint == null || oppPoint == null)
                    return false;
                if (!TryGetDirectionAndLength(refPoint, oppPoint, out var direction, out _))
                    return false;
                double bottom = (vertex[0] - refPoint[0]) * direction[0] + (vertex[1] - refPoint[1]) * direction[1];
                stations.Add(stationAlongChain * (referenceLengthMm / chainLength));
                bottoms.Add(bottom);
            }
            if (stations.Count == 0 || bottoms.Count == 0)
                return false;
            double start = stations.Min();
            double end = stations.Max();
            double bottomMm = Math.Max(0, bottoms.Min());
            double topMm = bottoms.Max();
            if (end - start <= 0.5 || topMm - bottomMm <= 0.5)
                return false;
            opening = new TenderOpeningRow
            {
                Type = TenderOpening.ResolveTypeByBottomElevation(bottomMm),
                Width = Math.Round(end - start),
                Height = Math.Round(topMm - bottomMm),
                BottomElevationMm = Math.Round(bottomMm),
                StationStartMm = Math.Round(start),
                StationEndMm = Math.Round(end),
                CenterStationMm = Math.Round((start + end) * 0.5),
                ResolvedChainRatioStart = Math.Max(0, Math.Min(1, start / Math.Max(1, referenceLengthMm))),
                ResolvedChainRatioEnd = Math.Max(0, Math.Min(1, end / Math.Max(1, referenceLengthMm))),
                Quantity = 1,
                OpeningPolygon = openingPolygon.Select(v => v.ToArray()).ToList()
            };
            return true;
        }
        private bool TryPickOpeningForPopup(
            TenderPopupGeometryMode mode,
            IReadOnlyList<PopupSegmentRow> segmentRows,
            List<double[]>? polygonVertices,
            double referenceLengthMm,
            out TenderOpeningRow opening)
        {
            opening = new TenderOpeningRow();
            if (mode == TenderPopupGeometryMode.WallLineChain)
            {
                if (!TryPickOpeningFromCadForPopup(
                        segmentRows.Select(r => new TenderHeightSegment
                        {
                            LengthMm = r.LengthMm,
                            HeightMm = r.HeightMm,
                            CadHandle = r.CadHandle
                        }).ToList(),
                        out var picked))
                {
                    return false;
                }
                opening = new TenderOpeningRow
                {
                    Type = picked.Type,
                    Width = picked.Width,
                    Height = picked.Height,
                    BottomElevationMm = picked.BottomElevationMm,
                    CenterStationMm = picked.CenterStationMm,
                    StationStartMm = picked.StationStartMm,
                    StationEndMm = picked.StationEndMm,
                    ResolvedChainRatioStart = picked.ResolvedChainRatioStart,
                    ResolvedChainRatioEnd = picked.ResolvedChainRatioEnd,
                    Quantity = picked.Quantity,
                    OpeningPolygon = picked.OpeningPolygon?.Select(p => p.ToArray()).ToList()
                };
                return true;
            }
            if (polygonVertices == null || polygonVertices.Count < 3)
                return false;
            var choice = UiFeedback.AskYesNoCancel("C\u00d3 = Pick 2 \u0111i\u1ec3m \u0111\u1ec3 l\u1ea5y LT/R\u1ed9ng, nh\u1eadp Cao + Cao \u0111\u1ed9 \u0111\u00e1y\nKH\u00d4NG = Pick v\u00f9ng l\u1ed7 m\u1edf", "Pick l\u1ed7 m\u1edf");
            if (choice == MessageBoxResult.Cancel)
                return false;
            if (choice == MessageBoxResult.No)
            {
                if (!TryPickClosedPolygonVertices(out var openingPolygon, out _, out _))
                    return false;
                return TryProjectOpeningPolygon(polygonVertices, openingPolygon, Math.Max(1, referenceLengthMm), out opening);
            }
            var doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            if (doc == null)
                return false;
            var ed = doc.Editor;
            var p1Opt = new Autodesk.AutoCAD.EditorInput.PromptPointOptions("\nCh\u1ecdn \u0111i\u1ec3m 1 l\u1ed7 m\u1edf (Enter \u0111\u1ec3 k\u1ebft th\u00fac):") { AllowNone = true };
            var p1Res = ed.GetPoint(p1Opt);
            if (p1Res.Status != Autodesk.AutoCAD.EditorInput.PromptStatus.OK)
                return false;
            var p2Opt = new Autodesk.AutoCAD.EditorInput.PromptPointOptions("\nCh\u1ecdn \u0111i\u1ec3m 2 l\u1ed7 m\u1edf:");
            p2Opt.UseBasePoint = true;
            p2Opt.BasePoint = p1Res.Value;
            var p2Res = ed.GetPoint(p2Opt);
            if (p2Res.Status != Autodesk.AutoCAD.EditorInput.PromptStatus.OK)
                return false;
            if (!TryResolveOpeningStationAndWidthFromPolygon(
                    p1Res.Value,
                    p2Res.Value,
                    polygonVertices,
                    Math.Max(1, referenceLengthMm),
                    preferAxisProjection: !HasNonOrthogonalEdges(polygonVertices),
                    stationMm: out var stationStartMm,
                    projectedWidthMm: out var projectedWidthMm,
                    chainRatioStart: out var ratioStart,
                    chainRatioEnd: out var ratioEnd))
            {
                return false;
            }
            var heightOpt = new Autodesk.AutoCAD.EditorInput.PromptDistanceOptions("\nNhập hoặc pick 2 điểm khoảng cách cao lỗ mở (mm):")
            {
                DefaultValue = 2100,
                AllowNegative = false,
                AllowZero = false,
                UseDefaultValue = true
            };
            var heightRes = ed.GetDistance(heightOpt);
            if (heightRes.Status != Autodesk.AutoCAD.EditorInput.PromptStatus.OK)
                return false;
            var bottomOpt = new Autodesk.AutoCAD.EditorInput.PromptDistanceOptions("\nNhập hoặc pick 2 điểm khoảng cách cao độ đáy lỗ mở (mm):")
            {
                DefaultValue = 0,
                AllowNegative = false,
                AllowZero = true,
                UseDefaultValue = true
            };
            var bottomRes = ed.GetDistance(bottomOpt);
            if (bottomRes.Status != Autodesk.AutoCAD.EditorInput.PromptStatus.OK)
                return false;
            opening = new TenderOpeningRow
            {
                Type = TenderOpening.ResolveTypeByBottomElevation(bottomRes.Value),
                Width = Math.Round(projectedWidthMm),
                Height = Math.Round(heightRes.Value),
                BottomElevationMm = Math.Max(0, Math.Round(bottomRes.Value)),
                StationStartMm = Math.Round(stationStartMm),
                StationEndMm = Math.Round(stationStartMm + projectedWidthMm),
                CenterStationMm = Math.Round(stationStartMm + projectedWidthMm * 0.5),
                ResolvedChainRatioStart = ratioStart,
                ResolvedChainRatioEnd = ratioEnd,
                Quantity = 1
            };
            return true;
        }
        private bool TryPromptTenderGeometryPopup(
            TenderWallRow seedRow,
            bool isRepick,
            out DraftGeometrySession result)
        {
            result = new DraftGeometrySession();
            var popupResult = new DraftGeometrySession();
            popupResult.PanelWidthMm = seedRow.PanelWidth;
            popupResult.AppliedGroupId = seedRow.AppliedGroupId;
            bool accepted = false;
            bool isCeiling = IsCeilingCategory(seedRow.Category);
            TenderPopupGeometryMode mode = ResolvePopupMode(seedRow);
            double referenceLengthMm = Math.Max(0, seedRow.HeightSegments?.Sum(s => Math.Max(0, s.LengthMm)) ?? seedRow.Length);
            double referenceHeightMm = Math.Max(1, seedRow.Height > 0 ? seedRow.Height : 3000);
            double previewZoom = 1.0;
            var polygonVertices = seedRow.PolygonVertices?.Select(v => v.ToArray()).ToList();
            var segmentRows = new ObservableCollection<PopupSegmentRow>(
                (seedRow.HeightSegments ?? new List<TenderHeightSegment>()).Select(s =>
                {
                    var row = new PopupSegmentRow
                    {
                        LengthMm = Math.Round(Math.Max(0, s.LengthMm)),
                        HeightMm = Math.Round(Math.Max(0, s.HeightMm)),
                        CadHandle = s.CadHandle,
                        IsDraftCadHandle = false
                    };
                    if (TryResolveLineEndpointsByHandle(s.CadHandle, out var startPt, out var endPt))
                    {
                        row.StartPoint = startPt;
                        row.EndPoint = endPt;
                    }
                    return row;
                }));
            var openingRows = new ObservableCollection<TenderOpeningRow>(
                (seedRow.Openings ?? new List<TenderOpening>()).Select(op => new TenderOpeningRow
                {
                    Type = op.Type,
                    Width = op.Width,
                    Height = op.Height,
                    BottomElevationMm = op.BottomElevationMm,
                    CenterStationMm = op.CenterStationMm,
                    StationStartMm = op.StationStartMm,
                    StationEndMm = op.StationEndMm,
                    ResolvedChainRatioStart = op.ResolvedChainRatioStart,
                    ResolvedChainRatioEnd = op.ResolvedChainRatioEnd,
                    Quantity = op.Quantity,
                    OpeningPolygon = op.OpeningPolygon?.Select(p => p.ToArray()).ToList()
                }));
            Dispatcher.Invoke(() =>
            {
                Canvas previewCanvas = new Canvas();
                ScrollViewer previewScroll = new ScrollViewer();
                TextBlock lblNote = new TextBlock();
                DataGrid segmentGrid = new DataGrid();
                DataGrid openingGrid = new DataGrid();
                ComboBox cboLayoutDirection = new ComboBox();
                Point? panStart = null;
                double panStartH = 0;
                double panStartV = 0;
                void SetZoom(double zoom)
                {
                    previewZoom = Math.Max(0.25, Math.Min(5.0, zoom));
                    previewCanvas!.LayoutTransform = new ScaleTransform(previewZoom, previewZoom);
                }
                void FitPreview()
                {
                    double viewportW = Math.Max(1.0, previewScroll?.ViewportWidth ?? 0);
                    double viewportH = Math.Max(1.0, previewScroll?.ViewportHeight ?? 0);
                    double contentW = Math.Max(1.0, previewCanvas?.Width ?? 0);
                    double contentH = Math.Max(1.0, previewCanvas?.Height ?? 0);
                    if (viewportW <= 1.0 || viewportH <= 1.0)
                    {
                        SetZoom(1.0);
                        return;
                    }
                    double fitZoom = Math.Min(viewportW / contentW, viewportH / contentH);
                    SetZoom(fitZoom);
                    previewScroll!.ScrollToHorizontalOffset(0);
                    previewScroll.ScrollToVerticalOffset(0);
                }
                void RefreshPreview()
                {
                    previewCanvas.Width = 960;
                    previewCanvas.Height = 480;
                    previewCanvas.Children.Clear();
                    string layout = string.Equals(cboLayoutDirection.SelectedItem as string, "Ngang", StringComparison.OrdinalIgnoreCase) ? "Ngang" : "D\u1ecdc";
                    if (mode == TenderPopupGeometryMode.WallLineChain)
                    {
                        BuildNormalizedSegments(
                            segmentRows.Select(r => new HeightSegmentInputRow { LengthMm = r.LengthMm, HeightMm = r.HeightMm, CadHandle = r.CadHandle }),
                            Math.Max(1, referenceLengthMm),
                            Math.Max(1, referenceHeightMm),
                            out var normalized,
                            out var note,
                            autoFillMissing: false);
                        referenceLengthMm = Math.Max(1, normalized.Sum(s => Math.Max(0, s.LengthMm)));
                        DrawHeightProfilePreview(previewCanvas, normalized, referenceLengthMm, seedRow.PanelWidth, layout, openingRows.Select(ToTenderOpeningFromRow).ToList());
                        lblNote.Text = string.IsNullOrWhiteSpace(note) ? $"Ch\u1ebf \u0111\u1ed9: {mode} | D\u00e0i={referenceLengthMm:F0} mm" : note;
                        lblNote.Foreground = string.IsNullOrWhiteSpace(note) ? Brushes.DarkGreen : Brushes.DarkGoldenrod;
                        return;
                    }
                    if ((mode == TenderPopupGeometryMode.WallPolygon || mode == TenderPopupGeometryMode.CeilingPolygon)
                        && polygonVertices != null
                        && polygonVertices.Count >= 3)
                    {
                        DrawLocalPolygonPreview(previewCanvas, polygonVertices, seedRow.PanelWidth, string.Equals(layout, "Ngang", StringComparison.OrdinalIgnoreCase), openingRows.Select(ToTenderOpeningFromRow).ToList(), 0, false, seedRow);
                        lblNote.Text = $"Ch\u1ebf \u0111\u1ed9: {mode} | Bi\u00ean d\u1ea1ng v\u00f9ng: {polygonVertices.Count} \u0111\u1ec9nh";
                        lblNote.Foreground = Brushes.DarkGreen;
                        return;
                    }
                    lblNote.Text = isCeiling ? "Ch\u1ecdn Pick v\u00f9ng \u0111\u1ec3 l\u1ea5y bi\u00ean d\u1ea1ng tr\u1ea7n." : "Ch\u1ecdn Pick nh\u1ecbp ho\u1eb7c Pick v\u00f9ng \u0111\u1ec3 b\u1eaft \u0111\u1ea7u.";
                    lblNote.Foreground = Brushes.Firebrick;
                }
                var dlg = new Window
                {
                    Title = isRepick ? "Pick l\u1ea1i h\u00ecnh h\u1ecdc Tender" : "Pick h\u00ecnh h\u1ecdc Tender",
                    Width = 1180,
                    Height = 860,
                    MinWidth = 1100,
                    MinHeight = 760,
                    WindowStartupLocation = WindowStartupLocation.CenterScreen,
                    ResizeMode = ResizeMode.CanResize,
                    Background = new SolidColorBrush(Color.FromRgb(250, 250, 252)),
                    Owner = this
                };
                var root = new Grid { Margin = new Thickness(14) };
                root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(220) });
                root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                dlg.Content = root;
                root.Children.Add(new TextBlock
                {
                    Text = isCeiling
                        ? "Tr\u1ea7n ch\u1ec9 cho Pick v\u00f9ng. M\u1ecdi thao t\u00e1c h\u00ecnh h\u1ecdc, preview v\u00e0 apply th\u1ef1c hi\u1ec7n trong popup n\u00e0y."
                        : "V\u00e1ch cho ph\u00e9p Pick nh\u1ecbp, Pick v\u00f9ng v\u00e0 Pick l\u1ed7 m\u1edf. \u00c1p d\u1ee5ng xong s\u1ebd ch\u1ecdn \u0111i\u1ec3m \u0111\u1eb7t \u0111\u1ec3 d\u1ef1ng CAD th\u1eadt.",
                    TextWrapping = TextWrapping.Wrap,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = FgDark,
                    Margin = new Thickness(0, 0, 0, 8)
                });
                var toolbar = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
                var leftTools = new StackPanel { Orientation = Orientation.Horizontal };
                var rightTools = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
                leftTools.Children.Add(new TextBlock
                {
                    Text = "H\u01b0\u1edbng chia t\u1ea5m:",
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 6, 0),
                    FontWeight = FontWeights.SemiBold,
                    Foreground = FgDark
                });
                cboLayoutDirection = new ComboBox
                {
                    Width = 120,
                    ItemsSource = new[] { "D\u1ecdc", "Ngang" },
                    SelectedValue = string.Equals(seedRow.LayoutDirection, "Ngang", StringComparison.OrdinalIgnoreCase) ? "Ngang" : "D\u1ecdc",
                    Margin = new Thickness(0, 0, 8, 0)
                };
                cboLayoutDirection.SelectionChanged += (_, _) => RefreshPreview();
                leftTools.Children.Add(cboLayoutDirection);
                var btnPickSpan = Btn("Pick nh\u1ecbp", AccentBlue, Brushes.White, (_, _) =>
                {
                    if (isCeiling)
                        return;
                    dlg.Hide();
                    try
                    {
                        var doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
                        if (doc == null)
                            return;
                        var ed = doc.Editor;
                        while (true)
                        {
                            var p1Opt = new Autodesk.AutoCAD.EditorInput.PromptPointOptions("\nCh\u1ecdn \u0111i\u1ec3m \u0111\u1ea7u nh\u1ecbp (Enter \u0111\u1ec3 k\u1ebft th\u00fac):") { AllowNone = true };
                            var p1Res = ed.GetPoint(p1Opt);
                            if (p1Res.Status == Autodesk.AutoCAD.EditorInput.PromptStatus.None || p1Res.Status != Autodesk.AutoCAD.EditorInput.PromptStatus.OK)
                                break;
                            var p2Opt = new Autodesk.AutoCAD.EditorInput.PromptPointOptions("\nCh\u1ecdn \u0111i\u1ec3m cu\u1ed1i nh\u1ecbp:");
                            p2Opt.UseBasePoint = true;
                            p2Opt.BasePoint = p1Res.Value;
                            var p2Res = ed.GetPoint(p2Opt);
                            if (p2Res.Status != Autodesk.AutoCAD.EditorInput.PromptStatus.OK)
                                break;
                            double lengthMm = Math.Round(p1Res.Value.DistanceTo(p2Res.Value));
                            if (lengthMm <= 0)
                                continue;
                            if (!TryPromptWallHeightInput(referenceHeightMm, out var heightMm) || heightMm <= 0)
                                break;
                            TryCreatePersistentPickSpanLine(p1Res.Value, p2Res.Value, out var handle, out _);
                            segmentRows.Add(new PopupSegmentRow
                            {
                                LengthMm = lengthMm,
                                HeightMm = Math.Round(heightMm),
                                CadHandle = string.IsNullOrWhiteSpace(handle) ? null : handle,
                                IsDraftCadHandle = !string.IsNullOrWhiteSpace(handle),
                                StartPoint = p1Res.Value,
                                EndPoint = p2Res.Value
                            });
                            mode = TenderPopupGeometryMode.WallLineChain;
                            polygonVertices = null;
                        }
                    }
                    finally
                    {
                        dlg.Show();
                        dlg.Activate();
                        RefreshPreview();
                    }
                }, 110);
                var btnPickArea = Btn("Pick v\u00f9ng", AccentBlue, Brushes.White, (_, _) =>
                {
                    dlg.Hide();
                    try
                    {
                        if (!TryPickClosedPolygonVertices(out var pickedVertices, out _, out var approxHeightMm))
                            return;
                        polygonVertices = pickedVertices;
                        referenceHeightMm = Math.Max(1, approxHeightMm > 0 ? approxHeightMm : referenceHeightMm);
                        mode = isCeiling ? TenderPopupGeometryMode.CeilingPolygon : TenderPopupGeometryMode.WallPolygon;
                        if (isCeiling && IsSuspendedCeilingRow(seedRow))
                        {
                            var tempRow = seedRow.Clone();
                            tempRow.PolygonVertices = polygonVertices.Select(v => v.ToArray()).ToList();
                            tempRow.LayoutDirection = string.Equals(cboLayoutDirection.SelectedItem as string, "Ngang", StringComparison.OrdinalIgnoreCase) ? "Ngang" : "D\u1ecdc";
                            if (!TryConfigureSuspendedCeilingDivision(tempRow))
                            {
                                polygonVertices = seedRow.PolygonVertices?.Select(v => v.ToArray()).ToList();
                                mode = ResolvePopupMode(seedRow);
                                return;
                            }
                            seedRow.SuspensionLayoutDirection = tempRow.SuspensionLayoutDirection;
                            seedRow.ColdStorageDivideFromMaxSide = tempRow.ColdStorageDivideFromMaxSide;
                        }
                    }
                    finally
                    {
                        dlg.Show();
                        dlg.Activate();
                        RefreshPreview();
                    }
                }, 110);
                var btnPickOpening = Btn("Pick l\u1ed7 m\u1edf", AccentOrange, Brushes.White, (_, _) =>
                {
                    if (isCeiling || mode == TenderPopupGeometryMode.None)
                        return;
                    dlg.Hide();
                    try
                    {
                        if (TryPickOpeningForPopup(mode, segmentRows.ToList(), polygonVertices, referenceLengthMm, out var opening))
                            openingRows.Add(opening);
                    }
                    finally
                    {
                        dlg.Show();
                        dlg.Activate();
                        RefreshPreview();
                    }
                }, 110);
                var btnDeleteSpan = Btn("X\u00f3a nh\u1ecbp", AccentRed, Brushes.White, (_, _) =>
                {
                    var current = segmentGrid?.SelectedItem as PopupSegmentRow ?? segmentRows.LastOrDefault();
                    if (current == null)
                        return;
                    if (current.IsDraftCadHandle && !string.IsNullOrWhiteSpace(current.CadHandle))
                        TryEraseCadEntitiesByHandles(new[] { current.CadHandle! });
                    segmentRows.Remove(current);
                    RefreshPreview();
                }, 100);
                var btnDeleteOpening = Btn("X\u00f3a l\u1ed7 m\u1edf", AccentRed, Brushes.White, (_, _) =>
                {
                    var current = openingGrid?.SelectedItem as TenderOpeningRow ?? openingRows.LastOrDefault();
                    if (current == null)
                        return;
                    openingRows.Remove(current);
                    RefreshPreview();
                }, 110);
                btnPickSpan.IsEnabled = !isCeiling;
                btnPickOpening.IsEnabled = !isCeiling;
                btnDeleteSpan.IsEnabled = !isCeiling;
                btnDeleteOpening.IsEnabled = !isCeiling;
                leftTools.Children.Add(btnPickSpan);
                leftTools.Children.Add(btnPickArea);
                leftTools.Children.Add(btnPickOpening);
                leftTools.Children.Add(btnDeleteSpan);
                leftTools.Children.Add(btnDeleteOpening);
                rightTools.Children.Add(Btn("Fit", BtnGray, Brushes.White, (_, _) => FitPreview(), 72));
                rightTools.Children.Add(Btn("100%", BtnGray, Brushes.White, (_, _) => SetZoom(1.0), 72));
                rightTools.Children.Add(Btn("+", BtnGray, Brushes.White, (_, _) => SetZoom(previewZoom * 1.2), 56));
                rightTools.Children.Add(Btn("-", BtnGray, Brushes.White, (_, _) => SetZoom(previewZoom / 1.2), 56));
                toolbar.Children.Add(leftTools);
                DockPanel.SetDock(rightTools, Dock.Right);
                toolbar.Children.Add(rightTools);
                Grid.SetRow(toolbar, 1);
                root.Children.Add(toolbar);
                var editorGrid = new Grid();
                editorGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                editorGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                Grid.SetRow(editorGrid, 2);
                root.Children.Add(editorGrid);
                segmentGrid = new DataGrid
                {
                    AutoGenerateColumns = false,
                    CanUserAddRows = false,
                    CanUserDeleteRows = false,
                    HeadersVisibility = DataGridHeadersVisibility.Column,
                    ItemsSource = segmentRows
                };
                segmentGrid.Columns.Add(new DataGridTextColumn
                {
                    Header = "D\u00e0i (mm)",
                    Binding = new Binding(nameof(PopupSegmentRow.LengthMm)) { StringFormat = "F0", UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged },
                    Width = new DataGridLength(1, DataGridLengthUnitType.Star)
                });
                segmentGrid.Columns.Add(new DataGridTextColumn
                {
                    Header = "Cao (mm)",
                    Binding = new Binding(nameof(PopupSegmentRow.HeightMm)) { StringFormat = "F0", UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged },
                    Width = new DataGridLength(1, DataGridLengthUnitType.Star)
                });
                segmentGrid.CellEditEnding += (_, _) => Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(RefreshPreview));
                Grid.SetColumn(segmentGrid, 0);
                editorGrid.Children.Add(segmentGrid);
                openingGrid = new DataGrid
                {
                    AutoGenerateColumns = false,
                    CanUserAddRows = false,
                    CanUserDeleteRows = false,
                    HeadersVisibility = DataGridHeadersVisibility.Column,
                    ItemsSource = openingRows,
                    Margin = new Thickness(8, 0, 0, 0)
                };
                openingGrid.Columns.Add(new DataGridTextColumn { Header = "Lo\u1ea1i", Binding = new Binding(nameof(TenderOpeningRow.Type)), Width = new DataGridLength(1.2, DataGridLengthUnitType.Star) });
                openingGrid.Columns.Add(new DataGridTextColumn { Header = "R\u1ed9ng", Binding = new Binding(nameof(TenderOpeningRow.Width)) { StringFormat = "F0" }, Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
                openingGrid.Columns.Add(new DataGridTextColumn { Header = "Cao", Binding = new Binding(nameof(TenderOpeningRow.Height)) { StringFormat = "F0" }, Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
                openingGrid.Columns.Add(new DataGridTextColumn { Header = "\u0110\u00e1y", Binding = new Binding(nameof(TenderOpeningRow.BottomElevationMm)) { StringFormat = "F0" }, Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
                openingGrid.Columns.Add(new DataGridTextColumn { Header = "LT", Binding = new Binding(nameof(TenderOpeningRow.StationStartMm)) { StringFormat = "F0" }, Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
                Grid.SetColumn(openingGrid, 1);
                editorGrid.Children.Add(openingGrid);
                var previewGrid = new Grid();
                previewGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                previewGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                previewGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                Grid.SetRow(previewGrid, 3);
                root.Children.Add(previewGrid);
                previewGrid.Children.Add(new TextBlock
                {
                    Text = "Xem tr\u01b0\u1edbc popup",
                    FontWeight = FontWeights.SemiBold,
                    Foreground = FgDark,
                    Margin = new Thickness(0, 0, 0, 6)
                });
                previewScroll = new ScrollViewer
                {
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    Background = new SolidColorBrush(Color.FromRgb(245, 248, 255))
                };
                previewCanvas = new Canvas
                {
                    Width = 960,
                    Height = 480,
                    Background = new SolidColorBrush(Color.FromRgb(245, 248, 255)),
                    LayoutTransform = new ScaleTransform(1, 1)
                };
                previewCanvas.MouseWheel += (_, e) =>
                {
                    SetZoom(previewZoom * (e.Delta > 0 ? 1.1 : 0.9));
                    e.Handled = true;
                };
                previewScroll.PreviewMouseLeftButtonDown += (_, e) =>
                {
                    panStart = e.GetPosition(previewScroll);
                    panStartH = previewScroll.HorizontalOffset;
                    panStartV = previewScroll.VerticalOffset;
                    previewScroll.Cursor = Cursors.SizeAll;
                };
                previewScroll.PreviewMouseMove += (_, e) =>
                {
                    if (!panStart.HasValue || e.LeftButton != MouseButtonState.Pressed)
                        return;
                    var current = e.GetPosition(previewScroll);
                    previewScroll.ScrollToHorizontalOffset(panStartH - (current.X - panStart.Value.X));
                    previewScroll.ScrollToVerticalOffset(panStartV - (current.Y - panStart.Value.Y));
                };
                previewScroll.PreviewMouseLeftButtonUp += (_, _) =>
                {
                    panStart = null;
                    previewScroll.Cursor = Cursors.Arrow;
                };
                previewScroll.Content = previewCanvas;
                Grid.SetRow(previewScroll, 1);
                previewGrid.Children.Add(previewScroll);
                lblNote = new TextBlock
                {
                    Margin = new Thickness(0, 6, 0, 0),
                    FontWeight = FontWeights.SemiBold,
                    Foreground = Brushes.DarkGreen
                };
                Grid.SetRow(lblNote, 2);
                previewGrid.Children.Add(lblNote);
                var footer = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Margin = new Thickness(0, 10, 0, 0)
                };
                Grid.SetRow(footer, 4);
                root.Children.Add(footer);
                footer.Children.Add(Btn("H\u1ee7y", BtnGray, Brushes.White, (_, _) =>
                {
                    accepted = false;
                    foreach (var draftHandle in segmentRows.Where(r => r.IsDraftCadHandle && !string.IsNullOrWhiteSpace(r.CadHandle)).Select(r => r.CadHandle!).Distinct(StringComparer.OrdinalIgnoreCase))
                        TryEraseCadEntitiesByHandles(new[] { draftHandle });
                    dlg.Close();
                }, 110));
                footer.Children.Add(Btn("\u00c1p d\u1ee5ng", AccentGreen, Brushes.White, (_, _) =>
                {
                    if (mode == TenderPopupGeometryMode.None)
                    {
                        lblNote.Text = "Ch\u01b0a c\u00f3 h\u00ecnh h\u1ecdc \u0111\u1ec3 \u00e1p d\u1ee5ng.";
                        lblNote.Foreground = Brushes.Firebrick;
                        return;
                    }
                    string layout = string.Equals(cboLayoutDirection.SelectedItem as string, "Ngang", StringComparison.OrdinalIgnoreCase) ? "Ngang" : "D\u1ecdc";
                    if (mode == TenderPopupGeometryMode.WallLineChain)
                    {
                        if (!BuildNormalizedSegments(
                                segmentRows.Select(r => new HeightSegmentInputRow { LengthMm = r.LengthMm, HeightMm = r.HeightMm, CadHandle = r.CadHandle }),
                                Math.Max(1, referenceLengthMm),
                                Math.Max(1, referenceHeightMm),
                                out var normalized,
                                out var note,
                                autoFillMissing: true))
                        {
                            lblNote.Text = note;
                            lblNote.Foreground = Brushes.Firebrick;
                            return;
                        }
                        popupResult.Segments = normalized;
                        popupResult.RepresentativeHeightMm = normalized.Count > 0 ? normalized.Max(s => s.HeightMm) : referenceHeightMm;
                    }
                    else
                    {
                        if (polygonVertices == null || polygonVertices.Count < 3)
                        {
                            lblNote.Text = "Cần Pick vùng trước khi áp dụng.";
                            lblNote.Foreground = Brushes.Firebrick;
                            return;
                        }
                        popupResult.PolygonVertices = polygonVertices.Select(v => v.ToArray()).ToList();
                        
                        double minX = popupResult.PolygonVertices.Min(v => v[0]);
                        double maxX = popupResult.PolygonVertices.Max(v => v[0]);
                        double minY = popupResult.PolygonVertices.Min(v => v[1]);
                        double maxY = popupResult.PolygonVertices.Max(v => v[1]);
                        
                        popupResult.RepresentativeHeightMm = Math.Max(1.0, maxY - minY);
                        popupResult.Segments = new List<TenderHeightSegment>
                        {
                            new TenderHeightSegment { LengthMm = Math.Max(1.0, maxX - minX), HeightMm = popupResult.RepresentativeHeightMm }
                        };
                    }
                    popupResult.Mode = mode;
                    popupResult.LayoutDirection = layout;
                    popupResult.Openings = openingRows.Select(ToTenderOpeningFromRow).ToList();
                    popupResult.DraftCadHandles = segmentRows.Where(r => r.IsDraftCadHandle && !string.IsNullOrWhiteSpace(r.CadHandle)).Select(r => r.CadHandle!).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                    popupResult.CadDraftEntityIds = popupResult.DraftCadHandles.ToList();
                    popupResult.HeightSegmentsDraft = popupResult.Segments.Select(s => new TenderHeightSegment
                    {
                        LengthMm = s.LengthMm,
                        HeightMm = s.HeightMm,
                        CadHandle = s.CadHandle
                    }).ToList();
                    popupResult.OpeningsDraft = CloneOpenings(popupResult.Openings);
                    if (popupResult.ReferenceGeometry == null && popupResult.Mode == TenderPopupGeometryMode.WallLineChain)
                    {
                        double lineChainLength = popupResult.Segments.Sum(s => Math.Max(0, s.LengthMm));
                        double lineChainHeight = popupResult.Segments.Count > 0 ? popupResult.Segments.Max(s => Math.Max(0, s.HeightMm)) : 0;
                        var developedChain = new List<double[]> { new[] { 0.0, 0.0 } };
                        double chainCursor = 0;
                        foreach (var seg in popupResult.Segments)
                        {
                            chainCursor += Math.Max(0, seg.LengthMm);
                            developedChain.Add(new[] { chainCursor, 0.0 });
                        }
                        popupResult.ReferenceGeometry = new ReferenceGeometry
                        {
                            GeometryMode = TenderPopupGeometryMode.WallLineChain,
                            ReferenceLengthMm = lineChainLength,
                            ReferenceHeightMm = lineChainHeight,
                            BoundaryVertices = BuildStepBoundaryFromSegments(popupResult.Segments),
                            DevelopedChainVertices = developedChain,
                            Origin = new[] { 0.0, 0.0 },
                            UAxis = new[] { 1.0, 0.0 },
                            VAxis = new[] { 0.0, 1.0 },
                            IsRectangularLike = true
                        };
                    }
                    popupResult.SuspensionLayoutDirection = seedRow.SuspensionLayoutDirection;
                    popupResult.ColdStorageDivideFromMaxSide = seedRow.ColdStorageDivideFromMaxSide;
                    accepted = true;
                    dlg.Close();
                }, 110));
                dlg.Loaded += (_, _) =>
                {
                    RefreshPreview();
                    FitPreview();
                };
                dlg.SizeChanged += (_, _) => RefreshPreview();
                dlg.ShowDialog();
            });
            if (accepted)
                result = popupResult;
            return accepted;
        }
        private static List<TenderHeightSegment> BuildRepickSeedSegments(
            IReadOnlyList<TenderHeightSegment>? sourceSegments,
            double currentLengthMm,
            double repickedLengthMm)
        {
            var cloned = (sourceSegments ?? Array.Empty<TenderHeightSegment>())
                .Where(s => s != null && s.LengthMm > 0 && s.HeightMm > 0)
                .Select(s => new TenderHeightSegment
                {
                    LengthMm = s.LengthMm,
                    HeightMm = s.HeightMm,
                    // Repick sang tuyáº¿n má»›i pháº£i cáº¯t liÃªn káº¿t CAD cÅ© cá»§a tá»«ng nhá»‹p,
                    // náº¿u khÃ´ng timer sync sáº½ kÃ©o chiá»u dÃ i vá» line cÅ©.
                    CadHandle = null
                })
                .ToList();
            if (cloned.Count == 0)
                return cloned;
            double sourceLength = cloned.Sum(s => Math.Max(0, s.LengthMm));
            if (sourceLength <= 0 && currentLengthMm > 0)
                sourceLength = currentLengthMm;
            if (sourceLength <= 0 || repickedLengthMm <= 0)
                return cloned;
            if (Math.Abs(sourceLength - repickedLengthMm) < 1.0)
                return cloned;
            double ratio = repickedLengthMm / sourceLength;
            double accumulated = 0;
            for (int i = 0; i < cloned.Count; i++)
            {
                if (i == cloned.Count - 1)
                {
                    cloned[i].LengthMm = Math.Max(1, Math.Round(repickedLengthMm - accumulated));
                }
                else
                {
                    cloned[i].LengthMm = Math.Max(1, Math.Round(cloned[i].LengthMm * ratio));
                    accumulated += cloned[i].LengthMm;
                }
            }
            return cloned;
        }
        private static List<TenderOpening> BuildRepickSeedOpenings(
            IReadOnlyList<TenderOpening>? sourceOpenings,
            double currentLengthMm,
            double repickedLengthMm)
        {
            var cloned = CloneOpenings(sourceOpenings);
            if (cloned.Count == 0 || currentLengthMm <= 0 || repickedLengthMm <= 0)
                return cloned;
            if (Math.Abs(currentLengthMm - repickedLengthMm) < 1.0)
                return cloned;
            double ratio = repickedLengthMm / currentLengthMm;
            foreach (var opening in cloned)
            {
                if (opening.CenterStationMm >= 0)
                {
                    opening.CenterStationMm = Math.Max(
                        0,
                        Math.Min(
                            Math.Round(opening.CenterStationMm * ratio),
                            Math.Round(repickedLengthMm)));
                }
                if (opening.StationStartMm >= 0)
                    opening.StationStartMm = Math.Max(0, Math.Min(Math.Round(opening.StationStartMm * ratio), Math.Round(repickedLengthMm)));
                if (opening.StationEndMm >= 0)
                    opening.StationEndMm = Math.Max(0, Math.Min(Math.Round(opening.StationEndMm * ratio), Math.Round(repickedLengthMm)));
            }
            return cloned;
        }
        private static List<TenderOpening> CloneOpenings(IEnumerable<TenderOpening>? openings)
        {
            return (openings ?? Enumerable.Empty<TenderOpening>())
                .Where(o => o != null && o.Width > 0 && o.Height > 0 && o.Quantity > 0)
                .Select(o => new TenderOpening
                {
                    Type = string.IsNullOrWhiteSpace(o.Type) ? "C\u1eeda \u0111i" : o.Type,
                    Width = Math.Max(1, Math.Round(o.Width)),
                    Height = Math.Max(1, Math.Round(o.Height)),
                    BottomElevationMm = Math.Max(0, Math.Round(o.BottomElevationMm)),
                    CenterStationMm = o.CenterStationMm,
                    StationStartMm = o.StationStartMm,
                    StationEndMm = o.StationEndMm,
                    ResolvedChainRatioStart = o.ResolvedChainRatioStart,
                    ResolvedChainRatioEnd = o.ResolvedChainRatioEnd,
                    Quantity = Math.Max(1, o.Quantity),
                    OpeningPolygon = o.OpeningPolygon?.Select(p => p.ToArray()).ToList()
                })
                .ToList();
        }
        private static void DrawHeightProfilePreview(
            Canvas canvas,
            IReadOnlyList<TenderHeightSegment> segments,
            double totalLengthMm,
            int panelWidthMm,
            string layoutDirection,
            IReadOnlyList<TenderOpening>? openings = null)
        {
            canvas.Children.Clear();
            double drawingLength = segments.Sum(s => Math.Max(0, s.LengthMm));
            if (segments.Count == 0 || drawingLength <= 0)
                return;
            double w = Math.Max(100, canvas.ActualWidth <= 0 ? canvas.Width : canvas.ActualWidth);
            double h = Math.Max(100, canvas.ActualHeight <= 0 ? canvas.Height : canvas.ActualHeight);
            double margin = 36;
            double plotW = Math.Max(20, w - margin * 2);
            double plotH = Math.Max(20, h - margin * 2);
            double maxHeight = Math.Max(1, segments.Max(s => s.HeightMm));
            double xCursor = margin;
            for (int i = 0; i < segments.Count; i++)
            {
                var segment = segments[i];
                double segW = plotW * segment.LengthMm / drawingLength;
                double segH = plotH * segment.HeightMm / maxHeight;
                double top = margin + (plotH - segH);
                var label = new TextBlock
                {
                    Text = $"L{segment.LengthMm:F0} / H{segment.HeightMm:F0}",
                    FontSize = 11,
                    Foreground = Brushes.Black,
                    Background = new SolidColorBrush(Color.FromArgb(180, 255, 255, 255))
                };
                Canvas.SetLeft(label, xCursor + 4);
                Canvas.SetTop(label, Math.Max(margin, top - 18));
                canvas.Children.Add(label);
                xCursor += segW;
            }
            double bottomY = margin + plotH;
            var baseLine = new System.Windows.Shapes.Line
            {
                X1 = margin,
                X2 = margin + plotW,
                Y1 = bottomY,
                Y2 = bottomY,
                Stroke = new SolidColorBrush(Color.FromRgb(45, 45, 45)),
                StrokeThickness = 1.5
            };
            canvas.Children.Add(baseLine);
            double cursorMm = 0;
            for (int i = 0; i < segments.Count; i++)
            {
                double x1 = margin + (cursorMm / drawingLength) * plotW;
                cursorMm += segments[i].LengthMm;
                double x2 = margin + (cursorMm / drawingLength) * plotW;
                double yTop = margin + (plotH - (segments[i].HeightMm / maxHeight) * plotH);
                var topLine = new System.Windows.Shapes.Line
                {
                    X1 = x1,
                    X2 = x2,
                    Y1 = yTop,
                    Y2 = yTop,
                    Stroke = new SolidColorBrush(Color.FromRgb(45, 45, 45)),
                    StrokeThickness = 1.5
                };
                canvas.Children.Add(topLine);
                if (i == 0)
                {
                    var leftUp = new System.Windows.Shapes.Line
                    {
                        X1 = x1,
                        X2 = x1,
                        Y1 = bottomY,
                        Y2 = yTop,
                        Stroke = new SolidColorBrush(Color.FromRgb(45, 45, 45)),
                        StrokeThickness = 1.5
                    };
                    canvas.Children.Add(leftUp);
                }
                if (i < segments.Count - 1)
                {
                    double yNext = margin + (plotH - (segments[i + 1].HeightMm / maxHeight) * plotH);
                    var stepLine = new System.Windows.Shapes.Line
                    {
                        X1 = x2,
                        X2 = x2,
                        Y1 = yTop,
                        Y2 = yNext,
                        Stroke = new SolidColorBrush(Color.FromRgb(45, 45, 45)),
                        StrokeThickness = 1.5
                    };
                    canvas.Children.Add(stepLine);
                }
                else
                {
                    var rightDown = new System.Windows.Shapes.Line
                    {
                        X1 = x2,
                        X2 = x2,
                        Y1 = yTop,
                        Y2 = bottomY,
                        Stroke = new SolidColorBrush(Color.FromRgb(45, 45, 45)),
                        StrokeThickness = 1.5
                    };
                    canvas.Children.Add(rightDown);
                }
            }
            if (panelWidthMm <= 0)
            {
                DrawOpeningPreviewMarkers(canvas, openings, segments, margin, plotW, plotH, bottomY, maxHeight, drawingLength);
                return;
            }
            if (string.Equals(layoutDirection, "D\u1ecdc", StringComparison.OrdinalIgnoreCase))
            {
                for (double boundary = panelWidthMm; boundary < drawingLength - 0.5; boundary += panelWidthMm)
                {
                    double x = margin + (boundary / drawingLength) * plotW;
                    double hLeft = GetHeightAt(boundary - 1, segments, drawingLength);
                    double hRight = GetHeightAt(boundary + 1, segments, drawingLength);
                    double hBoundary = Math.Max(hLeft, hRight);
                    double top = margin + (plotH - (hBoundary / maxHeight) * plotH);
                    var divLine = new System.Windows.Shapes.Line
                    {
                        X1 = x,
                        X2 = x,
                        Y1 = margin + plotH,
                        Y2 = top,
                        Stroke = new SolidColorBrush(Color.FromRgb(30, 30, 30)),
                        StrokeDashArray = new DoubleCollection { 3, 2 },
                        StrokeThickness = 1
                    };
                    canvas.Children.Add(divLine);

                    double centerX = margin + ((boundary - panelWidthMm/2.0) / drawingLength) * plotW;
                    int panelIndex = (int)(boundary / panelWidthMm);
                    var pText = new TextBlock { Text = $"{panelWidthMm:F0}", FontSize = 9, Foreground = Brushes.Gray };
                    pText.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                    Canvas.SetLeft(pText, centerX - pText.DesiredSize.Width / 2);
                    Canvas.SetTop(pText, margin + plotH + (panelIndex % 2 == 0 ? 12 : 2));
                    canvas.Children.Add(pText);
                }

                double lastBoundary = Math.Floor(drawingLength / panelWidthMm) * panelWidthMm;
                double lastWidth = drawingLength - lastBoundary;
                if (lastWidth > 1)
                {
                    double centerX = margin + ((lastBoundary + lastWidth / 2.0) / drawingLength) * plotW;
                    int panelIndex = (int)(lastBoundary / panelWidthMm) + 1;
                    var pText = new TextBlock { Text = $"{lastWidth:F0}", FontSize = 9, Foreground = Brushes.Gray };
                    pText.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                    Canvas.SetLeft(pText, centerX - pText.DesiredSize.Width / 2);
                    Canvas.SetTop(pText, margin + plotH + (panelIndex % 2 == 0 ? 12 : 2));
                    canvas.Children.Add(pText);
                }
            }
            else if (string.Equals(layoutDirection, "Ngang", StringComparison.OrdinalIgnoreCase))
            {
                double cursor = 0;
                foreach (var segment in segments)
                {
                    double x1 = margin + (cursor / drawingLength) * plotW;
                    double x2 = margin + ((cursor + segment.LengthMm) / drawingLength) * plotW;
                    cursor += segment.LengthMm;
                    for (double yMm = panelWidthMm; yMm < segment.HeightMm - 0.5; yMm += panelWidthMm)
                    {
                        double y = margin + (plotH - (yMm / maxHeight) * plotH);
                        var divLine = new System.Windows.Shapes.Line
                        {
                            X1 = x1,
                            X2 = x2,
                            Y1 = y,
                            Y2 = y,
                            Stroke = new SolidColorBrush(Color.FromRgb(30, 30, 30)),
                            StrokeDashArray = new DoubleCollection { 3, 2 },
                            StrokeThickness = 1
                        };
                        canvas.Children.Add(divLine);

                        double centerY = margin + (plotH - ((yMm - panelWidthMm/2.0) / maxHeight) * plotH);
                        int panelIndex = (int)(yMm / panelWidthMm);
                        var pText = new TextBlock { Text = $"{panelWidthMm:F0}", FontSize = 9, Foreground = Brushes.Gray };
                        pText.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                        Canvas.SetLeft(pText, x1 + (panelIndex % 2 == 0 ? -30 : 2));
                        Canvas.SetTop(pText, centerY - pText.DesiredSize.Height / 2);
                        canvas.Children.Add(pText);
                    }

                    double lastBoundary = Math.Floor(segment.HeightMm / panelWidthMm) * panelWidthMm;
                    double lastHeight = segment.HeightMm - lastBoundary;
                    if (lastHeight > 1)
                    {
                        double centerY = margin + (plotH - ((lastBoundary + lastHeight / 2.0) / maxHeight) * plotH);
                        int panelIndex = (int)(lastBoundary / panelWidthMm) + 1;
                        var pText = new TextBlock { Text = $"{lastHeight:F0}", FontSize = 9, Foreground = Brushes.Gray };
                        pText.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                        Canvas.SetLeft(pText, x1 + (panelIndex % 2 == 0 ? -30 : 2));
                        Canvas.SetTop(pText, centerY - pText.DesiredSize.Height / 2);
                        canvas.Children.Add(pText);
                    }
                }
            }
            // Overall dimensions
            double ovX1 = margin;
            double ovX2 = margin + plotW;
            double ovY = bottomY;
            canvas.Children.Add(new System.Windows.Shapes.Line { X1 = ovX1, Y1 = ovY + 20, X2 = ovX2, Y2 = ovY + 20, Stroke = Brushes.DimGray, StrokeThickness = 1 });
            canvas.Children.Add(new System.Windows.Shapes.Line { X1 = ovX1, Y1 = ovY, X2 = ovX1, Y2 = ovY + 24, Stroke = Brushes.DimGray, StrokeThickness = 1 });
            canvas.Children.Add(new System.Windows.Shapes.Line { X1 = ovX2, Y1 = ovY, X2 = ovX2, Y2 = ovY + 24, Stroke = Brushes.DimGray, StrokeThickness = 1 });
            var ovTextW = new TextBlock
            {
                Text = $"{Math.Round(drawingLength)}",
                FontSize = 11,
                Foreground = Brushes.DarkSlateGray,
                Background = new SolidColorBrush(Color.FromArgb(180, 255, 255, 255))
            };
            ovTextW.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(ovTextW, ovX1 + (ovX2 - ovX1) / 2 - ovTextW.DesiredSize.Width / 2);
            Canvas.SetTop(ovTextW, ovY + 20 - 16);
            canvas.Children.Add(ovTextW);

            double lX = margin;
            double lY1 = bottomY;
            double lY2 = margin;
            canvas.Children.Add(new System.Windows.Shapes.Line { X1 = lX - 20, Y1 = lY1, X2 = lX - 20, Y2 = lY2, Stroke = Brushes.DimGray, StrokeThickness = 1 });
            canvas.Children.Add(new System.Windows.Shapes.Line { X1 = lX - 24, Y1 = lY1, X2 = lX, Y2 = lY1, Stroke = Brushes.DimGray, StrokeThickness = 1 });
            canvas.Children.Add(new System.Windows.Shapes.Line { X1 = lX - 24, Y1 = lY2, X2 = lX, Y2 = lY2, Stroke = Brushes.DimGray, StrokeThickness = 1 });
            var ovTextH = new TextBlock
            {
                Text = $"{Math.Round(maxHeight)}",
                FontSize = 11,
                Foreground = Brushes.DarkSlateGray,
                Background = new SolidColorBrush(Color.FromArgb(180, 255, 255, 255))
            };
            ovTextH.RenderTransform = new RotateTransform(-90);
            ovTextH.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(ovTextH, lX - 20 - 16);
            Canvas.SetTop(ovTextH, lY2 + (lY1 - lY2) / 2 + ovTextH.DesiredSize.Width / 2);
            canvas.Children.Add(ovTextH);

            DrawOpeningPreviewMarkers(canvas, openings, segments, margin, plotW, plotH, bottomY, maxHeight, drawingLength);
        }
        private static void DrawOpeningPreviewMarkers(
            Canvas canvas,
            IReadOnlyList<TenderOpening>? openings,
            IReadOnlyList<TenderHeightSegment> segments,
            double margin,
            double plotW,
            double plotH,
            double bottomY,
            double maxHeight,
            double drawingLength)
        {
            if (openings == null || openings.Count == 0 || drawingLength <= 0 || plotW <= 0 || plotH <= 0 || maxHeight <= 0)
                return;
            var valid = openings
                .Where(o => o != null && o.Width > 0 && o.Height > 0 && o.Quantity > 0)
                .SelectMany(o => Enumerable.Range(0, Math.Max(1, o.Quantity)).Select(_ => o))
                .ToList();
            if (valid.Count == 0)
                return;
            var withoutStation = valid
                .Select((opening, idx) => new { opening, idx })
                .Where(x => x.opening.CenterStationMm < 0)
                .ToList();
            int count = valid.Count;
            for (int i = 0; i < count; i++)
            {
                var opening = valid[i];
                double stationStartMm = opening.StationStartMm >= 0 ? opening.StationStartMm : opening.CenterStationMm;
                if (stationStartMm < 0)
                {
                    int fallbackIndex = withoutStation.FindIndex(x => ReferenceEquals(x.opening, opening));
                    double ratio = (fallbackIndex + 1.0) / (withoutStation.Count + 1.0);
                    stationStartMm = Math.Max(0, ratio * drawingLength - opening.Width * 0.5);
                }
                stationStartMm = Math.Max(0, Math.Min(drawingLength, stationStartMm));
                double stationEndMm = opening.StationEndMm >= stationStartMm
                    ? Math.Max(stationStartMm, Math.Min(drawingLength, opening.StationEndMm))
                    : Math.Max(stationStartMm, Math.Min(drawingLength, stationStartMm + opening.Width));
                if (stationEndMm - stationStartMm <= 0.5)
                    continue;
                double stationCenterMm = (stationStartMm + stationEndMm) * 0.5;
                double left = margin + (stationStartMm / drawingLength) * plotW;
                double right = margin + (stationEndMm / drawingLength) * plotW;
                double centerX = (left + right) * 0.5;
                double rectW = Math.Max(2, right - left);
                double localHeightMm = Math.Max(1, GetHeightAt(stationCenterMm, segments, drawingLength));
                double bottomElevationMm = Math.Max(0, opening.BottomElevationMm);
                double visibleHeightMm = Math.Max(0, Math.Min(opening.Height, localHeightMm - bottomElevationMm));
                if (visibleHeightMm <= 0.5)
                    continue;
                double rectH = Math.Max(4, (visibleHeightMm / maxHeight) * plotH);
                double openingBottomY = bottomY - (bottomElevationMm / maxHeight) * plotH;
                openingBottomY = Math.Max(margin + rectH, Math.Min(bottomY, openingBottomY));
                double top = Math.Max(margin, openingBottomY - rectH);
                var rect = new System.Windows.Shapes.Rectangle
                {
                    Width = rectW,
                    Height = rectH,
                    Stroke = new SolidColorBrush(Color.FromRgb(196, 44, 44)),
                    StrokeThickness = 1.5,
                    Fill = new SolidColorBrush(Color.FromArgb(50, 255, 255, 255))
                };
                Canvas.SetLeft(rect, left);
                Canvas.SetTop(rect, top);
                canvas.Children.Add(rect);
                var dimBrush = new SolidColorBrush(Color.FromRgb(34, 90, 160));
                var tickBrush = dimBrush;
                // Dim Ä‘á»‹nh vá»‹ theo tuyáº¿n: tá»« Ä‘áº§u vÃ¡ch Ä‘áº¿n mÃ©p lá»— má»Ÿ.
                double dimY = Math.Min(bottomY + 12 + (i % 3) * 10, bottomY + Math.Max(6, margin - 2));
                var dimLt = new System.Windows.Shapes.Line
                {
                    X1 = margin,
                    Y1 = dimY,
                    X2 = left,
                    Y2 = dimY,
                    Stroke = dimBrush,
                    StrokeThickness = 1
                };
                canvas.Children.Add(dimLt);
                canvas.Children.Add(new System.Windows.Shapes.Line
                {
                    X1 = margin,
                    Y1 = bottomY,
                    X2 = margin,
                    Y2 = dimY,
                    Stroke = tickBrush,
                    StrokeThickness = 1
                });
                canvas.Children.Add(new System.Windows.Shapes.Line
                {
                    X1 = left,
                    Y1 = openingBottomY,
                    X2 = left,
                    Y2 = dimY,
                    Stroke = tickBrush,
                    StrokeThickness = 1
                });
                var ltLabel = new TextBlock
                {
                    Text = $"LT {stationStartMm:F0}",
                    FontSize = 9,
                    Foreground = dimBrush,
                    Background = new SolidColorBrush(Color.FromArgb(220, 255, 255, 255))
                };
                Canvas.SetLeft(ltLabel, Math.Max(margin, (margin + left) * 0.5 - 18));
                Canvas.SetTop(ltLabel, Math.Max(0, dimY - 14));
                canvas.Children.Add(ltLabel);
                // Dim cao Ä‘á»™ Ä‘Ã¡y lá»— má»Ÿ.
                double dimBottomX = Math.Max(margin + 4, left - 12 - (i % 2) * 8);
                canvas.Children.Add(new System.Windows.Shapes.Line
                {
                    X1 = dimBottomX,
                    Y1 = bottomY,
                    X2 = dimBottomX,
                    Y2 = openingBottomY,
                    Stroke = dimBrush,
                    StrokeThickness = 1
                });
                canvas.Children.Add(new System.Windows.Shapes.Line
                {
                    X1 = dimBottomX,
                    Y1 = bottomY,
                    X2 = left,
                    Y2 = bottomY,
                    Stroke = tickBrush,
                    StrokeThickness = 1
                });
                canvas.Children.Add(new System.Windows.Shapes.Line
                {
                    X1 = dimBottomX,
                    Y1 = openingBottomY,
                    X2 = left,
                    Y2 = openingBottomY,
                    Stroke = tickBrush,
                    StrokeThickness = 1
                });
                var bottomLabel = new TextBlock
                {
                    Text = $"\u0110\u00e1y {bottomElevationMm:F0}",
                    FontSize = 9,
                    Foreground = dimBrush,
                    Background = new SolidColorBrush(Color.FromArgb(220, 255, 255, 255))
                };
                Canvas.SetLeft(bottomLabel, Math.Max(margin, dimBottomX - 36));
                Canvas.SetTop(bottomLabel, Math.Max(margin, (openingBottomY + bottomY) * 0.5 - 7));
                canvas.Children.Add(bottomLabel);
                // Dim kÃ­ch thÆ°á»›c lá»— má»Ÿ: rá»™ng + cao.
                double dimWidthY = Math.Max(margin + 6, top - 12 - (i % 2) * 10);
                canvas.Children.Add(new System.Windows.Shapes.Line
                {
                    X1 = left,
                    Y1 = dimWidthY,
                    X2 = left + rectW,
                    Y2 = dimWidthY,
                    Stroke = dimBrush,
                    StrokeThickness = 1
                });
                canvas.Children.Add(new System.Windows.Shapes.Line
                {
                    X1 = left,
                    Y1 = dimWidthY,
                    X2 = left,
                    Y2 = top,
                    Stroke = tickBrush,
                    StrokeThickness = 1
                });
                canvas.Children.Add(new System.Windows.Shapes.Line
                {
                    X1 = right,
                    Y1 = dimWidthY,
                    X2 = right,
                    Y2 = top,
                    Stroke = tickBrush,
                    StrokeThickness = 1
                });
                var widthLabel = new TextBlock
                {
                    Text = $"W {opening.Width:F0}",
                    FontSize = 9,
                    Foreground = dimBrush,
                    Background = new SolidColorBrush(Color.FromArgb(220, 255, 255, 255))
                };
                Canvas.SetLeft(widthLabel, Math.Max(margin, (left + right) * 0.5 - 16));
                Canvas.SetTop(widthLabel, Math.Max(0, dimWidthY - 13));
                canvas.Children.Add(widthLabel);
                double dimHeightX = Math.Min(margin + plotW - 4, right + 12 + (i % 2) * 8);
                canvas.Children.Add(new System.Windows.Shapes.Line
                {
                    X1 = dimHeightX,
                    Y1 = top,
                    X2 = dimHeightX,
                    Y2 = openingBottomY,
                    Stroke = dimBrush,
                    StrokeThickness = 1
                });
                canvas.Children.Add(new System.Windows.Shapes.Line
                {
                    X1 = right,
                    Y1 = top,
                    X2 = dimHeightX,
                    Y2 = top,
                    Stroke = tickBrush,
                    StrokeThickness = 1
                });
                canvas.Children.Add(new System.Windows.Shapes.Line
                {
                    X1 = right,
                    Y1 = openingBottomY,
                    X2 = dimHeightX,
                    Y2 = openingBottomY,
                    Stroke = tickBrush,
                    StrokeThickness = 1
                });
                var heightLabel = new TextBlock
                {
                    Text = $"H {opening.Height:F0}",
                    FontSize = 9,
                    Foreground = dimBrush,
                    Background = new SolidColorBrush(Color.FromArgb(220, 255, 255, 255))
                };
                Canvas.SetLeft(heightLabel, Math.Max(margin, dimHeightX - 16));
                Canvas.SetTop(heightLabel, Math.Max(margin, (top + openingBottomY) * 0.5 - 7));
                canvas.Children.Add(heightLabel);
                var lbl = new TextBlock
                {
                    Text = $"L\u1ed7{i + 1}: {opening.Width:F0}x{opening.Height:F0} | LT {stationStartMm:F0} | \u0110\u00e1y {bottomElevationMm:F0}",
                    FontSize = 10,
                    Foreground = new SolidColorBrush(Color.FromRgb(128, 20, 20)),
                    Background = new SolidColorBrush(Color.FromArgb(210, 255, 255, 255))
                };
                Canvas.SetLeft(lbl, Math.Max(margin, left));
                Canvas.SetTop(lbl, Math.Max(margin, top - 16));
                canvas.Children.Add(lbl);
            }
        }
        private static double GetHeightAt(double xMm, IReadOnlyList<TenderHeightSegment> segments, double totalLengthMm)
        {
            double x = Math.Max(0, Math.Min(totalLengthMm, xMm));
            double cursor = 0;
            foreach (var segment in segments)
            {
                double next = cursor + Math.Max(0, segment.LengthMm);
                if (x <= next + 0.01)
                    return Math.Max(0, segment.HeightMm);
                cursor = next;
            }
            return Math.Max(0, segments.Last().HeightMm);
        }
        private bool TryPickOpeningFromCadForPopup(
            IReadOnlyList<TenderHeightSegment>? segments,
            out TenderOpening opening)
        {
            opening = new TenderOpening();
            var doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            if (doc == null)
                return false;
            var ed = doc.Editor;
            var p1Opt = new Autodesk.AutoCAD.EditorInput.PromptPointOptions("\nCh\u1ecdn \u0111i\u1ec3m 1 l\u1ed7 m\u1edf (Enter \u0111\u1ec3 k\u1ebft th\u00fac):")
            {
                AllowNone = true
            };
            var p1Result = ed.GetPoint(p1Opt);
            if (p1Result.Status == Autodesk.AutoCAD.EditorInput.PromptStatus.None)
                return false;
            if (p1Result.Status != Autodesk.AutoCAD.EditorInput.PromptStatus.OK)
                return false;
            var p2Opt = new Autodesk.AutoCAD.EditorInput.PromptPointOptions("\nCh\u1ecdn \u0111i\u1ec3m 2 l\u1ed7 m\u1edf:");
            p2Opt.UseBasePoint = true;
            p2Opt.BasePoint = p1Result.Value;
            var p2Result = ed.GetPoint(p2Opt);
            if (p2Result.Status != Autodesk.AutoCAD.EditorInput.PromptStatus.OK)
                return false;
            double widthMm = Math.Round(p1Result.Value.DistanceTo(p2Result.Value));
            if (widthMm <= 0)
                return false;
            double stationMm = -1;
            if (TryResolveOpeningStationAndWidthFromCad(
                p1Result.Value,
                p2Result.Value,
                segments,
                out var detectedStation,
                out var projectedWidth))
            {
                stationMm = Math.Round(detectedStation);
                if (projectedWidth > 0)
                    widthMm = Math.Round(projectedWidth);
                ed.WriteMessage($"\n\u0110\u1ecbnh v\u1ecb l\u1ed7 m\u1edf: LT={stationMm:F0} mm | R\u1ed9ng={widthMm:F0} mm");
            }
            var hOpt = new Autodesk.AutoCAD.EditorInput.PromptDistanceOptions("\nNhập hoặc pick ĐIỂM THỨ 3 chiều cao lỗ mở (mm):")
            {
                DefaultValue = 2100,
                AllowNegative = false,
                AllowZero = false,
                UseDefaultValue = true,
                UseBasePoint = false
            };
            var hRes = ed.GetDistance(hOpt);
            if (hRes.Status != Autodesk.AutoCAD.EditorInput.PromptStatus.OK)
                return false;
            double heightMm = Math.Round(hRes.Value);
            if (heightMm <= 0)
                return false;
            var bottomOpt = new Autodesk.AutoCAD.EditorInput.PromptDistanceOptions("\nNhập hoặc pick ĐIỂM THỨ 4 khoảng cách đáy (mm):")
            {
                DefaultValue = 0,
                AllowNegative = false,
                AllowZero = true,
                UseDefaultValue = true,
                UseBasePoint = false
            };
            var bottomRes = ed.GetDistance(bottomOpt);
            if (bottomRes.Status != Autodesk.AutoCAD.EditorInput.PromptStatus.OK)
                return false;
            double bottomElevationMm = Math.Max(0, Math.Round(bottomRes.Value));
            opening = new TenderOpening
            {
                Type = TenderOpening.ResolveTypeByBottomElevation(bottomElevationMm),
                Width = widthMm,
                Height = heightMm,
                BottomElevationMm = bottomElevationMm,
                StationStartMm = stationMm,
                StationEndMm = stationMm >= 0 ? stationMm + widthMm : -1,
                CenterStationMm = stationMm,
                Quantity = 1
            };
            if (opening.StationStartMm >= 0 && opening.StationEndMm >= opening.StationStartMm)
                opening.CenterStationMm = opening.StationStartMm + opening.Width * 0.5;
            return true;
        }
        private bool TryResolveOpeningStationAndWidthFromCad(
            Autodesk.AutoCAD.Geometry.Point3d pickPoint1,
            Autodesk.AutoCAD.Geometry.Point3d pickPoint2,
            IReadOnlyList<TenderHeightSegment>? segments,
            out double stationMm,
            out double projectedWidthMm)
        {
            stationMm = -1;
            projectedWidthMm = 0;
            if (segments == null || segments.Count == 0)
                return false;
            if (!segments.Any(s => s != null && !string.IsNullOrWhiteSpace(s.CadHandle)))
                return false;
            var doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            if (doc == null)
                return false;
            try
            {
                using (doc.LockDocument())
                using (var tr = doc.Database.TransactionManager.StartTransaction())
                {
                    var segmentData = new List<(double StartStation, double LengthMm, Autodesk.AutoCAD.Geometry.Point3d StartPoint, Autodesk.AutoCAD.Geometry.Vector3d Vector, double VectorLength)>();
                    double cumulative = 0;
                    foreach (var seg in segments)
                    {
                        double segLengthMm = Math.Max(0, seg.LengthMm);
                        if (string.IsNullOrWhiteSpace(seg.CadHandle))
                        {
                            cumulative += segLengthMm;
                            continue;
                        }
                        if (!long.TryParse(seg.CadHandle, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out var rawHandle))
                        {
                            cumulative += segLengthMm;
                            continue;
                        }
                        var handle = new Autodesk.AutoCAD.DatabaseServices.Handle(rawHandle);
                        if (!doc.Database.TryGetObjectId(handle, out var objId))
                        {
                            cumulative += segLengthMm;
                            continue;
                        }
                        var line = tr.GetObject(objId, Autodesk.AutoCAD.DatabaseServices.OpenMode.ForRead, false)
                            as Autodesk.AutoCAD.DatabaseServices.Line;
                        if (line == null)
                        {
                            cumulative += segLengthMm;
                            continue;
                        }
                        var vector = line.EndPoint - line.StartPoint;
                        double vectorLength = vector.Length;
                        if (vectorLength <= 1e-6)
                        {
                            cumulative += segLengthMm;
                            continue;
                        }
                        if (segLengthMm <= 0)
                            segLengthMm = vectorLength;
                        segmentData.Add((cumulative, segLengthMm, line.StartPoint, vector, vectorLength));
                        cumulative += segLengthMm;
                    }
                    if (segmentData.Count == 0)
                    {
                        tr.Commit();
                        return false;
                    }
                    static bool TryProjectPoint(
                        Autodesk.AutoCAD.Geometry.Point3d point,
                        List<(double StartStation, double LengthMm, Autodesk.AutoCAD.Geometry.Point3d StartPoint, Autodesk.AutoCAD.Geometry.Vector3d Vector, double VectorLength)> data,
                        out double station,
                        out double distance)
                    {
                        station = -1;
                        distance = double.MaxValue;
                        bool found = false;
                        foreach (var seg in data)
                        {
                            double t = (point - seg.StartPoint).DotProduct(seg.Vector) / (seg.VectorLength * seg.VectorLength);
                            t = Math.Max(0, Math.Min(1, t));
                            var projected = seg.StartPoint + (seg.Vector * t);
                            double d = point.DistanceTo(projected);
                            if (d < distance)
                            {
                                distance = d;
                                station = seg.StartStation + t * seg.LengthMm;
                                found = true;
                            }
                        }
                        return found;
                    }
                    bool p1Ok = TryProjectPoint(pickPoint1, segmentData, out var station1, out var dist1);
                    bool p2Ok = TryProjectPoint(pickPoint2, segmentData, out var station2, out var dist2);
                    tr.Commit();
                    if (!p1Ok || !p2Ok)
                        return false;
                    stationMm = Math.Max(0, Math.Min(station1, station2));
                    projectedWidthMm = Math.Max(0, Math.Abs(station2 - station1));
                    return dist1 < double.MaxValue && dist2 < double.MaxValue;
                }
            }
            catch
            {
                return false;
            }
        }
        private bool TryResolveOpeningStationAndWidthFromWallGeometry(
            Autodesk.AutoCAD.Geometry.Point3d pickPoint1,
            Autodesk.AutoCAD.Geometry.Point3d pickPoint2,
            TenderWallRow wallRow,
            out double stationMm,
            out double projectedWidthMm,
            out double chainRatioStart,
            out double chainRatioEnd)
        {
            stationMm = -1;
            projectedWidthMm = 0;
            chainRatioStart = -1;
            chainRatioEnd = -1;
            if (wallRow == null)
                return false;
            if (TryResolveOpeningStationAndWidthFromCad(
                pickPoint1,
                pickPoint2,
                wallRow.HeightSegments,
                out stationMm,
                out projectedWidthMm))
            {
                return true;
            }
            if (wallRow.PolygonVertices != null
                && wallRow.PolygonVertices.Count >= 3
                && TryResolveOpeningStationAndWidthFromPolygon(
                    pickPoint1,
                    pickPoint2,
                    wallRow.PolygonVertices,
                    Math.Max(1.0, wallRow.Length),
                    preferAxisProjection: !HasNonOrthogonalEdges(wallRow.PolygonVertices),
                    stationMm: out stationMm,
                    projectedWidthMm: out projectedWidthMm,
                    chainRatioStart: out chainRatioStart,
                    chainRatioEnd: out chainRatioEnd))
            {
                return true;
            }
            if (string.IsNullOrWhiteSpace(wallRow.CadHandle))
                return false;
            var doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            if (doc == null)
                return false;
            try
            {
                if (!long.TryParse(
                        wallRow.CadHandle,
                        System.Globalization.NumberStyles.HexNumber,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out var rawHandle))
                {
                    return false;
                }
                using (doc.LockDocument())
                using (var tr = doc.Database.TransactionManager.StartTransaction())
                {
                    var handle = new Autodesk.AutoCAD.DatabaseServices.Handle(rawHandle);
                    if (!doc.Database.TryGetObjectId(handle, out var objId))
                    {
                        tr.Commit();
                        return false;
                    }
                    var ent = tr.GetObject(objId, Autodesk.AutoCAD.DatabaseServices.OpenMode.ForRead, false)
                        as Autodesk.AutoCAD.DatabaseServices.Entity;
                    if (ent == null)
                    {
                        tr.Commit();
                        return false;
                    }
                    bool resolved = false;
                    if (ent is Autodesk.AutoCAD.DatabaseServices.Line line)
                    {
                        resolved = TryResolveOpeningStationAndWidthFromLine(
                            pickPoint1,
                            pickPoint2,
                            line.StartPoint,
                            line.EndPoint,
                            Math.Max(1.0, line.Length),
                            out stationMm,
                            out projectedWidthMm);
                    }
                    else if (ent is Autodesk.AutoCAD.DatabaseServices.Polyline polyline)
                    {
                        var vertices = GetPolylineVertices(polyline);
                        if (polyline.Closed && vertices.Count >= 3)
                        {
                            resolved = TryResolveOpeningStationAndWidthFromPolygon(
                                pickPoint1,
                                pickPoint2,
                                vertices,
                                Math.Max(1.0, wallRow.Length),
                                preferAxisProjection: !HasNonOrthogonalEdges(vertices),
                                stationMm: out stationMm,
                                projectedWidthMm: out projectedWidthMm,
                                chainRatioStart: out chainRatioStart,
                                chainRatioEnd: out chainRatioEnd);
                        }
                        else if (vertices.Count >= 2)
                        {
                            resolved = TryResolveOpeningStationAndWidthFromPolylineChain(
                                pickPoint1,
                                pickPoint2,
                                vertices,
                                out stationMm,
                                out projectedWidthMm);
                        }
                    }
                    tr.Commit();
                    return resolved;
                }
            }
            catch
            {
                return false;
            }
        }
        private static bool TryResolveOpeningStationAndWidthFromLine(
            Autodesk.AutoCAD.Geometry.Point3d pickPoint1,
            Autodesk.AutoCAD.Geometry.Point3d pickPoint2,
            Autodesk.AutoCAD.Geometry.Point3d lineStart,
            Autodesk.AutoCAD.Geometry.Point3d lineEnd,
            double referenceLengthMm,
            out double stationMm,
            out double projectedWidthMm)
        {
            stationMm = -1;
            projectedWidthMm = 0;
            var vector = lineEnd - lineStart;
            double len = vector.Length;
            if (len <= 1e-6)
                return false;
            double Project(Autodesk.AutoCAD.Geometry.Point3d point)
            {
                double t = (point - lineStart).DotProduct(vector) / (len * len);
                t = Math.Max(0, Math.Min(1, t));
                return t * Math.Max(1.0, referenceLengthMm);
            }
            double s1 = Project(pickPoint1);
            double s2 = Project(pickPoint2);
            stationMm = Math.Max(0, Math.Min(s1, s2));
            projectedWidthMm = Math.Max(0, Math.Abs(s2 - s1));
            return projectedWidthMm > 0.5;
        }
        private static bool TryResolveOpeningStationAndWidthFromPolylineChain(
            Autodesk.AutoCAD.Geometry.Point3d pickPoint1,
            Autodesk.AutoCAD.Geometry.Point3d pickPoint2,
            List<double[]> chainVertices,
            out double stationMm,
            out double projectedWidthMm)
        {
            stationMm = -1;
            projectedWidthMm = 0;
            if (chainVertices == null || chainVertices.Count < 2)
                return false;
            static bool TryProject(
                Autodesk.AutoCAD.Geometry.Point3d point,
                List<double[]> vertices,
                out double station,
                out double distance)
            {
                station = -1;
                distance = double.MaxValue;
                bool found = false;
                double walked = 0;
                for (int i = 0; i + 1 < vertices.Count; i++)
                {
                    var p0 = new Autodesk.AutoCAD.Geometry.Point3d(vertices[i][0], vertices[i][1], 0);
                    var p1 = new Autodesk.AutoCAD.Geometry.Point3d(vertices[i + 1][0], vertices[i + 1][1], 0);
                    var vector = p1 - p0;
                    double segLen = vector.Length;
                    if (segLen <= 1e-6)
                        continue;
                    double t = (point - p0).DotProduct(vector) / (segLen * segLen);
                    t = Math.Max(0, Math.Min(1, t));
                    var projected = p0 + (vector * t);
                    double d = point.DistanceTo(projected);
                    if (d < distance)
                    {
                        distance = d;
                        station = walked + t * segLen;
                        found = true;
                    }
                    walked += segLen;
                }
                return found;
            }
            bool p1Ok = TryProject(pickPoint1, chainVertices, out var s1, out var d1);
            bool p2Ok = TryProject(pickPoint2, chainVertices, out var s2, out var d2);
            if (!p1Ok || !p2Ok || d1 == double.MaxValue || d2 == double.MaxValue)
                return false;
            stationMm = Math.Max(0, Math.Min(s1, s2));
            projectedWidthMm = Math.Max(0, Math.Abs(s2 - s1));
            return projectedWidthMm > 0.5;
        }
        private static bool TryResolveOpeningStationAndWidthFromPolygon(
            Autodesk.AutoCAD.Geometry.Point3d pickPoint1,
            Autodesk.AutoCAD.Geometry.Point3d pickPoint2,
            IReadOnlyList<double[]> polygonVertices,
            double referenceLengthMm,
            bool preferAxisProjection,
            out double stationMm,
            out double projectedWidthMm,
            out double chainRatioStart,
            out double chainRatioEnd)
        {
            stationMm = -1;
            projectedWidthMm = 0;
            chainRatioStart = -1;
            chainRatioEnd = -1;
            if (polygonVertices == null || polygonVertices.Count < 3)
                return false;
            double lengthRef = Math.Max(1.0, referenceLengthMm);
            var polygon = polygonVertices.Select(v => v.ToArray()).ToList();
            bool TryResolveByDevelopedChain(
                out double resolvedStation,
                out double resolvedWidth,
                out double resolvedRatioStart,
                out double resolvedRatioEnd)
            {
                resolvedStation = -1;
                resolvedWidth = 0;
                resolvedRatioStart = -1;
                resolvedRatioEnd = -1;
                if (!TryResolvePolygonDevelopedGeometry(
                        polygon,
                        out var referenceChain,
                        out _,
                        out var chainLength))
                {
                    return false;
                }
                if (!TryProjectPointToPolylineChain(pickPoint1, referenceChain, out var station1, out _)
                    || !TryProjectPointToPolylineChain(pickPoint2, referenceChain, out var station2, out _))
                {
                    return false;
                }
                double scale = lengthRef / Math.Max(1.0, chainLength);
                resolvedStation = Math.Max(0, Math.Min(station1, station2) * scale);
                resolvedWidth = Math.Max(0, Math.Abs(station2 - station1) * scale);
                // Cache chain ratio Ä‘á»ƒ preview dÃ¹ng trá»±c tiáº¿p, trÃ¡nh re-resolve chain direction
                double cl = Math.Max(1.0, chainLength);
                resolvedRatioStart = Math.Max(0, Math.Min(1, Math.Min(station1, station2) / cl));
                resolvedRatioEnd = Math.Max(0, Math.Min(1, Math.Max(station1, station2) / cl));
                return resolvedWidth > 0.5;
            }
            bool TryResolveByAxisProjection(
                out double resolvedStation,
                out double resolvedWidth)
            {
                resolvedStation = -1;
                resolvedWidth = 0;
                double minX = polygonVertices.Min(v => v[0]);
                double maxX = polygonVertices.Max(v => v[0]);
                double minY = polygonVertices.Min(v => v[1]);
                double maxY = polygonVertices.Max(v => v[1]);
                double spanX = Math.Max(1.0, maxX - minX);
                double spanY = Math.Max(1.0, maxY - minY);
                bool stationAlongX = spanX >= spanY;
                double axisMin = stationAlongX ? minX : minY;
                double axisMax = stationAlongX ? maxX : maxY;
                double axisSpan = Math.Max(1.0, axisMax - axisMin);
                double axis1 = stationAlongX ? pickPoint1.X : pickPoint1.Y;
                double axis2 = stationAlongX ? pickPoint2.X : pickPoint2.Y;
                double axisStart = Math.Max(axisMin, Math.Min(axisMax, Math.Min(axis1, axis2)));
                double axisEnd = Math.Max(axisMin, Math.Min(axisMax, Math.Max(axis1, axis2)));
                double axisWidth = Math.Max(0, axisEnd - axisStart);
                if (axisWidth <= 0.5)
                    return false;
                resolvedStation = ((axisStart - axisMin) / axisSpan) * lengthRef;
                resolvedWidth = (axisWidth / axisSpan) * lengthRef;
                return resolvedWidth > 0.5;
            }
            if (TryResolveByDevelopedChain(out var developedStation, out var developedWidth,
                    out var devRatioStart, out var devRatioEnd))
            {
                stationMm = developedStation;
                projectedWidthMm = developedWidth;
                chainRatioStart = devRatioStart;
                chainRatioEnd = devRatioEnd;
                return true;
            }
            if (TryResolveByAxisProjection(out var fallbackStation, out var fallbackWidth))
            {
                stationMm = fallbackStation;
                projectedWidthMm = fallbackWidth;
                // chainRatioStart/End remain -1 for axis projection fallback
                return true;
            }
            return false;
        }
        private static bool TryProjectPointToPolylineChain(
            Autodesk.AutoCAD.Geometry.Point3d point,
            IReadOnlyList<double[]> chain,
            out double stationMm,
            out double distanceMm)
        {
            stationMm = 0;
            distanceMm = double.MaxValue;
            if (chain == null || chain.Count < 2)
                return false;
            bool found = false;
            double walked = 0;
            for (int i = 0; i + 1 < chain.Count; i++)
            {
                var start = chain[i];
                var end = chain[i + 1];
                double dx = end[0] - start[0];
                double dy = end[1] - start[1];
                double segLen = Math.Sqrt(dx * dx + dy * dy);
                if (segLen <= 1e-6)
                    continue;
                double vx = point.X - start[0];
                double vy = point.Y - start[1];
                double t = (vx * dx + vy * dy) / (segLen * segLen);
                t = Math.Max(0, Math.Min(1, t));
                double px = start[0] + dx * t;
                double py = start[1] + dy * t;
                double dist = Math.Sqrt((point.X - px) * (point.X - px) + (point.Y - py) * (point.Y - py));
                if (dist < distanceMm)
                {
                    distanceMm = dist;
                    stationMm = walked + t * segLen;
                    found = true;
                }
                walked += segLen;
            }
            return found;
        }
        private bool TryResolveOpeningCenterStationFromCad(
            Autodesk.AutoCAD.Geometry.Point3d pickPoint1,
            Autodesk.AutoCAD.Geometry.Point3d pickPoint2,
            IReadOnlyList<TenderHeightSegment>? segments,
            out double stationMm)
        {
            stationMm = -1;
            if (!TryResolveOpeningStationAndWidthFromCad(
                pickPoint1,
                pickPoint2,
                segments,
                out var stationStart,
                out var width))
            {
                return false;
            }
            stationMm = stationStart + width * 0.5;
            return true;
        }
        private bool TryCreatePersistentPickSpanLine(
            Autodesk.AutoCAD.Geometry.Point3d start,
            Autodesk.AutoCAD.Geometry.Point3d end,
            out string cadHandle,
            out Autodesk.AutoCAD.DatabaseServices.ObjectId entityId)
        {
            cadHandle = string.Empty;
            entityId = Autodesk.AutoCAD.DatabaseServices.ObjectId.Null;
            if (start.DistanceTo(end) <= 1.0)
                return false;
            try
            {
                var doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
                if (doc == null)
                    return false;
                using (doc.LockDocument())
                using (var tr = doc.Database.TransactionManager.StartTransaction())
                {
                    var layerId = EnsureHighlightLayer(doc.Database, tr);
                    var bt = (Autodesk.AutoCAD.DatabaseServices.BlockTable)tr.GetObject(
                        doc.Database.BlockTableId, Autodesk.AutoCAD.DatabaseServices.OpenMode.ForRead);
                    var btr = (Autodesk.AutoCAD.DatabaseServices.BlockTableRecord)tr.GetObject(
                        bt[Autodesk.AutoCAD.DatabaseServices.BlockTableRecord.ModelSpace],
                        Autodesk.AutoCAD.DatabaseServices.OpenMode.ForWrite);
                    var line = new Autodesk.AutoCAD.DatabaseServices.Line(start, end)
                    {
                        LayerId = layerId,
                        ColorIndex = PanelPreviewColorIndex,
                        LineWeight = Autodesk.AutoCAD.DatabaseServices.LineWeight.LineWeight030
                    };
                    btr.AppendEntity(line);
                    tr.AddNewlyCreatedDBObject(line, true);
                    entityId = line.ObjectId;
                    cadHandle = line.Handle.ToString();
                    tr.Commit();
                }
                return true;
            }
            catch
            {
                return false;
            }
        }
        private bool TryRebuildPickSpanSplinePreview(
            IEnumerable<HeightSegmentInputRow> rows,
            Autodesk.AutoCAD.DatabaseServices.ObjectId currentSplineId,
            out Autodesk.AutoCAD.DatabaseServices.ObjectId newSplineId)
        {
            newSplineId = Autodesk.AutoCAD.DatabaseServices.ObjectId.Null;
            try
            {
                var doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
                if (doc == null)
                    return false;
                using (doc.LockDocument())
                using (var tr = doc.Database.TransactionManager.StartTransaction())
                {
                    if (!currentSplineId.IsNull && currentSplineId.IsValid && !currentSplineId.IsErased)
                    {
                        var oldSpline = tr.GetObject(currentSplineId, Autodesk.AutoCAD.DatabaseServices.OpenMode.ForWrite, false);
                        oldSpline?.Erase();
                    }
                    var orderedPoints = new List<Autodesk.AutoCAD.Geometry.Point3d>();
                    foreach (var row in rows ?? Enumerable.Empty<HeightSegmentInputRow>())
                    {
                        if (row == null || string.IsNullOrWhiteSpace(row.CadHandle))
                            continue;
                        if (!long.TryParse(row.CadHandle, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out var rawHandle))
                            continue;
                        var handle = new Autodesk.AutoCAD.DatabaseServices.Handle(rawHandle);
                        if (!doc.Database.TryGetObjectId(handle, out var objId))
                            continue;
                        var line = tr.GetObject(objId, Autodesk.AutoCAD.DatabaseServices.OpenMode.ForRead, false)
                            as Autodesk.AutoCAD.DatabaseServices.Line;
                        if (line == null || line.Length <= 1.0)
                            continue;
                        var start = line.StartPoint;
                        var end = line.EndPoint;
                        if (orderedPoints.Count > 0)
                        {
                            var last = orderedPoints[^1];
                            if (last.DistanceTo(end) < last.DistanceTo(start))
                            {
                                var swap = start;
                                start = end;
                                end = swap;
                            }
                        }
                        if (orderedPoints.Count == 0 || orderedPoints[^1].DistanceTo(start) > 1.0)
                            orderedPoints.Add(start);
                        if (orderedPoints.Count == 0 || orderedPoints[^1].DistanceTo(end) > 1.0)
                            orderedPoints.Add(end);
                    }
                    if (orderedPoints.Count >= 3)
                    {
                        var layerId = EnsureHighlightLayer(doc.Database, tr);
                        var bt = (Autodesk.AutoCAD.DatabaseServices.BlockTable)tr.GetObject(
                            doc.Database.BlockTableId, Autodesk.AutoCAD.DatabaseServices.OpenMode.ForRead);
                        var btr = (Autodesk.AutoCAD.DatabaseServices.BlockTableRecord)tr.GetObject(
                            bt[Autodesk.AutoCAD.DatabaseServices.BlockTableRecord.ModelSpace],
                            Autodesk.AutoCAD.DatabaseServices.OpenMode.ForWrite);
                        var fitPoints = new Autodesk.AutoCAD.Geometry.Point3dCollection();
                        foreach (var pt in orderedPoints)
                            fitPoints.Add(pt);
                        var spline = new Autodesk.AutoCAD.DatabaseServices.Spline(fitPoints, 3, 0.0)
                        {
                            LayerId = layerId,
                            ColorIndex = 5,
                            LineWeight = Autodesk.AutoCAD.DatabaseServices.LineWeight.LineWeight015
                        };
                        btr.AppendEntity(spline);
                        tr.AddNewlyCreatedDBObject(spline, true);
                        newSplineId = spline.ObjectId;
                    }
                    tr.Commit();
                }
                return true;
            }
            catch
            {
                return false;
            }
        }
        private void TryEraseCadEntities(IEnumerable<Autodesk.AutoCAD.DatabaseServices.ObjectId> entityIds)
        {
            var ids = (entityIds ?? Enumerable.Empty<Autodesk.AutoCAD.DatabaseServices.ObjectId>())
                .Where(id => !id.IsNull)
                .Distinct()
                .ToList();
            if (ids.Count == 0)
                return;
            try
            {
                var doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
                if (doc == null)
                    return;
                using (doc.LockDocument())
                using (var tr = doc.Database.TransactionManager.StartTransaction())
                {
                    foreach (var id in ids)
                    {
                        if (!id.IsValid || id.IsErased)
                            continue;
                        var dbObj = tr.GetObject(id, Autodesk.AutoCAD.DatabaseServices.OpenMode.ForWrite, false);
                        dbObj?.Erase();
                    }
                    tr.Commit();
                }
            }
            catch
            {
                // KhÃ´ng cháº·n luá»“ng popup khi xÃ³a line táº¡m lá»—i.
            }
        }
        private void TryEraseCadEntitiesByHandles(IEnumerable<string> cadHandles)
        {
            var handles = (cadHandles ?? Enumerable.Empty<string>())
                .Where(h => !string.IsNullOrWhiteSpace(h))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (handles.Count == 0)
                return;
            try
            {
                var doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
                if (doc == null)
                    return;
                var ids = new List<Autodesk.AutoCAD.DatabaseServices.ObjectId>();
                foreach (string cadHandle in handles)
                {
                    if (!long.TryParse(cadHandle, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out var rawHandle))
                        continue;
                    var handle = new Autodesk.AutoCAD.DatabaseServices.Handle(rawHandle);
                    if (doc.Database.TryGetObjectId(handle, out var objId))
                        ids.Add(objId);
                }
                TryEraseCadEntities(ids);
            }
            catch
            {
                // KhÃ´ng cháº·n luá»“ng repick náº¿u xÃ³a line cÅ© tháº¥t báº¡i.
            }
        }
        private void CleanupDraftCadHandles(DraftGeometrySession result)
        {
            if (result == null || result.DraftCadHandles.Count == 0)
                return;
            TryEraseCadEntitiesByHandles(result.DraftCadHandles);
        }
        private void DeleteTenderCadArtifacts(TenderWallRow row)
        {
            if (row == null)
                return;
            var handles = new List<string>();
            if (row.AppliedEntityHandles != null)
                handles.AddRange(row.AppliedEntityHandles);
            if (!string.IsNullOrWhiteSpace(row.CadHandle))
                handles.Add(row.CadHandle!);
            handles.AddRange((row.HeightSegments ?? new List<TenderHeightSegment>())
                .Where(s => !string.IsNullOrWhiteSpace(s.CadHandle))
                .Select(s => s.CadHandle!));
            TryEraseCadEntitiesByHandles(handles);
            row.AppliedEntityHandles = new List<string>();
            row.AppliedGroupId = null;
            row.CadHandle = null;
        }
        private void ApplyPopupResultToRow(TenderWallRow row, DraftGeometrySession result)
        {
            row.DraftGeometryMode = result.Mode.ToString();
            row.LayoutDirection = result.LayoutDirection;
            row.Openings = CloneOpenings(result.Openings);
            row.PolygonVertices = result.PolygonVertices?.Select(v => v.ToArray()).ToList();
            row.SuspensionLayoutDirection = result.SuspensionLayoutDirection;
            row.ColdStorageDivideFromMaxSide = result.ColdStorageDivideFromMaxSide;
            if (result.Mode == TenderPopupGeometryMode.WallLineChain)
            {
                row.HeightSegments = result.Segments.Select(s => new TenderHeightSegment
                {
                    LengthMm = s.LengthMm,
                    HeightMm = s.HeightMm,
                    CadHandle = s.CadHandle
                }).ToList();
                row.Length = Math.Max(0, row.HeightSegments.Sum(s => Math.Max(0, s.LengthMm)));
                row.Height = result.RepresentativeHeightMm > 0
                    ? result.RepresentativeHeightMm
                    : row.HeightSegments.Max(s => Math.Max(0, s.HeightMm));
                row.PolygonVertices = null;
                return;
            }
            if (result.PolygonVertices != null && result.PolygonVertices.Count >= 3)
            {
                double minX = result.PolygonVertices.Min(v => v[0]);
                double maxX = result.PolygonVertices.Max(v => v[0]);
                double minY = result.PolygonVertices.Min(v => v[1]);
                double maxY = result.PolygonVertices.Max(v => v[1]);
                row.Length = Math.Max(0, maxX - minX);
                row.Height = Math.Max(0, maxY - minY);
                row.HeightSegments = new List<TenderHeightSegment>();
            }
        }
        private void RefreshWallGridViewAfterPopupApply()
        {
            try
            {
                if (_wallGrid?.ItemsSource == null)
                    return;
                CollectionViewSource.GetDefaultView(_wallGrid.ItemsSource)?.Refresh();
            }
            catch (Exception ex)
            {
                PluginLogger.Warn($"TenderApply.GridRefreshSkipped | {ex.Message}");
            }
        }
        private bool TryPromptAppliedGeometryPlacementPoint(
            TenderPopupGeometryMode mode,
            out Autodesk.AutoCAD.Geometry.Point3d placementPoint)
        {
            placementPoint = default;
            var doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            if (doc == null)
                return false;
            string message = mode == TenderPopupGeometryMode.CeilingPolygon
                ? "\nCh\u1ecdn \u0111i\u1ec3m \u0111\u1eb7t h\u00ecnh tr\u1ea7n:"
                : "\nCh\u1ecdn g\u00f3c tr\u00e1i d\u01b0\u1edbi \u0111\u1ec3 \u0111\u1eb7t m\u1eb7t \u0111\u1ee9ng:";
            var res = doc.Editor.GetPoint(new Autodesk.AutoCAD.EditorInput.PromptPointOptions(message));
            if (res.Status != Autodesk.AutoCAD.EditorInput.PromptStatus.OK)
                return false;
            placementPoint = res.Value;
            return true;
        }
        private bool TryApplyTenderPopupResult(
            TenderWallRow targetRow,
            DraftGeometrySession result,
            bool isRepick)
        {
            Autodesk.AutoCAD.Geometry.Point3d placementPoint;
            if (isRepick && targetRow.AppliedPlacementX.HasValue && targetRow.AppliedPlacementY.HasValue)
            {
                var keep = UiFeedback.AskYesNoCancel("Gi\u1eef v\u1ecb tr\u00ed d\u1ef1ng CAD c\u0169?", "Pick l\u1ea1i Tender");
                if (keep == MessageBoxResult.Cancel)
                    return false;
                if (keep == MessageBoxResult.Yes)
                {
                    placementPoint = new Autodesk.AutoCAD.Geometry.Point3d(
                        targetRow.AppliedPlacementX.Value,
                        targetRow.AppliedPlacementY.Value,
                        targetRow.AppliedPlacementZ ?? 0);
                }
                else if (!TryPromptAppliedGeometryPlacementPoint(result.Mode, out placementPoint))
                {
                    return false;
                }
            }
            else if (!TryPromptAppliedGeometryPlacementPoint(result.Mode, out placementPoint))
            {
                return false;
            }
            var oldHandles = (targetRow.AppliedEntityHandles ?? new List<string>())
                .Where(h => !string.IsNullOrWhiteSpace(h))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            var drawRow = targetRow.Clone();
            ApplyPopupResultToRow(drawRow, result);
            EnsurePopupReferenceGeometry(result, drawRow);
            PluginLogger.Info(
                $"TenderApply.Start | row={targetRow.Name} | mode={result.Mode} | " +
                $"segments={DescribeSegments(drawRow.HeightSegments)} | openings={DescribeOpenings(drawRow.Openings)} | " +
                $"poly={(drawRow.PolygonVertices?.Count ?? 0)} | panelWidth={drawRow.PanelWidth}");
            if (!TryDrawAppliedTenderGeometry(drawRow, result, placementPoint, out var appliedHandles, out var primaryHandle))
            {
                PluginLogger.Warn(
                    $"TenderApply.DrawFailed | row={targetRow.Name} | mode={result.Mode} | " +
                    $"segments={DescribeSegments(drawRow.HeightSegments)} | openings={DescribeOpenings(drawRow.Openings)}");
                return false;
            }
            TryDrawElevationLinkLineToCad(drawRow, placementPoint, appliedHandles);
            var handlesToGroup = new List<string>(appliedHandles);
            if (!string.IsNullOrWhiteSpace(drawRow.CadHandle)) handlesToGroup.Add(drawRow.CadHandle);
            if (drawRow.HeightSegments != null)
            {
                handlesToGroup.AddRange(drawRow.HeightSegments.Select(s => s.CadHandle).Where(h => !string.IsNullOrWhiteSpace(h)).Select(h => h!));
            }
            TryGroupEntities(handlesToGroup);

            var newHandleSet = new HashSet<string>(appliedHandles, StringComparer.OrdinalIgnoreCase);
            TryEraseCadEntitiesByHandles(oldHandles.Where(h => !newHandleSet.Contains(h)));
            // Đồng bộ hình học popup -> row chính thức ngay tại điểm apply thành công.
            ApplyPopupResultToRow(targetRow, result);
            targetRow.AppliedEntityHandles = appliedHandles;
            targetRow.AppliedGroupId = Guid.NewGuid().ToString("N");
            targetRow.AppliedPlacementX = placementPoint.X;
            targetRow.AppliedPlacementY = placementPoint.Y;
            targetRow.AppliedPlacementZ = placementPoint.Z;
            if (string.IsNullOrWhiteSpace(targetRow.CadHandle))
            {
                targetRow.CadHandle = string.IsNullOrWhiteSpace(primaryHandle)
                    ? appliedHandles.FirstOrDefault()
                    : primaryHandle;
            }
            PluginLogger.Info(
                $"TenderApply.Done | row={targetRow.Name} | primary={targetRow.CadHandle} | " +
                $"handles={appliedHandles.Count} | length={targetRow.Length:F0} | height={targetRow.Height:F0} | " +
                $"panels={targetRow.EstimatedPanelCountDisplay}");
            return true;
        }
        private void EnsurePopupReferenceGeometry(DraftGeometrySession result, TenderWallRow row)
        {
            if (result == null || result.ReferenceGeometry != null)
                return;
            if (result.Mode == TenderPopupGeometryMode.WallLineChain)
            {
                var segments = (result.Segments != null && result.Segments.Count > 0)
                    ? result.Segments
                    : row.HeightSegments;
                double length = Math.Max(0, segments.Sum(s => Math.Max(0, s.LengthMm)));
                double height = segments.Count > 0 ? segments.Max(s => Math.Max(0, s.HeightMm)) : Math.Max(0, row.Height);
                var developedChain = new List<double[]> { new[] { 0.0, 0.0 } };
                double cursor = 0;
                foreach (var segment in segments)
                {
                    cursor += Math.Max(0, segment.LengthMm);
                    developedChain.Add(new[] { cursor, 0.0 });
                }
                result.ReferenceGeometry = new ReferenceGeometry
                {
                    GeometryMode = TenderPopupGeometryMode.WallLineChain,
                    BoundaryVertices = BuildStepBoundaryFromSegments(segments),
                    DevelopedChainVertices = developedChain,
                    ReferenceLengthMm = length,
                    ReferenceHeightMm = height,
                    Origin = new[] { 0.0, 0.0 },
                    UAxis = new[] { 1.0, 0.0 },
                    VAxis = new[] { 0.0, 1.0 },
                    IsRectangularLike = true
                };
                return;
            }
        }
        private static List<double[]> BuildStepBoundaryFromSegments(IReadOnlyList<TenderHeightSegment> segments)
        {
            var normalized = (segments ?? Array.Empty<TenderHeightSegment>())
                .Where(s => s != null && s.LengthMm > 0 && s.HeightMm > 0)
                .ToList();
            if (normalized.Count == 0)
                return new List<double[]>();
            double total = normalized.Sum(s => Math.Max(0, s.LengthMm));
            var bottom = new List<double[]> { new[] { 0.0, 0.0 }, new[] { total, 0.0 } };
            var top = new List<double[]>();
            double cursor = 0;
            foreach (var segment in normalized)
            {
                top.Add(new[] { cursor, segment.HeightMm });
                cursor += segment.LengthMm;
                top.Add(new[] { cursor, segment.HeightMm });
            }
            return bottom.Concat(top.AsEnumerable().Reverse()).ToList();
        }
        private bool TryDrawAppliedTenderGeometry(
            TenderWallRow row,
            DraftGeometrySession result,
            Autodesk.AutoCAD.Geometry.Point3d origin,
            out List<string> appliedHandles,
            out string primaryHandle)
        {
            appliedHandles = new List<string>();
            primaryHandle = string.Empty;
            var localAppliedHandles = new List<string>();
            var localPrimaryHandle = string.Empty;
            Autodesk.AutoCAD.Geometry.Point3d? lineChainAnchorPoint = null;
            Autodesk.AutoCAD.Geometry.Point3d? lineChainTextPoint = null;
            try
            {
                var doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
                if (doc == null)
                    return false;
                using (doc.LockDocument())
                using (var tr = doc.Database.TransactionManager.StartTransaction())
                {
                    var layerId = EnsureHighlightLayer(doc.Database, tr);
                    var bt = (Autodesk.AutoCAD.DatabaseServices.BlockTable)tr.GetObject(doc.Database.BlockTableId, Autodesk.AutoCAD.DatabaseServices.OpenMode.ForRead);
                    var btr = (Autodesk.AutoCAD.DatabaseServices.BlockTableRecord)tr.GetObject(bt[Autodesk.AutoCAD.DatabaseServices.BlockTableRecord.ModelSpace], Autodesk.AutoCAD.DatabaseServices.OpenMode.ForWrite);
                    Autodesk.AutoCAD.Geometry.Point3d Map(double[] p) => new(origin.X + p[0], origin.Y + p[1], origin.Z);
                    void Register(Autodesk.AutoCAD.DatabaseServices.Entity entity, bool primary = false)
                    {
                        entity.LayerId = layerId;
                        btr.AppendEntity(entity);
                        tr.AddNewlyCreatedDBObject(entity, true);
                        string handle = entity.Handle.ToString();
                        localAppliedHandles.Add(handle);
                        if (primary || string.IsNullOrWhiteSpace(localPrimaryHandle))
                            localPrimaryHandle = handle;
                    }
                    void AddLine(double[] a, double[] b, short color, Autodesk.AutoCAD.DatabaseServices.LineWeight weight)
                    {
                        Register(new Autodesk.AutoCAD.DatabaseServices.Line(Map(a), Map(b))
                        {
                            ColorIndex = color,
                            LineWeight = weight
                        });
                    }
                    void AddClosedPolyline(List<double[]> vertices, short color, Autodesk.AutoCAD.DatabaseServices.LineWeight weight, bool primary = false)
                    {
                        var pl = new Autodesk.AutoCAD.DatabaseServices.Polyline();
                        for (int i = 0; i < vertices.Count; i++)
                            pl.AddVertexAt(i, new Autodesk.AutoCAD.Geometry.Point2d(origin.X + vertices[i][0], origin.Y + vertices[i][1]), 0, 0, 0);
                        pl.Closed = true;
                        pl.ColorIndex = color;
                        pl.LineWeight = weight;
                        Register(pl, primary);
                    }
                    void AddText(double[] p, string text, short color, double height)
                    {
                        var dbText = new Autodesk.AutoCAD.DatabaseServices.DBText
                        {
                            Position = Map(p),
                            TextString = text,
                            Height = height,
                            ColorIndex = color,
                            TextStyleId = BlockManager.EnsureArialStyle(btr.Database, tr)
                        };
                        Register(dbText);
                    }
                    void AddWorldText(Autodesk.AutoCAD.Geometry.Point3d p, string text, short color, double height)
                    {
                        var dbText = new Autodesk.AutoCAD.DatabaseServices.DBText
                        {
                            Position = p,
                            TextString = text,
                            Height = height,
                            ColorIndex = color,
                            TextStyleId = BlockManager.EnsureArialStyle(btr.Database, tr)
                        };
                        Register(dbText);
                    }
                    void AddLeader(Autodesk.AutoCAD.Geometry.Point3d from, Autodesk.AutoCAD.Geometry.Point3d to, short color)
                    {
                        var leader = new Autodesk.AutoCAD.DatabaseServices.Leader
                        {
                            ColorIndex = color,
                            HasArrowHead = true,
                            TextStyleId = BlockManager.EnsureArialStyle(btr.Database, tr)
                        };
                        leader.AppendVertex(from);
                        leader.AppendVertex(to);
                        Register(leader);
                    }
                    List<double[]> localVertices;
                    double globalOffsetX = 0;
                    double globalOffsetY = 0;
                    bool stationOpeningMode = result.Mode != TenderPopupGeometryMode.CeilingPolygon;
                    if (result.Mode == TenderPopupGeometryMode.WallLineChain)
                    {
                        localVertices = result.ReferenceGeometry?.BoundaryVertices?.Count >= 3
                            ? result.ReferenceGeometry.BoundaryVertices.Select(v => v.ToArray()).ToList()
                            : BuildStepBoundaryFromSegments(row.HeightSegments);
                        foreach (var handle in result.Segments
                                     .Where(s => !string.IsNullOrWhiteSpace(s.CadHandle))
                                     .Select(s => s.CadHandle!)
                                     .Distinct(StringComparer.OrdinalIgnoreCase))
                        {
                            localAppliedHandles.Add(handle);
                            if (lineChainAnchorPoint.HasValue)
                                continue;
                            if (!long.TryParse(handle, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out var rawHandle))
                                continue;
                            var cadHandle = new Autodesk.AutoCAD.DatabaseServices.Handle(rawHandle);
                            if (!doc.Database.TryGetObjectId(cadHandle, out var objId))
                                continue;
                            if (tr.GetObject(objId, Autodesk.AutoCAD.DatabaseServices.OpenMode.ForRead, false) is not Autodesk.AutoCAD.DatabaseServices.Line line)
                                continue;
                            var mid = new Autodesk.AutoCAD.Geometry.Point3d(
                                (line.StartPoint.X + line.EndPoint.X) * 0.5,
                                (line.StartPoint.Y + line.EndPoint.Y) * 0.5,
                                (line.StartPoint.Z + line.EndPoint.Z) * 0.5);
                            lineChainAnchorPoint = mid;
                            var dir = line.EndPoint - line.StartPoint;
                            lineChainTextPoint = dir.Length > 1.0
                                ? mid + dir.GetNormal().MultiplyBy(180.0)
                                : new Autodesk.AutoCAD.Geometry.Point3d(mid.X, mid.Y + 180.0, mid.Z);
                        }
                    }
                    else if (result.PolygonVertices != null && result.PolygonVertices.Count >= 3)
                    {
                        globalOffsetX = result.PolygonVertices.Min(v => v[0]);
                        globalOffsetY = result.PolygonVertices.Min(v => v[1]);
                        localVertices = result.PolygonVertices
                            .Select(v => new[] { v[0] - globalOffsetX, v[1] - globalOffsetY })
                            .ToList();
                    }
                    else
                    {
                        tr.Commit();
                        return false;
                    }
                    if (localVertices.Count < 3)
                    {
                        tr.Commit();
                        return false;
                    }
                    AddClosedPolyline(localVertices, PreviewBoundaryColorIndex, Autodesk.AutoCAD.DatabaseServices.LineWeight.LineWeight050, primary: true);
                    for (int i = 0; i < localVertices.Count; i++)
                    {
                        var a = localVertices[i];
                        var b = localVertices[(i + 1) % localVertices.Count];
                        if (Math.Abs(a[0] - b[0]) > 0.5 || Math.Abs(a[1] - b[1]) > 0.5)
                            AddLine(a, b, PreviewBoundaryColorIndex, Autodesk.AutoCAD.DatabaseServices.LineWeight.LineWeight050);
                    }
                    bool horizontal = string.Equals(row.LayoutDirection, "Ngang", StringComparison.OrdinalIgnoreCase);
                    double minAxis = horizontal ? localVertices.Min(v => v[1]) : localVertices.Min(v => v[0]);
                    double maxAxis = horizontal ? localVertices.Max(v => v[1]) : localVertices.Max(v => v[0]);
                    double minCrossAxis = horizontal ? localVertices.Min(v => v[0]) : localVertices.Min(v => v[1]);
                    if (row.PanelWidth > 0)
                    {
                        double pos = minAxis + row.PanelWidth;
                        for (; pos < maxAxis - 1.0; pos += row.PanelWidth)
                        {
                            foreach (var segment in GetScanSegments(localVertices, pos, horizontal))
                            {
                                var a = horizontal ? new[] { segment.Start, pos } : new[] { pos, segment.Start };
                                var b = horizontal ? new[] { segment.End, pos } : new[] { pos, segment.End };
                                AddLine(a, b, PanelPreviewColorIndex, Autodesk.AutoCAD.DatabaseServices.LineWeight.LineWeight025);
                            }
                            
                            double center = pos - row.PanelWidth / 2.0;
                            // Thêm text ra CAD text
                            int panelIndex = (int)((pos - minAxis) / row.PanelWidth);
                            double textOffset = (panelIndex % 2 == 0) ? -150.0 : -350.0;
                            if (horizontal)
                            {
                                AddText(new[] { minCrossAxis + textOffset, center }, $"{row.PanelWidth:F0}", PanelPreviewColorIndex, 70);
                            }
                            else
                            {
                                AddText(new[] { center - 30.0, minCrossAxis + textOffset }, $"{row.PanelWidth:F0}", PanelPreviewColorIndex, 70);
                            }
                        }

                        double lastPos = (pos - row.PanelWidth);
                        double lastWidth = maxAxis - lastPos;
                        if (lastWidth > 1)
                        {
                            double center = lastPos + lastWidth / 2.0;
                            int panelIndex = (int)((lastPos - minAxis) / row.PanelWidth) + 1;
                            double textOffset = (panelIndex % 2 == 0) ? -150.0 : -350.0;
                            if (horizontal)
                            {
                                AddText(new[] { minCrossAxis + textOffset, center }, $"{lastWidth:F0}", PanelPreviewColorIndex, 70);
                            }
                            else
                            {
                                AddText(new[] { center - 30.0, minCrossAxis + textOffset }, $"{lastWidth:F0}", PanelPreviewColorIndex, 70);
                            }
                        }
                    }
                    if (stationOpeningMode)
                    {
                        foreach (var opening in row.Openings.Where(o => o.Width > 0 && o.Height > 0))
                        {
                            double left = opening.StationStartMm >= 0 ? opening.StationStartMm : opening.CenterStationMm;
                            if (left < 0)
                                continue;
                            double right = opening.StationEndMm >= left ? opening.StationEndMm : left + opening.Width;
                            double bottom = Math.Max(0, opening.BottomElevationMm);
                            double top = bottom + opening.Height;
                            AddLine(new[] { left, bottom }, new[] { right, bottom }, OpeningPreviewColorIndex, Autodesk.AutoCAD.DatabaseServices.LineWeight.LineWeight030);
                            AddLine(new[] { right, bottom }, new[] { right, top }, OpeningPreviewColorIndex, Autodesk.AutoCAD.DatabaseServices.LineWeight.LineWeight030);
                            AddLine(new[] { right, top }, new[] { left, top }, OpeningPreviewColorIndex, Autodesk.AutoCAD.DatabaseServices.LineWeight.LineWeight030);
                            AddLine(new[] { left, top }, new[] { left, bottom }, OpeningPreviewColorIndex, Autodesk.AutoCAD.DatabaseServices.LineWeight.LineWeight030);
                            AddText(new[] { left, top + 90 }, $"LT {left:F0} | {opening.Width:F0}x{opening.Height:F0} | Đáy {bottom:F0}", OpeningPreviewTextColorIndex, 120);
                        }
                    }
                    else
                    {
                        foreach (var opening in row.Openings.Where(o => o.OpeningPolygon != null && o.OpeningPolygon.Count >= 3))
                        {
                            for (int i = 0; i < opening.OpeningPolygon!.Count; i++)
                            {
                                var a = opening.OpeningPolygon[i];
                                var b = opening.OpeningPolygon[(i + 1) % opening.OpeningPolygon.Count];
                                AddLine(new[] { a[0] - globalOffsetX, a[1] - globalOffsetY }, new[] { b[0] - globalOffsetX, b[1] - globalOffsetY }, OpeningPreviewColorIndex, Autodesk.AutoCAD.DatabaseServices.LineWeight.LineWeight030);
                            }
                            double minO_x = opening.OpeningPolygon.Min(p => p[0]) - globalOffsetX;
                            double maxO_y = opening.OpeningPolygon.Max(p => p[1]) - globalOffsetY;
                            AddText(new[] { minO_x, maxO_y + 90 }, $"{opening.Width:F0}x{opening.Height:F0}", OpeningPreviewTextColorIndex, 120);
                        }
                    }
                    if (result.Mode == TenderPopupGeometryMode.CeilingPolygon && IsSuspendedCeilingRow(row))
                    {
                        var preview = TenderBomCalculator.GetColdStorageCeilingPreviewData(row.ToModel());
                        if (preview.HasValue)
                        {
                            bool runAlongX = IsColdStorageRunAlongX(row);
                            foreach (var pos in BuildSuspensionLinePositions(localVertices, runAlongX, row.ColdStorageDivideFromMaxSide, preview.Value.TSpacingMm, preview.Value.TSpacingMm, preview.Value.TLineCount))
                            {
                                foreach (var segment in GetScanSegments(localVertices, pos, runAlongX))
                                    AddLine(runAlongX ? new[] { segment.Start, pos } : new[] { pos, segment.Start }, runAlongX ? new[] { segment.End, pos } : new[] { pos, segment.End }, SuspensionTColorIndex, Autodesk.AutoCAD.DatabaseServices.LineWeight.LineWeight050);
                            }
                            foreach (var pos in BuildSuspensionLinePositions(localVertices, runAlongX, row.ColdStorageDivideFromMaxSide, preview.Value.TSpacingMm, preview.Value.MushroomOffsetMm, preview.Value.MushroomLineCount))
                            {
                                foreach (var segment in GetScanSegments(localVertices, pos, runAlongX))
                                    AddLine(runAlongX ? new[] { segment.Start, pos } : new[] { pos, segment.Start }, runAlongX ? new[] { segment.End, pos } : new[] { pos, segment.End }, SuspensionMushroomColorIndex, Autodesk.AutoCAD.DatabaseServices.LineWeight.LineWeight035);
                            }
                        }
                    }
                    AddText(new[] { 120.0, localVertices.Max(v => v[1]) + 180.0 }, row.Name, PreviewSummaryTextColorIndex, 160.0);
                    if (result.Mode == TenderPopupGeometryMode.WallLineChain && lineChainAnchorPoint.HasValue)
                    {
                        AddWorldText(
                            lineChainTextPoint ?? new Autodesk.AutoCAD.Geometry.Point3d(lineChainAnchorPoint.Value.X, lineChainAnchorPoint.Value.Y + 180.0, lineChainAnchorPoint.Value.Z),
                            row.Name,
                            PreviewSummaryTextColorIndex,
                            140.0);
                        var facadeAttach = Map(new[] { 120.0, localVertices.Max(v => v[1]) + 120.0 });
                        AddLeader(lineChainAnchorPoint.Value, facadeAttach, PreviewSummaryTextColorIndex);
                    }
                    tr.Commit();
                    appliedHandles = localAppliedHandles.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                    primaryHandle = localPrimaryHandle;
                    return appliedHandles.Count > 0;
                }
            }
            catch (Exception ex)
            {
                PluginLogger.Error("TenderApplyCad.Failed", ex);
                return false;
            }
        }
        private bool SyncHeightSegmentsFromCadLines(TenderWallRow row)
        {
            if (row == null || row.HeightSegments == null || row.HeightSegments.Count == 0)
                return false;
            var linked = row.HeightSegments
                .Where(s => s != null && !string.IsNullOrWhiteSpace(s.CadHandle))
                .ToList();
            if (linked.Count == 0)
                return false;
            bool changed = false;
            try
            {
                var doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
                if (doc == null)
                    return false;
                using (doc.LockDocument())
                using (var tr = doc.Database.TransactionManager.StartTransaction())
                {
                    foreach (var seg in linked)
                    {
                        if (!long.TryParse(seg.CadHandle, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out var rawHandle))
                            continue;
                        var handle = new Autodesk.AutoCAD.DatabaseServices.Handle(rawHandle);
                        if (!doc.Database.TryGetObjectId(handle, out var objId))
                            continue;
                        var line = tr.GetObject(objId, Autodesk.AutoCAD.DatabaseServices.OpenMode.ForRead, false)
                            as Autodesk.AutoCAD.DatabaseServices.Line;
                        if (line == null)
                            continue;
                        double newLength = Math.Round(line.Length);
                        if (newLength <= 0)
                            continue;
                        if (Math.Abs(seg.LengthMm - newLength) > 0.5)
                        {
                            seg.LengthMm = newLength;
                            changed = true;
                        }
                    }
                    tr.Commit();
                }
            }
            catch
            {
                return false;
            }
            if (!changed)
                return false;
            double totalLength = row.HeightSegments.Sum(s => Math.Max(0, s.LengthMm));
            if (totalLength > 0)
            {
                row.Length = totalLength;
                row.Height = row.HeightSegments.Sum(s => Math.Max(0, s.LengthMm) * Math.Max(0, s.HeightMm)) / totalLength;
            }
            row.Refresh();
            return true;
        }
        private void ClearHighlight()

        {
            ClearHighlightCore(ignoreGuards: false);
        }

        private void ForceClearHighlight()
        {
            ClearHighlightCore(ignoreGuards: true);
        }

        private void ClearHighlightCore(bool ignoreGuards)
        {
            if (!ignoreGuards && (_isEditingCell || _suspendCadOperations)) return;
            try
            {
                if (_highlightedSourceEntityIds.Count == 0
                    && _previewEntityIds.Count == 0) return;
                var doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
                if (doc == null)
                {
                    _highlightedSourceEntityIds.Clear();
                    _previewEntityIds.Clear();
                    return;
                }
                using (doc.LockDocument())
                using (var tr = doc.Database.TransactionManager.StartTransaction())
                {
                    foreach (var objId in _highlightedSourceEntityIds)
                    {
                        if (objId != Autodesk.AutoCAD.DatabaseServices.ObjectId.Null
                            && !objId.IsErased
                            && tr.GetObject(objId, Autodesk.AutoCAD.DatabaseServices.OpenMode.ForRead, false) is Autodesk.AutoCAD.DatabaseServices.Entity sourceEnt)
                        {
                            sourceEnt.Unhighlight();
                        }
                    }
                    foreach (var objId in _previewEntityIds)
                    {
                        if (objId != Autodesk.AutoCAD.DatabaseServices.ObjectId.Null && !objId.IsErased)
                        {
                            var dbObj = tr.GetObject(objId, Autodesk.AutoCAD.DatabaseServices.OpenMode.ForWrite, false);
                            dbObj?.Erase();
                        }
                    }
                    tr.Commit();
                }
                _highlightedSourceEntityIds.Clear();
                _previewEntityIds.Clear();
            }
            catch
            {
                _highlightedSourceEntityIds.Clear();
                _previewEntityIds.Clear();
            }
        }

        private void OnCadPreviewTimerTick(object? sender, EventArgs e)
        {
            _cadPreviewTimer.Stop();

            if (_pendingPreviewRow != null)
                ShowCadPreview(_pendingPreviewRow);
        }
        private void OnCadSegmentSyncTimerTick(object? sender, EventArgs e)
        {
            if (_suspendCadOperations || _isEditingCell || _wallGrid == null)
                return;
            if (!(_wallGrid.SelectedItem is TenderWallRow row))
                return;
            if (!SyncHeightSegmentsFromCadLines(row))
                return;
            SafeRefreshWallGrid();
            RefreshFooter();
            RefreshPanelBreakdown(row);
            RequestCadPreview(row, force: true);
        }

        private void RequestCadPreview(TenderWallRow row, bool force = false)
        {
            if (_suspendCadOperations)
                return;

            _pendingPreviewRow = row;
            _cadPreviewTimer.Stop();

            if (force)
            {
                ShowCadPreview(row, true);
                return;
            }

            _cadPreviewTimer.Start();
        }

        private void HighlightEntity(string handleStr)
        {
            if (_isEditingCell || _suspendCadOperations) return;
            try
            {
                ClearHighlight();

                var doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
                if (doc == null) { SetStatus("C\u1ea3nh b\u00e1o: Kh\u00f4ng t\u00ecm th\u1ea5y document"); return; }

                using (doc.LockDocument())
                using (var tr = doc.Database.TransactionManager.StartTransaction())
                {
                    var handle = new Autodesk.AutoCAD.DatabaseServices.Handle(Convert.ToInt64(handleStr, 16));
                    if (!doc.Database.TryGetObjectId(handle, out var objId))
                    {
                SetStatus("C\u1ea3nh b\u00e1o: \u0110\u1ed1i t\u01b0\u1ee3ng kh\u00f4ng t\u1ed3n t\u1ea1i ho\u1eb7c \u0111\u00e3 b\u1ecb thay \u0111\u1ed5i.");
                        tr.Commit(); return;
                    }

                    var ent = tr.GetObject(objId, Autodesk.AutoCAD.DatabaseServices.OpenMode.ForRead)
                              as Autodesk.AutoCAD.DatabaseServices.Entity;
                    if (ent == null) { tr.Commit(); return; }

                    ent.Highlight();
                    if (!_highlightedSourceEntityIds.Contains(objId))
                        _highlightedSourceEntityIds.Add(objId);

                    tr.Commit();
                }
            }
            catch (Exception ex) { SetStatus($"C\u1ea3nh b\u00e1o: Highlight {ex.Message}"); }
        }

        private void ZoomToEntity(string handleStr)
        {
            if (_isEditingCell || _suspendCadOperations) return;
            try
            {
                ClearHighlight();

                var doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
                if (doc == null) { SetStatus("C\u1ea3nh b\u00e1o: Kh\u00f4ng t\u00ecm th\u1ea5y document"); return; }

                using (doc.LockDocument())
                using (var tr = doc.Database.TransactionManager.StartTransaction())
                {
                    var handle = new Autodesk.AutoCAD.DatabaseServices.Handle(Convert.ToInt64(handleStr, 16));
                    if (!doc.Database.TryGetObjectId(handle, out var objId))
                    {
                SetStatus("C\u1ea3nh b\u00e1o: \u0110\u1ed1i t\u01b0\u1ee3ng kh\u00f4ng t\u1ed3n t\u1ea1i ho\u1eb7c \u0111\u00e3 b\u1ecb thay \u0111\u1ed5i.");
                        tr.Commit(); return;
                    }

                    var ent = tr.GetObject(objId, Autodesk.AutoCAD.DatabaseServices.OpenMode.ForRead)
                              as Autodesk.AutoCAD.DatabaseServices.Entity;
                    if (ent == null) { tr.Commit(); return; }

                    ent.Highlight();
                    if (!_highlightedSourceEntityIds.Contains(objId))
                        _highlightedSourceEntityIds.Add(objId);

                    var ext = ent.GeometricExtents;
                    var view = doc.Editor.GetCurrentView();
                    view.CenterPoint = new Autodesk.AutoCAD.Geometry.Point2d(
                        (ext.MinPoint.X + ext.MaxPoint.X) / 2,
                        (ext.MinPoint.Y + ext.MaxPoint.Y) / 2);
                    view.Height = (ext.MaxPoint.Y - ext.MinPoint.Y) * 1.5;
                    view.Width = (ext.MaxPoint.X - ext.MinPoint.X) * 1.5;
                    doc.Editor.SetCurrentView(view);

                    tr.Commit();
                }
            }
            catch (Exception ex) { SetStatus($"C\u1ea3nh b\u00e1o: Highlight {ex.Message}"); }
        }

        private void ShowCadPreview(TenderWallRow row, bool force = false)
        {
            var handles = new List<string>();
            if (row.AppliedEntityHandles != null)
                handles.AddRange(row.AppliedEntityHandles.Where(h => !string.IsNullOrWhiteSpace(h)));
            if (!string.IsNullOrWhiteSpace(row.CadHandle))
                handles.Add(row.CadHandle!);
            handles.AddRange((row.HeightSegments ?? new List<TenderHeightSegment>())
                .Where(s => !string.IsNullOrWhiteSpace(s.CadHandle))
                .Select(s => s.CadHandle!));
            handles = handles
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (handles.Count > 0)
            {
                HighlightCadHandles(handles, focusView: force);
                _lastCadPreviewKey = BuildCadPreviewKey(row);
                SetStatus($"V\u1ecb tr\u00ed: {row.Name}");
                return;
            }
            ClearHighlight();
            _lastCadPreviewKey = null;
            SetStatus($"D\u00f2ng {row.Name} ch\u01b0a c\u00f3 h\u00ecnh CAD \u0111\u00e3 d\u1ef1ng.");
        }
        private void HighlightCadHandles(IEnumerable<string> handles, bool focusView)
        {
            if (_isEditingCell || _suspendCadOperations)
                return;
            ClearHighlight();
            var handleList = (handles ?? Enumerable.Empty<string>())
                .Where(h => !string.IsNullOrWhiteSpace(h))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (handleList.Count == 0)
                return;
            try
            {
                var doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
                if (doc == null)
                    return;
                Autodesk.AutoCAD.DatabaseServices.Extents3d? extents = null;
                using (doc.LockDocument())
                using (var tr = doc.Database.TransactionManager.StartTransaction())
                {
                    foreach (string handleStr in handleList)
                    {
                        if (!long.TryParse(handleStr, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out var rawHandle))
                            continue;
                        var handle = new Autodesk.AutoCAD.DatabaseServices.Handle(rawHandle);
                        if (!doc.Database.TryGetObjectId(handle, out var objId))
                            continue;
                        if (tr.GetObject(objId, Autodesk.AutoCAD.DatabaseServices.OpenMode.ForRead, false) is not Autodesk.AutoCAD.DatabaseServices.Entity ent)
                            continue;
                        ent.Highlight();
                        _highlightedSourceEntityIds.Add(objId);
                        try
                        {
                            var currentExt = ent.GeometricExtents;
                            if (!extents.HasValue)
                            {
                                extents = currentExt;
                            }
                            else
                            {
                                extents = new Autodesk.AutoCAD.DatabaseServices.Extents3d(
                                    new Autodesk.AutoCAD.Geometry.Point3d(
                                        Math.Min(extents.Value.MinPoint.X, currentExt.MinPoint.X),
                                        Math.Min(extents.Value.MinPoint.Y, currentExt.MinPoint.Y),
                                        Math.Min(extents.Value.MinPoint.Z, currentExt.MinPoint.Z)),
                                    new Autodesk.AutoCAD.Geometry.Point3d(
                                        Math.Max(extents.Value.MaxPoint.X, currentExt.MaxPoint.X),
                                        Math.Max(extents.Value.MaxPoint.Y, currentExt.MaxPoint.Y),
                                        Math.Max(extents.Value.MaxPoint.Z, currentExt.MaxPoint.Z)));
                            }
                        }
                        catch
                        {
                        }
                    }
                    tr.Commit();
                }
                if (focusView && extents.HasValue)
                {
                    var view = doc.Editor.GetCurrentView();
                    view.CenterPoint = new Autodesk.AutoCAD.Geometry.Point2d(
                        (extents.Value.MinPoint.X + extents.Value.MaxPoint.X) * 0.5,
                        (extents.Value.MinPoint.Y + extents.Value.MaxPoint.Y) * 0.5);
                    view.Width = Math.Max(1000, (extents.Value.MaxPoint.X - extents.Value.MinPoint.X) * 1.2);
                    view.Height = Math.Max(1000, (extents.Value.MaxPoint.Y - extents.Value.MinPoint.Y) * 1.2);
                    doc.Editor.SetCurrentView(view);
                }
            }
            catch (Exception ex)
            {
                SetStatus($"C\u1ea3nh b\u00e1o: Highlight {ex.Message}");
            }
        }

        private static string BuildCadPreviewKey(TenderWallRow row)
        {
            string handle = row.CadHandle ?? "";
            string length = row.Length.ToString("F0");
            string height = row.Height.ToString("F0");
            string heightSegments = row.HeightSegmentsInput ?? string.Empty;
            string drop = row.CableDropLengthMm.ToString("F0");

            return string.Join("|",
                UiText.Normalize(row.Category),
                UiText.Normalize(row.Application),
                row.Name,
                handle,
                row.LayoutDirection,
                row.SuspensionLayoutDirection,
                row.PanelWidth,
                row.PanelThickness,
                row.ColdStorageDivideFromMaxSide,
                row.TopEdgeExposed,
                row.BottomEdgeExposed,
                row.StartEdgeExposed,
                row.EndEdgeExposed,
                row.TopPanelTreatment,
                row.EndPanelTreatment,
                row.BottomPanelTreatment,
                length,
                height,
                heightSegments,
                drop);
        }

                                private bool TryConfigureSuspendedCeilingDivision(TenderWallRow row)
        {
            string? suspensionLayoutDirection = PromptSuspendedCeilingLayoutDirection(row);
            if (string.IsNullOrWhiteSpace(suspensionLayoutDirection))
                return false;

            row.SuspensionLayoutDirection = suspensionLayoutDirection;

            bool? divideFromMaxSide = PromptColdStorageDivideDirection(row);
            if (!divideFromMaxSide.HasValue)
                return false;

            row.ColdStorageDivideFromMaxSide = divideFromMaxSide.Value;
            row.Refresh();
            return true;
        }

        private string? PromptSuspendedCeilingLayoutDirection(TenderWallRow row)
        {
            string? result = null;

            Dispatcher.Invoke(() =>
            {
                var dlg = new Window
                {
                    Title = "Ch\u1ecdn h\u01b0\u1edbng chia ph\u1ee5 ki\u1ec7n tr\u1ea7n",
                    Width = 480,
                    Height = 250,
                    MinWidth = 480,
                    MinHeight = 250,
                    WindowStartupLocation = WindowStartupLocation.CenterScreen,
                    ResizeMode = ResizeMode.NoResize,
                    Background = new SolidColorBrush(Color.FromRgb(250, 250, 252))
                };

                var root = new StackPanel { Margin = new Thickness(20, 18, 20, 18) };
                root.Children.Add(new TextBlock
                {
                    Text = "Ch\u1ecdn h\u01b0\u1edbng chia ph\u1ee5 ki\u1ec7n v\u00e0 tuy\u1ebfn treo cho v\u00f9ng tr\u1ea7n \u0111ang pick:",
                    TextWrapping = TextWrapping.Wrap,
                    FontSize = 14,
                    FontWeight = FontWeights.SemiBold,
                    Margin = new Thickness(0, 0, 0, 10)
                });

                root.Children.Add(new TextBlock
                {
                    Text = "H\u01b0\u1edbng n\u00e0y \u0111\u1ed9c l\u1eadp v\u1edbi h\u01b0\u1edbng chia t\u1ea5m trong c\u1ed9t H\u01b0\u1edbng. D\u1ecdc/Ngang \u1edf \u0111\u00e2y ch\u1ec9 \u00e1p d\u1ee5ng cho tuy\u1ebfn ph\u1ee5 ki\u1ec7n tr\u1ea7n.",
                    TextWrapping = TextWrapping.Wrap,
                    FontSize = 12,
                    Foreground = FgGray,
                    Margin = new Thickness(0, 0, 0, 18)
                });

                var buttonBar = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Center
                };

                buttonBar.Children.Add(Btn("D\u1ecdc", AccentBlue, Brushes.White, (s, e) =>
                {
                    result = "D\u1ecdc";
                    dlg.Close();
                }, 170));

                buttonBar.Children.Add(Btn("Ngang", AccentOrange, Brushes.White, (s, e) =>
                {
                    result = "Ngang";
                    dlg.Close();
                }, 170));

                root.Children.Add(buttonBar);

                var btnCancel = Btn("H\u1ee7y", BtnGray, Brushes.White, (s, e) =>
                {
                    result = null;
                    dlg.Close();
                }, 120);
                btnCancel.HorizontalAlignment = HorizontalAlignment.Center;
                btnCancel.Margin = new Thickness(0, 14, 0, 0);
                root.Children.Add(btnCancel);

                dlg.Content = root;
                dlg.ShowDialog();
            });

            return result;
        }

        private bool? PromptColdStorageDivideDirection(TenderWallRow row)
        {
            bool runAlongX = IsColdStorageRunAlongX(row);
            bool? result = null;

            Dispatcher.Invoke(() =>
            {
                string primaryLabel = runAlongX ? "T\u1eeb c\u1ea1nh d\u01b0\u1edbi" : "T\u1eeb c\u1ea1nh tr\u00e1i";
                string secondaryLabel = runAlongX ? "T\u1eeb c\u1ea1nh tr\u00ean" : "T\u1eeb c\u1ea1nh ph\u1ea3i";
                string axisText = runAlongX ? "theo b\u1ec1 r\u1ed9ng \u0111\u1ee9ng" : "theo b\u1ec1 r\u1ed9ng ngang";

                var dlg = new Window
                {
                    Title = "Ch\u1ecdn ph\u01b0\u01a1ng chia tuy\u1ebfn treo",
                    Width = 480,
                    Height = 240,
                    MinWidth = 480,
                    MinHeight = 240,
                    WindowStartupLocation = WindowStartupLocation.CenterScreen,
                    ResizeMode = ResizeMode.NoResize,
                    Background = new SolidColorBrush(Color.FromRgb(250, 250, 252))
                };

                var root = new StackPanel { Margin = new Thickness(20, 18, 20, 18) };
                root.Children.Add(new TextBlock
                {
                    Text = $"V\u00f9ng pick s\u1ebd chia tuy\u1ebfn treo {axisText}. Ch\u1ecdn c\u1ea1nh g\u1ed1c \u0111\u1ec3 b\u1eaft \u0111\u1ea7u chia nh\u1ecbp:",
                    TextWrapping = TextWrapping.Wrap,
                    FontSize = 14,
                    FontWeight = FontWeights.SemiBold,
                    Margin = new Thickness(0, 0, 0, 10)
                });

                root.Children.Add(new TextBlock
                {
                    Text = "L\u1ef1a ch\u1ecdn n\u00e0y ch\u1ec9 x\u00e1c \u0111\u1ecbnh c\u1ea1nh b\u1eaft \u0111\u1ea7u chia tuy\u1ebfn treo, kh\u00f4ng thay \u0111\u1ed5i h\u01b0\u1edbng chia t\u1ea5m panel trong b\u1ea3ng.",
                    TextWrapping = TextWrapping.Wrap,
                    FontSize = 12,
                    Foreground = FgGray,
                    Margin = new Thickness(0, 0, 0, 18)
                });

                var buttonBar = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Center
                };

                buttonBar.Children.Add(Btn(primaryLabel, AccentBlue, Brushes.White, (s, e) =>
                {
                    result = false;
                    dlg.Close();
                }, 170));

                buttonBar.Children.Add(Btn(secondaryLabel, AccentOrange, Brushes.White, (s, e) =>
                {
                    result = true;
                    dlg.Close();
                }, 170));

                root.Children.Add(buttonBar);

                var btnCancel = Btn("H\u1ee7y", BtnGray, Brushes.White, (s, e) =>
                {
                    result = null;
                    dlg.Close();
                }, 120);
                btnCancel.HorizontalAlignment = HorizontalAlignment.Center;
                btnCancel.Margin = new Thickness(0, 14, 0, 0);
                root.Children.Add(btnCancel);

                dlg.Content = root;
                dlg.ShowDialog();
            });

            return result;
        }

        private static bool IsSuspendedCeilingRow(TenderWallRow row)
        {
            string category = UiText.Normalize(row.Category);
            string application = UiText.Normalize(row.Application);
            return string.Equals(category, "Tr\u1ea7n", StringComparison.OrdinalIgnoreCase)
                && (string.Equals(application, "Kho l\u1ea1nh", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(application, "Ph\u00f2ng s\u1ea1ch", StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsColdStorageCeilingRow(TenderWallRow row)
        {
            return string.Equals(UiText.Normalize(row.Category), "Tr\u1ea7n", StringComparison.OrdinalIgnoreCase)
                && string.Equals(UiText.Normalize(row.Application), "Kho l\u1ea1nh", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsColdStorageRunAlongX(TenderWallRow row)
        {
            if (!string.IsNullOrWhiteSpace(row.SuspensionLayoutDirection))
                return string.Equals(row.SuspensionLayoutDirection, "Ngang", StringComparison.OrdinalIgnoreCase);

            return !string.Equals(row.LayoutDirection, "Ngang", StringComparison.OrdinalIgnoreCase);
        }

        private static List<double[]> GetPolylineVertices(Autodesk.AutoCAD.DatabaseServices.Polyline pl)
        {
            var vertices = new List<double[]>();
            for (int i = 0; i < pl.NumberOfVertices; i++)
            {
                var pt = pl.GetPoint2dAt(i);
                vertices.Add(new[] { pt.X, pt.Y });
            }

            if (vertices.Count >= 2)
            {
                var first = vertices[0];
                var last = vertices[vertices.Count - 1];
                if (Math.Abs(last[0] - first[0]) < 1e-6 && Math.Abs(last[1] - first[1]) < 1e-6)
                    vertices.RemoveAt(vertices.Count - 1);
            }

            return vertices;
        }

        private static string BuildPreviewLineKey(double[] start, double[] end)
        {
            string pointA = $"{Math.Round(start[0], 3):F3},{Math.Round(start[1], 3):F3}";
            string pointB = $"{Math.Round(end[0], 3):F3},{Math.Round(end[1], 3):F3}";
            return string.CompareOrdinal(pointA, pointB) <= 0
                ? $"{pointA}|{pointB}"
                : $"{pointB}|{pointA}";
        }

        private static List<double[]> BuildOffsetPolylineBoundary(List<double[]> vertices, double offsetDistance)
        {
            var offsetVertices = new List<double[]>();
            if (vertices.Count < 2 || offsetDistance <= 0)
                return offsetVertices;

            var segments = new List<(double[] Start, double[] End, double[] Normal)>();
            for (int i = 0; i + 1 < vertices.Count; i++)
            {
                double[] start = vertices[i];
                double[] end = vertices[i + 1];
                double dx = end[0] - start[0];
                double dy = end[1] - start[1];
                double length = Math.Sqrt(dx * dx + dy * dy);
                if (length <= 1e-6)
                    continue;

                segments.Add((
                    start,
                    end,
                    new[] { -dy / length, dx / length }));
            }

            if (segments.Count == 0)
                return offsetVertices;

            offsetVertices.Add(OffsetPoint(segments[0].Start, segments[0].Normal, offsetDistance));
            for (int i = 0; i + 1 < segments.Count; i++)
            {
                var current = segments[i];
                var next = segments[i + 1];
                var intersection = TryIntersectOffsetSegments(current, next, offsetDistance);
                if (intersection != null)
                {
                    offsetVertices.Add(intersection);
                }
                else
                {
                    offsetVertices.Add(OffsetPoint(current.End, current.Normal, offsetDistance));
                }
            }

            offsetVertices.Add(OffsetPoint(segments[^1].End, segments[^1].Normal, offsetDistance));
            return offsetVertices;
        }

        private static double[] OffsetPoint(double[] point, double[] normal, double distance)
        {
            return new[]
            {
                point[0] + normal[0] * distance,
                point[1] + normal[1] * distance
            };
        }

        private static double GetPolylineChainLength(List<double[]> vertices)
        {
            double length = 0;
            for (int i = 0; i + 1 < vertices.Count; i++)
            {
                double dx = vertices[i + 1][0] - vertices[i][0];
                double dy = vertices[i + 1][1] - vertices[i][1];
                length += Math.Sqrt(dx * dx + dy * dy);
            }

            return length;
        }

        private static double GetPolylineLength(List<double[]> vertices)
        {
            double length = 0;
            for (int i = 0; i + 1 < vertices.Count; i++)
            {
                double dx = vertices[i + 1][0] - vertices[i][0];
                double dy = vertices[i + 1][1] - vertices[i][1];
                length += Math.Sqrt(dx * dx + dy * dy);
            }

            return length;
        }

        private static double[]? GetPointAlongPolyline(List<double[]> vertices, double ratio)
        {
            if (vertices.Count == 0)
                return null;

            ratio = Math.Max(0, Math.Min(1, ratio));
            double totalLength = GetPolylineLength(vertices);
            if (totalLength <= 1e-6)
                return vertices[0].ToArray();

            double targetLength = totalLength * ratio;
            double walked = 0;
            for (int i = 0; i + 1 < vertices.Count; i++)
            {
                double[] start = vertices[i];
                double[] end = vertices[i + 1];
                double dx = end[0] - start[0];
                double dy = end[1] - start[1];
                double segmentLength = Math.Sqrt(dx * dx + dy * dy);
                if (segmentLength <= 1e-6)
                    continue;

                if (walked + segmentLength >= targetLength)
                {
                    double t = (targetLength - walked) / segmentLength;
                    return new[]
                    {
                        start[0] + dx * t,
                        start[1] + dy * t
                    };
                }

                walked += segmentLength;
            }

            return vertices[^1].ToArray();
        }

        private static double[] GetPolylineCentroid(List<double[]> polyline)
        {
            if (polyline.Count == 0)
                return new[] { 0.0, 0.0 };

            double sumX = 0;
            double sumY = 0;
            foreach (double[] point in polyline)
            {
                sumX += point[0];
                sumY += point[1];
            }

            return new[]
            {
                sumX / polyline.Count,
                sumY / polyline.Count
            };
        }

        private static double Cross(double ax, double ay, double bx, double by)
        {
            return ax * by - ay * bx;
        }

    
        private TenderWallRow? PrepareTargetRowForPick()
        {
            TenderWallRow? targetRow = null;
            Dispatcher.Invoke(() =>
            {
                targetRow = _wallGrid.SelectedItem as TenderWallRow;
                if (targetRow != null && targetRow.Length > 0)
                {
                    var res = System.Windows.MessageBox.Show(
                        "Dòng đang chọn đã có hình học/kích thước.\n\nBấm [Yes] để TẠO DÒNG MỚI.\nBấm [No] để CHỌN LẠI (Ghi đè) dòng hiện tại.",
                        "Xác nhận",
                        System.Windows.MessageBoxButton.YesNoCancel,
                        System.Windows.MessageBoxImage.Question);
                    
                    if (res == System.Windows.MessageBoxResult.Cancel) 
                    {
                        targetRow = new TenderWallRow { Index = -1 }; // Cancel marker
                    }
                    else if (res == System.Windows.MessageBoxResult.Yes)
                    {
                        targetRow = null; 
                    }
                }
                
                if (targetRow == null)
                {
                    targetRow = BuildPickTemplateRow();
                    targetRow.Index = _wallRows.Count + 1;
                    targetRow.Name = $"{TenderWall.GetCategoryPrefix(targetRow.Category)}-{_wallRows.Count + 1}";
                    if (targetRow.Height <= 0) targetRow.Height = 3000;
                    
                    _wallRows.Add(targetRow);
                    _wallGrid.SelectedItem = targetRow;
                    ReindexWalls();
                    _wallGrid.ScrollIntoView(targetRow);
                }
            });
            return targetRow;
        }

        private void PickSpanFromCad()
        {
            TenderWallRow? targetRow = PrepareTargetRowForPick();
            if (targetRow != null && targetRow.Index == -1) return; // Cancelled
            if (targetRow == null) return;

            BeginCadInteraction();
            try
            {
                var doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
                if (doc == null) return;
                var ed = doc.Editor;
                
                var segmentRows = new List<TenderHeightSegment>();
                double referenceHeightMm = Math.Max(1, targetRow.Height > 0 ? targetRow.Height : 3000);
                while (true)
                {
                    var p1Opt = new Autodesk.AutoCAD.EditorInput.PromptPointOptions("\nChọn điểm đầu nhịp (Enter để kết thúc):") { AllowNone = true };
                    var p1Res = ed.GetPoint(p1Opt);
                    if (p1Res.Status != Autodesk.AutoCAD.EditorInput.PromptStatus.OK) break;
                    var p2Opt = new Autodesk.AutoCAD.EditorInput.PromptPointOptions("\nChọn điểm cuối nhịp:");
                    p2Opt.UseBasePoint = true;
                    p2Opt.BasePoint = p1Res.Value;
                    var p2Res = ed.GetPoint(p2Opt);
                    if (p2Res.Status != Autodesk.AutoCAD.EditorInput.PromptStatus.OK) break;
                    double lengthMm = Math.Round(p1Res.Value.DistanceTo(p2Res.Value));
                    if (lengthMm <= 0) continue;
                    
                    if (!TryPromptWallHeightInput(referenceHeightMm, out var heightMm) || heightMm <= 0) break;
                    TryCreatePersistentPickSpanLine(p1Res.Value, p2Res.Value, out var handle, out _);
                    
                    segmentRows.Add(new TenderHeightSegment
                    {
                        LengthMm = lengthMm,
                        HeightMm = Math.Round(heightMm),
                        CadHandle = string.IsNullOrWhiteSpace(handle) ? null : handle
                    });
                    // Auto Draw temporary preview inside CAD can be added, but we skip for now.
                }
                if (segmentRows.Any())
                {
                    Dispatcher.Invoke(() =>
                    {
                        targetRow.HeightSegments = segmentRows;
                        targetRow.Length = Math.Max(0, segmentRows.Sum(s => Math.Max(0, s.LengthMm)));
                        targetRow.Height = Math.Max(0, segmentRows.Max(s => Math.Max(0, s.HeightMm)));
                        targetRow.DraftGeometryMode = "WallLineChain";
                        targetRow.PolygonVertices = null;
                        SyncWallRowSpecData(targetRow);
                        targetRow.Refresh();
                        SafeRefreshWallGrid();
                        RefreshFooter();
                        RefreshPanelBreakdown(targetRow);
                        RefreshBomSummary(allowDeferredRetry: false, forceWhenPendingEdits: true);
                        SetStatus($"Đã pick nhịp cho {targetRow.Name}.");
                    });
                }
            }
            catch (Exception ex)
            {
                Dispatcher.BeginInvoke(new Action(() => SetStatus($"Lỗi pick nhịp: {ex.Message}")));
            }
            finally
            {
                EndCadInteraction();
            }
        }
        private void PickAreaFromCad()
        {
            TenderWallRow? targetRow = PrepareTargetRowForPick();
            if (targetRow != null && targetRow.Index == -1) return; // Cancelled
            if (targetRow == null) return;

            BeginCadInteraction();
            try
            {
                var doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
                if (doc == null) return;
                if (TryPickClosedPolygonVertices(out var vertices, out var cadHandle, out _))
                {
                    Dispatcher.Invoke(() =>
                    {
                        targetRow.CadHandle = cadHandle;
                        targetRow.PolygonVertices = vertices.Select(v => v.ToArray()).ToList();
                        targetRow.DraftGeometryMode = "WallPolygon";
                        if (targetRow.PolygonVertices != null && targetRow.PolygonVertices.Count >= 3)
                        {
                            double minX = targetRow.PolygonVertices.Min(v => v[0]);
                            double maxX = targetRow.PolygonVertices.Max(v => v[0]);
                            double minY = targetRow.PolygonVertices.Min(v => v[1]);
                            double maxY = targetRow.PolygonVertices.Max(v => v[1]);
                            targetRow.Length = Math.Max(0, maxX - minX);
                            targetRow.Height = Math.Max(0, maxY - minY);
                            targetRow.HeightSegments = new List<TenderHeightSegment>
                            {
                                new TenderHeightSegment { LengthMm = targetRow.Length, HeightMm = targetRow.Height }
                            };
                        }
                        else
                        {
                            targetRow.HeightSegments = new List<TenderHeightSegment>();
                        }
                        SyncWallRowSpecData(targetRow);
                        targetRow.Refresh();
                        SafeRefreshWallGrid();
                        RefreshFooter();
                        RefreshPanelBreakdown(targetRow);
                        RefreshBomSummary(allowDeferredRetry: false, forceWhenPendingEdits: true);
                        SetStatus($"Đã pick vùng cho {targetRow.Name}.");
                    });
                }
            }
            catch (Exception ex)
            {
                Dispatcher.BeginInvoke(new Action(() => SetStatus($"Lỗi pick vùng: {ex.Message}")));
            }
            finally
            {
                EndCadInteraction();
            }
        }
        private void PickOpeningFromCad()
        {
            TenderWallRow? targetRow = _wallGrid.SelectedItem as TenderWallRow;
            if (targetRow == null)
            {
                SetStatus("Vui lòng chọn vách trước khi pick lỗ mở.");
                return;
            }
            BeginCadInteraction();
            try
            {
                var doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
                if (doc == null) return;
                var ed = doc.Editor;
                while (true)
                {
                    var p1Opt = new Autodesk.AutoCAD.EditorInput.PromptPointOptions("\nChọn điểm đầu chiều rộng lỗ mở (Enter để kết thúc):") { AllowNone = true };
                    var p1Res = ed.GetPoint(p1Opt);
                    if (p1Res.Status != Autodesk.AutoCAD.EditorInput.PromptStatus.OK) break;
                    
                    var p2Opt = new Autodesk.AutoCAD.EditorInput.PromptPointOptions("\nChọn điểm cuối chiều rộng lỗ mở:") { UseBasePoint = true, BasePoint = p1Res.Value };
                    var p2Res = ed.GetPoint(p2Opt);
                    if (p2Res.Status != Autodesk.AutoCAD.EditorInput.PromptStatus.OK) break;
                    
                    double widthMm = Math.Round(p1Res.Value.DistanceTo(p2Res.Value));
                    if (widthMm <= 0) continue;

                    double stationMm = -1;
                    if (TryResolveOpeningStationAndWidthFromWallGeometry(
                        p1Res.Value,
                        p2Res.Value,
                        targetRow,
                        out var detectedStation,
                        out var projectedWidth,
                        out _, out _))
                    {
                        stationMm = Math.Round(detectedStation);
                        if (projectedWidth > 0)
                            widthMm = Math.Round(projectedWidth);
                        ed.WriteMessage($"\nĐịnh vị lỗ mở: L={stationMm:F0} mm | Rộng={widthMm:F0} mm");
                    }

                    var hOpt = new Autodesk.AutoCAD.EditorInput.PromptDistanceOptions("\nNhập hoặc pick ĐIỂM THỨ 3 chiều cao lỗ mở (mm):")
                    {
                        DefaultValue = 2100,
                        AllowNegative = false,
                        AllowZero = false,
                        UseDefaultValue = true,
                        UseBasePoint = false
                    };
                    var hRes = ed.GetDistance(hOpt);
                    if (hRes.Status != Autodesk.AutoCAD.EditorInput.PromptStatus.OK) break;
                    double heightMm = Math.Round(hRes.Value);

                    var bottomOpt = new Autodesk.AutoCAD.EditorInput.PromptDistanceOptions("\nNhập hoặc pick ĐIỂM THỨ 4 khoảng cách đáy (mm):")
                    {
                        DefaultValue = 0,
                        AllowNegative = false,
                        AllowZero = true,
                        UseDefaultValue = true,
                        UseBasePoint = false
                    };
                    var bottomRes = ed.GetDistance(bottomOpt);
                    if (bottomRes.Status != Autodesk.AutoCAD.EditorInput.PromptStatus.OK) break;
                    double bottomElevationMm = Math.Max(0, Math.Round(bottomRes.Value));

                    string typeStr = TenderOpening.ResolveTypeByBottomElevation(bottomElevationMm);
                    
                    double dx = p2Res.Value.X - p1Res.Value.X;
                    double dy = p2Res.Value.Y - p1Res.Value.Y;
                    double len = Math.Sqrt(dx * dx + dy * dy);
                    List<double[]>? openingPoly = null;
                    if (len > 0)
                    {
                        double nx = -dy / len;
                        double ny = dx / len;
                        // Always extrude 'up' in standard XY projection
                        if (ny < 0) { nx = -nx; ny = -ny; }
                        
                        openingPoly = new List<double[]>
                        {
                            new[] { p1Res.Value.X, p1Res.Value.Y },
                            new[] { p2Res.Value.X, p2Res.Value.Y },
                            new[] { p2Res.Value.X + nx * heightMm, p2Res.Value.Y + ny * heightMm },
                            new[] { p1Res.Value.X + nx * heightMm, p1Res.Value.Y + ny * heightMm }
                        };
                    }

                    var newOp = new TenderOpening
                    {
                        Type = typeStr,
                        Width = widthMm,
                        Height = heightMm,
                        BottomElevationMm = bottomElevationMm,
                        StationStartMm = stationMm,
                        StationEndMm = stationMm >= 0 ? stationMm + widthMm : -1,
                        CenterStationMm = stationMm >= 0 ? stationMm + widthMm * 0.5 : stationMm,
                        Quantity = 1,
                        OpeningPolygon = openingPoly
                    };
                    targetRow.Openings = targetRow.Openings ?? new List<TenderOpening>();
                    targetRow.Openings.Add(newOp);
                }
                Dispatcher.Invoke(() =>
                {
                    LoadOpeningsForWall(targetRow);
                    RequestCadPreview(targetRow, force: true);
                    RefreshFooter();
                    RefreshPanelBreakdown(targetRow);
                        RefreshBomSummary(allowDeferredRetry: false, forceWhenPendingEdits: true);
                    SetStatus($"Đã pick lỗ mở.");
                });
            }
            catch (Exception ex)
            {
                Dispatcher.BeginInvoke(new Action(() => SetStatus($"Lỗi pick lỗ mở: {ex.Message}")));
            }
            finally
            {
                EndCadInteraction();
            }
        }
        private void DrawElevationForSelected()
        {
            TenderWallRow? targetRow = _wallGrid.SelectedItem as TenderWallRow;
            if (targetRow == null)
            {
                SetStatus("Chọn vách để vẽ MẶT ĐỨNG.");
                return;
            }
            BeginCadInteraction();
            try
            {
                DraftGeometrySession result = new DraftGeometrySession();
                result.Mode = targetRow.PolygonVertices != null && targetRow.PolygonVertices.Any() 
                    ? (IsSuspendedCeilingRow(targetRow) ? TenderPopupGeometryMode.CeilingPolygon : TenderPopupGeometryMode.WallPolygon) 
                    : TenderPopupGeometryMode.WallLineChain;
                if (!TryPromptAppliedGeometryPlacementPoint(result.Mode, out var placementPoint))
                {
                    return;
                }
                // Set fake result for TryDrawAppliedTenderGeometry requirement
                result.PanelWidthMm = targetRow.PanelWidth;
                result.AppliedGroupId = targetRow.AppliedGroupId;
                if ((result.Mode == TenderPopupGeometryMode.WallPolygon || result.Mode == TenderPopupGeometryMode.CeilingPolygon) && targetRow.PolygonVertices != null)
                {
                    result.PolygonVertices = targetRow.PolygonVertices.ToList();
                }
                else
                {
                    // For walls, we just ignore because the targetRow already has the HeightSegments
                }
                if (!TryDrawAppliedTenderGeometry(targetRow, result, placementPoint, out var appliedHandles, out var primaryHandle))
                {
                    SetStatus("Không thể vẽ MẶT ĐỨNG.");
                    return;
                }
                TryDrawElevationLinkLineToCad(targetRow, placementPoint, appliedHandles);
                
                var handlesToGroup = new List<string>(appliedHandles);
                if (!string.IsNullOrWhiteSpace(targetRow.CadHandle)) handlesToGroup.Add(targetRow.CadHandle);
                if (targetRow.HeightSegments != null)
                {
                    handlesToGroup.AddRange(targetRow.HeightSegments.Select(s => s.CadHandle).Where(h => !string.IsNullOrWhiteSpace(h)).Select(h => h!));
                }
                TryGroupEntities(handlesToGroup);

                Dispatcher.Invoke(() =>
                {
                    targetRow.AppliedEntityHandles = appliedHandles;
                    if (string.IsNullOrWhiteSpace(targetRow.CadHandle))
                    {
                        targetRow.CadHandle = string.IsNullOrWhiteSpace(primaryHandle) ? appliedHandles.FirstOrDefault() : primaryHandle;
                    }
                    targetRow.AppliedPlacementX = placementPoint.X;
                    targetRow.AppliedPlacementY = placementPoint.Y;
                    targetRow.AppliedPlacementZ = placementPoint.Z;
                    
                    targetRow.Refresh();
                    SafeRefreshWallGrid();
                    SetStatus($"Đã vẽ MẶT ĐỨNG cho {targetRow.Name}.");
                });
            }
            catch (Exception ex)
            {
                Dispatcher.BeginInvoke(new Action(() => SetStatus($"Lỗi dựng MĐ: {ex.Message}")));
            }
            finally
            {
                EndCadInteraction();
            }
        }
        
        private void TryDrawElevationLinkLineToCad(TenderWallRow targetRow, Autodesk.AutoCAD.Geometry.Point3d placementPoint, List<string> appliedHandles)
        {
            var doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            Autodesk.AutoCAD.Geometry.Point3d sourcePoint = Autodesk.AutoCAD.Geometry.Point3d.Origin;
            bool hasSource = false;

            try
            {
                if (targetRow.PolygonVertices != null && targetRow.PolygonVertices.Count > 0)
                {
                    double avgX = targetRow.PolygonVertices.Average(v => v[0]);
                    double avgY = targetRow.PolygonVertices.Average(v => v[1]);
                    sourcePoint = new Autodesk.AutoCAD.Geometry.Point3d(avgX, avgY, 0);
                    hasSource = true;
                }
                else if (!string.IsNullOrEmpty(targetRow.CadHandle))
                {
                    using (doc.LockDocument())
                    using (var tr = doc.Database.TransactionManager.StartTransaction())
                    {
                        var handle = new Autodesk.AutoCAD.DatabaseServices.Handle(Convert.ToInt64(targetRow.CadHandle, 16));
                        if (doc.Database.TryGetObjectId(handle, out var objId))
                        {
                            var ent = tr.GetObject(objId, Autodesk.AutoCAD.DatabaseServices.OpenMode.ForRead) as Autodesk.AutoCAD.DatabaseServices.Entity;
                            if (ent != null)
                            {
                                var ext = ent.GeometricExtents;
                                sourcePoint = new Autodesk.AutoCAD.Geometry.Point3d(
                                    (ext.MinPoint.X + ext.MaxPoint.X) / 2,
                                    (ext.MinPoint.Y + ext.MaxPoint.Y) / 2,
                                    0);
                                hasSource = true;
                            }
                        }
                        tr.Commit();
                    }
                }
                else if (targetRow.HeightSegments != null && targetRow.HeightSegments.Any(s => !string.IsNullOrWhiteSpace(s.CadHandle)))
                {
                    using (doc.LockDocument())
                    using (var tr = doc.Database.TransactionManager.StartTransaction())
                    {
                        var minPt = new Autodesk.AutoCAD.Geometry.Point3d(double.MaxValue, double.MaxValue, 0);
                        var maxPt = new Autodesk.AutoCAD.Geometry.Point3d(double.MinValue, double.MinValue, 0);
                        bool validExtents = false;
                        foreach (var seg in targetRow.HeightSegments.Where(x => !string.IsNullOrWhiteSpace(x.CadHandle)))
                        {
                            try
                            {
                                var handle = new Autodesk.AutoCAD.DatabaseServices.Handle(Convert.ToInt64(seg.CadHandle, 16));
                                if (doc.Database.TryGetObjectId(handle, out var objId))
                                {
                                    var ent = tr.GetObject(objId, Autodesk.AutoCAD.DatabaseServices.OpenMode.ForRead) as Autodesk.AutoCAD.DatabaseServices.Entity;
                                    if (ent != null)
                                    {
                                        var ext = ent.GeometricExtents;
                                        minPt = new Autodesk.AutoCAD.Geometry.Point3d(Math.Min(minPt.X, ext.MinPoint.X), Math.Min(minPt.Y, ext.MinPoint.Y), 0);
                                        maxPt = new Autodesk.AutoCAD.Geometry.Point3d(Math.Max(maxPt.X, ext.MaxPoint.X), Math.Max(maxPt.Y, ext.MaxPoint.Y), 0);
                                        validExtents = true;
                                    }
                                }
                            }
                            catch { }
                        }
                        if (validExtents)
                        {
                            sourcePoint = new Autodesk.AutoCAD.Geometry.Point3d(
                                (minPt.X + maxPt.X) / 2,
                                (minPt.Y + maxPt.Y) / 2,
                                0);
                            hasSource = true;
                        }
                        tr.Commit();
                    }
                }

                if (!hasSource) return;

                using (doc.LockDocument())
                using (var tr = doc.Database.TransactionManager.StartTransaction())
                {
                    var btr = (Autodesk.AutoCAD.DatabaseServices.BlockTableRecord)tr.GetObject(doc.Database.CurrentSpaceId, Autodesk.AutoCAD.DatabaseServices.OpenMode.ForWrite);
                    
                    string layerName = "SD_LINK";
                    var lt = (Autodesk.AutoCAD.DatabaseServices.LayerTable)tr.GetObject(doc.Database.LayerTableId, Autodesk.AutoCAD.DatabaseServices.OpenMode.ForRead);
                    if (!lt.Has(layerName))
                    {
                        lt.UpgradeOpen();
                        var ltr = new Autodesk.AutoCAD.DatabaseServices.LayerTableRecord();
                        ltr.Name = layerName;
                        ltr.Color = Autodesk.AutoCAD.Colors.Color.FromColorIndex(Autodesk.AutoCAD.Colors.ColorMethod.ByAci, 8); // Gray
                        lt.Add(ltr);
                        tr.AddNewlyCreatedDBObject(ltr, true);
                    }

                    var line = new Autodesk.AutoCAD.DatabaseServices.Line(sourcePoint, placementPoint);
                    line.Layer = layerName;
                    
                    btr.AppendEntity(line);
                    tr.AddNewlyCreatedDBObject(line, true);
                    
                    appliedHandles.Add(line.Handle.ToString());
                    tr.Commit();
                }
            }
            catch (Exception ex)
            {
                PluginLogger.Warn($"TryDrawElevationLinkLineToCad: {ex.Message}");
            }
        }

        private void TryGroupEntities(IEnumerable<string> handles)
        {
            var doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            if (doc == null || handles == null) return;

            try
            {
                using (doc.LockDocument())
                using (var tr = doc.Database.TransactionManager.StartTransaction())
                {
                    var ids = new Autodesk.AutoCAD.DatabaseServices.ObjectIdCollection();
                    foreach (var hStr in handles)
                    {
                        if (string.IsNullOrWhiteSpace(hStr)) continue;
                        try
                        {
                            var handle = new Autodesk.AutoCAD.DatabaseServices.Handle(Convert.ToInt64(hStr, 16));
                            if (doc.Database.TryGetObjectId(handle, out var objId))
                            {
                                ids.Add(objId);
                            }
                        }
                        catch { }
                    }

                    if (ids.Count > 1)
                    {
                        var dictId = doc.Database.GroupDictionaryId;
                        var dict = (Autodesk.AutoCAD.DatabaseServices.DBDictionary)tr.GetObject(dictId, Autodesk.AutoCAD.DatabaseServices.OpenMode.ForWrite);
                        
                        var group = new Autodesk.AutoCAD.DatabaseServices.Group("Tender Elevation Group", true);
                        dict.SetAt("*", group);
                        tr.AddNewlyCreatedDBObject(group, true);
                        group.Append(ids);
                        
                        try 
                        {
                            object pickStyleObj = Autodesk.AutoCAD.ApplicationServices.Application.GetSystemVariable("PICKSTYLE");
                            if (pickStyleObj is short ps)
                            {
                                if (ps == 0) Autodesk.AutoCAD.ApplicationServices.Application.SetSystemVariable("PICKSTYLE", (short)1);
                                else if (ps == 2) Autodesk.AutoCAD.ApplicationServices.Application.SetSystemVariable("PICKSTYLE", (short)3);
                            }
                        }
                        catch { }
                    }
                    tr.Commit();
                }
            }
            catch (Exception ex)
            {
                PluginLogger.Warn($"TryGroupEntities: {ex.Message}");
            }
        }
    
        private void UpdateLiveCanvasPreview(TenderWallRow targetRow)
        {
            if (_previewCanvas == null || targetRow == null) return;
            
            _previewCanvas.Children.Clear();
            _previewCanvas.Width = 600;
            _previewCanvas.Height = 300;

            try
            {
                string layout = string.Equals(targetRow.LayoutDirection, "Ngang", StringComparison.OrdinalIgnoreCase) ? "Ngang" : "Dọc";
                var mode = targetRow.PolygonVertices != null && targetRow.PolygonVertices.Any() 
                    ? TenderPopupGeometryMode.WallPolygon 
                    : TenderPopupGeometryMode.WallLineChain;

                double referenceLengthMm = Math.Max(0, targetRow.HeightSegments?.Sum(s => Math.Max(0, s.LengthMm)) ?? targetRow.Length);
                double referenceHeightMm = Math.Max(1, targetRow.Height > 0 ? targetRow.Height : 3000);

                var openings = (targetRow.Openings ?? new List<TenderOpening>())
                    .Select(ToTenderOpeningFromObject)
                    .ToList();

                if (mode == TenderPopupGeometryMode.WallLineChain)
                {
                    var segRows = (targetRow.HeightSegments ?? new List<TenderHeightSegment>())
                        .Select(r => new HeightSegmentInputRow { LengthMm = r.LengthMm, HeightMm = r.HeightMm, CadHandle = r.CadHandle });

                    BuildNormalizedSegments(
                        segRows,
                        Math.Max(1, referenceLengthMm),
                        Math.Max(1, referenceHeightMm),
                        out var normalized,
                        out var note,
                        autoFillMissing: false);
                    
                    referenceLengthMm = Math.Max(1, normalized.Sum(s => Math.Max(0, s.LengthMm)));
                    DrawHeightProfilePreview(_previewCanvas, normalized, referenceLengthMm, targetRow.PanelWidth, layout, openings);
                }
                else if (targetRow.PolygonVertices != null)
                {
                    var polyPts = targetRow.PolygonVertices.ToList();
                    var drawOpeningByStation = !IsSuspendedCeilingRow(targetRow);
                    DrawLocalPolygonPreview(_previewCanvas, polyPts, targetRow.PanelWidth, string.Equals(layout, "Ngang", StringComparison.OrdinalIgnoreCase), openings, 0, drawOpeningByStation, targetRow);
                }
            }
            catch (Exception ex)
            {
                PluginLogger.Warn("LiveCanvasPreview Error: " + ex.Message);
            }
        }

        private TenderOpening ToTenderOpeningFromObject(TenderOpening obj)
        {
            return new TenderOpening
            {
                Type = obj.Type,
                Width = obj.Width,
                Height = obj.Height,
                BottomElevationMm = obj.BottomElevationMm,
                CenterStationMm = obj.CenterStationMm,
                StationStartMm = obj.StationStartMm,
                StationEndMm = obj.StationEndMm,
                ResolvedChainRatioStart = obj.ResolvedChainRatioStart,
                ResolvedChainRatioEnd = obj.ResolvedChainRatioEnd,
                Quantity = obj.Quantity
            };
        }

    }
}
