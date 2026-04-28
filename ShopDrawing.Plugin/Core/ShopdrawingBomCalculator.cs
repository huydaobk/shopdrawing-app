using System;
using System.Collections.Generic;
using System.Linq;
using ShopDrawing.Plugin.Commands;
using ShopDrawing.Plugin.Data;
using ShopDrawing.Plugin.Models;
using ShopDrawing.Plugin.Modules.Accessories;

namespace ShopDrawing.Plugin.Core
{
    public class ShopdrawingBomCalculator
    {
        public class FactoryOrderRow
        {
            public string Priority { get; set; } = "";
            public string PanelIds { get; set; } = "";
            public string Spec { get; set; } = "";
            public double ThickMm { get; set; }
            public double WidthMm { get; set; }
            public double LengthMm { get; set; }
            public int Qty { get; set; }
            public double AreaM2 { get; set; }
            public string Note { get; set; } = "";
            public string Level { get; set; } = "";
        }

        public class ProjectSummary
        {
            public int TotalPanelQty { get; set; }
            public double TotalPanelArea { get; set; }
            public double DiscardedArea { get; set; }
            public double AvailableArea { get; set; }
            public double WastePercent { get; set; }
            public double StepArea { get; set; }
            public double OpenArea { get; set; }
            public double TrimArea { get; set; }
            public double RemArea { get; set; }
            public int AvailableQty { get; set; }
        }

        public class ShopdrawingReport
        {
            public List<BomRow> BomRows { get; } = new();
            public List<WastePanel> WastePanels { get; } = new();
            public ProjectSummary Summary { get; } = new();
            public List<FactoryOrderRow> FactoryOrders { get; } = new();
            public List<ShopdrawingAccessorySummaryRow> AccessoryRows { get; } = new();
        }

        public ShopdrawingReport CalculateReport(
            List<BomRow> bomRows, 
            List<WastePanel> wastePanels, 
            List<ShopdrawingAccessorySummaryRow> accessoryRows)
        {
            var report = new ShopdrawingReport();
            report.BomRows.AddRange(bomRows);
            report.WastePanels.AddRange(wastePanels);
            report.AccessoryRows.AddRange(accessoryRows);

            // Calculate Project Summary
            report.Summary.TotalPanelQty = bomRows.Sum(r => r.Qty);
            report.Summary.TotalPanelArea = bomRows.Sum(r => r.AreaM2 * r.Qty);
            
            report.Summary.DiscardedArea = wastePanels
                .Where(w => w.Status == "discarded")
                .Sum(w => (w.WidthMm * w.LengthMm) / 1_000_000.0);
                
            report.Summary.AvailableArea = wastePanels
                .Where(w => w.Status == "available")
                .Sum(w => (w.WidthMm * w.LengthMm) / 1_000_000.0);
                
            report.Summary.AvailableQty = wastePanels.Count(w => w.Status == "available");
            
            report.Summary.WastePercent = report.Summary.TotalPanelArea > 0 
                ? (report.Summary.DiscardedArea / report.Summary.TotalPanelArea) * 100.0 
                : 0;

            report.Summary.StepArea = wastePanels
                .Where(w => w.SourceType == "STEP")
                .Sum(w => (w.WidthMm * w.LengthMm) / 1_000_000.0);
                
            report.Summary.OpenArea = wastePanels
                .Where(w => w.SourceType == "OPEN")
                .Sum(w => (w.WidthMm * w.LengthMm) / 1_000_000.0);
                
            report.Summary.TrimArea = wastePanels
                .Where(w => w.SourceType == "TRIM")
                .Sum(w => (w.WidthMm * w.LengthMm) / 1_000_000.0);
                
            report.Summary.RemArea = wastePanels
                .Where(w => w.SourceType == "REM")
                .Sum(w => (w.WidthMm * w.LengthMm) / 1_000_000.0);

            // Calculate Factory Orders
            var factorySource = bomRows
                .Where(b => !b.Status.Contains("TÁI") && !b.Status.Contains("♻"))
                .ToList();

            var standardWidths = factorySource
                .GroupBy(r => r.WallCode ?? "")
                .ToDictionary(g => g.Key, g => g.Max(r => r.WidthMm));

            var factoryGroups = factorySource
                .Select(r => {
                    double w = r.WidthMm;
                    string wc = r.WallCode ?? "";
                    if (standardWidths.ContainsKey(wc) && w < standardWidths[wc])
                    {
                        w = standardWidths[wc];
                    }
                    return new { r.Id, r.Spec, WidthMm = w, r.LengthMm, r.ThickMm, r.Qty, r.Status, r.WallCode, r.Level };
                })
                .GroupBy(b => new { b.Id, b.Spec, b.WidthMm, b.LengthMm, b.ThickMm, b.Level })
                .OrderBy(g => g.Key.Spec)
                .ThenByDescending(g => g.Key.LengthMm)
                .ThenBy(g => g.Key.Id);

            string defaultSpec = ShopDrawingCommands.DefaultSpec;
            if (string.IsNullOrEmpty(defaultSpec))
            {
                var allSpecs = ShopDrawingCommands.SpecManager?.GetAll();
                if (allSpecs != null && allSpecs.Count > 0)
                    defaultSpec = allSpecs[0].Key;
            }

            foreach (var g in factoryGroups)
            {
                int qty = g.Sum(x => x.Qty);
                double area = (g.Key.WidthMm * g.Key.LengthMm) / 1_000_000.0 * qty;
                string note = "";

                bool hasCut = g.Any(x => !string.IsNullOrEmpty(x.Status) && (x.Status.Contains("CẮT") || x.Status.Contains("✂")));
                if (hasCut) note += "Cắt tại công trường";

                var walls = g.Select(x => x.WallCode).Where(w => !string.IsNullOrEmpty(w)).Distinct();
                if (walls.Any()) note += (note.Length > 0 ? " | " : "") + string.Join(",", walls);

                string finalSpec = string.IsNullOrWhiteSpace(g.Key.Spec) ? defaultSpec : g.Key.Spec;

                string firstPanelId = g.Key.Id?.Split(',').FirstOrDefault()?.Trim() ?? "";
                string batchNo = "";
                var allBatches = ShopDrawingCommands.WasteRepo?.GetAllBatches();
                if (allBatches != null && allBatches.TryGetValue(firstPanelId, out int bNo))
                {
                    if (bNo > 0) batchNo = bNo.ToString();
                }

                report.FactoryOrders.Add(new FactoryOrderRow
                {
                    Priority = batchNo,
                    PanelIds = g.Key.Id,
                    Spec = finalSpec,
                    ThickMm = g.Key.ThickMm,
                    WidthMm = g.Key.WidthMm,
                    LengthMm = g.Key.LengthMm,
                    Level = g.Key.Level,
                    Qty = qty,
                    AreaM2 = area,
                    Note = note
                });
            }

            return report;
        }
    }
}
