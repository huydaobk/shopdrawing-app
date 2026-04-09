using System;
using System.Collections.Generic;
using System.Linq;
using ShopDrawing.Plugin.Models;

namespace ShopDrawing.Plugin.Core
{
    public class OpeningCutter
    {
        // Tolerance: 1mm
        private const double TOL = 1.0;
        // Mảnh nhỏ hơn 10mm thì bỏ
        private const double MIN_PIECE = 10.0;

        /// <summary>
        /// Kích thước thực tế trên bản vẽ: chiều ngang (X) và chiều dọc (Y).
        /// Horizontal: drawW = LengthMm, drawH = WidthMm (swap)
        /// Vertical:   drawW = WidthMm,  drawH = LengthMm
        /// </summary>
        private static double DrawW(Panel p) => p.IsHorizontal ? p.LengthMm : p.WidthMm;
        private static double DrawH(Panel p) => p.IsHorizontal ? p.WidthMm : p.LengthMm;

        public void ProcessCuts(LayoutResult result, List<Opening> openings, LayoutRequest request)
        {
            if (openings == null || openings.Count == 0) return;

            var keptPanels = new List<Panel>();
            var cutPanels = new List<Panel>();
            var wasteEntries = new List<(string PanelId, double WidthMm, double HeightMm)>();
            int cutCounter = 1;

            foreach (var panel in result.AllPanels.ToList())
            {
                bool wasSplit = false;
                List<Panel> currentPieces = new List<Panel> { panel };

                foreach (var opening in openings)
                {
                    var nextPieces = new List<Panel>();
                    foreach (var piece in currentPieces)
                    {
                        if (!DoesIntersect(piece, opening))
                        {
                            // Không giao → giữ nguyên
                            nextPieces.Add(piece);
                        }
                        else if (IsPanelFullyInsideOpeningSpan(piece, opening))
                        {
                            // Tấm nằm gọn trong bề rộng lỗ mở sẽ tách thành 2 tấm nguyên khi sản xuất.
                            // Trường hợp này không ghi waste vào kho lẻ.
                            wasSplit = true;
                            nextPieces.AddRange(SplitPanel(piece, opening, request.WallCode, ref cutCounter, isBoundaryCut: false));
                        }
                        else
                        {
                            // Tấm BIÊN: opening cắt vào 1 phần → GIỮ NGUYÊN kích thước gốc
                            // Công trường chỉ cắt bỏ 1 notch, tấm vẫn là 1 tấm nguyên
                            wasSplit = true;
                            piece.IsCutPanel = true;
                            nextPieces.Add(piece);

                            TryAddOpeningWasteEntry(wasteEntries, piece, opening);
                        }
                    }
                    currentPieces = nextPieces;
                }

                if (wasSplit)
                {
                    cutPanels.AddRange(currentPieces);
                }
                else
                {
                    keptPanels.Add(panel);
                }
            }

            result.FullPanels = keptPanels.Where(p => p != result.RemnantPanel).ToList();
            if (result.RemnantPanel != null && !keptPanels.Contains(result.RemnantPanel))
            {
                result.RemnantPanel = null;
            }
            
            result.CutPanels = cutPanels;
            result.OpeningWasteEntries = wasteEntries;
        }

        private static void TryAddOpeningWasteEntry(
            ICollection<(string PanelId, double WidthMm, double HeightMm)> wasteEntries,
            Panel piece,
            Opening opening)
        {
            double pw = DrawW(piece);
            double ph = DrawH(piece);
            double overlapLeft = Math.Max(piece.X, opening.X);
            double overlapRight = Math.Min(piece.X + pw, opening.X + opening.Width);
            double overlapBot = Math.Max(piece.Y, opening.Y);
            double overlapTop = Math.Min(piece.Y + ph, opening.Y + opening.Height);
            double wasteW = Math.Max(0, overlapRight - overlapLeft);
            double wasteH = Math.Max(0, overlapTop - overlapBot);
            if (wasteW <= MIN_PIECE || wasteH <= MIN_PIECE)
            {
                return;
            }

            double widthMm = piece.IsHorizontal ? wasteH : wasteW;
            double lengthMm = piece.IsHorizontal ? wasteW : wasteH;
            wasteEntries.Add((piece.PanelId, Math.Round(widthMm, 0), Math.Round(lengthMm, 0)));
        }

