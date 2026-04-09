using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.PlottingServices;
using Autodesk.AutoCAD.Publishing;

namespace ShopDrawing.Plugin.Core
{
    public class PdfExportOptions
    {
        public string OutputFilePath { get; set; } = string.Empty;
        public string PlotterName { get; set; } = "DWG To PDF.pc3";
        public string PlotStyleName { get; set; } = PlotStyleInstaller.DefaultPlotStyleName;
        public bool OpenAfterExport { get; set; } = true;
    }

    public class PdfExportEngine
    {
        public static List<string> GetAvailablePlotters()
        {
            var plotters = new List<string>();
            try
            {
                using var plotSettings = new PlotSettings(true);
                var validator = PlotSettingsValidator.Current;
                validator.RefreshLists(plotSettings);
                
                var deviceList = validator.GetPlotDeviceList();
                foreach (string? device in deviceList)
                {
                    if (string.IsNullOrWhiteSpace(device))
                        continue;

                    if (device.EndsWith(".pc3", StringComparison.OrdinalIgnoreCase))
                    {
                        plotters.Add(device);
                    }
                }
            }
            catch (System.Exception ex)
            {
                ShopDrawing.Plugin.Core.PluginLogger.Error("Suppressed exception in PdfExportEngine.cs", ex);
            }
            return plotters.Any() ? plotters : new List<string> { "DWG To PDF.pc3" };
        }

