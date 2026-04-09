using System.Collections.Generic;
using System.Linq;
using ShopDrawing.Plugin.Models;

namespace ShopDrawing.Plugin.Core
{
    public static class PanelIdGenerator
    {
        public static void AssignIds(List<Panel> panels, string wallCode)
        {
            // Tách tấm mới và tấm tái sử dụng
            var newPanels = panels.Where(p => !p.IsReused).ToList();
            var reusedPanels = panels.Where(p => p.IsReused).ToList();

            // 1. Nhóm tấm MỚI theo (Width, Length, Thick, Spec, JointLeft, JointRight)
            // Round dimensions về mm nguyên để tránh lỗi floating-point (5500.001 ≠ 5500.002)
            int nextNewSeq = 1;
            var newGroups = newPanels
                .GroupBy(p => new { 
                    W = (int)Math.Round(p.WidthMm,  0), 
                    L = (int)Math.Round(p.LengthMm, 0), 
                    p.ThickMm, 
                    p.Spec,
                    p.JointLeft,
                    p.JointRight
                })
                .OrderByDescending(g => g.Key.L)   // Tấm dài nhất là 01
                .ThenByDescending(g => g.Key.W);    // Cùng dài → rộng hơn trước

            foreach (var group in newGroups)
            {
                string id = $"{wallCode}-{nextNewSeq:D2}";
                foreach (var p in group)
                {
                    p.PanelId = id;
                }
                nextNewSeq++;
            }

            // 2. Tấm TÁI SỬ DỤNG - gán ID riêng biệt có prefix R, không nhóm
            int nextReusedSeq = 1;
            foreach (var p in reusedPanels)
            {
                p.PanelId = $"{wallCode}-R{nextReusedSeq:D2}";
                nextReusedSeq++;
            }
        }
    }
}