        /// <summary>
        /// Panel giao lỗ mở hay không? Dùng kích thước thực tế trên bản vẽ.
        /// </summary>
        private bool DoesIntersect(Panel p, Opening o)
        {
            double pw = DrawW(p);
            double ph = DrawH(p);
            return !(p.X >= o.X + o.Width - TOL || 
                     p.X + pw <= o.X + TOL || 
                     p.Y >= o.Y + o.Height - TOL || 
                     p.Y + ph <= o.Y + TOL);
        }

        /// <summary>
        /// Panel nằm gọn trong "span direction" của lỗ mở?
        /// Vertical:   span = X (chiều ngang), panels xếp theo X
        /// Horizontal: span = Y (chiều dọc),   panels xếp theo Y
        /// </summary>
        private bool IsPanelFullyInsideOpeningSpan(Panel p, Opening o)
        {
            if (p.IsHorizontal)
            {
                // Horizontal: tấm xếp dọc (Y), chiều span = WidthMm → drawH
                double ph = DrawH(p);
                return (p.Y >= o.Y - TOL) && (p.Y + ph <= o.Y + o.Height + TOL);
            }
            else
            {
                // Vertical: tấm xếp ngang (X), chiều span = WidthMm → drawW
                double pw = DrawW(p);
                return (p.X >= o.X - TOL) && (p.X + pw <= o.X + o.Width + TOL);
            }
        }

        /// <summary>
        /// Cắt tấm NẰM GỌN TRONG opening span → tấm trên/dưới = MỚI (sản xuất mới)
        /// isBoundaryCut=false → IsCutPanel=false (tấm mới)
        /// </summary>
        private List<Panel> SplitPanel(Panel p, Opening o, string wallCode, ref int cutCounter, bool isBoundaryCut)
        {
            var pieces = new List<Panel>();

            if (p.IsHorizontal)
            {
                double pw = DrawW(p);

                // Mảnh bên PHẢI opening
                double rightW = (p.X + pw) - (o.X + o.Width);
                if (rightW > MIN_PIECE)
                {
                    pieces.Add(CreateCutPiece(p, o.X + o.Width, p.Y,
                        rightW, DrawH(p), wallCode, ref cutCounter, isHorizontal: true, isCut: isBoundaryCut));
                }

                // Mảnh bên TRÁI opening
                double leftW = o.X - p.X;
                if (leftW > MIN_PIECE)
                {
                    pieces.Add(CreateCutPiece(p, p.X, p.Y,
                        leftW, DrawH(p), wallCode, ref cutCounter, isHorizontal: true, isCut: isBoundaryCut));
                }
            }
            else
            {
                double ph = DrawH(p);

                // Mảnh TRÊN opening
                double topH = (p.Y + ph) - (o.Y + o.Height);
                if (topH > MIN_PIECE)
                {
                    pieces.Add(CreateCutPiece(p, p.X, o.Y + o.Height,
                        DrawW(p), topH, wallCode, ref cutCounter, isHorizontal: false, isCut: isBoundaryCut));
                }

                // Mảnh DƯỚI opening
                double botH = o.Y - p.Y;
                if (botH > MIN_PIECE)
                {
                    pieces.Add(CreateCutPiece(p, p.X, p.Y,
                        DrawW(p), botH, wallCode, ref cutCounter, isHorizontal: false, isCut: isBoundaryCut));
                }
            }

            return pieces;
        }

