using System;
using System.Collections.Generic;
using System.Linq;
using ShopDrawing.Plugin.Models;

namespace ShopDrawing.Plugin.Core
{
    /// <summary>
    /// Phân tích tấm panel bằng thuật toán scan-line.
    /// Quét đường thẳng song song cách nhau PanelWidth qua polygon bất kỳ
    /// → xác định chính xác số lượng + kích thước từng tấm.
    /// Pure geometry — không phụ thuộc AutoCAD API.
    /// </summary>
    public static class ScanLineAnalyzer
    {
        /// <summary>
        /// Phân tích polygon bằng scan-line.
        /// </summary>
        /// <param name="vertices">Danh sách đỉnh polygon [[x1,y1],[x2,y2],...] theo thứ tự</param>
        /// <param name="panelWidth">Khổ tấm (mm)</param>
        /// <param name="isHorizontal">true = xếp ngang (quét theo Y), false = xếp dọc (quét theo X)</param>
        /// <returns>Danh sách TenderPanelEntry</returns>
        public static List<TenderPanelEntry> Analyze(
            List<double[]> vertices, int panelWidth, bool isHorizontal, IReadOnlyList<TenderOpening> openings = null)
        {
            if (vertices == null || vertices.Count < 3 || panelWidth <= 0)
                return new List<TenderPanelEntry>();

            // Đảm bảo polygon kín (vertex cuối = vertex đầu không cần thiết)
            var pts = vertices.Select(v => (X: v[0], Y: v[1])).ToList();

            // Xác định trục quét và trục đo
            // Dọc: quét theo X (từ trái → phải), đo chiều dài theo Y
            // Ngang: quét theo X vẫn được, nhưng đảo vai trò:
            //   quét theo Y (từ dưới → trên), đo chiều dài theo X
            double scanMin, scanMax;
            if (isHorizontal)
            {
                scanMin = pts.Min(p => p.Y);
                scanMax = pts.Max(p => p.Y);
            }
            else
            {
                scanMin = pts.Min(p => p.X);
                scanMax = pts.Max(p => p.X);
            }

            double totalScanSpan = scanMax - scanMin;
            if (totalScanSpan <= 0) return new List<TenderPanelEntry>();

            int totalPanels = (int)Math.Ceiling(totalScanSpan / panelWidth);

            // Quét từng tấm — lấy chiều dài TỐI ĐA trong dải tấm để đặt hàng đúng
            var panelLengths = new List<double>(); // chiều dài thực tế mỗi tấm

            for (int i = 0; i < totalPanels; i++)
            {
                double panelStart = scanMin + i * panelWidth;
                double panelEnd   = scanMin + (i + 1) * panelWidth;

                // Clamp vào bounding box (tấm cuối có thể ngắn hơn PanelWidth)
                panelEnd = Math.Min(panelEnd, scanMax);
                double stripeW = panelEnd - panelStart;
                if (stripeW < 1.0) continue;

                // Sử dụng nhiều đường scan bên trong dải tấm để bù trừ biên dạng xiên
                int numScans = 5;
                double step = stripeW / (numScans + 1);
                var allStripes = new List<(double Start, double End)>();

                for (int s = 1; s <= numScans; s++)
                {
                    double scanPos = panelStart + s * step;
                    var solidSegs = GetSolidSegments(pts, scanPos, isHorizontal, openings);
                    allStripes.AddRange(solidSegs);
                }

                var finalPieces = UnionSegments(allStripes);
                foreach (var piece in finalPieces)
                {
                    double len = Math.Round(piece.End - piece.Start);
                    if (len > 0)
                        panelLengths.Add(len);
                }
            }

            // Nhóm theo chiều dài giống nhau → gọn bảng
            return GroupPanelEntries(panelLengths, panelWidth, totalScanSpan);
        }

        /// <summary>
        /// Tìm tất cả giao điểm của scan-line với polygon edges.
        /// </summary>
        private static List<double> GetIntersections(
            List<(double X, double Y)> pts, double scanPos, bool isHorizontal)
        {
            var intersections = new List<double>();
            int n = pts.Count;

            for (int i = 0; i < n; i++)
            {
                var p1 = pts[i];
                var p2 = pts[(i + 1) % n];

                double s1, s2, m1, m2;
                if (isHorizontal)
                {
                    // Quét theo Y, đo theo X
                    s1 = p1.Y; s2 = p2.Y;
                    m1 = p1.X; m2 = p2.X;
                }
                else
                {
                    // Quét theo X, đo theo Y
                    s1 = p1.X; s2 = p2.X;
                    m1 = p1.Y; m2 = p2.Y;
                }

                // Kiểm tra edge có cắt scan-line tại scanPos
                if ((s1 <= scanPos && s2 > scanPos) || (s2 <= scanPos && s1 > scanPos))
                {
                    // Nội suy tìm vị trí giao trên trục đo
                    double t = (scanPos - s1) / (s2 - s1);
                    double measurePos = m1 + t * (m2 - m1);
                    intersections.Add(measurePos);
                }
            }

            return intersections;
        }