        public static List<string> GetAvailablePlotStyles()
        {
            var styles = new List<string>();
            try
            {
                PlotStyleInstaller.EnsureInstalled(Application.DocumentManager.MdiActiveDocument);
                using var plotSettings = new PlotSettings(true);
                var validator = PlotSettingsValidator.Current;
                validator.RefreshLists(plotSettings);
                
                var styleList = validator.GetPlotStyleSheetList();
                foreach (string? style in styleList)
                {
                    if (string.IsNullOrWhiteSpace(style))
                        continue;

                    if (style.EndsWith(".ctb", StringComparison.OrdinalIgnoreCase))
                    {
                        styles.Add(style);
                    }
                }
            }
            catch (System.Exception ex)
            {
                ShopDrawing.Plugin.Core.PluginLogger.Error("Suppressed exception in PdfExportEngine.cs", ex);
            }

            if (!styles.Any())
                return new List<string> { PlotStyleInstaller.DefaultPlotStyleName, PlotStyleInstaller.FallbackPlotStyleName };

            return styles
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(style => !style.Equals(PlotStyleInstaller.DefaultPlotStyleName, StringComparison.OrdinalIgnoreCase))
                .ThenBy(style => style, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public void ExportDrawingsToPdf(Document doc, PdfExportOptions options, Action<string, int, int> progressCallback)
        {
            var db = doc.Database;
            var layoutNames = GetNonEmptyLayouts(db);
            
            if (!layoutNames.Any())
            {
                throw new InvalidOperationException("Bản vẽ không có Layout nào hợp lệ (hoặc tất cả đều trống).");
            }

            short bgPlot = (short)Application.GetSystemVariable("BACKGROUNDPLOT");
            Application.SetSystemVariable("BACKGROUNDPLOT", 0);

            try
            {
                PlotUsingEngine(doc, layoutNames, options);
            }
            finally
            {
                Application.SetSystemVariable("BACKGROUNDPLOT", bgPlot);
            }
        }

        private void PlotUsingEngine(Document doc, List<string> layoutNames, PdfExportOptions options)
        {
            var db = doc.Database;
            PlotStyleInstaller.EnsureInstalled(doc);
            using var tr = db.TransactionManager.StartTransaction();
            
            var layoutDict = (DBDictionary)tr.GetObject(db.LayoutDictionaryId, OpenMode.ForRead);
            var plotConfigs = new List<PlotInfo>();

            foreach (var layoutName in layoutNames)
            {
                if (!layoutDict.Contains(layoutName)) continue;
                var layoutId = layoutDict.GetAt(layoutName);
                var layout = (Layout)tr.GetObject(layoutId, OpenMode.ForRead);

                // Modify plot settings for each layout
                // We need to upgrade open to change CTB and plotter temporarily? 
                // Better to create a PlotInfo and override.
                var pi = new PlotInfo();
                pi.Layout = layoutId;

                var psv = PlotSettingsValidator.Current;
                var ps = new PlotSettings(layout.ModelType);
                ps.CopyFrom(layout);

                string currentMedia = layout.CanonicalMediaName;
                try
                {
                    psv.SetPlotConfigurationName(ps, options.PlotterName, currentMedia);
                }
                catch (System.Exception ex) { ShopDrawing.Plugin.Core.PluginLogger.Warn("Suppressed exception: " + ex.Message); 
                    psv.SetPlotConfigurationName(ps, options.PlotterName, null);
                }
                
                psv.RefreshLists(ps);

                string resolvedPlotStyle = PlotStyleInstaller.ResolvePreferredStyleName(
                    psv.GetPlotStyleSheetList().Cast<string>(),
                    options.PlotStyleName);

                try { psv.SetCurrentStyleSheet(ps, resolvedPlotStyle); } catch (System.Exception ex)
            {
                ShopDrawing.Plugin.Core.PluginLogger.Error("Suppressed exception in PdfExportEngine.cs", ex);
            }

                pi.OverrideSettings = ps;
                var piv = new PlotInfoValidator();
                piv.MediaMatchingPolicy = MatchingPolicy.MatchEnabled;
                piv.Validate(pi);

                plotConfigs.Add(pi);
            }

            if (!plotConfigs.Any()) return;

            using var pe = PlotFactory.CreatePublishEngine();
            using var ppd = new PlotProgressDialog(false, plotConfigs.Count, true);
            
            ppd.set_PlotMsgString(PlotMessageIndex.DialogTitle, "Exporting PDF (" + plotConfigs.Count + " trang)");
            ppd.OnBeginPlot();
            ppd.IsVisible = true;
            
            pe.BeginPlot(ppd, null);
            pe.BeginDocument(plotConfigs[0], doc.Name, null, 1, true, options.OutputFilePath);

            for (int i = 0; i < plotConfigs.Count; i++)
            {
                if (ppd.IsPlotCancelled) break;

                var pi = plotConfigs[i];
                var ppi = new PlotPageInfo();
                
                ppd.OnBeginSheet();
                ppd.LowerSheetProgressRange = 0;
                ppd.UpperSheetProgressRange = 100;
                ppd.SheetProgressPos = 0;

                pe.BeginPage(ppi, pi, true, null);
                pe.BeginGenerateGraphics(null);
                pe.EndGenerateGraphics(null);
                pe.EndPage(null);

                ppd.SheetProgressPos = 100;
                ppd.OnEndSheet();
            }

            pe.EndDocument(null);
            ppd.PlotProgressPos = 100;
            ppd.OnEndPlot();
            pe.EndPlot(null);

            tr.Commit();
        }

        private List<string> GetNonEmptyLayouts(Database db)
        {
            var validLayouts = new List<string>();
            using var tr = db.TransactionManager.StartTransaction();
            var layoutDict = (DBDictionary)tr.GetObject(db.LayoutDictionaryId, OpenMode.ForRead);
            
            // Get all layout names, sort by TabOrder
            var layoutPairs = new List<Tuple<string, int>>();
            foreach (var entry in layoutDict)
            {
                if (entry.Key.Equals("Model", StringComparison.OrdinalIgnoreCase)) continue;
                
                var layout = (Layout)tr.GetObject(entry.Value, OpenMode.ForRead);
                layoutPairs.Add(new Tuple<string, int>(layout.LayoutName, layout.TabOrder));
            }
            
            // Sort by tab order
            layoutPairs.Sort((a, b) => a.Item2.CompareTo(b.Item2));

            // Check if they are empty
            foreach (var pair in layoutPairs)
            {
                if (IsLayoutNonEmpty(db, tr, pair.Item1))
                {
                    validLayouts.Add(pair.Item1);
                }
            }
            
            tr.Commit();
            return validLayouts;
        }

        private bool IsLayoutNonEmpty(Database db, Transaction tr, string layoutName)
        {
            var layoutDict = (DBDictionary)tr.GetObject(db.LayoutDictionaryId, OpenMode.ForRead);
            if (!layoutDict.Contains(layoutName)) return false;
            
            var layout = (Layout)tr.GetObject(layoutDict.GetAt(layoutName), OpenMode.ForRead);
            var btr = (BlockTableRecord)tr.GetObject(layout.BlockTableRecordId, OpenMode.ForRead);
            
            int entityCount = 0;
            foreach (ObjectId id in btr)
            {
                var ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                // Exclude the layout's own paper space viewport
                if (ent is Viewport vp && vp.Number == 1) continue;
                entityCount++;
            }
            
            return entityCount > 0;
        }
    }
}