        /// <summary>
        /// Tấm BIÊN: opening cắt vào 1 phần cạnh → cắt tại công trường (IsCutPanel=true)
        /// Chỉ giữ phần KHÔNG bị opening che
        /// </summary>
        private List<Panel> SplitBoundaryPanel(Panel p, Opening o, string wallCode, ref int cutCounter)
        {
            var pieces = new List<Panel>();
            double pw = DrawW(p);
            double ph = DrawH(p);

            if (p.IsHorizontal)
            {
                // Mảnh TRÁI (nếu tấm nhô ra bên trái opening)
                double leftW = o.X - p.X;
                if (leftW > MIN_PIECE)
                {
                    pieces.Add(CreateCutPiece(p, p.X, p.Y,
                        leftW, ph, wallCode, ref cutCounter, isHorizontal: true, isCut: true));
                }
                // Mảnh PHẢI (nếu tấm nhô ra bên phải opening)
                double rightW = (p.X + pw) - (o.X + o.Width);
                if (rightW > MIN_PIECE)
                {
                    pieces.Add(CreateCutPiece(p, o.X + o.Width, p.Y,
                        rightW, ph, wallCode, ref cutCounter, isHorizontal: true, isCut: true));
                }

                // Phần TRÊN opening (toàn bộ chiều ngang tấm)
                double topH = (p.Y + ph) - (o.Y + o.Height);
                if (topH > MIN_PIECE)
                {
                    pieces.Add(CreateCutPiece(p, p.X, o.Y + o.Height,
                        pw, topH, wallCode, ref cutCounter, isHorizontal: true, isCut: true));
                }
                // Phần DƯỚI opening (toàn bộ chiều ngang tấm)
                double botH = o.Y - p.Y;
                if (botH > MIN_PIECE)
                {
                    pieces.Add(CreateCutPiece(p, p.X, p.Y,
                        pw, botH, wallCode, ref cutCounter, isHorizontal: true, isCut: true));
                }
            }
            else
            {
                // Vertical boundary panel: opening cắt vào 1 phần
                // Phần TRÊN opening
                double topH = (p.Y + ph) - (o.Y + o.Height);
                if (topH > MIN_PIECE)
                {
                    pieces.Add(CreateCutPiece(p, p.X, o.Y + o.Height,
                        pw, topH, wallCode, ref cutCounter, isHorizontal: false, isCut: true));
                }
                // Phần DƯỚI opening
                double botH = o.Y - p.Y;
                if (botH > MIN_PIECE)
                {
                    pieces.Add(CreateCutPiece(p, p.X, p.Y,
                        pw, botH, wallCode, ref cutCounter, isHorizontal: false, isCut: true));
                }
            }

            // Nếu không tạo mảnh nào → giữ nguyên tấm gốc (đánh dấu cắt)
            if (pieces.Count == 0)
            {
                p.IsCutPanel = true;
                pieces.Add(p);
            }

            return pieces;
        }

        private Panel CreateCutPiece(Panel orig, double x, double y, double drawW, double drawH,
            string wallCode, ref int cutCounter, bool isHorizontal, bool isCut)
        {
            // Chuyển ngược drawW/drawH về WidthMm/LengthMm theo hướng
            double widthMm = isHorizontal ? drawH : drawW;   // WidthMm = kích thước span
            double lengthMm = isHorizontal ? drawW : drawH;  // LengthMm = kích thước dài

            return new Panel
            {
                PanelId = string.IsNullOrEmpty(orig.PanelId) ? $"{wallCode}-C{cutCounter++:D2}" : $"{orig.PanelId}-C",
                Spec = orig.Spec,
                WidthMm = widthMm,
                LengthMm = lengthMm,
                ThickMm = orig.ThickMm,
                X = x,
                Y = y,
                WallCode = orig.WallCode,
                JointLeft = orig.JointLeft,
                JointRight = orig.JointRight,
                IsCutPanel = isCut,
                ParentPanelId = orig.PanelId,
                IsHorizontal = isHorizontal
            };
        }
    }
}
