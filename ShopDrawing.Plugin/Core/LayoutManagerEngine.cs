using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using ShopDrawing.Plugin.Commands;

namespace ShopDrawing.Plugin.Core
{
    public class LayoutManagerEngine
    {
        [DllImport("acad.exe", CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "?acedSetCurrentVPort@@YA?AW4ErrorStatus@Acad@@PBVAcDbViewport@@@Z")]
        private static extern int acedSetCurrentVPort(IntPtr viewport);

        public static readonly Dictionary<string, (double W, double H)> PaperSizes = new()
        {
            ["A3"] = (420, 297),
            ["A2"] = (594, 420),
            ["A1"] = (841, 594),
        };

        public const double TITLE_BLOCK_H = 55;
        public const double OVERLAP_MM = 1000;
        public const string TITLE_LAYER = "SD_TITLE";
        public const string TITLE_FRAME_LAYER = "SD_TITLE_FRAME";
        public const string TITLE_TEXT_LAYER = "SD_TITLE_TEXT";
        public const string VIEWPORT_LAYER = "SD_VIEWPORT";
        public const string SHEET_FRAME_LAYER = "SD_SHEET_FRAME";
        public const string DMBD_LAYER = "SD_DMBV";
        public const string LAYOUT_PREFIX = "SD-";
        public const string VIEW_TITLE_PREFIX = "MẶT ĐỨNG VÁCH - ";

        private const string DefaultPlotDevice = "DWG To PDF.pc3";

        public double MarginLeft { get; set; } = 25;
        public double MarginRight { get; set; } = 5;
        public double MarginTop { get; set; } = 5;
        public double MarginBot { get; set; } = 5;

        public static double GetCurrentScaleDenominator()
        {
            try
            {
                var value = Application.GetSystemVariable("CANNOSCALE")?.ToString()
                    ?? ShopDrawingCommands.DefaultAnnotationScales;
                var parts = value.Split(':');
                if (parts.Length == 2
                    && double.TryParse(parts[0].Trim(), out double numerator)
                    && double.TryParse(parts[1].Trim(), out double denominator)
                    && numerator > 0)
                {
                    return denominator / numerator;
                }
            }
            catch
            {
            }

            return 100.0;
        }

        public Extents3d? PickRegion()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null)
                return null;

            var editor = doc.Editor;
            var firstCorner = editor.GetPoint(new PromptPointOptions("\nChọn góc 1 của vùng layout: "));
            if (firstCorner.Status != PromptStatus.OK)
                return null;

            var secondCorner = editor.GetCorner(
                new PromptCornerOptions("\nChọn góc 2 của vùng layout: ", firstCorner.Value));
            if (secondCorner.Status != PromptStatus.OK)
                return null;

