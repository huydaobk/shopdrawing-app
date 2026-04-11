﻿using System.Collections.Generic;

using System.Collections.ObjectModel;

using System.ComponentModel;

using System.Linq;

using System.Windows;

using System.Windows.Controls;

using System.Windows.Data;

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

                SetStatus("Chọn vách hoặc trần cần preview CAD.");

                return;

            }



            RequestCadPreview(row, true);

        }



private void RepickWallFromCad(TenderWallRow targetRow, bool pickArea)

        {

            BeginCadInteraction();

            try

            {

                var doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;

                if (doc == null) return;

                var ed = doc.Editor;

                string prompt = pickArea

                    ? "\nChọn polyline kín để lấy Dài x Cao:"

                    : "\nChọn Đoạn thẳng hoặc Đa tuyến để lấy chiều dài:";



                var opt = new Autodesk.AutoCAD.EditorInput.PromptEntityOptions(prompt);

                opt.SetRejectMessage("\nPhải là Đoạn thẳng hoặc Đa tuyến!");

                opt.AddAllowedClass(typeof(Autodesk.AutoCAD.DatabaseServices.Line), true);

                opt.AddAllowedClass(typeof(Autodesk.AutoCAD.DatabaseServices.Polyline), true);



                var result = ed.GetEntity(opt);

                if (result.Status != Autodesk.AutoCAD.EditorInput.PromptStatus.OK) return;



                string? cadHandle = null;

                double length = targetRow.Length;

                double height = targetRow.Height;

                List<double[]>? polygonVertices = targetRow.PolygonVertices != null

                    ? new List<double[]>(targetRow.PolygonVertices.Select(v => v.ToArray()))

                    : null;



                using (var tr = doc.Database.TransactionManager.StartTransaction())

                {

                    var ent = tr.GetObject(result.ObjectId, Autodesk.AutoCAD.DatabaseServices.OpenMode.ForRead);

                    cadHandle = ent.Handle.ToString();



                    if (ent is Autodesk.AutoCAD.DatabaseServices.Line line)

                    {

                        length = line.Length;

                        if (!pickArea)

                        {

                            height = targetRow.Height;

                            polygonVertices = null;

                        }

                    }

                    else if (ent is Autodesk.AutoCAD.DatabaseServices.Polyline pl)

                    {

                        var vertices = new System.Collections.Generic.List<double[]>();

                        for (int i = 0; i < pl.NumberOfVertices; i++)

                        {

                            var pt = pl.GetPoint2dAt(i);

                            vertices.Add(new[] { pt.X, pt.Y });

                        }

                        if (!pl.Closed)

                        {

                            length = pl.Length;

                            if (!pickArea)

                            {

                                height = targetRow.Height;

                                polygonVertices = null;

                            }

                        }

                        else

                        {

                            double minVx = vertices.Min(v => v[0]);

                            double maxVx = vertices.Max(v => v[0]);

                            double minVy = vertices.Min(v => v[1]);

                            double maxVy = vertices.Max(v => v[1]);

                            length = maxVx - minVx;

                            height = maxVy - minVy;

                            polygonVertices = IsRectangleByVertices(vertices) ? null : vertices;

                        }

                    }

                    tr.Commit();

                }

                List<TenderHeightSegment> heightSegments = new();
                if (!pickArea)

                {
                    if (!TryPromptWallSegmentsInput(
                            totalLengthMm: length,
                            defaultHeightMm: height,
                            initialSegments: targetRow.HeightSegments,
                            initialOpenings: targetRow.Openings,
                            panelWidthMm: targetRow.PanelWidth,
                            layoutDirection: targetRow.LayoutDirection,
                            out var promptedSegments,
                            out var promptedHeight,
                            out var promptedLayoutDirection,
                            out var promptedOpenings))
                    {
                        PluginLogger.Warn($"TenderRepick.PopupCancelled | row={targetRow.Name}");
                        return;
                    }

                    height = promptedHeight;
                    heightSegments = promptedSegments;
                    var promptedLength = promptedSegments.Sum(s => Math.Max(0, s.LengthMm));
                    if (promptedLength > 0.5)
                        length = promptedLength;
                    targetRow.LayoutDirection = promptedLayoutDirection;
                    targetRow.Openings = promptedOpenings;
                    polygonVertices = null;
                    PluginLogger.Info(
                        $"TenderRepick.PopupApplied | row={targetRow.Name} | seg={DescribeSegments(promptedSegments)} | " +
                        $"openings={DescribeOpenings(promptedOpenings)} | layout={promptedLayoutDirection}");

                }



                string suspensionLayoutDirection = targetRow.SuspensionLayoutDirection;

                bool divideFromMaxSide = targetRow.ColdStorageDivideFromMaxSide;

                if (pickArea && IsSuspendedCeilingRow(targetRow))

                {

                    var draftRow = TenderWallRow.FromModel(targetRow.ToModel(), targetRow.Index);

                    draftRow.CadHandle = cadHandle;

                    draftRow.Length = length;

                    draftRow.Height = height;

                    draftRow.PolygonVertices = polygonVertices != null

                        ? new List<double[]>(polygonVertices.Select(v => v.ToArray()))

                        : null;



                    if (!TryConfigureSuspendedCeilingDivision(draftRow))

                    {

                        Dispatcher.BeginInvoke(new Action(() => SetStatus("Hãy chọn lại trần sau khi chọn hướng chia phụ kiện.")));

                        return;

                    }



                    suspensionLayoutDirection = draftRow.SuspensionLayoutDirection;

                    divideFromMaxSide = draftRow.ColdStorageDivideFromMaxSide;

                }



                Dispatcher.BeginInvoke(new Action(() =>

                {

                    targetRow.CadHandle = cadHandle;

                    targetRow.Length = length;

                    targetRow.Height = height;
                    targetRow.HeightSegments = heightSegments;

                    targetRow.PolygonVertices = polygonVertices;

                    targetRow.SuspensionLayoutDirection = suspensionLayoutDirection;

                    targetRow.ColdStorageDivideFromMaxSide = divideFromMaxSide;

                    SyncWallRowSpecData(targetRow);
                    targetRow.Refresh();
                    if (_wallGrid?.SelectedItem is TenderWallRow selectedRow && ReferenceEquals(selectedRow, targetRow))
                    {
                        LoadOpeningsForWall(targetRow);
                    }

                    SafeRefreshWallGrid();

                    RefreshFooter();

                    RefreshPanelBreakdown(targetRow);
                    RefreshBomSummary(allowDeferredRetry: false, forceWhenPendingEdits: true);
                    _project.Walls = GetWallModels();
                    PluginLogger.Info(
                        $"TenderRepick.Synced | row={targetRow.Name} | length={targetRow.Length:F0} | height={targetRow.Height:F0} | " +
                        $"seg={DescribeSegments(targetRow.HeightSegments)} | walls={_wallRows.Count}");

                    _lastCadPreviewKey = null;

                    SetStatus($"Đã chọn lại vách {targetRow.Name}");

                }));

            }

            catch (Exception ex)

            {

                Dispatcher.BeginInvoke(new Action(() => SetStatus($"Lỗi chọn lại: {ex.Message}")));

            }

            finally

            {

                EndCadInteraction();

            }

        }



        private void PickFromCad(bool pickArea)

        {

            BeginCadInteraction();

            try

            {

                var doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;

                if (doc == null) return;

                var ed = doc.Editor;

                if (!pickArea)
                {
                    var template = BuildPickTemplateRow();
                    var selectedRow = _wallGrid?.SelectedItem as TenderWallRow;
                    string category = template.Category;
                    double initialLength = template.HeightSegments?.Sum(s => Math.Max(0, s.LengthMm)) ?? 0;
                    if (initialLength <= 0)
                        initialLength = Math.Max(0, selectedRow?.Length ?? 0);
                    double initialHeight = Math.Max(1, selectedRow?.Height ?? 3000);

                    var popupRow = new TenderWallRow
                    {
                        Index = _wallRows.Count + 1,
                        Category = category,
                        Floor = template.Floor,
                        Name = $"{TenderWall.GetCategoryPrefix(category)}-{_wallRows.Count + 1}",
                        SpecKey = template.SpecKey,
                        PanelWidth = template.PanelWidth,
                        PanelThickness = template.PanelThickness,
                        LayoutDirection = template.LayoutDirection,
                        Application = template.Application,
                        CableDropLengthMm = template.CableDropLengthMm,
                        ColdStorageDivideFromMaxSide = template.ColdStorageDivideFromMaxSide,
                        SuspensionLayoutDirection = template.SuspensionLayoutDirection,
                        TopPanelTreatment = template.TopPanelTreatment,
                        EndPanelTreatment = template.EndPanelTreatment,
                        BottomPanelTreatment = template.BottomPanelTreatment,
                        TopEdgeExposed = template.TopEdgeExposed,
                        BottomEdgeExposed = template.BottomEdgeExposed,
                        StartEdgeExposed = template.StartEdgeExposed,
                        EndEdgeExposed = template.EndEdgeExposed,
                        HeightSegments = template.HeightSegments
                            ?.Select(s => new TenderHeightSegment { LengthMm = s.LengthMm, HeightMm = s.HeightMm, CadHandle = s.CadHandle })
                            .ToList()
                            ?? new List<TenderHeightSegment>(),
                        Openings = new List<TenderOpening>(),
                        Length = initialLength,
                        Height = initialHeight
                    };

                    if (!TryPromptWallSegmentsInput(
                            totalLengthMm: popupRow.Length,
                            defaultHeightMm: popupRow.Height,
                            initialSegments: popupRow.HeightSegments,
                            initialOpenings: popupRow.Openings,
                            panelWidthMm: popupRow.PanelWidth,
                            layoutDirection: popupRow.LayoutDirection,
                            out var promptedSegments,
                            out var promptedHeight,
                            out var promptedLayoutDirection,
                            out var promptedOpenings))
                    {
                        PluginLogger.Warn("TenderPickLength.PopupCancelled");
                        return;
                    }

                    popupRow.Height = promptedHeight;
                    popupRow.HeightSegments = promptedSegments;
                    popupRow.LayoutDirection = promptedLayoutDirection;
                    popupRow.Openings = promptedOpenings;
                    popupRow.Length = Math.Max(0, promptedSegments.Sum(s => Math.Max(0, s.LengthMm)));
                    if (popupRow.Length <= 0)
                        popupRow.Length = Math.Max(0, initialLength);
                    PluginLogger.Info(
                        $"TenderPickLength.PopupApplied | row={popupRow.Name} | category={popupRow.Category} | app={popupRow.Application} | " +
                        $"spec={popupRow.SpecKey} | seg={DescribeSegments(promptedSegments)} | openings={DescribeOpenings(promptedOpenings)}");
                    Dispatcher.Invoke(() =>
                    {
                        SyncWallRowSpecData(popupRow);
                        popupRow.Refresh();

                        _wallRows.Add(popupRow);
                        ReindexWalls();
                        if (_wallGrid != null)
                        {
                            _wallGrid.SelectedItem = popupRow;
                            _wallGrid.ScrollIntoView(popupRow);
                            _wallGrid.Items.Refresh();
                        }
                        SafeRefreshWallGrid();
                        LoadOpeningsForWall(popupRow);
                        RefreshFooter();
                        RefreshPanelBreakdown(popupRow);
                        RefreshBomSummary(allowDeferredRetry: false, forceWhenPendingEdits: true);
                        _project.Walls = GetWallModels();
                        PluginLogger.Info(
                            $"TenderPickLength.Synced | row={popupRow.Name} | length={popupRow.Length:F0} | height={popupRow.Height:F0} | " +
                            $"seg={DescribeSegments(popupRow.HeightSegments)} | walls={_wallRows.Count}");
                        _lastCadPreviewKey = null;
                        SetStatus($"Đã thêm {popupRow.Name} bằng popup Pick Nhịp.");
                    });
                    return;
                }



                string prompt = pickArea

                    ? "\nChọn polyline kín để lấy diện tích (chữ nhật hoặc đa giác):"

                    : "\nChọn Đoạn thẳng hoặc Đa tuyến để lấy chiều dài:";



                var opt = new Autodesk.AutoCAD.EditorInput.PromptEntityOptions(prompt);

                opt.SetRejectMessage("\nPhải là Đoạn thẳng hoặc Đa tuyến!");

                opt.AddAllowedClass(typeof(Autodesk.AutoCAD.DatabaseServices.Line), true);

                opt.AddAllowedClass(typeof(Autodesk.AutoCAD.DatabaseServices.Polyline), true);



                var result = ed.GetEntity(opt);

                if (result.Status != Autodesk.AutoCAD.EditorInput.PromptStatus.OK) return;



                TenderWallRow? newRow = null;

                string polygonTag = string.Empty;



                using (var tr = doc.Database.TransactionManager.StartTransaction())

                {

                    var ent = tr.GetObject(result.ObjectId, Autodesk.AutoCAD.DatabaseServices.OpenMode.ForRead);

                    var template = BuildPickTemplateRow();

                    string category = template.Category;

                    newRow = new TenderWallRow

                    {

                        Index = _wallRows.Count + 1,

                        Category = category,

                        Floor = template.Floor,

                        SpecKey = template.SpecKey,

                        PanelWidth = template.PanelWidth,

                        PanelThickness = template.PanelThickness,

                        LayoutDirection = template.LayoutDirection,

                        Application = template.Application,

                        CableDropLengthMm = template.CableDropLengthMm,

                        ColdStorageDivideFromMaxSide = template.ColdStorageDivideFromMaxSide,

                        SuspensionLayoutDirection = template.SuspensionLayoutDirection,

                        TopPanelTreatment = template.TopPanelTreatment,
                        EndPanelTreatment = template.EndPanelTreatment,
                        BottomPanelTreatment = template.BottomPanelTreatment,

                        TopEdgeExposed = template.TopEdgeExposed,

                        BottomEdgeExposed = template.BottomEdgeExposed,

                        StartEdgeExposed = template.StartEdgeExposed,

                        EndEdgeExposed = template.EndEdgeExposed,

                        CadHandle = ent.Handle.ToString()

                    };



                    if (ent is Autodesk.AutoCAD.DatabaseServices.Line line)

                    {

                        newRow.Length = line.Length;

                        newRow.Name = $"{TenderWall.GetCategoryPrefix(category)}-{_wallRows.Count + 1}";

                    }

                    else if (ent is Autodesk.AutoCAD.DatabaseServices.Polyline pl)

                    {

                        // LÃƒÆ’Ã‚Â¡Ãƒâ€šÃ‚ÂºÃƒâ€šÃ‚Â¥y OCS vertices cÃƒÆ’Ã‚Â¡Ãƒâ€šÃ‚Â»Ãƒâ€šÃ‚Â§a polyline

                        var vertices = new List<double[]>();

                        for (int i = 0; i < pl.NumberOfVertices; i++)

                        {

                            var pt = pl.GetPoint2dAt(i);

                            vertices.Add(new[] { pt.X, pt.Y });

                        }



                        // Detect "ÃƒÆ’Ã¢â‚¬Å¾ÃƒÂ¢Ã¢â€šÂ¬Ã‹Å“ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â³ng" ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â chÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â­nh thÃƒÆ’Ã‚Â¡Ãƒâ€šÃ‚Â»Ãƒâ€šÃ‚Â©c (Closed=true) hoÃƒÆ’Ã‚Â¡Ãƒâ€šÃ‚ÂºÃƒâ€šÃ‚Â·c thÃƒÆ’Ã‚Â¡Ãƒâ€šÃ‚Â»Ãƒâ€šÃ‚Â±c tÃƒÆ’Ã‚Â¡Ãƒâ€šÃ‚ÂºÃƒâ€šÃ‚Â¿ (vertex cuÃƒÆ’Ã‚Â¡Ãƒâ€šÃ‚Â»ÃƒÂ¢Ã¢â€šÂ¬Ã‹Å“i ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â°Ãƒâ€¹Ã¢â‚¬Â  vertex ÃƒÆ’Ã¢â‚¬Å¾ÃƒÂ¢Ã¢â€šÂ¬Ã‹Å“ÃƒÆ’Ã‚Â¡Ãƒâ€šÃ‚ÂºÃƒâ€šÃ‚Â§u)

                        bool isClosed = pl.Closed;

                        if (!isClosed && vertices.Count >= 4)

                        {

                            var first = vertices[0];

                            var last  = vertices[vertices.Count - 1];

                            double closingDist = Math.Sqrt(

                                Math.Pow(last[0] - first[0], 2) + Math.Pow(last[1] - first[1], 2));

                            isClosed = closingDist < 1.0; // < 1mm = ÃƒÆ’Ã¢â‚¬Å¾ÃƒÂ¢Ã¢â€šÂ¬Ã‹Å“ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â³ng thÃƒÆ’Ã‚Â¡Ãƒâ€šÃ‚Â»Ãƒâ€šÃ‚Â±c tÃƒÆ’Ã‚Â¡Ãƒâ€šÃ‚ÂºÃƒâ€šÃ‚Â¿

                        }



                        if (pickArea && isClosed && vertices.Count >= 3)

                        {

                            // XÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â³a vertex trÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¹ng cuÃƒÆ’Ã‚Â¡Ãƒâ€šÃ‚Â»ÃƒÂ¢Ã¢â€šÂ¬Ã‹Å“i (ÃƒÆ’Ã¢â‚¬Å¾ÃƒÂ¢Ã¢â€šÂ¬Ã‹Å“ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â³ng thÃƒÆ’Ã‚Â¡Ãƒâ€šÃ‚Â»Ãƒâ€šÃ‚Â±c tÃƒÆ’Ã‚Â¡Ãƒâ€šÃ‚ÂºÃƒâ€šÃ‚Â¿) nÃƒÆ’Ã‚Â¡Ãƒâ€šÃ‚ÂºÃƒâ€šÃ‚Â¿u cÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â³

                            if (!pl.Closed && vertices.Count >= 2)

                            {

                                var first = vertices[0]; var last = vertices[vertices.Count - 1];

                                if (Math.Abs(last[0]-first[0]) < 1 && Math.Abs(last[1]-first[1]) < 1)

                                    vertices.RemoveAt(vertices.Count - 1);

                            }



                            // TÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â­nh bounding box tÃƒÆ’Ã‚Â¡Ãƒâ€šÃ‚Â»Ãƒâ€šÃ‚Â« OCS vertices

                            double minVx = vertices.Min(v => v[0]);

                            double maxVx = vertices.Max(v => v[0]);

                            double minVy = vertices.Min(v => v[1]);

                            double maxVy = vertices.Max(v => v[1]);

                            newRow.Length = maxVx - minVx;

                            newRow.Height = maxVy - minVy;



                            // ChÃƒÆ’Ã‚Â¡Ãƒâ€šÃ‚Â»ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â° lÃƒÆ’Ã¢â‚¬Â Ãƒâ€šÃ‚Â°u vertices nÃƒÆ’Ã‚Â¡Ãƒâ€šÃ‚ÂºÃƒâ€šÃ‚Â¿u KHÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚ÂNG phÃƒÆ’Ã‚Â¡Ãƒâ€šÃ‚ÂºÃƒâ€šÃ‚Â£i chÃƒÆ’Ã‚Â¡Ãƒâ€šÃ‚Â»Ãƒâ€šÃ‚Â¯ nhÃƒÆ’Ã‚Â¡Ãƒâ€šÃ‚ÂºÃƒâ€šÃ‚Â­t ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ dÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¹ng scan-line

                            bool isRectangle = IsRectangleByVertices(vertices);

                            if (!isRectangle)

                                newRow.PolygonVertices = vertices;

                        }

                        else

                        {

                            // Polyline hÃƒÆ’Ã‚Â¡Ãƒâ€šÃ‚Â»Ãƒâ€¦Ã‚Â¸ ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ lÃƒÆ’Ã‚Â¡Ãƒâ€šÃ‚ÂºÃƒâ€šÃ‚Â¥y tÃƒÆ’Ã‚Â¡Ãƒâ€šÃ‚Â»ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â¢ng chiÃƒÆ’Ã‚Â¡Ãƒâ€šÃ‚Â»Ãƒâ€šÃ‚Âu dÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â i tuyÃƒÆ’Ã‚Â¡Ãƒâ€šÃ‚ÂºÃƒâ€šÃ‚Â¿n ÃƒÆ’Ã¢â‚¬Å¾ÃƒÂ¢Ã¢â€šÂ¬Ã‹Å“ÃƒÆ’Ã‚Â¡Ãƒâ€šÃ‚Â»Ãƒâ€ Ã¢â‚¬â„¢ trÃƒÆ’Ã‚Â¡Ãƒâ€šÃ‚ÂºÃƒâ€šÃ‚Â£i tÃƒÆ’Ã‚Â¡Ãƒâ€šÃ‚ÂºÃƒâ€šÃ‚Â¥m theo tuyÃƒÆ’Ã‚Â¡Ãƒâ€šÃ‚ÂºÃƒâ€šÃ‚Â¿n gÃƒÆ’Ã‚Â¡Ãƒâ€šÃ‚Â»ÃƒÂ¢Ã¢â€šÂ¬Ã‹Å“c

                            newRow.Length = pl.Length;

                        }

                        newRow.Name = $"{TenderWall.GetCategoryPrefix(category)}-{_wallRows.Count + 1}";

                    }



                    tr.Commit();

                    polygonTag = newRow.PolygonVertices != null ? " [Polygon]" : " [Rect]";

                    ed.WriteMessage($"\nĐã chọn: {newRow.Name}{polygonTag} | Dài={newRow.Length:F0}mm | Cao={newRow.Height:F0}mm");

                }

                if (newRow != null && !pickArea)

                {
                    if (!TryPromptWallSegmentsInput(
                            totalLengthMm: newRow.Length,
                            defaultHeightMm: newRow.Height,
                            initialSegments: newRow.HeightSegments,
                            initialOpenings: newRow.Openings,
                            panelWidthMm: newRow.PanelWidth,
                            layoutDirection: newRow.LayoutDirection,
                            out var promptedSegments,
                            out var promptedHeight,
                            out var promptedLayoutDirection,
                            out var promptedOpenings))
                    {
                        PluginLogger.Warn($"TenderPickEntity.PopupCancelled | row={newRow.Name}");
                        return;
                    }

                    newRow.Height = promptedHeight;
                    newRow.HeightSegments = promptedSegments;
                    var promptedLength = promptedSegments.Sum(s => Math.Max(0, s.LengthMm));
                    if (promptedLength > 0.5)
                        newRow.Length = promptedLength;
                    newRow.LayoutDirection = promptedLayoutDirection;
                    newRow.Openings = promptedOpenings;
                    PluginLogger.Info(
                        $"TenderPickEntity.PopupApplied | row={newRow.Name} | category={newRow.Category} | app={newRow.Application} | " +
                        $"spec={newRow.SpecKey} | seg={DescribeSegments(promptedSegments)} | openings={DescribeOpenings(promptedOpenings)}");

                    polygonTag = string.IsNullOrWhiteSpace(polygonTag) ? " [Đoạn thẳng]" : polygonTag;

                    ed.WriteMessage($"\nCập nhật chiều cao: {newRow.Height:F0}mm");

                }



                if (newRow != null

                    && pickArea

                    && IsSuspendedCeilingRow(newRow)

                    && !TryConfigureSuspendedCeilingDivision(newRow))

                {

                    SetStatus("Hãy thêm vùng trần sau khi chọn hướng chia phụ kiện.");

                    return;

                }



                if (newRow != null)

                {
                    newRow.Refresh();
                    SyncWallRowSpecData(newRow);

                    _wallRows.Add(newRow);
                    ReindexWalls();

                    _wallGrid.SelectedItem = newRow;

                    _wallGrid.ScrollIntoView(newRow);
                    _wallGrid.Items.Refresh();
                    SafeRefreshWallGrid();
                    LoadOpeningsForWall(newRow);

                    RefreshFooter();

                    RefreshPanelBreakdown(newRow);
                    RefreshBomSummary(allowDeferredRetry: false, forceWhenPendingEdits: true);
                    _project.Walls = GetWallModels();
                    PluginLogger.Info(
                        $"TenderPickEntity.Synced | row={newRow.Name} | length={newRow.Length:F0} | height={newRow.Height:F0} | " +
                        $"seg={DescribeSegments(newRow.HeightSegments)} | walls={_wallRows.Count}");

                    _lastCadPreviewKey = null;

                }

            }

            catch (Exception ex) { SetStatus($"Lỗi pick: {ex.Message}"); }

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

                // Vector cÃƒÆ’Ã‚Â¡Ãƒâ€šÃ‚Â»Ãƒâ€šÃ‚Â§a 2 cÃƒÆ’Ã‚Â¡Ãƒâ€šÃ‚ÂºÃƒâ€šÃ‚Â¡nh liÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Âªn tiÃƒÆ’Ã‚Â¡Ãƒâ€šÃ‚ÂºÃƒâ€šÃ‚Â¿p

                var a = v[i];

                var b = v[(i + 1) % 4];

                var c = v[(i + 2) % 4];

                double ax = b[0] - a[0], ay = b[1] - a[1];

                double bx = c[0] - b[0], by = c[1] - b[1];

                // Dot product: 0 = vuÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â´ng gÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â³c

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

                    Title = "Nhập chiều cao",

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

                    Text = "Đã pick chiều dài. Nhập chiều cao để hoàn tất dòng khối lượng:",

                    TextWrapping = TextWrapping.Wrap,

                    FontSize = 14,

                    FontWeight = FontWeights.SemiBold,

                    Margin = new Thickness(0, 0, 0, 12)

                });



                root.Children.Add(new TextBlock

                {

                    Text = "Chiều cao (mm)",

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

                    Text = "Ví dụ: 3000",

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



                var btnOk = Btn("OK", AccentGreen, Brushes.White, (s, e) => ConfirmAndClose(), 110);

                var btnCancel = Btn("Hủy", BtnGray, Brushes.White, (s, e) =>

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

        private bool TryPromptWallSegmentsInput(
            double totalLengthMm,
            double defaultHeightMm,
            IReadOnlyList<TenderHeightSegment>? initialSegments,
            IReadOnlyList<TenderOpening>? initialOpenings,
            int panelWidthMm,
            string layoutDirection,
            out List<TenderHeightSegment> segments,
            out double representativeHeightMm,
            out string selectedLayoutDirection,
            out List<TenderOpening> selectedOpenings)
        {
            segments = new List<TenderHeightSegment>();
            representativeHeightMm = Math.Round(defaultHeightMm > 0 ? defaultHeightMm : 3000.0);
            selectedLayoutDirection = string.Equals(layoutDirection, "Ngang", StringComparison.OrdinalIgnoreCase) ? "Ngang" : "Dọc";
            selectedOpenings = new List<TenderOpening>();
            bool confirmed = false;
            var selectedSegments = new List<TenderHeightSegment>();
            var pickedOpenings = new List<TenderOpening>();
            var createdSpanEntityIds = new List<Autodesk.AutoCAD.DatabaseServices.ObjectId>();
            double selectedHeight = representativeHeightMm;
            double seedLength = initialSegments?.Sum(s => Math.Max(0, s.LengthMm)) ?? 0;
            bool hasLengthReference = totalLengthMm > 0.5 || seedLength > 0.5;
            double lengthTarget = hasLengthReference
                ? Math.Max(1, Math.Round(totalLengthMm > 0.5 ? totalLengthMm : seedLength))
                : 1000;
            double defaultHeight = Math.Round(defaultHeightMm > 0 ? defaultHeightMm : 3000.0);
            string currentLayoutDirection = selectedLayoutDirection;
            string selectedDirection = selectedLayoutDirection;
            var draftOpenings = (initialOpenings ?? Array.Empty<TenderOpening>())
                .Select(o => new TenderOpening
                {
                    Type = string.IsNullOrWhiteSpace(o.Type) ? "Cửa đi" : o.Type,
                    Width = Math.Max(1, Math.Round(o.Width)),
                    Height = Math.Max(1, Math.Round(o.Height)),
                    BottomElevationMm = Math.Max(0, Math.Round(o.BottomElevationMm)),
                    CenterStationMm = o.CenterStationMm,
                    Quantity = Math.Max(1, o.Quantity)
                })
                .ToList();

            Dispatcher.Invoke(() =>
            {
                Canvas previewCanvas = null!;
                TextBlock lblNote = null!;
                ComboBox cboLayoutDirection = null!;
                TextBlock lblOpeningCount = null!;
                var rows = new ObservableCollection<HeightSegmentInputRow>();
                var seedSegments = WallHeightResolver.Normalize(lengthTarget, defaultHeight, initialSegments);
                if (seedSegments.Count == 0)
                {
                    if (hasLengthReference)
                    {
                        rows.Add(new HeightSegmentInputRow { LengthMm = lengthTarget, HeightMm = defaultHeight });
                    }
                }
                else
                {
                    foreach (var segment in seedSegments)
                    {
                        rows.Add(new HeightSegmentInputRow
                        {
                            LengthMm = Math.Round(segment.LengthMm),
                            HeightMm = Math.Round(segment.HeightMm),
                            CadHandle = segment.CadHandle
                        });
                    }
                }

                var dlg = new Window
                {
                    Title = "Cấu hình cao độ vách theo nhịp",
                    Width = 860,
                    Height = 680,
                    MinWidth = 860,
                    MinHeight = 680,
                    WindowStartupLocation = WindowStartupLocation.CenterScreen,
                    ResizeMode = ResizeMode.CanResize,
                    Background = new SolidColorBrush(Color.FromRgb(250, 250, 252)),
                    Owner = this
                };

                var root = new Grid { Margin = new Thickness(14) };
                root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                dlg.Content = root;

                var header = new TextBlock
                {
                    Text = hasLengthReference
                        ? $"Chiều dài tham chiếu tuyến: {lengthTarget:F0} mm. Nhập/Pick nhịp độc lập theo thực tế thi công."
                        : "Chưa có chiều dài tham chiếu. Dùng Pick Nhịp để lấy từng nhịp theo thực tế thi công.",
                    TextWrapping = TextWrapping.Wrap,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = FgDark,
                    Margin = new Thickness(0, 0, 0, 8)
                };
                Grid.SetRow(header, 0);
                root.Children.Add(header);

                var topPanel = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
                var directionPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 16, 0)
                };
                directionPanel.Children.Add(new TextBlock
                {
                    Text = "Hướng Chia Tấm:",
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 6, 0),
                    Foreground = FgDark,
                    FontWeight = FontWeights.SemiBold
                });
                cboLayoutDirection = new ComboBox
                {
                    Width = 120,
                    ItemsSource = new[] { "Dọc", "Ngang" },
                    SelectedValue = currentLayoutDirection
                };
                cboLayoutDirection.SelectionChanged += (_, _) =>
                {
                    currentLayoutDirection = string.Equals(cboLayoutDirection.SelectedItem as string, "Ngang", StringComparison.OrdinalIgnoreCase)
                        ? "Ngang"
                        : "Dọc";
                    RefreshPreview();
                };
                directionPanel.Children.Add(cboLayoutDirection);
                topPanel.Children.Add(directionPanel);
                var btnPickSpan = Btn("Pick Nhịp", AccentBlue, Brushes.White, (_, _) =>
                {
                    dlg.Hide();
                    try
                    {
                        var doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
                        if (doc == null)
                            return;

                        var ed = doc.Editor;
                        while (true)
                        {
                            var p1Opt = new Autodesk.AutoCAD.EditorInput.PromptPointOptions("\nChọn điểm đầu nhịp (Enter để kết thúc):")
                            {
                                AllowNone = true
                            };
                            var p1Result = ed.GetPoint(p1Opt);
                            if (p1Result.Status == Autodesk.AutoCAD.EditorInput.PromptStatus.None)
                                break;
                            if (p1Result.Status != Autodesk.AutoCAD.EditorInput.PromptStatus.OK)
                                break;

                            var p2Opt = new Autodesk.AutoCAD.EditorInput.PromptPointOptions("\nChọn điểm cuối nhịp:");
                            p2Opt.UseBasePoint = true;
                            p2Opt.BasePoint = p1Result.Value;
                            var p2Result = ed.GetPoint(p2Opt);
                            if (p2Result.Status != Autodesk.AutoCAD.EditorInput.PromptStatus.OK)
                                break;

                            double lengthMm = Math.Round(p1Result.Value.DistanceTo(p2Result.Value));
                            if (lengthMm <= 0)
                                continue;

                            if (!TryPromptWallHeightInput(defaultHeight, out var heightMm) || heightMm <= 0)
                                break;

                            if (rows.Count == 1
                                && Math.Abs(rows[0].LengthMm - lengthTarget) < 0.5
                                && Math.Abs(rows[0].HeightMm - defaultHeight) < 0.5)
                            {
                                rows.Clear();
                            }

                            string? cadHandle = null;
                            Autodesk.AutoCAD.DatabaseServices.ObjectId createdId = Autodesk.AutoCAD.DatabaseServices.ObjectId.Null;
                            if (TryCreatePersistentPickSpanLine(p1Result.Value, p2Result.Value, out var createdHandle, out var entityId))
                            {
                                cadHandle = createdHandle;
                                createdId = entityId;
                            }

                            rows.Add(new HeightSegmentInputRow
                            {
                                LengthMm = lengthMm,
                                HeightMm = Math.Round(heightMm),
                                CadHandle = cadHandle
                            });
                            if (!createdId.IsNull)
                                createdSpanEntityIds.Add(createdId);
                        }
                    }
                    finally
                    {
                        dlg.Show();
                        dlg.Activate();
                        RefreshPreview();
                    }
                }, 100);
                topPanel.Children.Add(btnPickSpan);
                var btnPickOpening = Btn("Pick Lỗ Mở", AccentOrange, Brushes.White, (_, _) =>
                {
                    dlg.Hide();
                    try
                    {
                        while (TryPickOpeningFromCadForPopup(
                            rows.Select(r => new TenderHeightSegment
                            {
                                LengthMm = Math.Max(0, r.LengthMm),
                                HeightMm = Math.Max(0, r.HeightMm),
                                CadHandle = string.IsNullOrWhiteSpace(r.CadHandle) ? null : r.CadHandle
                            }).ToList(),
                            out var opening))
                        {
                            draftOpenings.Add(opening);
                        }
                    }
                    finally
                    {
                        dlg.Show();
                        dlg.Activate();
                        lblOpeningCount.Text = $"Lỗ Mở: {draftOpenings.Count}";
                        RefreshPreview();
                    }
                }, 120);
                topPanel.Children.Add(btnPickOpening);
                lblOpeningCount = new TextBlock
                {
                    Text = $"Lỗ Mở: {draftOpenings.Count}",
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(16, 0, 0, 0),
                    FontWeight = FontWeights.SemiBold,
                    Foreground = FgDark
                };
                topPanel.Children.Add(lblOpeningCount);
                Grid.SetRow(topPanel, 1);
                root.Children.Add(topPanel);

                var rowGrid = new DataGrid
                {
                    AutoGenerateColumns = false,
                    CanUserAddRows = false,
                    CanUserDeleteRows = false,
                    SelectionMode = DataGridSelectionMode.Single,
                    HeadersVisibility = DataGridHeadersVisibility.Column,
                    ItemsSource = rows,
                    Height = 190
                };
                rowGrid.Columns.Add(new DataGridTextColumn
                {
                    Header = "Dài (mm)",
                    Binding = new Binding(nameof(HeightSegmentInputRow.LengthMm))
                    {
                        StringFormat = "F0",
                        UpdateSourceTrigger = UpdateSourceTrigger.LostFocus
                    },
                    Width = new DataGridLength(1, DataGridLengthUnitType.Star)
                });
                rowGrid.Columns.Add(new DataGridTextColumn
                {
                    Header = "Cao (mm)",
                    Binding = new Binding(nameof(HeightSegmentInputRow.HeightMm))
                    {
                        StringFormat = "F0",
                        UpdateSourceTrigger = UpdateSourceTrigger.LostFocus
                    },
                    Width = new DataGridLength(1, DataGridLengthUnitType.Star)
                });
                rowGrid.CellEditEnding += (_, _) =>
                    Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(RefreshPreview));
                Grid.SetRow(rowGrid, 2);
                root.Children.Add(rowGrid);

                var previewPanel = new Grid();
                previewPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                previewPanel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                previewPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                Grid.SetRow(previewPanel, 3);
                root.Children.Add(previewPanel);

                var lblPreviewTitle = new TextBlock
                {
                    Text = "Xem trước hình học (trục ngang = chiều dài, trục dọc = cao độ)",
                    FontWeight = FontWeights.SemiBold,
                    Foreground = FgDark,
                    Margin = new Thickness(0, 0, 0, 6)
                };
                Grid.SetRow(lblPreviewTitle, 0);
                previewPanel.Children.Add(lblPreviewTitle);

                previewCanvas = new Canvas
                {
                    Background = new SolidColorBrush(Color.FromRgb(245, 248, 255)),
                    Height = 270
                };
                Grid.SetRow(previewCanvas, 1);
                previewPanel.Children.Add(previewCanvas);

                lblNote = new TextBlock
                {
                    Foreground = Brushes.Firebrick,
                    FontWeight = FontWeights.SemiBold,
                    Margin = new Thickness(0, 6, 0, 0)
                };
                Grid.SetRow(lblNote, 2);
                previewPanel.Children.Add(lblNote);

                var footer = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Margin = new Thickness(0, 10, 0, 0)
                };
                Grid.SetRow(footer, 4);
                root.Children.Add(footer);

                var btnCancel = Btn("Hủy", BtnGray, Brushes.White, (_, _) =>
                {
                    confirmed = false;
                    dlg.Close();
                }, 120);
                var btnApply = Btn("Áp dụng", AccentGreen, Brushes.White, (_, _) =>
                {
                    PluginLogger.Info(
                        $"TenderPopupApply.Start | lengthTarget={lengthTarget:F0} | defaultHeight={defaultHeight:F0} | " +
                        $"rows={rows.Count} | panelWidth={panelWidthMm} | layout={currentLayoutDirection} | openingsDraft={draftOpenings.Count}");

                    try { rowGrid.CommitEdit(DataGridEditingUnit.Cell, true); } catch (System.Exception ex)
            {
                ShopDrawing.Plugin.Core.PluginLogger.Error("Suppressed exception in TenderBomDialog.cs", ex);
            }
                    try { rowGrid.CommitEdit(DataGridEditingUnit.Row, true); } catch (System.Exception ex)
            {
                ShopDrawing.Plugin.Core.PluginLogger.Error("Suppressed exception in TenderBomDialog.cs", ex);
            }
                    if (CollectionViewSource.GetDefaultView(rowGrid.ItemsSource) is IEditableCollectionView editView)
                    {
                        try { if (editView.IsEditingItem) editView.CommitEdit(); } catch (System.Exception ex)
            {
                ShopDrawing.Plugin.Core.PluginLogger.Error("Suppressed exception in TenderBomDialog.cs", ex);
            }
                        try { if (editView.IsAddingNew) editView.CommitNew(); } catch (System.Exception ex)
            {
                ShopDrawing.Plugin.Core.PluginLogger.Error("Suppressed exception in TenderBomDialog.cs", ex);
            }
                    }

                if (!BuildNormalizedSegments(rows, lengthTarget, defaultHeight, out var normalized, out var note, autoFillMissing: true))
                    {
                        PluginLogger.Warn($"TenderPopupApply.ValidationFailed | note={note}");
                        lblNote.Text = note;
                        lblNote.Foreground = Brushes.Firebrick;
                        return;
                    }

                    selectedSegments = normalized;
                    double total = selectedSegments.Sum(s => s.LengthMm);
                    selectedHeight = total > 0
                        ? selectedSegments.Sum(s => s.LengthMm * s.HeightMm) / total
                        : defaultHeight;
                    selectedDirection = string.Equals(cboLayoutDirection.SelectedItem as string, "Ngang", StringComparison.OrdinalIgnoreCase)
                        ? "Ngang"
                        : "Dọc";
                    pickedOpenings = draftOpenings
                        .Select(o => new TenderOpening
                        {
                            Type = string.IsNullOrWhiteSpace(o.Type) ? "Cửa đi" : o.Type,
                            Width = Math.Max(1, Math.Round(o.Width)),
                            Height = Math.Max(1, Math.Round(o.Height)),
                            BottomElevationMm = Math.Max(0, Math.Round(o.BottomElevationMm)),
                            CenterStationMm = o.CenterStationMm,
                            Quantity = Math.Max(1, o.Quantity)
                        })
                        .ToList();
                    PluginLogger.Info(
                        $"TenderPopupApply.Confirmed | direction={selectedDirection} | " +
                        $"segments={DescribeSegments(selectedSegments)} | openings={DescribeOpenings(pickedOpenings)}");
                    createdSpanEntityIds.Clear();
                    confirmed = true;
                    dlg.Close();
                }, 120);

                footer.Children.Add(btnCancel);
                footer.Children.Add(btnApply);

                void RefreshPreview()
                {
                    BuildNormalizedSegments(rows, lengthTarget, defaultHeight, out var normalized, out var note, autoFillMissing: false);
                    DrawHeightProfilePreview(previewCanvas, normalized, lengthTarget, panelWidthMm, currentLayoutDirection, draftOpenings);

                    int panelCount = 0;
                    if (panelWidthMm > 0)
                    {
                        double totalLen = normalized.Sum(s => s.LengthMm);
                        if (string.Equals(currentLayoutDirection, "Dọc", StringComparison.OrdinalIgnoreCase))
                        {
                            panelCount = (int)Math.Ceiling(totalLen / panelWidthMm);
                        }
                        else
                        {
                            double avgH = totalLen > 0 ? normalized.Sum(s => s.LengthMm * s.HeightMm) / totalLen : defaultHeight;
                            panelCount = (int)Math.Ceiling(avgH / panelWidthMm);
                        }
                    }

                    if (string.IsNullOrWhiteSpace(note))
                    {
                        lblNote.Foreground = Brushes.DarkGreen;
                        lblNote.Text = $"Ước tính số tấm sơ bộ: {panelCount} (khổ {panelWidthMm} mm, hướng {currentLayoutDirection})";
                    }
                    else
                    {
                        lblNote.Foreground = note.StartsWith("Vượt", StringComparison.OrdinalIgnoreCase)
                            ? Brushes.Firebrick
                            : new SolidColorBrush(Color.FromRgb(191, 108, 0));
                        lblNote.Text = $"{note} | Ước tính số tấm: {panelCount}";
                    }
                }

                dlg.Loaded += (_, _) => RefreshPreview();
                dlg.SizeChanged += (_, _) => RefreshPreview();
                dlg.ShowDialog();
            });

            if (confirmed)
            {
                segments = selectedSegments;
                representativeHeightMm = selectedHeight;
                selectedLayoutDirection = selectedDirection;
                selectedOpenings = pickedOpenings;
            }

            return confirmed;
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
                note = "Đang dùng 1 nhịp mặc định toàn tuyến.";
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
            double margin = 18;
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

            if (string.Equals(layoutDirection, "Dọc", StringComparison.OrdinalIgnoreCase))
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
                    }
                }
            }

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
                double stationStartMm = opening.CenterStationMm;
                if (stationStartMm < 0)
                {
                    int fallbackIndex = withoutStation.FindIndex(x => ReferenceEquals(x.opening, opening));
                    double ratio = (fallbackIndex + 1.0) / (withoutStation.Count + 1.0);
                    stationStartMm = Math.Max(0, ratio * drawingLength - opening.Width * 0.5);
                }
                stationStartMm = Math.Max(0, Math.Min(drawingLength, stationStartMm));
                double stationEndMm = Math.Max(stationStartMm, Math.Min(drawingLength, stationStartMm + opening.Width));
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

                // Dim định vị theo tuyến: từ đầu vách đến mép lỗ mở.
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

                // Dim cao độ đáy lỗ mở.
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
                    Text = $"Đáy {bottomElevationMm:F0}",
                    FontSize = 9,
                    Foreground = dimBrush,
                    Background = new SolidColorBrush(Color.FromArgb(220, 255, 255, 255))
                };
                Canvas.SetLeft(bottomLabel, Math.Max(margin, dimBottomX - 36));
                Canvas.SetTop(bottomLabel, Math.Max(margin, (openingBottomY + bottomY) * 0.5 - 7));
                canvas.Children.Add(bottomLabel);

                // Dim kích thước lỗ mở: rộng + cao.
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
                    Text = $"Lỗ{i + 1}: {opening.Width:F0}x{opening.Height:F0} | LT {stationStartMm:F0} | Đáy {bottomElevationMm:F0}",
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
            var p1Opt = new Autodesk.AutoCAD.EditorInput.PromptPointOptions("\nChọn điểm 1 lỗ mở (Enter để kết thúc):")
            {
                AllowNone = true
            };
            var p1Result = ed.GetPoint(p1Opt);
            if (p1Result.Status == Autodesk.AutoCAD.EditorInput.PromptStatus.None)
                return false;
            if (p1Result.Status != Autodesk.AutoCAD.EditorInput.PromptStatus.OK)
                return false;

            var p2Opt = new Autodesk.AutoCAD.EditorInput.PromptPointOptions("\nChọn điểm 2 lỗ mở:");
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

                ed.WriteMessage($"\nĐịnh vị lỗ mở: LT={stationMm:F0} mm | Rộng={widthMm:F0} mm");
            }

            var hOpt = new Autodesk.AutoCAD.EditorInput.PromptDoubleOptions("\nNhập cao lỗ mở (mm):")
            {
                DefaultValue = 2100,
                AllowNegative = false,
                AllowZero = false
            };
            var hRes = ed.GetDouble(hOpt);
            if (hRes.Status != Autodesk.AutoCAD.EditorInput.PromptStatus.OK)
                return false;
            double heightMm = Math.Round(hRes.Value);
            if (heightMm <= 0)
                return false;

            var bottomOpt = new Autodesk.AutoCAD.EditorInput.PromptDoubleOptions("\nNhập cao độ đáy lỗ mở (mm):")
            {
                DefaultValue = 0,
                AllowNegative = false,
                AllowZero = true
            };
            var bottomRes = ed.GetDouble(bottomOpt);
            if (bottomRes.Status != Autodesk.AutoCAD.EditorInput.PromptStatus.OK)
                return false;
            double bottomElevationMm = Math.Max(0, Math.Round(bottomRes.Value));

            opening = new TenderOpening
            {
                Type = heightMm >= 2000 ? "Cửa đi" : "Cửa sổ",
                Width = widthMm,
                Height = heightMm,
                BottomElevationMm = bottomElevationMm,
                CenterStationMm = stationMm,
                Quantity = 1
            };
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
                // Không chặn luồng popup khi xóa line tạm lỗi.
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

        private void PickOpeningFromCad(bool isElevation, TenderOpeningRow? existingRow)

        {

            if (!(_wallGrid.SelectedItem is TenderWallRow wallRow))

            {

                SetStatus("Chọn vách trước khi chọn lỗ mở từ CAD.");

                return;

            }



            BeginCadInteraction();

            try

            {

                var doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;

                if (doc == null) return;

                var ed = doc.Editor;



                string mode = isElevation ? "MẶT ĐỨNG" : "MẶT BẰNG";

                ed.WriteMessage($"\n=== CHỌN LỖ MỞ ({mode}) ===");



                var p1Result = ed.GetPoint(new Autodesk.AutoCAD.EditorInput.PromptPointOptions(

                    "\nChọn điểm 1 của lỗ mở:"));

                if (p1Result.Status != Autodesk.AutoCAD.EditorInput.PromptStatus.OK) return;



                var p2Opt = new Autodesk.AutoCAD.EditorInput.PromptPointOptions(

                    "\nChọn điểm 2 của lỗ mở:");

                p2Opt.UseBasePoint = true;

                p2Opt.BasePoint = p1Result.Value;

                var p2Result = ed.GetPoint(p2Opt);

                if (p2Result.Status != Autodesk.AutoCAD.EditorInput.PromptStatus.OK) return;



                var p1 = p1Result.Value;

                var p2 = p2Result.Value;



                double dx = Math.Abs(p2.X - p1.X);

                double dy = Math.Abs(p2.Y - p1.Y);



                double widthMm;

                double heightMm;



                if (isElevation)

                {

                    double dim1 = Math.Round(dx);

                    double dim2 = Math.Round(dy);

                    widthMm = Math.Min(dim1, dim2);

                    heightMm = Math.Max(dim1, dim2);

                    ed.WriteMessage($"\nMặt đứng: Rộng={widthMm:F0}mm | Cao={heightMm:F0}mm");

                }

                else

                {

                    widthMm = Math.Round(Math.Sqrt(dx * dx + dy * dy));

                    ed.WriteMessage($"\nMặt bằng: Rộng={widthMm:F0}mm");



                    var hOpt = new Autodesk.AutoCAD.EditorInput.PromptDoubleOptions(

                        "\nNhập chiều cao lỗ mở (mm):");

                    hOpt.DefaultValue = 2500;

                    hOpt.AllowNegative = false;

                    hOpt.AllowZero = false;

                    var hResult = ed.GetDouble(hOpt);

                    if (hResult.Status != Autodesk.AutoCAD.EditorInput.PromptStatus.OK) return;

                    heightMm = Math.Round(hResult.Value);

                    ed.WriteMessage($" | Cao={heightMm:F0}mm");

                }



                double finalW = widthMm;

                double finalH = heightMm;
                double finalBottom = Math.Max(0, Math.Round(existingRow?.BottomElevationMm ?? 0));
                double finalStation = Math.Round(existingRow?.CenterStationMm ?? -1);
                if (!isElevation && TryResolveOpeningStationAndWidthFromCad(
                    p1,
                    p2,
                    wallRow.HeightSegments,
                    out var detectedStation,
                    out var projectedWidth))
                {
                    finalStation = Math.Round(detectedStation);
                    if (projectedWidth > 0)
                        finalW = Math.Round(projectedWidth);

                    ed.WriteMessage($" | LT={finalStation:F0}mm | Rộng={finalW:F0}mm");
                }

                var bottomOpt = new Autodesk.AutoCAD.EditorInput.PromptDoubleOptions(

                    "\nNhập cao độ đáy lỗ mở (mm):");

                bottomOpt.DefaultValue = finalBottom;

                bottomOpt.AllowNegative = false;

                bottomOpt.AllowZero = true;

                var bottomResult = ed.GetDouble(bottomOpt);

                if (bottomResult.Status != Autodesk.AutoCAD.EditorInput.PromptStatus.OK) return;

                finalBottom = Math.Max(0, Math.Round(bottomResult.Value));

                bool confirmed = false;



                Dispatcher.Invoke(() =>

                {

                    var dlg = new Window

                    {

                    Title = "Xác nhận lỗ mở",

                        Width = 320,

                        Height = 180,

                        WindowStartupLocation = WindowStartupLocation.CenterScreen,

                        ResizeMode = ResizeMode.NoResize,

                        Background = new SolidColorBrush(Color.FromRgb(250, 250, 252))

                    };



                    var sp = new StackPanel { Margin = new Thickness(16) };



                    var lblInfo = new TextBlock

                    {

                        Text = $"Rộng: {finalW:F0} mm\nCao:  {finalH:F0} mm\nĐáy: {finalBottom:F0} mm\nLT:   {(finalStation >= 0 ? finalStation.ToString("F0") : "N/A")} mm",

                        FontSize = 15,

                        FontWeight = FontWeights.SemiBold,

                        Margin = new Thickness(0, 0, 0, 12)

                    };

                    sp.Children.Add(lblInfo);



                    var btnBar = new StackPanel { Orientation = Orientation.Horizontal };



                    var btnSwap = Btn("Đổi chiều", AccentOrange, Brushes.White, (s2, e2) =>

                    {

                        double tmp = finalW;

                        finalW = finalH;

                        finalH = tmp;

                        lblInfo.Text = $"Rộng: {finalW:F0} mm\nCao:  {finalH:F0} mm\nĐáy: {finalBottom:F0} mm\nLT:   {(finalStation >= 0 ? finalStation.ToString("F0") : "N/A")} mm";

                    });

                    btnBar.Children.Add(btnSwap);



                    var btnOk = Btn("OK", AccentGreen, Brushes.White, (s2, e2) =>

                    {

                        confirmed = true;

                        dlg.Close();

                    });

                    btnBar.Children.Add(btnOk);



                    var btnCancel = Btn("Hủy", BtnGray, Brushes.White, (s2, e2) => dlg.Close());

                    btnBar.Children.Add(btnCancel);



                    sp.Children.Add(btnBar);

                    dlg.Content = sp;

                    dlg.ShowDialog();

                });



                if (!confirmed) return;



                Dispatcher.BeginInvoke(new Action(() =>

                {

                    if (existingRow != null)

                    {

                        existingRow.Width = finalW;

                        existingRow.Height = finalH;
                        existingRow.BottomElevationMm = finalBottom;
                        existingRow.CenterStationMm = finalStation;

                        existingRow.Type = finalH >= 2000 ? "Cửa đi" : "Cửa sổ";

                        existingRow.Refresh();

                    }

                    else

                    {

                        var openingRow = new TenderOpeningRow

                        {

                            Type = finalH >= 2000 ? "Cửa đi" : "Cửa sổ",

                            Width = finalW,

                            Height = finalH,

                            BottomElevationMm = finalBottom,
                            CenterStationMm = finalStation,

                            Quantity = 1

                        };

                        _openingRows.Add(openingRow);

                    }



                    if (_wallGrid.SelectedItem is TenderWallRow selectedWall)

                    {

                        selectedWall.SyncOpenings(_openingRows);

                        selectedWall.Refresh();

                    }



                    SafeRefreshWallGrid();

                    RefreshFooter();

                    string action = existingRow != null ? "Cập nhật" : "Đã thêm";

                    var ltText = finalStation >= 0 ? $" | LT {finalStation:F0} mm" : string.Empty;
                    SetStatus($"{action} lỗ mở {finalW:F0}x{finalH:F0} mm | Đáy {finalBottom:F0} mm{ltText}");

                }));

            }

            catch (Exception ex)

            {

                SetStatus($"Lỗi chọn lỗ mở: {ex.Message}");

            }

            finally

            {

                EndCadInteraction();

            }

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

                    && _previewEntityIds.Count == 0

                    && _transientPreviewEntities.Count == 0) return;

                var doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;

                if (doc == null)

                {

                    _highlightedSourceEntityIds.Clear();

                    _previewEntityIds.Clear();

                    foreach (var entity in _transientPreviewEntities)

                    {

                        try { entity.Dispose(); } catch (System.Exception ex)

            {

                ShopDrawing.Plugin.Core.PluginLogger.Error("Suppressed exception in TenderBomDialog.cs", ex);

            }

                    }

                    _transientPreviewEntities.Clear();

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

                var transientManager = Autodesk.AutoCAD.GraphicsInterface.TransientManager.CurrentTransientManager;

                var viewportIds = new Autodesk.AutoCAD.Geometry.IntegerCollection();

                foreach (var entity in _transientPreviewEntities)

                {

                    try { transientManager.EraseTransient(entity, viewportIds); } catch (System.Exception ex)

            {

                ShopDrawing.Plugin.Core.PluginLogger.Error("Suppressed exception in TenderBomDialog.cs", ex);

            }

                    try { entity.Dispose(); } catch (System.Exception ex)

            {

                ShopDrawing.Plugin.Core.PluginLogger.Error("Suppressed exception in TenderBomDialog.cs", ex);

            }

                }

                _highlightedSourceEntityIds.Clear();

                _previewEntityIds.Clear();

                _transientPreviewEntities.Clear();

            }

            catch

            {

                _highlightedSourceEntityIds.Clear();

                _previewEntityIds.Clear();

                foreach (var entity in _transientPreviewEntities)

                {

                    try { entity.Dispose(); } catch (System.Exception ex)

            {

                ShopDrawing.Plugin.Core.PluginLogger.Error("Suppressed exception in TenderBomDialog.cs", ex);

            }

                }

                _transientPreviewEntities.Clear();

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

                if (doc == null) { SetStatus("Cảnh báo: Không tìm thấy document"); return; }



                using (doc.LockDocument())

                using (var tr = doc.Database.TransactionManager.StartTransaction())

                {

                    var handle = new Autodesk.AutoCAD.DatabaseServices.Handle(Convert.ToInt64(handleStr, 16));

                    if (!doc.Database.TryGetObjectId(handle, out var objId))

                    {

                SetStatus("Cảnh báo: đối tượng không tồn tại hoặc đã bị thay đổi.");

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

            catch (Exception ex) { SetStatus($"Cảnh báo: Highlight {ex.Message}"); }

        }



        private void ZoomToEntity(string handleStr)

        {

            if (_isEditingCell || _suspendCadOperations) return;

            try

            {

                ClearHighlight();



                var doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;

                if (doc == null) { SetStatus("Cảnh báo: Không tìm thấy document"); return; }



                using (doc.LockDocument())

                using (var tr = doc.Database.TransactionManager.StartTransaction())

                {

                    var handle = new Autodesk.AutoCAD.DatabaseServices.Handle(Convert.ToInt64(handleStr, 16));

                    if (!doc.Database.TryGetObjectId(handle, out var objId))

                    {

                SetStatus("Cảnh báo: đối tượng không tồn tại hoặc đã bị thay đổi.");

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

            catch (Exception ex) { SetStatus($"Cảnh báo: Highlight {ex.Message}"); }

        }



        private void ShowCadPreview(TenderWallRow row, bool force = false)

        {

            string previewKey = BuildCadPreviewKey(row);

            if (!force

                && (_highlightedSourceEntityIds.Count > 0

                    || _previewEntityIds.Count > 0

                    || _transientPreviewEntities.Count > 0)

                && string.Equals(_lastCadPreviewKey, previewKey, StringComparison.Ordinal))

            {

                return;

            }



            if ((EnableTenderCadOverlayPreview || IsSuspendedCeilingRow(row))

                && TryDrawColdStorageCeilingPreview(row, force))

            {

                _lastCadPreviewKey = previewKey;

                SetStatus($"Vùng tính khối lượng: {row.Name} | Xem trước tuyến treo");

                return;

            }



            if (TryShowRowRegionPreview(row))

            {

                _lastCadPreviewKey = previewKey;

                SetStatus($"Vùng tính khối lượng: {row.Name}");

                return;

            }



            if (!string.IsNullOrEmpty(row.CadHandle))

            {

                HighlightEntity(row.CadHandle);

                _lastCadPreviewKey = previewKey;

                SetStatus($"Vị trí: {row.Name}");

            }

            else

            {

                ClearHighlight();

                _lastCadPreviewKey = null;

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



        private bool TryShowRowRegionPreview(TenderWallRow row)

        {

            if (_isEditingCell || _suspendCadOperations || string.IsNullOrWhiteSpace(row.CadHandle))

                return false;



            var doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;

            if (doc == null)

                return false;



            ClearHighlight();



            try

            {

                Autodesk.AutoCAD.DatabaseServices.Entity? previewEntity = null;



                using (doc.LockDocument())

                using (var tr = doc.Database.TransactionManager.StartTransaction())

                {

                    var handle = new Autodesk.AutoCAD.DatabaseServices.Handle(Convert.ToInt64(row.CadHandle, 16));

                    if (!doc.Database.TryGetObjectId(handle, out var objId))

                    {

                        tr.Commit();

                        return false;

                    }



                    var sourceEnt = tr.GetObject(objId, Autodesk.AutoCAD.DatabaseServices.OpenMode.ForRead, false)

                        as Autodesk.AutoCAD.DatabaseServices.Entity;

                    if (sourceEnt == null)

                    {

                        tr.Commit();

                        return false;

                    }



                    sourceEnt.Highlight();

                    if (!_highlightedSourceEntityIds.Contains(objId))

                        _highlightedSourceEntityIds.Add(objId);



                    if (sourceEnt is Autodesk.AutoCAD.DatabaseServices.Polyline sourcePolyline

                        && !sourcePolyline.Closed

                        && sourcePolyline.NumberOfVertices >= 2

                        && row.Height > 0)

                    {

                        var layerId = EnsureHighlightLayer(doc.Database, tr);

                        var bt = (Autodesk.AutoCAD.DatabaseServices.BlockTable)tr.GetObject(

                            doc.Database.BlockTableId, Autodesk.AutoCAD.DatabaseServices.OpenMode.ForRead);

                        var btr = (Autodesk.AutoCAD.DatabaseServices.BlockTableRecord)tr.GetObject(

                            bt[Autodesk.AutoCAD.DatabaseServices.BlockTableRecord.ModelSpace],

                            Autodesk.AutoCAD.DatabaseServices.OpenMode.ForWrite);



                        AddDevelopedPolylinePreview(sourcePolyline, row, layerId, btr, tr);

                        AddPreviewSummaryText(GetPolylineVertices(sourcePolyline), row, layerId, btr, tr);

                        tr.Commit();

                        return true;

                    }



                    previewEntity = BuildRowRegionPreviewEntity(row, sourceEnt);

                    var previewVertices = GetPreviewVertices(row, sourceEnt);

                    if (previewVertices.Count >= 3)

                    {

                        var layerId = EnsureHighlightLayer(doc.Database, tr);

                        var bt = (Autodesk.AutoCAD.DatabaseServices.BlockTable)tr.GetObject(

                            doc.Database.BlockTableId, Autodesk.AutoCAD.DatabaseServices.OpenMode.ForRead);

                        var btr = (Autodesk.AutoCAD.DatabaseServices.BlockTableRecord)tr.GetObject(

                            bt[Autodesk.AutoCAD.DatabaseServices.BlockTableRecord.ModelSpace],

                            Autodesk.AutoCAD.DatabaseServices.OpenMode.ForWrite);



                        AddPanelPreviewLines(previewVertices, row, layerId, btr, tr);

                        AddPreviewSummaryText(previewVertices, row, layerId, btr, tr);

                    }

                    tr.Commit();

                }



                if (previewEntity != null)

                    AddTransientPreviewEntity(previewEntity);



                return true;

            }

            catch (Exception ex)

            {

                SetStatus($"Highlight vùng: {ex.Message}");

                return false;

            }

        }



        private void AddTransientPreviewEntity(Autodesk.AutoCAD.DatabaseServices.Entity entity)

        {

            entity.SetDatabaseDefaults();

            entity.Color = Autodesk.AutoCAD.Colors.Color.FromColorIndex(

                Autodesk.AutoCAD.Colors.ColorMethod.ByAci, PreviewBoundaryColorIndex);

            entity.LineWeight = Autodesk.AutoCAD.DatabaseServices.LineWeight.LineWeight211;



            var transientManager = Autodesk.AutoCAD.GraphicsInterface.TransientManager.CurrentTransientManager;

            var viewportIds = new Autodesk.AutoCAD.Geometry.IntegerCollection();

            transientManager.AddTransient(

                entity,

                Autodesk.AutoCAD.GraphicsInterface.TransientDrawingMode.DirectShortTerm,

                0,

                viewportIds);



            _transientPreviewEntities.Add(entity);

        }



        private bool TryDrawColdStorageCeilingPreview(TenderWallRow row, bool focusView = false)

        {

            if (!IsSuspendedCeilingRow(row) || string.IsNullOrWhiteSpace(row.CadHandle))

                return false;



            var doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;

            if (doc == null)

                return false;



            ClearHighlight();



            try

            {

                Autodesk.AutoCAD.DatabaseServices.Entity? previewBoundary = null;



                using (doc.LockDocument())

                using (var tr = doc.Database.TransactionManager.StartTransaction())

                {

                    var handle = new Autodesk.AutoCAD.DatabaseServices.Handle(Convert.ToInt64(row.CadHandle, 16));

                    if (!doc.Database.TryGetObjectId(handle, out var objId))

                    {

                        tr.Commit();

                        return false;

                    }



                    var pl = tr.GetObject(objId, Autodesk.AutoCAD.DatabaseServices.OpenMode.ForRead)

                        as Autodesk.AutoCAD.DatabaseServices.Polyline;

                    if (pl == null || !pl.Closed)

                    {

                        tr.Commit();

                        return false;

                    }



                    var vertices = GetPolylineVertices(pl);

                    if (vertices.Count < 3)

                    {

                        tr.Commit();

                        return false;

                    }



                    var layerId = EnsureHighlightLayer(doc.Database, tr);

                    var bt = (Autodesk.AutoCAD.DatabaseServices.BlockTable)tr.GetObject(

                        doc.Database.BlockTableId, Autodesk.AutoCAD.DatabaseServices.OpenMode.ForRead);

                    var btr = (Autodesk.AutoCAD.DatabaseServices.BlockTableRecord)tr.GetObject(

                        bt[Autodesk.AutoCAD.DatabaseServices.BlockTableRecord.ModelSpace],

                        Autodesk.AutoCAD.DatabaseServices.OpenMode.ForWrite);

                    pl.Highlight();

                    _highlightedSourceEntityIds.Add(objId);

                    previewBoundary = BuildRowRegionPreviewEntity(row, pl);

                    AddPanelPreviewLines(vertices, row, layerId, btr, tr);

                    AddPreviewSummaryText(vertices, row, layerId, btr, tr);



                    var preview = TenderBomCalculator.GetColdStorageCeilingPreviewData(row.ToModel());

                    if (preview.HasValue)

                    {

                        bool runAlongX = IsColdStorageRunAlongX(row);

                        var tPositions = BuildSuspensionLinePositions(

                            vertices,

                            runAlongX,

                            row.ColdStorageDivideFromMaxSide,

                            preview.Value.TSpacingMm,

                            preview.Value.TSpacingMm,

                            preview.Value.TLineCount);

                        var mushroomPositions = BuildSuspensionLinePositions(

                            vertices,

                            runAlongX,

                            row.ColdStorageDivideFromMaxSide,

                            preview.Value.TSpacingMm,

                            preview.Value.MushroomOffsetMm,

                            preview.Value.MushroomLineCount);



                        AddSuspensionPreviewLines(

                            vertices,

                            runAlongX,

                            tPositions,

                            preview.Value.TSpacingMm,

                            "T",

                            SuspensionTColorIndex,

                            Autodesk.AutoCAD.DatabaseServices.LineWeight.LineWeight050,

                            false,

                            false,

                            false,

                            layerId,

                            btr,

                            tr);



                        AddSuspensionPreviewLines(

                            vertices,

                            runAlongX,

                            mushroomPositions,

                            preview.Value.MushroomOffsetMm,

                            "M",

                            SuspensionMushroomColorIndex,

                            Autodesk.AutoCAD.DatabaseServices.LineWeight.LineWeight035,

                            false,

                            false,

                            false,

                            layerId,

                            btr,

                            tr);

                    }



                    if (focusView)

                    {

                        var ext = pl.GeometricExtents;

                        var view = doc.Editor.GetCurrentView();

                        view.CenterPoint = new Autodesk.AutoCAD.Geometry.Point2d(

                            (ext.MinPoint.X + ext.MaxPoint.X) / 2,

                            (ext.MinPoint.Y + ext.MaxPoint.Y) / 2);

                        view.Height = (ext.MaxPoint.Y - ext.MinPoint.Y) * 1.5;

                        view.Width = (ext.MaxPoint.X - ext.MinPoint.X) * 1.5;

                        doc.Editor.SetCurrentView(view);

                    }



                    tr.Commit();

                    if (previewBoundary != null)

                        AddTransientPreviewEntity(previewBoundary);

                    return true;

                }

            }

            catch (Exception ex)

            {

                SetStatus($"Lỗi preview trần: {ex.Message}");

                return false;

            }

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

                    Title = "Chọn hướng chia phụ kiện trần",

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

                    Text = "Chọn hướng chia phụ kiện và tuyến treo cho vùng trần đang pick:",

                    TextWrapping = TextWrapping.Wrap,

                    FontSize = 14,

                    FontWeight = FontWeights.SemiBold,

                    Margin = new Thickness(0, 0, 0, 10)

                });



                root.Children.Add(new TextBlock

                {

                    Text = "Hướng này độc lập với hướng chia tấm trong cột Hướng. Dọc/Ngang ở đây chỉ áp dụng cho tuyến phụ kiện trần.",

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



                buttonBar.Children.Add(Btn("Dọc", AccentBlue, Brushes.White, (s, e) =>

                {

                    result = "Dọc";

                    dlg.Close();

                }, 170));



                buttonBar.Children.Add(Btn("Ngang", AccentOrange, Brushes.White, (s, e) =>

                {

                    result = "Ngang";

                    dlg.Close();

                }, 170));



                root.Children.Add(buttonBar);



                var btnCancel = Btn("Hủy", BtnGray, Brushes.White, (s, e) =>

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

                string primaryLabel = runAlongX ? "Từ cạnh dưới" : "Từ cạnh trái";

                string secondaryLabel = runAlongX ? "Từ cạnh trên" : "Từ cạnh phải";

                string axisText = runAlongX ? "theo bề rộng đứng" : "theo bề rộng ngang";



                var dlg = new Window

                {

                    Title = "Chọn phương chia tuyến treo",

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

                    Text = $"Vùng pick sẽ chia tuyến treo {axisText}. Chọn cạnh gốc để bắt đầu chia nhịp:",

                    TextWrapping = TextWrapping.Wrap,

                    FontSize = 14,

                    FontWeight = FontWeights.SemiBold,

                    Margin = new Thickness(0, 0, 0, 10)

                });



                root.Children.Add(new TextBlock

                {

                    Text = "Lựa chọn này chỉ xác định cạnh bắt đầu chia tuyến treo, không thay đổi hướng chia tấm panel trong bảng.",

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



                var btnCancel = Btn("Hủy", BtnGray, Brushes.White, (s, e) =>

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

            string suspensionDirection = string.IsNullOrWhiteSpace(row.SuspensionLayoutDirection)

                ? row.LayoutDirection

                : row.SuspensionLayoutDirection;

            return !string.Equals(suspensionDirection, "Ngang", StringComparison.OrdinalIgnoreCase);

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



    }

}

