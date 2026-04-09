using System.Collections.Generic;
using System.Linq;
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

        public const double CUT_KERF_MM = 3.0;

        public WasteMatcher(WasteRepository repo)
        {
            _repo = repo;
        }

        public WasteMatchResult FindBestMatchWithDirection(Panel remnant)
        {
            List<WastePanel> candidates = _repo.FindMatchesBySpec(
                remnant.Spec,
                remnant.ThickMm,
                remnant.WidthMm,
                remnant.LengthMm);

            if (candidates.Count == 0)
            {
                return new WasteMatchResult();
            }

            foreach (WastePanel waste in candidates)
            {
                if (waste.JointLeft == remnant.JointLeft && waste.JointRight == remnant.JointRight)
                {
                    return new WasteMatchResult { Panel = waste, Direction = MatchDirection.Direct };
                }
            }

            foreach (WastePanel waste in candidates)
            {
                if (waste.JointRight == remnant.JointLeft && waste.JointLeft == remnant.JointRight)
                {
                    return new WasteMatchResult { Panel = waste, Direction = MatchDirection.Flipped };
                }
            }

            WastePanel best = candidates[0];
            int scoreD = AdjoinScore(best.JointLeft, JointType.Male);
            int scoreF = AdjoinScore(best.JointRight, JointType.Female);

            return new WasteMatchResult
            {
                Panel = best,
                Direction = scoreF > scoreD ? MatchDirection.Flipped : MatchDirection.Direct
            };
        }

        public WastePanel? FindBestMatch(Panel remnant)
        {
            WasteMatchResult result = FindBestMatchWithDirection(remnant);
            return result.Panel;
        }

        private static int AdjoinScore(JointType remLeft, JointType stdRight)
        {
            if ((remLeft == JointType.Female && stdRight == JointType.Male) ||
                (remLeft == JointType.Male && stdRight == JointType.Female))
            {
                return 3;
            }

            if (remLeft == JointType.Cut || stdRight == JointType.Cut)
            {
                return 2;
            }

            return 1;
        }

        public void AcceptReuse(int wasteId)
        {
            _repo.MarkAsUsed(wasteId);
        }

        public void SaveReuseLeftover(WastePanel leftover)
        {
            _repo.UpdatePanel(leftover);
        }

        public WastePanel? CreateReuseLeftover(WastePanel sourcePanel, Panel usedPanel, MatchDirection direction)
        {
            double leftoverWidth = sourcePanel.WidthMm - usedPanel.WidthMm - CUT_KERF_MM;
            if (leftoverWidth < 50)
            {
                return null;
            }

            (JointType Left, JointType Right) orientedSourceJoints = direction == MatchDirection.Flipped
                ? (sourcePanel.JointRight, sourcePanel.JointLeft)
                : (sourcePanel.JointLeft, sourcePanel.JointRight);

            (JointType Left, JointType Right) orientedLeftoverJoints = BuildReuseLeftoverJoints(
                orientedSourceJoints.Left,
                orientedSourceJoints.Right,
                usedPanel.JointLeft,
                usedPanel.JointRight);

            (JointType Left, JointType Right) storedLeftoverJoints = direction == MatchDirection.Flipped
                ? (orientedLeftoverJoints.Right, orientedLeftoverJoints.Left)
                : orientedLeftoverJoints;

            return new WastePanel
            {
                Id = sourcePanel.Id,
                PanelCode = sourcePanel.PanelCode,
                WidthMm = leftoverWidth,
                LengthMm = sourcePanel.LengthMm,
                ThickMm = sourcePanel.ThickMm,
                PanelSpec = sourcePanel.PanelSpec,
                JointLeft = storedLeftoverJoints.Left,
                JointRight = storedLeftoverJoints.Right,
                SourceWall = sourcePanel.SourceWall,
                Project = sourcePanel.Project,
                Status = "available",
                SourceType = sourcePanel.SourceType,
                SourcePanelX = sourcePanel.SourcePanelX,
                SourcePanelY = sourcePanel.SourcePanelY
            };
        }

        public WastePanel? CreateLeftover(Panel remnant, double fullPanelWidth, string wallCode, string project)
        {
            double leftoverWidth = fullPanelWidth - remnant.WidthMm - CUT_KERF_MM;
            if (leftoverWidth < 50)
            {
                return null;
            }

            JointType leftoverLeft;
            JointType leftoverRight;

            if (remnant.JointRight == JointType.Cut)
            {
                leftoverLeft = JointType.Cut;
                leftoverRight = JointType.Male;
            }
            else if (remnant.JointLeft == JointType.Cut)
            {
                leftoverLeft = JointType.Female;
                leftoverRight = JointType.Cut;
            }
            else
            {
                return null;
            }

            return new WastePanel
            {
                PanelCode = $"{remnant.PanelId}-REM",
                WidthMm = leftoverWidth,
                LengthMm = remnant.LengthMm,
                ThickMm = remnant.ThickMm,
                PanelSpec = remnant.Spec,
                JointLeft = leftoverLeft,
                JointRight = leftoverRight,
                SourceWall = wallCode,
                Project = project,
                Status = "available",
                SourcePanelX = remnant.X,
                SourcePanelY = remnant.Y
            };
        }

        private static (JointType Left, JointType Right) BuildReuseLeftoverJoints(
            JointType stockLeft,
            JointType stockRight,
            JointType usedLeft,
            JointType usedRight)
        {
            if (usedLeft == JointType.Cut && usedRight != JointType.Cut)
            {
                return (stockLeft, JointType.Cut);
            }

            if (usedRight == JointType.Cut && usedLeft != JointType.Cut)
            {
                return (JointType.Cut, stockRight);
            }

            if (usedLeft == JointType.Cut && usedRight == JointType.Cut)
            {
                return (JointType.Cut, JointType.Cut);
            }

            return (JointType.Cut, stockRight);
        }

        public void SaveLeftover(WastePanel leftover)
        {
            if (leftover != null && leftover.WidthMm >= 50)
            {
                _repo.AddPanel(leftover);
            }
        }

        [System.Obsolete("Dung CreateLeftover(remnant, actualPanelWidthMm, wallCode, project) thay the")]
        public void SaveRemnant(Panel remnant, string wallCode, string project)
        {
            WastePanel? leftover = CreateLeftover(remnant, 1100, wallCode, project);
            if (leftover != null)
            {
                SaveLeftover(leftover);
            }
        }
    }
}
