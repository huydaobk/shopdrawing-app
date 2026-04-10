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

        // Strict mode: reject unsafe fallbacks to keep PDF output technically consistent.
        public bool StrictTechnicalStandard { get; set; } = true;

        // Require user viewports in layouts (Number > 1) to be locked before export.
        public bool RequireLockedViewports { get; set; } = true;
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
                    {
                        continue;
                    }

                    if (device.EndsWith(".pc3", StringComparison.OrdinalIgnoreCase))
                    {
                        plotters.Add(device);
                    }
                }
            }
            catch (Exception ex)
            {
                PluginLogger.Error("Suppressed exception in PdfExportEngine.cs", ex);
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
                    {
                        continue;
                    }

                    if (style.EndsWith(".ctb", StringComparison.OrdinalIgnoreCase))
                    {
                        styles.Add(style);
                    }
                }
            }
            catch (Exception ex)
            {
                PluginLogger.Error("Suppressed exception in PdfExportEngine.cs", ex);
            }

            if (!styles.Any())
            {
                return new List<string> { PlotStyleInstaller.DefaultPlotStyleName, PlotStyleInstaller.FallbackPlotStyleName };
            }

            return styles
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(style => !style.Equals(PlotStyleInstaller.DefaultPlotStyleName, StringComparison.OrdinalIgnoreCase))
                .ThenBy(style => style, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public void ExportDrawingsToPdf(Document doc, PdfExportOptions options, Action<string, int, int>? progressCallback)
        {
            ValidateOptions(options);

            var db = doc.Database;
            var layoutNames = GetExportableLayouts(db, options.RequireLockedViewports, options.StrictTechnicalStandard);
            if (!layoutNames.Any())
            {
                throw new InvalidOperationException("Bản vẽ không có Layout nào hợp lệ để xuất PDF.");
            }

            var styleInstalled = PlotStyleInstaller.EnsureInstalled(doc);
            if (options.StrictTechnicalStandard && !styleInstalled)
            {
                throw new InvalidOperationException("Không thể cài đặt SD_Black.ctb. Chế độ strict yêu cầu plot style chuẩn.");
            }

            if (options.StrictTechnicalStandard &&
                !PlotStyleInstaller.IsInstalledStyleSynced(out var styleSyncIssue))
            {
                throw new InvalidOperationException(
                    $"Plot style chuẩn chưa đồng bộ: {styleSyncIssue}. Vui lòng đồng bộ SD_Black.ctb trước khi xuất.");
            }

            ValidateLayerStandards(db, options.StrictTechnicalStandard);

            var outputPlan = PrepareOutputPath(options.OutputFilePath);
            short bgPlot = (short)Application.GetSystemVariable("BACKGROUNDPLOT");
            Application.SetSystemVariable("BACKGROUNDPLOT", 0);

            try
            {
                progressCallback?.Invoke("Đang chuẩn bị danh sách layout để xuất PDF...", 0, layoutNames.Count);
                PlotUsingEngine(doc, layoutNames, options, outputPlan.WorkingFilePath, progressCallback);
                FinalizeOutput(outputPlan);
            }
            catch
            {
                TryDeleteFile(outputPlan.WorkingFilePath);
                throw;
            }
            finally
            {
                Application.SetSystemVariable("BACKGROUNDPLOT", bgPlot);
            }
        }

        private void PlotUsingEngine(
            Document doc,
            List<string> layoutNames,
            PdfExportOptions options,
            string workingFilePath,
            Action<string, int, int>? progressCallback)
        {
            var db = doc.Database;
            using var tr = db.TransactionManager.StartTransaction();

            var layoutDict = (DBDictionary)tr.GetObject(db.LayoutDictionaryId, OpenMode.ForRead);
            var psv = PlotSettingsValidator.Current;
            var resolvedPlotter = ResolvePlotter(psv, options.PlotterName, options.StrictTechnicalStandard);
            var plotConfigs = new List<(string LayoutName, PlotInfo PlotInfo)>();

            foreach (var layoutName in layoutNames)
            {
                if (!layoutDict.Contains(layoutName))
                {
                    continue;
                }

                var layoutId = layoutDict.GetAt(layoutName);
                var layout = (Layout)tr.GetObject(layoutId, OpenMode.ForRead);
                var pi = new PlotInfo { Layout = layoutId };

                var ps = new PlotSettings(layout.ModelType);
                ps.CopyFrom(layout);

                ApplyPlotConfiguration(psv, ps, resolvedPlotter, layout, options.StrictTechnicalStandard);
                psv.RefreshLists(ps);

                var styleList = psv.GetPlotStyleSheetList().Cast<string>();
                var resolvedStyle = ResolvePlotStyle(styleList, options.PlotStyleName, options.StrictTechnicalStandard, layoutName);
                psv.SetCurrentStyleSheet(ps, resolvedStyle);

                pi.OverrideSettings = ps;
                var piv = new PlotInfoValidator
                {
                    MediaMatchingPolicy = MatchingPolicy.MatchEnabled
                };
                piv.Validate(pi);

                plotConfigs.Add((layoutName, pi));
            }

            if (!plotConfigs.Any())
            {
                throw new InvalidOperationException("Không có layout hợp lệ sau khi chạy preflight.");
            }

            using var pe = PlotFactory.CreatePublishEngine();
            using var ppd = new PlotProgressDialog(false, plotConfigs.Count, true);
            ppd.set_PlotMsgString(PlotMessageIndex.DialogTitle, "Exporting PDF (" + plotConfigs.Count + " trang)");
            ppd.OnBeginPlot();
            ppd.IsVisible = true;

            pe.BeginPlot(ppd, null);
            pe.BeginDocument(plotConfigs[0].PlotInfo, doc.Name, null, 1, true, workingFilePath);

            var cancelled = false;
            for (var i = 0; i < plotConfigs.Count; i++)
            {
                if (ppd.IsPlotCancelled)
                {
                    cancelled = true;
                    break;
                }

                var (layoutName, pi) = plotConfigs[i];
                var ppi = new PlotPageInfo();
                progressCallback?.Invoke(BuildProgressMessage(layoutName, i + 1, plotConfigs.Count), i + 1, plotConfigs.Count);

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

            if (cancelled)
            {
                throw new OperationCanceledException("Đã hủy xuất PDF.");
            }
        }

        private static void ValidateOptions(PdfExportOptions options)
        {
            if (string.IsNullOrWhiteSpace(options.OutputFilePath))
            {
                throw new ArgumentException("OutputFilePath không hợp lệ.", nameof(options.OutputFilePath));
            }

            if (!options.OutputFilePath.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("OutputFilePath phải có đuôi .pdf.", nameof(options.OutputFilePath));
            }

            if (options.StrictTechnicalStandard &&
                !string.Equals(options.PlotStyleName, PlotStyleInstaller.DefaultPlotStyleName, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException(
                    $"StrictTechnicalStandard yêu cầu PlotStyleName='{PlotStyleInstaller.DefaultPlotStyleName}'.",
                    nameof(options.PlotStyleName));
            }
        }

        private static void ValidateLayerStandards(Database db, bool strict)
        {
            var expectedLayers = ShopDrawingLayerStandardProfile.GetExpectedLayers();
            if (expectedLayers.Count == 0)
            {
                return;
            }

            var mismatches = new List<string>();
            using var tr = db.TransactionManager.StartTransaction();
            var layerTable = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);

            foreach (var expected in expectedLayers)
            {
                if (!layerTable.Has(expected.Key))
                {
                    continue;
                }

                var layer = (LayerTableRecord)tr.GetObject(layerTable[expected.Key], OpenMode.ForRead);
                var profile = expected.Value;
                var actualColorAci = layer.Color.ColorIndex;

                var colorMismatch = actualColorAci != profile.ColorAci;
                var lineWeightMismatch = layer.LineWeight != profile.LineWeight;
                var plottableMismatch = layer.IsPlottable != profile.IsPlottable;
                if (!colorMismatch && !lineWeightMismatch && !plottableMismatch)
                {
                    continue;
                }

                mismatches.Add(
                    $"{expected.Key}[ACI:{actualColorAci}/{profile.ColorAci},LW:{layer.LineWeight}/{profile.LineWeight},Plot:{layer.IsPlottable}/{profile.IsPlottable}]");
            }

            tr.Commit();
            if (!mismatches.Any())
            {
                return;
            }

            const int maxItems = 10;
            var summary = string.Join("; ", mismatches.Take(maxItems));
            if (mismatches.Count > maxItems)
            {
                summary += $" (+{mismatches.Count - maxItems} more)";
            }

            var message = "ShopDrawing layer profile mismatch: " + summary;
            if (strict)
            {
                throw new InvalidOperationException(
                    message + ". Chế độ strict yêu cầu layer đúng chuẩn trước khi export PDF.");
            }

            PluginLogger.Warn(message);
        }

        private static string ResolvePlotter(PlotSettingsValidator psv, string requestedPlotter, bool strict)
        {
            using var probe = new PlotSettings(true);
            psv.RefreshLists(probe);

            var plotters = psv.GetPlotDeviceList()
                .Cast<string>()
                .Where(d => d.EndsWith(".pc3", StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (plotters.Any(p => p.Equals(requestedPlotter, StringComparison.OrdinalIgnoreCase)))
            {
                return requestedPlotter;
            }

            if (strict)
            {
                throw new InvalidOperationException($"Không tìm thấy plotter '{requestedPlotter}' trong hệ thống.");
            }

            var fallback = plotters.FirstOrDefault(p => p.Equals("DWG To PDF.pc3", StringComparison.OrdinalIgnoreCase))
                ?? plotters.FirstOrDefault();

            if (!string.IsNullOrWhiteSpace(fallback))
            {
                PluginLogger.Warn($"Plotter '{requestedPlotter}' không tồn tại. Fallback sang '{fallback}'.");
                return fallback;
            }

            return requestedPlotter;
        }

        private static void ApplyPlotConfiguration(
            PlotSettingsValidator psv,
            PlotSettings ps,
            string resolvedPlotter,
            Layout layout,
            bool strict)
        {
            var currentMedia = layout.CanonicalMediaName;
            try
            {
                psv.SetPlotConfigurationName(ps, resolvedPlotter, currentMedia);
            }
            catch (Exception ex)
            {
                if (strict)
                {
                    throw new InvalidOperationException(
                        $"Layout '{layout.LayoutName}' không map được media '{currentMedia}' với plotter '{resolvedPlotter}'.",
                        ex);
                }

                PluginLogger.Warn(
                    $"Layout '{layout.LayoutName}' fallback media do lỗi '{ex.Message}'. Plotter '{resolvedPlotter}'.");
                psv.SetPlotConfigurationName(ps, resolvedPlotter, null);
            }
        }

        private static string ResolvePlotStyle(
            IEnumerable<string> availableStyles,
            string requestedStyle,
            bool strict,
            string layoutName)
        {
            var styles = availableStyles.ToList();
            var resolved = PlotStyleInstaller.ResolvePreferredStyleName(styles, requestedStyle);

            if (strict && !styles.Any(s => s.Equals(requestedStyle, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException(
                    $"Layout '{layoutName}': thiếu plot style '{requestedStyle}'. Chế độ strict không cho fallback.");
            }

            if (!resolved.Equals(requestedStyle, StringComparison.OrdinalIgnoreCase))
            {
                PluginLogger.Warn(
                    $"Layout '{layoutName}' fallback plot style từ '{requestedStyle}' sang '{resolved}'.");
            }

            return resolved;
        }

        private List<string> GetExportableLayouts(Database db, bool requireLockedViewports, bool strict)
        {
            var validLayouts = new List<(string Name, int TabOrder)>();
            using var tr = db.TransactionManager.StartTransaction();
            var layoutDict = (DBDictionary)tr.GetObject(db.LayoutDictionaryId, OpenMode.ForRead);

            foreach (var entry in layoutDict)
            {
                if (entry.Key.Equals("Model", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var layout = (Layout)tr.GetObject(entry.Value, OpenMode.ForRead);
                if (!HasExportableContent(layout, tr, requireLockedViewports, strict))
                {
                    continue;
                }

                validLayouts.Add((layout.LayoutName, layout.TabOrder));
            }

            tr.Commit();
            return validLayouts
                .OrderBy(x => x.TabOrder)
                .Select(x => x.Name)
                .ToList();
        }

        private static bool HasExportableContent(Layout layout, Transaction tr, bool requireLockedViewports, bool strict)
        {
            var btr = (BlockTableRecord)tr.GetObject(layout.BlockTableRecordId, OpenMode.ForRead);
            var hasContent = false;

            foreach (ObjectId id in btr)
            {
                if (!(tr.GetObject(id, OpenMode.ForRead) is Entity ent))
                {
                    continue;
                }

                if (ent is Viewport viewport)
                {
                    if (viewport.Number == 1)
                    {
                        continue;
                    }

                    if (requireLockedViewports && !viewport.Locked)
                    {
                        var message = $"Layout '{layout.LayoutName}' có viewport chưa lock. Vui lòng lock viewport trước khi export.";
                        if (strict)
                        {
                            throw new InvalidOperationException(message);
                        }

                        PluginLogger.Warn(message);
                    }

                    hasContent = true;
                    continue;
                }

                hasContent = true;
            }

            return hasContent;
        }

        private static ExportOutputPlan PrepareOutputPath(string outputFilePath)
        {
            var finalPath = Path.GetFullPath(outputFilePath);
            var directory = Path.GetDirectoryName(finalPath);
            if (string.IsNullOrWhiteSpace(directory))
            {
                throw new InvalidOperationException("Đường dẫn thư mục output không hợp lệ.");
            }

            Directory.CreateDirectory(directory);
            var tempFile = Path.Combine(directory, $".sd_export_{Guid.NewGuid():N}.pdf");
            return new ExportOutputPlan(finalPath, tempFile);
        }

        private static void FinalizeOutput(ExportOutputPlan plan)
        {
            if (!File.Exists(plan.WorkingFilePath))
            {
                throw new FileNotFoundException("Không tìm thấy file PDF tạm sau khi plot.", plan.WorkingFilePath);
            }

            if (!File.Exists(plan.FinalFilePath))
            {
                File.Move(plan.WorkingFilePath, plan.FinalFilePath);
                return;
            }

            try
            {
                File.Replace(plan.WorkingFilePath, plan.FinalFilePath, null, true);
            }
            catch (Exception)
            {
                File.Copy(plan.WorkingFilePath, plan.FinalFilePath, true);
                TryDeleteFile(plan.WorkingFilePath);
            }
        }

        private static string BuildProgressMessage(string layoutName, int currentSheet, int totalSheets)
        {
            return $"Đang xuất layout {currentSheet}/{totalSheets}: {layoutName}";
        }

        private static void TryDeleteFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return;
            }

            try
            {
                File.Delete(path);
            }
            catch (Exception ex)
            {
                PluginLogger.Warn("Không thể xóa file tạm: " + ex.Message);
            }
        }

        private readonly struct ExportOutputPlan
        {
            public ExportOutputPlan(string finalFilePath, string workingFilePath)
            {
                FinalFilePath = finalFilePath;
                WorkingFilePath = workingFilePath;
            }

            public string FinalFilePath { get; }

            public string WorkingFilePath { get; }
        }
    }
}
