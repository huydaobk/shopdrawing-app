using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using ShopDrawing.Plugin.Models;

namespace ShopDrawing.Plugin.Core
{
    public enum LayoutDirection { Horizontal, Vertical }
    public enum StartEdge { Left, Right }

    public class LayoutRequest
    {
        public Polyline? BoundaryPolyline { get; set; }
        public LayoutDirection Direction { get; set; }
        public StartEdge StartEdge { get; set; }
        public double PanelWidthMm { get; set; }
        public int ThicknessMm { get; set; }
        public string Spec { get; set; } = string.Empty;
        public double JointGapMm { get; set; }
        public string WallCode { get; set; } = string.Empty;
        public List<Opening> Openings { get; set; } = new List<Opening>();
    }

    public class LayoutEngine
    {
        public LayoutResult Calculate(LayoutRequest request)
        {
            if (request.BoundaryPolyline == null) 
                throw new ArgumentException("Boundary polyline is required.");

            var res = new LayoutResult();
            var pl = request.BoundaryPolyline;
            var extents = pl.GeometricExtents;
            
            double totalWidth = extents.MaxPoint.X - extents.MinPoint.X;
            double totalHeight = extents.MaxPoint.Y - extents.MinPoint.Y;

            // Ngang: tấm nằm ngang (dài theo X), xếp từ dưới lên (Y)
            // Dọc:  tấm đứng dọc  (dài theo Y), xếp từ trái qua phải (X)
            double span = request.Direction == LayoutDirection.Horizontal ? totalHeight : totalWidth;

            double slotWidth = request.PanelWidthMm + request.JointGapMm;
            int nFull = (int)Math.Floor(span / slotWidth);
            double remnantWidth = span - (nFull * slotWidth);

            // Nếu phần lẻ vừa đúng 1 tấm nguyên khổ (±1mm tolerance do float):
            // → KHÔNG tạo remnant, thêm vào nFull để xếp như tấm bình thường.
            // VD: wall=2220, panelW=1100, gap=20 → slotW=1120, nFull=1, remnant=1100 ← trường hợp này
            if (Math.Abs(remnantWidth - request.PanelWidthMm) < 1.0)
            {
                nFull++;
                remnantWidth = 0;
            }

            // Bắt đầu tính tọa độ
            double currentPos = 0;
            
            // 1. Tạo các tấm Full
            for (int i = 0; i < nFull; i++)
            {
                // Tính chiều dài thực: sample ở MÉP TRÁI + MÉP PHẢI, lấy MAX
                // → tấm nằm đúng chỗ bậc nhảy dùng chiều cao LỚN hơn (factory nguyên)
                // → phần thừa ra bậc thấp = waste (STEP)
                double leftEdge = currentPos + 1.0;  // offset nhỏ tránh biên
                double rightEdge = currentPos + request.PanelWidthMm - 1.0;
                var spanLeft = GetPolylineSpanAt(pl, leftEdge, request.Direction, extents);
                var spanRight = GetPolylineSpanAt(pl, rightEdge, request.Direction, extents);
                var spanResult = spanLeft.Span >= spanRight.Span ? spanLeft : spanRight;

                var p = new Panel
                {
                    WallCode = request.WallCode,
                    ThickMm = request.ThicknessMm,
                    Spec = request.Spec,
                    WidthMm = request.PanelWidthMm,
                    LengthMm = spanResult.Span,
                    IsReused = false,
                    IsHorizontal = request.Direction == LayoutDirection.Horizontal
                };

                if (request.Direction == LayoutDirection.Horizontal)
                {
                    p.X = spanResult.Origin;
                    p.Y = extents.MinPoint.Y + currentPos;
                }
                else
                {
                    if (request.StartEdge == StartEdge.Left)
                        p.X = extents.MinPoint.X + currentPos;
                    else
                        p.X = extents.MaxPoint.X - currentPos - request.PanelWidthMm;
                    p.Y = spanResult.Origin;
                }

                // Ngàm luôn -/+ (không phụ thuộc chiều xếp)
                p.JointLeft = JointType.Female;
                p.JointRight = JointType.Male;

                // === STEP waste: tính waste bậc thang per-panel ===
                double spanDiff = Math.Abs(spanLeft.Span - spanRight.Span);
                if (spanDiff > 1.0)
                {
                    // Có bậc nhảy trong tấm → binary search tìm vị trí step
                    double stepPos = BinarySearchStepPosition(
                        pl, leftEdge, rightEdge, request.Direction, extents,
                        spanLeft.Span, spanRight.Span);

                    // Waste = phần panel ở bên "ngắn"
                    if (spanLeft.Span < spanRight.Span)
                    {
                        // Bên trái ngắn → waste ở góc trái
                        p.StepWasteWidth = stepPos - leftEdge;
                    }
                    else
                    {
                        // Bên phải ngắn → waste ở góc phải
                        p.StepWasteWidth = rightEdge - stepPos;
                    }
                    p.StepWasteHeight = spanDiff;
                }

                res.FullPanels.Add(p);
                currentPos += slotWidth;
            }

            // 2. Tạo tấm lẻ (Remnant)
            if (remnantWidth > 1.0)
            {
                double leftEdge = currentPos + 1.0;
                double rightEdge = currentPos + remnantWidth - 1.0;
                if (rightEdge < leftEdge) rightEdge = leftEdge;
                var spanLeft = GetPolylineSpanAt(pl, leftEdge, request.Direction, extents);
                var spanRight = GetPolylineSpanAt(pl, rightEdge, request.Direction, extents);
                var spanResult = spanLeft.Span >= spanRight.Span ? spanLeft : spanRight;

                var remnant = new Panel
                {
                    WallCode = request.WallCode,
                    ThickMm = request.ThicknessMm,
                    Spec = request.Spec,
                    WidthMm = remnantWidth,
                    LengthMm = spanResult.Span,
                    IsReused = false,
                    IsHorizontal = request.Direction == LayoutDirection.Horizontal
                };

                if (request.Direction == LayoutDirection.Horizontal)
                {
                    remnant.X = spanResult.Origin;
                    remnant.Y = extents.MinPoint.Y + currentPos;
                }
                else
                {
                    if (request.StartEdge == StartEdge.Left)
                        remnant.X = extents.MinPoint.X + currentPos;
                    else
                        remnant.X = extents.MaxPoint.X - currentPos - remnantWidth;
                    remnant.Y = spanResult.Origin;
                }

                // Remnant: cạnh trái giữ dấu khớp nối với tấm liền kề
                // Cạnh phải = cắt tại công trường → JointType.Cut + IsCutPanel=true
                remnant.JointLeft  = JointType.Female;
                remnant.JointRight = JointType.Cut;
                remnant.IsCutPanel = true;  // ← đánh dấu để DrawJointSigns hiện "0" đúng chỗ

                res.RemnantPanel = remnant;
            }

            // 3. Cắt Opening (nếu có)
            if (request.Openings.Count > 0)
            {
                var cutter = new OpeningCutter();
                cutter.ProcessCuts(res, request.Openings, request);
            }

            // 4. Gán ID (nhóm BOM)
            PanelIdGenerator.AssignIds(res.AllPanels, request.WallCode);

            return res;
        }

