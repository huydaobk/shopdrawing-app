using System;
using System.Collections.Generic;
using System.Linq;
using ShopDrawing.Plugin.Data;
using ShopDrawing.Plugin.Models;
using ShopDrawing.Plugin.Modules.Accessories;

namespace ShopDrawing.Plugin.Core
{
    public class ShopdrawingBomCalculator
    {
        public class FactoryOrderRow
        {
            public string Spec { get; set; } = "";
            public double ThickMm { get; set; }
            public double WidthMm { get; set; }
            public double LengthMm { get; set; }
            public int Qty { get; set; }
            public double AreaM2 { get; set; }
            public string Note { get; set; } = "";
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
            var factoryGroups = bomRows
                .GroupBy(b => new { b.Spec, b.WidthMm, b.LengthMm, b.ThickMm })
                .OrderBy(g => g.Key.Spec)
                .ThenByDescending(g => g.Key.LengthMm)
                .ThenBy(g => g.Key.WidthMm);

            foreach (var g in factoryGroups)
            {
                int qty = g.Sum(x => x.Qty);
                double area = (g.Key.WidthMm * g.Key.LengthMm) / 1_000_000.0 * qty;
                string note = "";

                bool hasCut = g.Any(x => x.Status == "✂ CẮT" || x.Status == "CẮT");
                bool hasReused = g.Any(x => x.Status.Contains("TÁI") || x.Status.Contains("♻"));
                
                if (hasCut) note += "Cắt tại công trường";
                if (hasReused) note += (note.Length > 0 ? " + " : "") + "Tận dụng kho";

                var walls = g.Select(x => x.WallCode).Where(w => !string.IsNullOrEmpty(w)).Distinct();
                if (walls.Any()) note += (note.Length > 0 ? " | " : "") + string.Join(",", walls);

                report.FactoryOrders.Add(new FactoryOrderRow
                {
                    Spec = g.Key.Spec,
                    ThickMm = g.Key.ThickMm,
                    WidthMm = g.Key.WidthMm,
                    LengthMm = g.Key.LengthMm,
                    Qty = qty,
                    AreaM2 = area,
                    Note = note
                });
            }

            return report;
        }
    }
}