        private static List<(double Start, double End)> GetSolidSegments(
            List<(double X, double Y)> wallPts, 
            double scanPos, 
            bool isHorizontal, 
            IReadOnlyList<TenderOpening> openings)
        {
            var intersections = GetIntersections(wallPts, scanPos, isHorizontal);
            intersections.Sort();
            var segments = new List<(double Start, double End)>();
            for (int j = 0; j + 1 < intersections.Count; j += 2)
            {
                 if (intersections[j+1] - intersections[j] > 1.0)
                     segments.Add((intersections[j], intersections[j + 1]));
            }

            if (openings != null)
            {
                foreach (var op in openings.Where(o => o?.OpeningPolygon != null && o.OpeningPolygon.Count >= 3))
                {
                    var opPts = op.OpeningPolygon.Select(v => (X: v[0], Y: v[1])).ToList();
                    var opIntersections = GetIntersections(opPts, scanPos, isHorizontal);
                    opIntersections.Sort();
                    for (int j = 0; j + 1 < opIntersections.Count; j += 2)
                    {
                        SubtractSegment(segments, opIntersections[j], opIntersections[j + 1]);
                    }
                }
            }
            return segments;
        }

        private static void SubtractSegment(List<(double Start, double End)> segments, double cutStart, double cutEnd)
        {
            for (int i = segments.Count - 1; i >= 0; i--)
            {
                var seg = segments[i];
                if (cutEnd <= seg.Start || cutStart >= seg.End)
                    continue;

                segments.RemoveAt(i);
                
                if (cutStart > seg.Start + 1.0 && cutEnd < seg.End - 1.0)
                {
                    segments.Insert(i, (cutEnd, seg.End));
                    segments.Insert(i, (seg.Start, cutStart));
                }
                else if (cutStart <= seg.Start + 1.0 && cutEnd < seg.End - 1.0)
                {
                    segments.Insert(i, (cutEnd, seg.End));
                }
                else if (cutStart > seg.Start + 1.0 && cutEnd >= seg.End - 1.0)
                {
                    segments.Insert(i, (seg.Start, cutStart));
                }
            }
        }

        private static List<(double Start, double End)> UnionSegments(List<(double Start, double End)> segments)
        {
            if (segments.Count == 0) return new List<(double, double)>();
            
            var sorted = segments.OrderBy(s => s.Start).ToList();
            var result = new List<(double Start, double End)>();
            
            var current = sorted[0];
            for (int i = 1; i < sorted.Count; i++)
            {
                var next = sorted[i];
                if (next.Start <= current.End)
                {
                    current.End = Math.Max(current.End, next.End);
                }
                else
                {
                    result.Add(current);
                    current = next;
                }
            }
            result.Add(current);
            return result;
        }

        /// <summary>
        /// Nhóm danh sách panel lengths thành TenderPanelEntry.
        /// Panels cùng chiều dài (tolerance 50mm) → gộp thành 1 dòng.
        /// </summary>
        private static List<TenderPanelEntry> GroupPanelEntries(
            List<double> panelLengths, int panelWidth, double totalScanSpan)
        {
            var entries = new List<TenderPanelEntry>();
            if (panelLengths.Count == 0) return entries;

            // Tolerance cho nhóm (50mm)
            const double tolerance = 50;

            // Group by similar lengths
            var groups = new List<(double Length, int Count)>();
            var sorted = panelLengths.Where(l => l > 0).OrderByDescending(l => l).ToList();

            foreach (var len in sorted)
            {
                var match = groups.FindIndex(g => Math.Abs(g.Length - len) <= tolerance);
                if (match >= 0)
                {
                    var g = groups[match];
                    groups[match] = (g.Length, g.Count + 1); // Giữ length của nhóm đầu tiên
                }
                else
                {
                    groups.Add((len, 1));
                }
            }

            // Convert to entries
            foreach (var g in groups.OrderByDescending(g => g.Length))
            {
                entries.Add(new TenderPanelEntry
                {
                    WidthMm = panelWidth,
                    LengthMm = g.Length,
                    Count = g.Count,
                    Label = "Nguyên"
                });
            }

            // Tính hao hụt tấm cuối (remnant)
            int totalPanels = panelLengths.Count;
            double remnantW = totalScanSpan - (totalPanels - 1) * panelWidth;
            if (remnantW > 0 && remnantW < panelWidth - 1)
            {
                double wasteW = panelWidth - remnantW;
                if (wasteW > 1)
                {
                    // Tìm chiều dài tấm cuối để tính DT hao hụt
                    double lastPanelLength = panelLengths.LastOrDefault();
                    if (lastPanelLength > 0)
                    {
                        entries.Add(new TenderPanelEntry
                        {
                            WidthMm = Math.Round(wasteW),
                            LengthMm = lastPanelLength,
                            Count = 1,
                            Label = "Hao hụt"
                        });
                    }
                }
            }

            return entries;
        }
    }
}