            return new Extents3d(
                new Point3d(
                    Math.Min(firstCorner.Value.X, secondCorner.Value.X),
                    Math.Min(firstCorner.Value.Y, secondCorner.Value.Y),
                    0),
                new Point3d(
                    Math.Max(firstCorner.Value.X, secondCorner.Value.X),
                    Math.Max(firstCorner.Value.Y, secondCorner.Value.Y),
                    0));
        }

        public List<Extents3d> ComputePages(Extents3d region, double scaleDenominator, string paperSize = "A3",
            double titleBlockHeight = TITLE_BLOCK_H, double titleBlockRightWidth = 0)
        {
            var pages = new List<Extents3d>();
            var geometry = BuildPaperGeometry(paperSize, titleBlockHeight, titleBlockRightWidth);

            double usableModelWidth = geometry.ViewportWidth * scaleDenominator;
            double usableModelHeight = geometry.ViewportHeight * scaleDenominator;
            double regionWidth = region.MaxPoint.X - region.MinPoint.X;
            double regionHeight = region.MaxPoint.Y - region.MinPoint.Y;

            bool fitsInOnePage = regionWidth <= usableModelWidth && regionHeight <= usableModelHeight;
            if (fitsInOnePage)
            {
                pages.Add(new Extents3d(
                    new Point3d(region.MinPoint.X, region.MinPoint.Y, 0),
                    new Point3d(region.MaxPoint.X, region.MaxPoint.Y, 0)));
                return pages;
            }

            bool splitByX = (regionWidth / usableModelWidth) >= (regionHeight / usableModelHeight);

            if (splitByX)
            {
                for (double x = region.MinPoint.X; x < region.MaxPoint.X;)
                {
                    double xEnd = x + usableModelWidth;

                    if (xEnd >= region.MaxPoint.X)
                    {
                        // Last page: extend backward to fill full usable width
                        double xStart = Math.Max(region.MinPoint.X, region.MaxPoint.X - usableModelWidth);
                        xEnd = region.MaxPoint.X;

                        // Avoid duplicate if this overlaps entirely with previous page  
                        if (pages.Count > 0)
                        {
                            var prev = pages[pages.Count - 1];
                            if (Math.Abs(xStart - prev.MinPoint.X) < 1.0)
                                break; // Already covered
                        }

                        pages.Add(new Extents3d(
                            new Point3d(xStart, region.MinPoint.Y, 0),
                            new Point3d(xEnd, region.MaxPoint.Y, 0)));
                        break;
                    }

                    pages.Add(new Extents3d(
                        new Point3d(x, region.MinPoint.Y, 0),
                        new Point3d(xEnd, region.MaxPoint.Y, 0)));

                    x = xEnd - OVERLAP_MM;
                }
            }
            else
            {
                for (double y = region.MinPoint.Y; y < region.MaxPoint.Y;)
                {
                    double yEnd = y + usableModelHeight;

                    if (yEnd >= region.MaxPoint.Y)
                    {
                        // Last page: extend backward to fill full usable height
                        double yStart = Math.Max(region.MinPoint.Y, region.MaxPoint.Y - usableModelHeight);
                        yEnd = region.MaxPoint.Y;

                        if (pages.Count > 0)
                        {
                            var prev = pages[pages.Count - 1];
                            if (Math.Abs(yStart - prev.MinPoint.Y) < 1.0)
                                break;
                        }

                        pages.Add(new Extents3d(
                            new Point3d(region.MinPoint.X, yStart, 0),
                            new Point3d(region.MaxPoint.X, yEnd, 0)));
                        break;
                    }

                    pages.Add(new Extents3d(
                        new Point3d(region.MinPoint.X, y, 0),
                        new Point3d(region.MaxPoint.X, yEnd, 0)));

                    y = yEnd - OVERLAP_MM;
                }
            }

            return pages;
        }

        public List<LayoutPlanItem> BuildLayoutPlan(LayoutCreationRequest request)
        {
            ApplyRequestSettings(request);

            double scaleDenominator = GetCurrentScaleDenominator();
            var resolvedConfig = EnsureStripDetected(request.TitleBlockConfig, Application.DocumentManager.MdiActiveDocument?.Database);
            var (tbH, tbW) = GetStripReduction(resolvedConfig);
            var pages = ComputePages(request.Region, scaleDenominator, request.PaperSize, tbH, tbW);

            // Pre-allocate drawing numbers so layout tab names match the SO BV field in the title block.
            var doc = Application.DocumentManager.MdiActiveDocument;
            int nextNumber = doc != null
                ? DrawingListManager.PeekNextDrawingNumber(doc)
                : 1;

            return pages
                .Select((page, index) =>
                {
                    string pageLabel = LayoutManagerRules.BuildPageLabel(
                        request.UserTitle,
                        index + 1,
                        pages.Count);

                    string layoutTabName = $"SD-{nextNumber + index:D3}";

                    return new LayoutPlanItem(
                        page,
                        VIEW_TITLE_PREFIX + pageLabel,
                        layoutTabName);
                })
                .ToList();
        }

        public List<string> CreateLayoutsFromRegion(LayoutCreationRequest request)
        {
            return CreateLayoutsFromRegion(request, string.Empty);
        }

        public List<string> CreateLayoutsFromRegion(LayoutCreationRequest request, string revisionContent)
        {
            var plan = BuildLayoutPlan(request);
            return CreateLayoutsFromPlan(plan, request, revisionContent);
        }

        public List<string> CreateLayoutsFromRegion(
            string userTitle,
            Extents3d region,
            string paperSize = "A3",
            string projectName = "")
        {
            return CreateLayoutsFromRegion(new LayoutCreationRequest
            {
                UserTitle = userTitle,
                Region = region,
                PaperSize = paperSize,
                ProjectName = projectName,
                MarginLeft = MarginLeft,
                MarginRight = MarginRight,
                MarginTop = MarginTop,
                MarginBot = MarginBot,
                TitleBlockConfig = new LayoutTitleBlockConfig()
            });
        }

        private List<string> CreateLayoutsFromPlan(
            IReadOnlyList<LayoutPlanItem> plan,
            LayoutCreationRequest request,
            string revisionContent)
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null)
                return [];

            double scaleDenominator = GetCurrentScaleDenominator();
            string scaleText = BuildScaleValue(scaleDenominator);
            var createdLayouts = new List<string>();

            using (doc.LockDocument())
            {
                var db = doc.Database;
                var resolvedConfig2 = EnsureStripDetected(request.TitleBlockConfig, db);
                var (tbH2, tbW2) = GetStripReduction(resolvedConfig2);
                var geometry = BuildPaperGeometry(request.PaperSize, tbH2, tbW2);
                string currentTargetLayout = null;

                foreach (var item in plan)
                {
                    double reqW = (item.PageExtents.MaxPoint.X - item.PageExtents.MinPoint.X) / scaleDenominator;
                    double reqH = (item.PageExtents.MaxPoint.Y - item.PageExtents.MinPoint.Y) / scaleDenominator;

                    bool packed = false;
                    Point2d insertTopLeft = Point2d.Origin;

                    // 1. Same batch packing
                    if (!string.IsNullOrEmpty(currentTargetLayout))
                    {
                        if (TryFindSpaceInLayout(db, currentTargetLayout, geometry, reqW, reqH, out insertTopLeft))
                            packed = true;
                    }

                    // 2. Scan if not packed
                    if (!packed)
                    {
                        string possibleLayout = FindAnyLayoutWithSpace(db, geometry, reqW, reqH, out Point2d possiblePos);
                        if (!string.IsNullOrEmpty(possibleLayout))
                        {
                            var result = System.Windows.MessageBox.Show(
                                $"Layout '{possibleLayout}' còn chỗ trống.\nBạn có muốn ghép thêm vào layout này không?\n\n- Yes: Ghép vào layout\n- No: Tạo riêng layout mới\n- Cancel: Dừng lệnh",
                                "Multi-Viewport layout",
                                System.Windows.MessageBoxButton.YesNoCancel,
                                System.Windows.MessageBoxImage.Question);

                            if (result == System.Windows.MessageBoxResult.Cancel)
                                break;
                            if (result == System.Windows.MessageBoxResult.Yes)
                            {
                                currentTargetLayout = possibleLayout;
                                insertTopLeft = possiblePos;
                                packed = true;
                            }
                        }
                    }

                    string actualLayoutName;
                    if (packed)
                    {
                        actualLayoutName = AddViewportToLayout(doc, currentTargetLayout, insertTopLeft, item.PageExtents, scaleDenominator, item.ViewTitleText, scaleText, revisionContent);
                        currentTargetLayout = actualLayoutName; 
                    }
                    else
                    {
                        var meta = DrawingListManager.PrepareLayoutMeta(
                            doc,
                            item.LayoutName,
                            scaleText,
                            request.ProjectName,
                            StripPageSuffix(item.ViewTitleText),
                            revisionContent);

                        string vTitle = item.ViewTitleText;
                        if (vTitle.Contains("(PHẦN"))
                        {
                            // If it's a part, remove the part tracking for the layout name and title block base
                            // Wait, the PRD says view title should be W1 (PHẦN...). But Tên bản vẽ: W1.
                            // We will let DrawTitleBlock format the base name, and View Title keeps the original.
                        }

                        CreateLayoutWithViewport(
                            doc,
                            item.LayoutName,
                            item.PageExtents,
                            scaleDenominator,
                            request.PaperSize,
                            item.ViewTitleText,
                            request.TitleBlockConfig,
                            meta);

                        actualLayoutName = item.LayoutName;
                        currentTargetLayout = actualLayoutName;
                    }

                    if (!createdLayouts.Contains(actualLayoutName))
                        createdLayouts.Add(actualLayoutName);
                }

                DrawingListManager.Sync(doc);
            }

            return createdLayouts;
        }

        private bool TryFindSpaceInLayout(Database db, string layoutName, PaperGeometry geometry, double reqW, double reqH, out Point2d newTopLeft)
        {
            using var tr = db.TransactionManager.StartTransaction();
            var layoutDict = (DBDictionary)tr.GetObject(db.LayoutDictionaryId, OpenMode.ForRead);
            if (!layoutDict.Contains(layoutName))
            {
                newTopLeft = Point2d.Origin;
                return false;
            }

            var layout = (Layout)tr.GetObject(layoutDict.GetAt(layoutName), OpenMode.ForRead);
            var usedRegions = LayoutPackingService.GetUsedRegions(tr, layout);

            return LayoutPackingService.TryPackViewport(
                usedRegions,
                geometry.ViewportLeft, geometry.ViewportLeft + geometry.ViewportWidth,
                geometry.ViewportBottom, geometry.ViewportBottom + geometry.ViewportHeight,
                reqW, reqH,
                out newTopLeft);
        }

        private string FindAnyLayoutWithSpace(Database db, PaperGeometry geometry, double reqW, double reqH, out Point2d newTopLeft)
        {
            using var tr = db.TransactionManager.StartTransaction();
            var layoutDict = (DBDictionary)tr.GetObject(db.LayoutDictionaryId, OpenMode.ForRead);

            // Filter SD layouts (avoid DMBV), sort by name alphabetically
            var layoutNames = new List<string>();
            foreach (DictionaryEntry entry in layoutDict)
            {
                string name = (string)entry.Key;
                bool isManaged = name.StartsWith(LAYOUT_PREFIX, StringComparison.OrdinalIgnoreCase);
                if (isManaged && !string.Equals(name, DrawingListManager.DMBD_LAYOUT, StringComparison.OrdinalIgnoreCase))
                {
                    layoutNames.Add(name);
                }
            }

            layoutNames.Sort();

            foreach (var layoutName in layoutNames)
            {
                if (TryFindSpaceInLayout(db, layoutName, geometry, reqW, reqH, out newTopLeft))
                {
                    return layoutName;
                }
            }
            newTopLeft = Point2d.Origin;
            return string.Empty;
        }

        private string AddViewportToLayout(
            Document doc,
            string targetLayoutName,
            Point2d insertTopLeft,
            Extents3d pageExtents,
            double scaleDenominator,
            string viewTitleText, // The specific title for this vp, e.g. "W2 (PHẦN...)"
            string scaleText,
            string revisionContent)
        {
            var editor = doc.Editor;
            var db = doc.Database;
            string resultingLayoutName = targetLayoutName;

            using (var transaction = db.TransactionManager.StartTransaction())
            {
                var layoutDict = (DBDictionary)transaction.GetObject(db.LayoutDictionaryId, OpenMode.ForWrite);
                var layout = (Layout)transaction.GetObject(layoutDict.GetAt(targetLayoutName), OpenMode.ForWrite);
                var paperSpace = (BlockTableRecord)transaction.GetObject(layout.BlockTableRecordId, OpenMode.ForWrite);

                EnsureTitleLayers(db, transaction);
                
                // 1. Calculate physical VPN dimensions
                double vpWidth = (pageExtents.MaxPoint.X - pageExtents.MinPoint.X) / scaleDenominator;
                double vpHeight = (pageExtents.MaxPoint.Y - pageExtents.MinPoint.Y) / scaleDenominator;
                Point3d vpCenter = new Point3d(insertTopLeft.X + vpWidth / 2, insertTopLeft.Y - vpHeight / 2, 0);



                var viewport = new Viewport
                {
                    CenterPoint = vpCenter,
                    Width = vpWidth,
                    Height = vpHeight,
                    Layer = VIEWPORT_LAYER
                };

                paperSpace.AppendEntity(viewport);
                transaction.AddNewlyCreatedDBObject(viewport, true);

                // Re-use ConfigureViewportView by constructing a dummy geometry that matches this specific slot
                var dummyGeometry = new PaperGeometry(
                    0, 0, 0, 0,
                    vpWidth, vpHeight,
                    insertTopLeft.Y - vpHeight, // Bottom
                    insertTopLeft.X,            // Left
                    insertTopLeft.Y,            // Top
                    vpCenter
                );

                ConfigureViewportView(viewport, dummyGeometry, pageExtents, scaleDenominator);



                try { viewport.On = true; } catch { }
                TryActivateViewport(editor, viewport);

                DrawingListManager.LayoutMeta existingMeta = DrawingListManager.GetLayoutMeta(doc, targetLayoutName);
                DrawViewTitle(paperSpace, transaction, insertTopLeft.X, insertTopLeft.Y - vpHeight, viewTitleText, existingMeta);

                // --- Naming Logic ---
                // Merge existing layout name (e.g. "MẶT ĐỨNG VÁCH - W1") with new view title (e.g. "MẶT ĐỨNG VÁCH - W3")
                string updatedTitleText = LayoutManagerRules.MergeWallNames(targetLayoutName, viewTitleText);
                string expectedLayoutName = LayoutManagerRules.GetMergedLayoutTabName(updatedTitleText);

                // Try changing layout name
                if (expectedLayoutName != targetLayoutName)
                {
                    if (layoutDict.Contains(expectedLayoutName))
                    {
                        // Fallback using random if collision
                        expectedLayoutName += "-" + Guid.NewGuid().ToString("N")[..4];
                    }

                    layout.LayoutName = expectedLayoutName;
                    DrawingListManager.RenameLayoutMeta(doc, targetLayoutName, expectedLayoutName);
                    
                    // We must also update the title block drawing title text/attribute.
                    string previousDrawingTitle = string.IsNullOrWhiteSpace(existingMeta.DrawingTitle)
                        ? LayoutManagerRules.InferDrawingTitleFromLayoutTabName(targetLayoutName)
                        : existingMeta.DrawingTitle;
                    UpdateTBTenBanVe(paperSpace, transaction, previousDrawingTitle, StripPageSuffix(updatedTitleText));

                    resultingLayoutName = expectedLayoutName;
                }

                // Always update meta so DMBV has content
                DrawingListManager.PrepareLayoutMeta(
                    doc,
                    resultingLayoutName,
                    existingMeta.TyLe,
                    existingMeta.ProjectName,
                    StripPageSuffix(updatedTitleText),
                    existingMeta.GhiChu);

                transaction.Commit();
            }

            return resultingLayoutName;
        }

        private void UpdateTBTenBanVe(BlockTableRecord paperSpace, Transaction tr, string oldVal, string newVal)
        {
            if (string.IsNullOrWhiteSpace(oldVal)) return;

            // Simple replace in MText/DBText on TITLE_TEXT_LAYER
            foreach (ObjectId id in paperSpace)
            {
                if (tr.GetObject(id, OpenMode.ForRead) is not Entity ent) continue;
                
                if (ent.Layer.Equals(TITLE_TEXT_LAYER, StringComparison.OrdinalIgnoreCase))
                {
                    if (ent is MText mtext && mtext.Text.Equals(oldVal, StringComparison.OrdinalIgnoreCase))
                    {
                        mtext.UpgradeOpen();
                        mtext.Contents = newVal.ToUpperInvariant();
                    }
                    else if (ent is DBText dbtext && dbtext.TextString.Equals(oldVal, StringComparison.OrdinalIgnoreCase))
                    {
                        dbtext.UpgradeOpen();
                        dbtext.TextString = newVal.ToUpperInvariant();
                    }
                }
                else if (ent is BlockReference br)
                {
                    // Check attributes if external block
                    if (br.AttributeCollection != null)
                    {
                        foreach (ObjectId attId in br.AttributeCollection)
                        {
                            var att = (AttributeReference)tr.GetObject(attId, OpenMode.ForWrite);
                            if (att.TextString.Equals(oldVal, StringComparison.OrdinalIgnoreCase))
                            {
                                att.TextString = newVal.ToUpperInvariant();
                            }
                        }
                    }
                }
            }
        }

        private void CreateLayoutWithViewport(
            Document doc,
            string layoutName,
            Extents3d pageExtents,
            double scaleDenominator,
            string paperSize,
            string viewTitleText,
            LayoutTitleBlockConfig titleBlockConfig,
            DrawingListManager.LayoutMeta meta)
        {
            var editor = doc.Editor;
            var db = doc.Database;

            // Ensure strip detection is up-to-date
            titleBlockConfig = EnsureStripDetected(titleBlockConfig, db);

            var (tbH3, tbW3) = GetStripReduction(titleBlockConfig);
            var geometry = BuildPaperGeometry(paperSize, tbH3, tbW3);

            using (var transaction = db.TransactionManager.StartTransaction())
            {
                var layoutDictionary =
                    (DBDictionary)transaction.GetObject(db.LayoutDictionaryId, OpenMode.ForWrite);
                ObjectId layoutId = layoutDictionary.Contains(layoutName)
                    ? layoutDictionary.GetAt(layoutName)
                    : LayoutManager.Current.CreateLayout(layoutName);

                var layout = (Layout)transaction.GetObject(layoutId, OpenMode.ForWrite);
                ApplyPageSetup(layout, paperSize);
                transaction.Commit();
            }

            LayoutManager.Current.CurrentLayout = layoutName;
            PreparePaperSpaceEnvironment(editor);

            using (var transaction = db.TransactionManager.StartTransaction())
            {
                // *** FIX: Use layout.BlockTableRecordId instead of blockTable[PaperSpace] ***
                // blockTable["*Paper_Space"] ALWAYS returns the default layout's BTR,
                // NOT the newly created layout's BTR. This caused viewports to be added
                // to the wrong paper space, making GetUsedRegions unable to find them.
                var layoutDict = (DBDictionary)transaction.GetObject(db.LayoutDictionaryId, OpenMode.ForRead);
                var layout = (Layout)transaction.GetObject(layoutDict.GetAt(layoutName), OpenMode.ForRead);
                var paperSpace = (BlockTableRecord)transaction.GetObject(layout.BlockTableRecordId, OpenMode.ForWrite);

                ClearExistingPaperSpaceEntities(paperSpace, transaction);
                EnsureTitleLayers(db, transaction);

                // Skip frame drawing when using external title block — the customer's block IS the frame
                if (titleBlockConfig.Mode != LayoutTitleBlockMode.External)
                {
                    DrawSheetFrame(paperSpace, transaction, geometry);
                }

                // Size viewport to ACTUAL content, not to full usable area.
                // This allows subsequent viewports to pack alongside.
                double vpWidth = (pageExtents.MaxPoint.X - pageExtents.MinPoint.X) / scaleDenominator;
                double vpHeight = (pageExtents.MaxPoint.Y - pageExtents.MinPoint.Y) / scaleDenominator;

                // Clamp: if content is larger than usable, use usable
                vpWidth = Math.Min(vpWidth, geometry.ViewportWidth);
                vpHeight = Math.Min(vpHeight, geometry.ViewportHeight);

                // Place at top-left of usable area
                Point2d insertTopLeft = new Point2d(geometry.ViewportLeft, geometry.ViewportBottom + geometry.ViewportHeight);
                Point3d vpCenter = new Point3d(insertTopLeft.X + vpWidth / 2, insertTopLeft.Y - vpHeight / 2, 0);



                var viewport = new Viewport
                {
                    CenterPoint = vpCenter,
                    Width = vpWidth,
                    Height = vpHeight,
                    Layer = VIEWPORT_LAYER
                };

                paperSpace.AppendEntity(viewport);
                transaction.AddNewlyCreatedDBObject(viewport, true);



                // Build a geometry that matches the actual content viewport (not full usable)
                var contentGeometry = new PaperGeometry(
                    0, 0, 0, 0,
                    vpWidth, vpHeight,
                    insertTopLeft.Y - vpHeight, // Bottom
                    insertTopLeft.X,            // Left
                    insertTopLeft.Y,            // Top
                    vpCenter
                );

                ConfigureViewportView(viewport, contentGeometry, pageExtents, scaleDenominator);


                try
                {
                    viewport.On = true;
                }
                catch
                {
                }

                TryActivateViewport(editor, viewport);

                // View title nằm ngay dưới viewport thực tế
                double vpLeft = viewport.CenterPoint.X - viewport.Width / 2;
                double vpBottom = viewport.CenterPoint.Y - viewport.Height / 2;
                DrawViewTitle(paperSpace, transaction, vpLeft, vpBottom, viewTitleText, meta);
                DrawTitleBlock(paperSpace, transaction, geometry, viewTitleText, titleBlockConfig, meta, editor, layoutName);

                transaction.Commit();
            }

            editor.WriteMessage($"\n[SD] Layout '{layoutName}' hoàn tất.");
            editor.WriteMessage($"\n[SD][Debug] TitleBlock: Mode={titleBlockConfig.Mode}, BlockW={titleBlockConfig.BlockWidth:F1}, BlockH={titleBlockConfig.BlockHeight:F1}");
            editor.WriteMessage($"\n[SD][Debug] Strip: Position={titleBlockConfig.StripPosition}, StripSize={titleBlockConfig.StripSize:F1}");
            var (dbgH, dbgW) = GetStripReduction(titleBlockConfig);
            editor.WriteMessage($"\n[SD][Debug] Reduction: HeightReduction={dbgH:F1}, WidthReduction={dbgW:F1}");
        }

        private static void TryActivateViewport(Editor editor, Viewport viewport)
        {
            try
            {
                editor.SwitchToModelSpace();
                acedSetCurrentVPort(viewport.UnmanagedObject);
            }
            catch
            {
            }
            finally
            {
                try
                {
                    editor.SwitchToPaperSpace();
                }
                catch
                {
                }
            }
        }

        private static void ClearExistingPaperSpaceEntities(BlockTableRecord paperSpace, Transaction transaction)
        {
            var idsToErase = new List<ObjectId>();

            foreach (ObjectId entityId in paperSpace)
            {
                var dbObject = transaction.GetObject(entityId, OpenMode.ForRead);
                if (dbObject is Viewport viewport && viewport.Number > 1)
                {
                    idsToErase.Add(entityId);
                    continue;
                }

                if (dbObject is Entity entity
                    && (string.Equals(entity.Layer, TITLE_LAYER, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(entity.Layer, TITLE_FRAME_LAYER, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(entity.Layer, TITLE_TEXT_LAYER, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(entity.Layer, VIEWPORT_LAYER, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(entity.Layer, SHEET_FRAME_LAYER, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(entity.Layer, DMBD_LAYER, StringComparison.OrdinalIgnoreCase)))
                {
                    idsToErase.Add(entityId);
                }
            }

            foreach (ObjectId entityId in idsToErase.Distinct())
            {
                var entity = transaction.GetObject(entityId, OpenMode.ForWrite);
                entity.Erase();
            }
        }

        private void DrawViewTitle(
            BlockTableRecord paperSpace,
            Transaction transaction,
            double vpLeft,
            double vpBottom,
            string viewTitleText,
            DrawingListManager.LayoutMeta meta)
        {
            double titleX = vpLeft + 2;
            double titleY = vpBottom - 7;

            AddText(paperSpace, transaction, viewTitleText.ToUpperInvariant(), titleX, titleY, 3.5, true);
            AddText(
                paperSpace,
                transaction,
                LayoutManagerRules.BuildScaleLabel(meta.TyLe),
                titleX,
                titleY - 5,
                2.5,
                false);
        }

        private void DrawTitleBlock(
            BlockTableRecord paperSpace,
            Transaction transaction,
            PaperGeometry geometry,
            string viewTitleText,
            LayoutTitleBlockConfig titleBlockConfig,
            DrawingListManager.LayoutMeta meta,
            Editor editor,
            string layoutName)
        {
            string drawingTitle = string.IsNullOrWhiteSpace(meta.DrawingTitle)
                ? StripPageSuffix(viewTitleText)
                : meta.DrawingTitle;
            var titleValues = new LayoutTitleBlockValueSet
            {
                ProjectName = meta.ProjectName,
                DrawingTitle = drawingTitle.ToUpperInvariant(),
                DrawingNumber = meta.SoBanVe,
                Scale = meta.TyLe,
                Date = meta.Ngay,
                Revision = meta.Revision,
                RevisionContent = meta.GhiChu
            };

            editor.WriteMessage(
                $"\n[SD][Debug] Values: ProjectName='{titleValues.ProjectName}' " +
                $"DrawingTitle='{titleValues.DrawingTitle}' " +
                $"DrawingNumber='{titleValues.DrawingNumber}'");

            bool insertedExternal = false;
            if (titleBlockConfig.Mode == LayoutTitleBlockMode.External)
            {
                // External blocks always insert at inner frame corner (MarginLeft, MarginBot)
                    var insertPos = new Point3d(MarginLeft, MarginBot, 0);
                    insertedExternal = LayoutTitleBlockConfigStore.TryInsertTitleBlock(
                    paperSpace,
                    transaction,
                    titleBlockConfig,
                    insertPos,
                    titleValues,
                    out string warning);

                if (!insertedExternal)
                {
                    editor.WriteMessage($"\n[SD] External title block fallback for '{layoutName}': {warning}");
                }
            }

            if (!insertedExternal)
            {
                DrawBuiltInTitleBlock(
                    paperSpace,
                    transaction,
                    geometry.PaperWidth,
                    geometry.PaperHeight,
                    viewTitleText,
                    meta);
            }
        }

        private static void ConfigureViewportView(
            Viewport viewport,
            PaperGeometry geometry,
            Extents3d pageExtents,
            double scaleDenominator)
        {
            double modelWidth = Math.Max(1.0, pageExtents.MaxPoint.X - pageExtents.MinPoint.X);
            double modelHeight = Math.Max(1.0, pageExtents.MaxPoint.Y - pageExtents.MinPoint.Y);
            double modelCenterX = (pageExtents.MinPoint.X + pageExtents.MaxPoint.X) / 2;
            double modelCenterY = (pageExtents.MinPoint.Y + pageExtents.MaxPoint.Y) / 2;
            
            double paperVpWidth = modelWidth / scaleDenominator;
            double paperVpHeight = modelHeight / scaleDenominator;

            // Giới hạn viewport không vượt quá vùng usable
            paperVpWidth = Math.Min(paperVpWidth, geometry.ViewportWidth);
            paperVpHeight = Math.Min(paperVpHeight, geometry.ViewportHeight);

            viewport.Locked = false;
            
            viewport.Width = paperVpWidth;
            viewport.Height = paperVpHeight;

            // Căn viewport về góc trên-trái vùng usable (chuyên nghiệp hơn căn giữa)
            double vpCenterX = geometry.ViewportLeft + paperVpWidth / 2;
            double vpCenterY = geometry.ViewportTop - paperVpHeight / 2;
            viewport.CenterPoint = new Point3d(vpCenterX, vpCenterY, 0);
            
            viewport.ViewDirection = Vector3d.ZAxis;
            viewport.ViewTarget = new Point3d(modelCenterX, modelCenterY, 0);
            viewport.ViewCenter = Point2d.Origin;
            viewport.ViewHeight = modelHeight;
            viewport.CustomScale = 1.0 / scaleDenominator;
            
            viewport.Locked = true;
        }

        private void DrawBuiltInTitleBlock(
            BlockTableRecord paperSpace,
            Transaction transaction,
            double paperWidth,
            double paperHeight,
            string viewTitleText,
            DrawingListManager.LayoutMeta meta)
        {
            string drawingTitle = string.IsNullOrWhiteSpace(meta.DrawingTitle)
                ? StripPageSuffix(viewTitleText)
                : meta.DrawingTitle;
            double x0 = MarginLeft;
            double y0 = MarginBot;
            double width = paperWidth - MarginLeft - MarginRight;
            double height = TITLE_BLOCK_H;

            DrawRect(paperSpace, transaction, x0, y0, x0 + width, y0 + height, 0.5, TITLE_LAYER);

            double revisionWidth = 60;
            DrawRect(paperSpace, transaction, x0, y0, x0 + revisionWidth, y0 + height, 0.25, TITLE_FRAME_LAYER);

            AddText(paperSpace, transaction, "SỬA ĐỔI", x0 + 1, y0 + height - 6, 2.5, false);
            DrawLine(paperSpace, transaction, x0, y0 + height - 8, x0 + revisionWidth, y0 + height - 8, 0.25, TITLE_FRAME_LAYER);

            double column1 = x0 + 8;
            double column2 = x0 + 24;
            DrawLine(paperSpace, transaction, column1, y0, column1, y0 + height - 8, 0.18, TITLE_FRAME_LAYER);
            DrawLine(paperSpace, transaction, column2, y0, column2, y0 + height - 8, 0.18, TITLE_FRAME_LAYER);
            AddText(paperSpace, transaction, "REV", x0 + 1, y0 + height - 13, 2.0, false);
            AddText(paperSpace, transaction, "NGÀY", column1 + 1, y0 + height - 13, 2.0, false);
            AddText(paperSpace, transaction, "NỘI DUNG", column2 + 1, y0 + height - 13, 2.0, false);
            DrawLine(paperSpace, transaction, x0, y0 + height - 15, x0 + revisionWidth, y0 + height - 15, 0.18, TITLE_FRAME_LAYER);

            AddText(paperSpace, transaction, meta.Revision, x0 + 1, y0 + height - 22, 2.5, true);
            AddText(paperSpace, transaction, meta.Ngay, column1 + 1, y0 + height - 22, 2.5, false);
            AddMText(
                paperSpace,
                transaction,
                meta.GhiChu,
                column2 + 1,
                y0 + height - 17,
                revisionWidth - (column2 - x0) - 2,
                2.0);

            double nameBlockX = x0 + revisionWidth;
            double nameBlockWidth = width - revisionWidth;

            DrawLine(
                paperSpace,
                transaction,
                nameBlockX,
                y0 + height - 20,
                nameBlockX + nameBlockWidth,
                y0 + height - 20,
                0.25,
                TITLE_FRAME_LAYER);
            AddText(paperSpace, transaction, "TÊN DỰ ÁN:", nameBlockX + 2, y0 + height - 5, 2.0, false);
            AddText(
                paperSpace,
                transaction,
                (meta.ProjectName ?? string.Empty).ToUpperInvariant(),
                nameBlockX + 2,
                y0 + height - 12,
                3.5,
                true);

            DrawLine(
                paperSpace,
                transaction,
                nameBlockX,
                y0 + height - 38,
                nameBlockX + nameBlockWidth,
                y0 + height - 38,
                0.25,
                TITLE_FRAME_LAYER);
            AddText(paperSpace, transaction, "TÊN BẢN VẼ:", nameBlockX + 2, y0 + height - 23, 2.0, false);
            AddText(
                paperSpace,
                transaction,
                drawingTitle.ToUpperInvariant(),
                nameBlockX + 2,
                y0 + height - 31,
                3.0,
                true);

            double cellWidth = nameBlockWidth / 3;
            DrawLine(
                paperSpace,
                transaction,
                nameBlockX + cellWidth,
                y0,
                nameBlockX + cellWidth,
                y0 + height - 38,
                0.18,
                TITLE_FRAME_LAYER);
            DrawLine(
                paperSpace,
                transaction,
                nameBlockX + cellWidth * 2,
                y0,
                nameBlockX + cellWidth * 2,
                y0 + height - 38,
                0.18,
                TITLE_FRAME_LAYER);

            AddText(paperSpace, transaction, "SỐ BẢN VẼ", nameBlockX + 2, y0 + height - 42, 2.0, false);
            AddText(paperSpace, transaction, "TỶ LỆ", nameBlockX + cellWidth + 2, y0 + height - 42, 2.0, false);
            AddText(paperSpace, transaction, "NGÀY", nameBlockX + cellWidth * 2 + 2, y0 + height - 42, 2.0, false);

            AddText(paperSpace, transaction, meta.SoBanVe, nameBlockX + 2, y0 + height - 52, 2.5, true);
            AddText(paperSpace, transaction, meta.TyLe, nameBlockX + cellWidth + 2, y0 + height - 52, 2.5, true);
            AddText(
                paperSpace,
                transaction,
                meta.Ngay,
                nameBlockX + cellWidth * 2 + 2,
                y0 + height - 52,
                2.5,
                false);
        }

        private void ApplyRequestSettings(LayoutCreationRequest request)
        {
            MarginLeft = request.MarginLeft;
            MarginRight = request.MarginRight;
            MarginTop = request.MarginTop;
            MarginBot = request.MarginBot;
        }

        private static string BuildScaleValue(double scaleDenominator)
        {
            int denominator = (int)Math.Round(scaleDenominator);
            return $"1:{denominator}";
        }

        /// <summary>
        /// If the config is External but strip info is missing (StripSize=0),
        /// re-open the external DWG and run detection on the fly.
        /// Returns the original or hot-patched config.
        /// </summary>
        private static LayoutTitleBlockConfig EnsureStripDetected(LayoutTitleBlockConfig config, Database db)
        {
            // Always re-detect for External blocks — never trust the saved StripPosition/StripSize
            // since a previously wrong detection (e.g., Bottom instead of Right) would persist forever.
            if (config.Mode != LayoutTitleBlockMode.External
                || string.IsNullOrWhiteSpace(config.DwgPath)
                || !System.IO.File.Exists(config.DwgPath))
                return config;

            try
            {
                var sources = LayoutTitleBlockConfigStore.DiscoverSources(config.DwgPath);
                var match = sources.FirstOrDefault(s =>
                    string.Equals(s.Name, config.SourceName, System.StringComparison.OrdinalIgnoreCase));
                if (match != null && match.StripSize > 0)
                {
                    return config with
                    {
                        StripPosition = match.StripPosition,
                        StripSize = match.StripSize,
                        BlockWidth = match.Width,
                        BlockHeight = match.Height
                    };
                }
            }
            catch { }

            return config;
        }

        /// <summary>
        /// Removes the page-split suffix like " (PHẦN 1/2)" or " (PART 1/2)"
        /// from the view title text for use in the title block DrawingTitle field.
        /// The viewport label on paper retains the full string.
        /// </summary>
        private static string StripPageSuffix(string title)
        {
            if (string.IsNullOrWhiteSpace(title))
                return title;

            // Strip " (PHẦN ..." or " (PART ..."
            int phanIdx = title.IndexOf(" (PHẦN", StringComparison.OrdinalIgnoreCase);
            if (phanIdx < 0)
                phanIdx = title.IndexOf(" (PART", StringComparison.OrdinalIgnoreCase);

            return phanIdx >= 0
                ? title[..phanIdx].TrimEnd('-', ' ')
                : title.Trim();
        }

        /// <summary>
        /// Returns how much to subtract from usable area based on title block strip position.
        /// Bottom strip → reduce height. Right strip → reduce width.
        /// </summary>
        private static (double HeightReduction, double WidthReduction) GetStripReduction(LayoutTitleBlockConfig config)
        {
            const double MaxStripSize = 200;  // mm — strips wider/taller than this are likely full-frame
            const double MinStripSize = 20;   // mm — safety minimum

            if (config.Mode != LayoutTitleBlockMode.External || config.StripSize <= 0)
                return (TITLE_BLOCK_H, 0); // Built-in default: 55mm bottom

            double size = config.StripSize;
            if (size > MaxStripSize)
                return (TITLE_BLOCK_H, 0); // Full-frame fallback

            size = Math.Max(size, MinStripSize);

            return config.StripPosition == TitleBlockStripPosition.Right
                ? (0, size)    // Right strip: subtract from width only
                : (size, 0);  // Bottom strip: subtract from height only
        }

        private PaperGeometry BuildPaperGeometry(
            string paperSize,
            double titleBlockHeight = TITLE_BLOCK_H,
            double titleBlockRightWidth = 0)
        {
            var (paperWidth, paperHeight) = PaperSizes.TryGetValue(paperSize, out var paper)
                ? paper
                : (420.0, 297.0);

            double frameWidth = paperWidth - MarginLeft - MarginRight;
            double frameHeight = paperHeight - MarginTop - MarginBot;
            double viewportWidth = Math.Max(frameWidth - titleBlockRightWidth, 50);
            double viewportHeight = Math.Max(frameHeight - titleBlockHeight, 50);
            double viewportBottom = MarginBot + titleBlockHeight;
            double viewportLeft = MarginLeft;
            double viewportTop = viewportBottom + viewportHeight;

            return new PaperGeometry(
                paperWidth,
                paperHeight,
                frameWidth,
                frameHeight,
                viewportWidth,
                viewportHeight,
                viewportBottom,
                viewportLeft,
                viewportTop,
                new Point3d(MarginLeft + viewportWidth / 2, viewportBottom + viewportHeight / 2, 0));
        }

        internal static void ApplyPageSetup(Layout layout, string paperSize)
        {
            PlotStyleInstaller.EnsureInstalled(Application.DocumentManager.MdiActiveDocument);
            using var plotSettings = new PlotSettings(layout.ModelType);
            plotSettings.CopyFrom(layout);

            var validator = PlotSettingsValidator.Current;

            validator.SetPlotConfigurationName(plotSettings, DefaultPlotDevice, null);
            validator.RefreshLists(plotSettings);

            // Tìm canonical media name phù hợp từ danh sách plotter hỗ trợ
            string? matchedMedia = FindMediaName(plotSettings, validator, paperSize);
            if (matchedMedia != null)
            {
                validator.SetPlotConfigurationName(plotSettings, DefaultPlotDevice, matchedMedia);
            }
            else
            {
                // Fallback: thử set bằng tên phổ biến nhất
                try
                {
                    validator.SetPlotConfigurationName(plotSettings, DefaultPlotDevice,
                        $"ISO_full_bleed_{paperSize}_({PaperSizes[paperSize].H:F2}_x_{PaperSizes[paperSize].W:F2}_MM)");
                }
                catch
                {
                    // Giữ nguyên media mặc định của plotter
                }
            }

            validator.SetPlotPaperUnits(plotSettings, PlotPaperUnit.Millimeters);
            validator.SetPlotType(plotSettings, PlotType.Layout);
            validator.SetUseStandardScale(plotSettings, true);
            validator.SetStdScaleType(plotSettings, StdScaleType.ScaleToFit);
            validator.SetPlotRotation(plotSettings, PlotRotation.Degrees090);
            validator.SetZoomToPaperOnUpdate(plotSettings, true);
            validator.RefreshLists(plotSettings);

            string resolvedPlotStyle = PlotStyleInstaller.ResolvePreferredStyleName(
                validator.GetPlotStyleSheetList().Cast<string>(),
                PlotStyleInstaller.DefaultPlotStyleName);

            try
            {
                validator.SetCurrentStyleSheet(plotSettings, resolvedPlotStyle);
            }
            catch
            {
            }

            try
            {
                validator.SetPlotCentered(plotSettings, true);
            }
            catch
            {
                try
                {
                    validator.SetPlotOrigin(plotSettings, Point2d.Origin);
                }
                catch
                {
                }
            }

            layout.CopyFrom(plotSettings);
        }

        /// <summary>
        /// Duyệt danh sách canonical media names của plotter, 
        /// tìm khổ giấy phù hợp (ưu tiên "full bleed", fallback "ISO expand").
        /// </summary>
        private static string? FindMediaName(PlotSettings ps, PlotSettingsValidator validator, string paperSize)
        {
            var mediaList = validator.GetCanonicalMediaNameList(ps);
            string? bestMatch = null;
            string? fallbackMatch = null;

            foreach (string canonical in mediaList)
            {
                string locale = validator.GetLocaleMediaName(ps, canonical);
                // Bỏ qua các entry rỗng hoặc "None"
                if (string.IsNullOrWhiteSpace(locale) || locale == "None") continue;

                // Kiểm tra tên chứa đúng khổ giấy (e.g. "A3", "A2", "A1")
                bool matchesPaperSize = locale.Contains(paperSize, StringComparison.OrdinalIgnoreCase)
                                     || canonical.Contains(paperSize, StringComparison.OrdinalIgnoreCase);

                if (!matchesPaperSize) continue;

                // Ưu tiên "full bleed" (không margin) → chính xác nhất cho shopdrawing
                if (canonical.Contains("full_bleed", StringComparison.OrdinalIgnoreCase)
                    || locale.Contains("full bleed", StringComparison.OrdinalIgnoreCase))
                {
                    bestMatch = canonical;
                    break; // Perfect match
                }

                // Fallback: bất kỳ entry nào chứa tên khổ giấy
                fallbackMatch ??= canonical;
            }

            return bestMatch ?? fallbackMatch;
        }

        private void DrawSheetFrame(
            BlockTableRecord paperSpace,
            Transaction transaction,
            PaperGeometry geometry)
        {
            double x0 = MarginLeft;
            double y0 = MarginBot;
            double x1 = geometry.PaperWidth - MarginRight;
            double y1 = geometry.PaperHeight - MarginTop;

            DrawRect(paperSpace, transaction, x0, y0, x1, y1, 0.5, SHEET_FRAME_LAYER);
            DrawLine(paperSpace, transaction, x0, geometry.ViewportBottom, x1, geometry.ViewportBottom, 0.25, SHEET_FRAME_LAYER);
            DrawRect(paperSpace, transaction, x0, geometry.ViewportBottom, x1, y1, 0.25, SHEET_FRAME_LAYER);
        }

        public static void EnsureTitleLayers(Database db, Transaction transaction)
        {
            EnsureLayer(db, transaction, TITLE_LAYER, 7, LineWeight.LineWeight050, true);
            EnsureLayer(db, transaction, TITLE_FRAME_LAYER, 7, LineWeight.LineWeight035, true);
            EnsureLayer(db, transaction, TITLE_TEXT_LAYER, 7, LineWeight.LineWeight018, true);
            EnsureLayer(db, transaction, VIEWPORT_LAYER, 3, LineWeight.LineWeight018, false);
            EnsureLayer(db, transaction, SHEET_FRAME_LAYER, 7, LineWeight.LineWeight018, true);
            EnsureLayer(db, transaction, DMBD_LAYER, 7, LineWeight.ByLayer, true);
        }

        private static void EnsureLayer(
            Database db,
            Transaction transaction,
            string name,
            short colorAci,
            LineWeight lineWeight,
            bool isPlottable)
        {
            var layers = (LayerTable)transaction.GetObject(db.LayerTableId, OpenMode.ForRead);
            if (layers.Has(name))
            {
                var existing = (LayerTableRecord)transaction.GetObject(layers[name], OpenMode.ForRead);
                if (existing.IsPlottable != isPlottable || existing.LineWeight != lineWeight)
                {
                    existing.UpgradeOpen();
                    existing.IsPlottable = isPlottable;
                    existing.LineWeight = lineWeight;
                }

                return;
            }

            layers.UpgradeOpen();
            var layer = new LayerTableRecord
            {
                Name = name,
                Color = Autodesk.AutoCAD.Colors.Color.FromColorIndex(
                    Autodesk.AutoCAD.Colors.ColorMethod.ByAci,
                    colorAci),
                LineWeight = lineWeight,
                IsPlottable = isPlottable
            };
            layers.Add(layer);
            transaction.AddNewlyCreatedDBObject(layer, true);
        }

        private static void PreparePaperSpaceEnvironment(Editor editor)
        {
            TrySetSystemVariable("TILEMODE", 0);
            TrySetSystemVariable("PSLTSCALE", 1);
            TrySetSystemVariable("GRIDMODE", 0);
            TrySetSystemVariable("SNAPMODE", 0);
            TrySetSystemVariable("UCSICON", 0);

            try
            {
                editor.SwitchToPaperSpace();
            }
            catch
            {
            }
        }

        private static void TrySetSystemVariable(string name, object value)
        {
            try
            {
                Application.SetSystemVariable(name, value);
            }
            catch
            {
            }
        }

        private static void AddText(
            BlockTableRecord paperSpace,
            Transaction transaction,
            string text,
            double x,
            double y,
            double heightMm,
            bool isBold)
        {
            var textEntity = new DBText
            {
                TextString = text,
                Position = new Point3d(x, y, 0),
                Height = heightMm,
                Layer = TITLE_TEXT_LAYER,
                WidthFactor = isBold ? 0.85 : 1.0,
                TextStyleId = BlockManager.EnsureArialStyle(paperSpace.Database, transaction)
            };

            paperSpace.AppendEntity(textEntity);
            transaction.AddNewlyCreatedDBObject(textEntity, true);
        }

        private static void AddMText(
            BlockTableRecord paperSpace,
            Transaction transaction,
            string text,
            double x,
            double y,
            double width,
            double heightMm)
        {
            if (string.IsNullOrWhiteSpace(text))
                return;

            var mtext = new MText
            {
                Contents = text,
                Location = new Point3d(x, y, 0),
                Width = width,
                TextHeight = heightMm,
                Layer = TITLE_TEXT_LAYER,
                TextStyleId = BlockManager.EnsureArialStyle(paperSpace.Database, transaction),
                Attachment = AttachmentPoint.TopLeft
            };

            paperSpace.AppendEntity(mtext);
            transaction.AddNewlyCreatedDBObject(mtext, true);
        }

        internal static void DrawRect(
            BlockTableRecord paperSpace,
            Transaction transaction,
            double x0,
            double y0,
            double x1,
            double y1,
            double lineWeight,
            string layerName)
        {
            var polyline = new Polyline();
            polyline.AddVertexAt(0, new Point2d(x0, y0), 0, lineWeight, lineWeight);
            polyline.AddVertexAt(1, new Point2d(x1, y0), 0, lineWeight, lineWeight);
            polyline.AddVertexAt(2, new Point2d(x1, y1), 0, lineWeight, lineWeight);
            polyline.AddVertexAt(3, new Point2d(x0, y1), 0, lineWeight, lineWeight);
            polyline.Closed = true;
            polyline.Layer = layerName;
            paperSpace.AppendEntity(polyline);
            transaction.AddNewlyCreatedDBObject(polyline, true);
        }

        private static void DrawLine(
            BlockTableRecord paperSpace,
            Transaction transaction,
            double x0,
            double y0,
            double x1,
            double y1,
            double lineWeight,
            string layerName)
        {
            var polyline = new Polyline();
            polyline.AddVertexAt(0, new Point2d(x0, y0), 0, lineWeight, lineWeight);
            polyline.AddVertexAt(1, new Point2d(x1, y1), 0, lineWeight, lineWeight);
            polyline.Layer = layerName;
            paperSpace.AppendEntity(polyline);
            transaction.AddNewlyCreatedDBObject(polyline, true);
        }

        private sealed record PaperGeometry(
            double PaperWidth,
            double PaperHeight,
            double FrameWidth,
            double FrameHeight,
            double ViewportWidth,
            double ViewportHeight,
            double ViewportBottom,
            double ViewportLeft,
            double ViewportTop,
            Point3d ViewportCenter);

        public sealed record LayoutPlanItem(Extents3d PageExtents, string ViewTitleText, string LayoutName);
    }
}
