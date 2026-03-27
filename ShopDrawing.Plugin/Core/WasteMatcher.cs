using System.Linq;
using System.Collections.Generic;
using ShopDrawing.Plugin.Data;
using ShopDrawing.Plugin.Models;
using Panel = ShopDrawing.Plugin.Models.Panel;

namespace ShopDrawing.Plugin.Core
{
    public enum MatchDirection { None, Direct, Flipped }

    public class WasteMatchResult
    {
        public WastePanel? Panel { get; set; }
        public MatchDirection Direction { get; set; } = MatchDirection.None;
    }

    public class WasteMatcher
    {
        private readonly WasteRepository _repo;
        public const double CUT_KERF_MM = 3.0; // Chiều dày lưỡi cắt panel (cố định)

        public WasteMatcher(WasteRepository repo)
        {
            _repo = repo;
        }

        /// <summary>
        /// Tìm tấm lẻ phù hợp nhất theo Spec + Dày + Rộng.
        /// Ngàm không là điều kiện bắt buộc — chỉ ưu tiên tấm có ngàm khớp.
        /// </summary>
        public WasteMatchResult FindBestMatchWithDirection(Panel remnant)
        {
            var candidates = _repo.FindMatchesBySpec(
                remnant.Spec, remnant.ThickMm, remnant.WidthMm, remnant.LengthMm);

            if (candidates.Count == 0)
                return new WasteMatchResult();

            // Ưu tiên 1: Tìm tấm có ngàm khớp trực tiếp
            foreach (var waste in candidates)
            {
                if (waste.JointLeft == remnant.JointLeft && waste.JointRight == remnant.JointRight)
                    return new WasteMatchResult { Panel = waste, Direction = MatchDirection.Direct };
            }

            // Ưu tiên 2: Tìm tấm lật thì khớp
            foreach (var waste in candidates)
            {
                if (waste.JointRight == remnant.JointLeft && waste.JointLeft == remnant.JointRight)
                    return new WasteMatchResult { Panel = waste, Direction = MatchDirection.Flipped };
            }

            // Ưu tiên 3: Không khớp chính xác → chọn hướng tiếp giáp TỐT NHẤT
            // Direct:  standard.Right = Male(+)   → cần remnant.Left = Female(-) tốt nhất
            //          remnant.Left = kho.JointLeft
            // Flipped: standard.Right = Female(-) → cần remnant.Left = Male(+) tốt nhất
            //          remnant.Left = kho.JointRight
            var best = candidates[0];
            int scoreD = AdjoinScore(best.JointLeft,  JointType.Male);   // Direct: std.Right=Male, rem.Left=kho.Left
            int scoreF = AdjoinScore(best.JointRight, JointType.Female); // Flipped: std.Right=Female, rem.Left=kho.Right
            return new WasteMatchResult
            {
                Panel = best,
                Direction = scoreF > scoreD ? MatchDirection.Flipped : MatchDirection.Direct
            };
        }

        /// <summary>
        /// (Legacy) Tìm match cũ — giữ để backward-compatible
        /// </summary>
        public WastePanel? FindBestMatch(Panel remnant)
        {
            var result = FindBestMatchWithDirection(remnant);
            return result.Panel;
        }

        /// <summary>
        /// Điểm tiếp giáp: remLeft là ngàm trái remnant, stdRight là ngàm phải tấm chuẩn kề.
        /// Interlock hoàn hảo (F+M hoặc M+F) = 3, Cut = 2, cùng loại = 1
        /// </summary>
        private static int AdjoinScore(JointType remLeft, JointType stdRight)
        {
            // Interlock hoàn hảo
            if ((remLeft == JointType.Female && stdRight == JointType.Male) ||
                (remLeft == JointType.Male   && stdRight == JointType.Female))
                return 3;
            // Một bên là Cut → chấp nhận được
            if (remLeft == JointType.Cut || stdRight == JointType.Cut)
                return 2;
            // Cùng loại (Male+Male, Female+Female) → worst
            return 1;
        }

        /// <summary>
        /// Đánh dấu tấm trong kho đã được sử dụng
        /// </summary>
        public void AcceptReuse(int wasteId)
        {
            _repo.MarkAsUsed(wasteId);
        }

        /// <summary>
        /// Tạo tấm leftover (phần CÒN LẠI) khi cắt tấm mới cho remnant.
        /// Remnant lấy bên TRÁI tấm factory → leftover là bên PHẢI.
        /// VD: Factory -/+, remnant cần -/0 → cắt trái → leftover = 0/+
        /// VD: Factory +/-, remnant cần +/0 → cắt trái → leftover = 0/-
        /// Quy tắc: leftover.Left = Cut (chỗ cắt), leftover.Right = factory.Right
        ///          factory.Right = remnant gốc là tấm cuối cùng, trước nó là tấm full.
        ///          Tấm full luôn là -/+, nên factory.Right = Male(+).
        ///          Nhưng nếu dãy bị flip thành +/-, factory.Right = Female(-).
        /// → Dùng ngàm PHẢI factory = ngàm ĐỐI DIỆN ngàm TRÁI remnant.
        ///   Vì: Factory left = Remnant left, Factory right = leftover right.
        /// </summary>
        public WastePanel? CreateLeftover(Panel remnant, double fullPanelWidth, string wallCode, string project)
        {
            double leftoverWidth = fullPanelWidth - remnant.WidthMm - CUT_KERF_MM;
            if (leftoverWidth < 50) return null; // quá nhỏ, bỏ

            // Factory panel joint: Left = remnant.JointLeft, Right = đối diện
            // Standard: remnant.Left = Female(-) → factory Right = Male(+)
            // Flipped:  remnant.Left = Male(+)   → factory Right = Female(-)
            JointType factoryRight = remnant.JointLeft == JointType.Female 
                ? JointType.Male 
                : (remnant.JointLeft == JointType.Male ? JointType.Female : JointType.Cut);

            return new WastePanel
            {
                PanelCode = $"{remnant.PanelId}-REM",
                WidthMm = leftoverWidth,
                LengthMm = remnant.LengthMm,
                ThickMm = remnant.ThickMm,
                PanelSpec = remnant.Spec,
                JointLeft = JointType.Cut,      // 0 — bên cắt (luôn là Cut)
                JointRight = factoryRight,       // Bên factory còn lại
                SourceWall = wallCode,
                Project = project,
                Status = "available"
            };
        }

        /// <summary>
        /// Lưu tấm leftover vào kho nếu đủ kích thước.
        /// </summary>
        public void SaveLeftover(WastePanel leftover)
        {
            if (leftover != null && leftover.WidthMm >= 50)
                _repo.AddPanel(leftover);
        }

        /// <summary>
        /// (Legacy) SaveRemnant — redirect sang CreateLeftover + SaveLeftover
        /// </summary>
        public void SaveRemnant(Panel remnant, string wallCode, string project)
        {
            var leftover = CreateLeftover(remnant, 1100, wallCode, project);
            if (leftover != null)
                SaveLeftover(leftover);
        }
    }
}