        /// <summary>
        /// Tính chiều dài (span) của polyline tại 1 vị trí dọc theo trục layout.
        /// Dùng raycast vuông góc với trục layout để tìm giao điểm với polyline.
        /// 
        /// VD: Layout Vertical (xếp trái→phải theo X):
        ///   - pos = vị trí X tương đối (từ MinPoint.X)
        ///   - Raycast theo Y → tìm Ymin, Ymax giao polyline
        ///   - Span = Ymax - Ymin (chiều dài tấm)
        ///   - Origin = Ymin (vị trí Y bắt đầu tấm)
        /// </summary>
        private SpanResult GetPolylineSpanAt(Polyline pl, double relativePos, LayoutDirection dir, Extents3d extents)
        {
            // Vị trí tuyệt đối trên bản vẽ
            double absPos;
            if (dir == LayoutDirection.Vertical)
                absPos = extents.MinPoint.X + relativePos;  // X position
            else
                absPos = extents.MinPoint.Y + relativePos;  // Y position

            var intersections = new List<double>();
            int numVerts = pl.NumberOfVertices;

            for (int i = 0; i < numVerts; i++)
            {
                int j = (i + 1) % numVerts;
                var p1 = pl.GetPoint2dAt(i);
                var p2 = pl.GetPoint2dAt(j);

                double a, b, c, d;
                if (dir == LayoutDirection.Vertical)
                {
                    // Layout along X → ray along Y
                    a = p1.X; b = p2.X;   // along layout axis
                    c = p1.Y; d = p2.Y;   // perpendicular (span axis)
                }
                else
                {
                    // Layout along Y → ray along X
                    a = p1.Y; b = p2.Y;
                    c = p1.X; d = p2.X;
                }

                // Kiểm tra absPos nằm trong đoạn [a, b]
                double minAB = Math.Min(a, b);
                double maxAB = Math.Max(a, b);

                if (absPos < minAB - 0.01 || absPos > maxAB + 0.01)
                    continue;

                if (Math.Abs(b - a) < 0.01)
                {
                    // Đoạn song song với ray (vertical riser) → BỎ QUA
                    // Các đoạn ngang (top/bottom) đã cung cấp đủ giao điểm
                    continue;
                }
                else
                {
                    double t = (absPos - a) / (b - a);
                    double intersection = c + t * (d - c);
                    intersections.Add(intersection);
                }
            }

            if (intersections.Count < 2)
            {
                // Fallback: dùng bounding box
                if (dir == LayoutDirection.Vertical)
                    return new SpanResult(extents.MaxPoint.Y - extents.MinPoint.Y, extents.MinPoint.Y);
                else
                    return new SpanResult(extents.MaxPoint.X - extents.MinPoint.X, extents.MinPoint.X);
            }

            double spanMin = intersections.Min();
            double spanMax = intersections.Max();
            return new SpanResult(spanMax - spanMin, spanMin);
        }

        /// <summary>Kết quả raycast: chiều dài span và vị trí gốc.</summary>
        private record SpanResult(double Span, double Origin);

        /// <summary>
        /// Binary search tìm vị trí X (hoặc Y) nơi span thay đổi đột ngột (bậc nhảy).
        /// Trả về tọa độ position của step trong hệ local (offset từ biên polyline).
        /// </summary>
        private double BinarySearchStepPosition(
            Polyline pl, double posA, double posB,
            LayoutDirection dir, Extents3d extents,
            double spanA, double spanB, int maxIter = 15)
        {
            double tol = 2.0; // mm tolerance
            for (int i = 0; i < maxIter; i++)
            {
                double mid = (posA + posB) / 2.0;
                var spanMid = GetPolylineSpanAt(pl, mid, dir, extents);

                // So sánh span tại mid với spanA
                if (Math.Abs(spanMid.Span - spanA) < tol)
                {
                    // mid cùng bậc với A → step nằm giữa mid và B
                    posA = mid;
                }
                else
                {
                    // mid cùng bậc với B → step nằm giữa A và mid
                    posB = mid;
                }

                if (Math.Abs(posB - posA) < tol)
                    break;
            }
            return (posA + posB) / 2.0;
        }
    }
}
