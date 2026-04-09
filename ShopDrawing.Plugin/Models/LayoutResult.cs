using System.Collections.Generic;
using System.Linq;

namespace ShopDrawing.Plugin.Models
{
    public class LayoutResult
    {
        public List<Panel> FullPanels { get; set; } = new List<Panel>();
        public Panel? RemnantPanel { get; set; }   // null nếu vừa khít
        public WastePanel? SuggestedWaste { get; set; } // null nếu không có match
        public List<Panel> CutPanels { get; set; } = new List<Panel>();
        
        /// <summary>
        /// Hao hụt phát sinh do lỗ mở (chỉ dùng để thống kê và tính hao hụt).
        /// </summary>
        public List<(string PanelId, double WidthMm, double HeightMm)> OpeningWasteEntries { get; set; } = new();
        
        public List<Panel> AllPanels
        {
            get
            {
                var all = new List<Panel>(FullPanels);
                if (RemnantPanel != null) all.Add(RemnantPanel);
                all.AddRange(CutPanels);
                return all;
            }
        }

        public int TotalCount => AllPanels.Count;
    }
}
