using System;
using System.Collections.Generic;
using System.Linq;
using ShopDrawing.Plugin.Core;

namespace ShopDrawing.Plugin.Models
{
    public class TenderHeightSegment
    {
        /// <summary>Chiều dài đoạn theo tuyến vách (mm)</summary>
        public double LengthMm { get; set; }

        /// <summary>Chiều cao đoạn (mm)</summary>
        public double HeightMm { get; set; }

        /// <summary>Handle CAD của line nhịp (nếu đoạn được pick từ CAD)</summary>
        public string? CadHandle { get; set; }
    }

    /// <summary>Mot dong trong bang phan tich tam so bo</summary>
    public class TenderPanelEntry
    {
        /// <summary>Kho tam (mm) - chieu hep</summary>
        public double WidthMm { get; set; }

        /// <summary>Chieu dai tam (mm) - chieu span</summary>
        public double LengthMm { get; set; }

        /// <summary>So luong</summary>
        public int Count { get; set; }

        /// <summary>Ghi chu</summary>
        public string Label { get; set; } = "";

        /// <summary>DT (m2) = W x L x Count</summary>
        public double AreaM2 => WidthMm * LengthMm * Count / 1_000_000.0;
    }

    public class TenderWall
    {
        private const string WasteLabelPrefix = "Hao hụt";
        private const string WasteLabelOpening = "Hao hụt (Lỗ mở)";

        private static bool IsWasteLabel(string? label)
        {
            if (string.IsNullOrWhiteSpace(label))
                return false;

            return label.Trim().StartsWith(WasteLabelPrefix, StringComparison.OrdinalIgnoreCase);
        }
        /// <summary>Hang muc: Vach / Tran / Nen / Op cot</summary>
        public string Category { get; set; } = "Vách";

        /// <summary>Tang / Khu vuc</summary>
        public string Floor { get; set; } = string.Empty;

        /// <summary>Ten vach (VD: "W-A1")</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Chieu dai vach (mm)</summary>
        public double Length { get; set; }

        /// <summary>Chieu cao vach (mm)</summary>
        public double Height { get; set; }

        /// <summary>
        /// Danh sách cao độ theo từng đoạn chiều dài.
        /// Để trống => dùng Height như logic cũ.
        /// </summary>
        public List<TenderHeightSegment> HeightSegments { get; set; } = new();

        /// <summary>Ma Spec (VD: "ISOFRIGO-TT")</summary>
        public string SpecKey { get; set; } = string.Empty;

        /// <summary>Kho tam panel du kien (mm)</summary>
        public int PanelWidth { get; set; } = 1100;

        /// <summary>Chieu day panel (mm) - lay tu PanelSpec.Thickness, dung cho auto-sizing vit TEK</summary>
        public int PanelThickness { get; set; } = 50;

        /// <summary>Huong xep tam: "Dọc" hoặc "Ngang"</summary>
        public string LayoutDirection { get; set; } = "Dọc";

        /// <summary>Ung dung: Ngoai nha / Phong sach / Kho lanh</summary>
        public string Application { get; set; } = "Ngoài nhà";

        /// <summary>Danh sach ung dung cho dropdown</summary>
        public static readonly string[] ApplicationOptions = { "Ngoài nhà", "Phòng sạch", "Kho lạnh" };

        public List<string> AppliedEntityHandles { get; set; } = new();
        public string? AppliedGroupId { get; set; }
        public double? AppliedPlacementX { get; set; }
        public double? AppliedPlacementY { get; set; }
        public double? AppliedPlacementZ { get; set; }

        /// <summary>
        /// Prefix ký hiệu theo hạng mục: Vách→W, Trần→C, Nền→F, Mái→R.
        /// </summary>
        public static string GetCategoryPrefix(string? category) =>
            (category ?? string.Empty).Trim() switch
            {
                "Vách" => "W",
                "Trần" => "C",
                "Nền"  => "F",
                "Mái"  => "R",
                _      => "W"
            };

        public const string TopPanelTreatmentNone = "Không áp dụng";
        public const string TopPanelTreatmentCeilingCenter = "Giao trần giữa";
        public const string TopPanelTreatmentCeilingPerimeter = "Giao biên trần";
        public const string TopPanelTreatmentFree = "Mép trên tự do";
        public const string EndPanelTreatmentNone = "Không áp dụng";
        public const string EndPanelTreatmentCenter = "Giao giữa";
        public const string EndPanelTreatmentPerimeter = "Giao biên";
        public const string EndPanelTreatmentFree = "Mép tự do";
        public const string BottomPanelTreatmentNone = "Không áp dụng";
        public const string BottomPanelTreatmentCurb = "Trên bệ chân (curb)";

        public static readonly string[] TopPanelTreatmentOptions =
        {
            TopPanelTreatmentNone,
            TopPanelTreatmentCeilingCenter,
            TopPanelTreatmentCeilingPerimeter,
            TopPanelTreatmentFree
        };

        public static readonly string[] EndPanelTreatmentOptions =
        {
            EndPanelTreatmentNone,
            EndPanelTreatmentCenter,
            EndPanelTreatmentPerimeter
        };

        public static readonly string[] BottomPanelTreatmentOptions =
        {
            BottomPanelTreatmentNone,
            BottomPanelTreatmentCurb
        };

        public static string NormalizeTopPanelTreatment(string? treatment, bool fallbackLegacyExposed = false)
        {
            var normalized = (treatment ?? string.Empty).Trim();
            if (string.Equals(normalized, TopPanelTreatmentCeilingCenter, StringComparison.OrdinalIgnoreCase))
                return TopPanelTreatmentCeilingCenter;
            if (string.Equals(normalized, TopPanelTreatmentCeilingPerimeter, StringComparison.OrdinalIgnoreCase))
                return TopPanelTreatmentCeilingPerimeter;
            if (string.Equals(normalized, TopPanelTreatmentFree, StringComparison.OrdinalIgnoreCase))
                return TopPanelTreatmentFree;
            if (string.Equals(normalized, TopPanelTreatmentNone, StringComparison.OrdinalIgnoreCase))
                return TopPanelTreatmentNone;

            return fallbackLegacyExposed ? TopPanelTreatmentFree : TopPanelTreatmentNone;
        }

        public static string NormalizeEndPanelTreatment(string? treatment, bool fallbackLegacyExposed = false)
        {
            var normalized = (treatment ?? string.Empty).Trim();
            if (string.Equals(normalized, EndPanelTreatmentCenter, StringComparison.OrdinalIgnoreCase))
                return EndPanelTreatmentCenter;
            if (string.Equals(normalized, EndPanelTreatmentPerimeter, StringComparison.OrdinalIgnoreCase))
                return EndPanelTreatmentPerimeter;
            if (string.Equals(normalized, EndPanelTreatmentFree, StringComparison.OrdinalIgnoreCase))
                return EndPanelTreatmentFree;
            if (string.Equals(normalized, EndPanelTreatmentNone, StringComparison.OrdinalIgnoreCase))
                return EndPanelTreatmentNone;

            return fallbackLegacyExposed ? EndPanelTreatmentCenter : EndPanelTreatmentNone;
        }

        public static string NormalizeBottomPanelTreatment(string? treatment, bool fallbackLegacyExposed = false)
        {
            var normalized = (treatment ?? string.Empty).Trim();
            if (string.Equals(normalized, BottomPanelTreatmentCurb, StringComparison.OrdinalIgnoreCase))
                return BottomPanelTreatmentCurb;
            if (string.Equals(normalized, BottomPanelTreatmentNone, StringComparison.OrdinalIgnoreCase))
                return BottomPanelTreatmentNone;

            return fallbackLegacyExposed ? BottomPanelTreatmentCurb : BottomPanelTreatmentNone;
        }

        public string TopPanelTreatment { get; set; } = string.Empty;
        public string EndPanelTreatment { get; set; } = string.Empty;
        public string BottomPanelTreatment { get; set; } = string.Empty;

        /// <summary>Canh tren co lo de tinh up noc hay khong</summary>
        public bool TopEdgeExposed { get; set; } = true;

        /// <summary>Canh duoi co lo de tinh up chan hay khong</summary>
        public bool BottomEdgeExposed { get; set; } = true;

        /// <summary>Dau vach ben trai co lo hay khong</summary>
        public bool StartEdgeExposed { get; set; }

        /// <summary>Dau vach ben phai co lo hay khong</summary>
        public bool EndEdgeExposed { get; set; }

        /// <summary>
        /// Số góc ngoài đi qua theo chiều cao vách.
        /// ⚠️ Nếu cạnh vách đã là góc ngoài, KHÔNG bật StartEdgeExposed/EndEdgeExposed
        /// cho cùng cạnh đó — sẽ tính trùng cả Úp góc ngoài lẫn xử lý mép đứng tự do.
        /// </summary>
        public int OutsideCornerCount { get; set; }

        /// <summary>Số góc trong đi qua theo chiều cao vách</summary>
        public int InsideCornerCount { get; set; }

        /// <summary>
        /// Số khe nối đứng khi vách xếp Ngang (tấm panel nằm ngang, khe nối chạy dọc).
        /// Dùng để tính Omega nhôm, Foam, Gioăng xốp làm kín.
        /// CHỈ có tác dụng khi LayoutDirection = "Ngang". Bỏ qua khi xếp Dọc.
        /// </summary>
        public int VerticalJointCount { get; set; }

        /// <summary>CAD Handle cho zoom/highlight</summary>
        public string? CadHandle { get; set; }

        /// <summary>Danh sach opening cua vach nay</summary>
        public List<TenderOpening> Openings { get; set; } = new();

        /// <summary>
        /// Chieu dai tha cap/ty treo thuc te cho tran kho lanh (mm).
        /// Dung de quy doi so md wire rope so bo.
        /// </summary>
        public double CableDropLengthMm { get; set; }

        /// <summary>
        /// Huong chia tuyen treo tran kho lanh.
        /// false = tu canh min (trai hoac duoi), true = tu canh max (phai hoac tren).
        /// </summary>
        public bool ColdStorageDivideFromMaxSide { get; set; }

        /// <summary>
        /// Huong chia phu kien/tuyen treo tran.
        /// Doc lap voi huong chia tam (LayoutDirection).
        /// </summary>
        public string SuspensionLayoutDirection { get; set; } = string.Empty;

        /// <summary>
        /// Dinh polygon [[x,y],...] - khi pick polyline khong phai chu nhat.
        /// null = vach chu nhat thong thuong.
        /// </summary>
        public List<double[]>? PolygonVertices { get; set; }

        public IReadOnlyList<TenderHeightSegment> GetEffectiveHeightSegments()
        {
            return WallHeightResolver.Normalize(Length, Height, HeightSegments);
        }

        public double RepresentativeHeightMm
        {
            get
            {
                var segments = GetEffectiveHeightSegments();
                if (segments.Count == 0)
                    return Math.Max(0, Height);

                double totalLength = segments.Sum(s => s.LengthMm);
                if (totalLength <= 0)
                    return Math.Max(0, Height);

                return segments.Sum(s => s.LengthMm * s.HeightMm) / totalLength;
            }
        }

        public double StartEdgeHeightMm
        {
            get
            {
                var segments = GetEffectiveHeightSegments();
                return segments.Count == 0 ? Math.Max(0, Height) : Math.Max(0, segments[0].HeightMm);
            }
        }

        public double EndEdgeHeightMm
        {
            get
            {
                var segments = GetEffectiveHeightSegments();
                return segments.Count == 0 ? Math.Max(0, Height) : Math.Max(0, segments[^1].HeightMm);
            }
        }

        private double ComputePolygonAreaM2(List<double[]>? vertices)
        {
            if (vertices == null || vertices.Count < 3) return 0;
            double area = 0;
            int j = vertices.Count - 1;
            for (int i = 0; i < vertices.Count; i++)
            {
                area += (vertices[j][0] + vertices[i][0]) * (vertices[j][1] - vertices[i][1]);
                j = i;
            }
            return Math.Abs(area) / 2.0 / 1_000_000.0;
        }

        public double TrueGeometricAreaM2 => ComputePolygonAreaM2(PolygonVertices);

        public double WallAreaM2
        {
            get
            {
                double exactArea = TrueGeometricAreaM2;
                if (exactArea > 0)
                    return exactArea;

                var segments = GetEffectiveHeightSegments();
                if (segments.Count == 0)
                    return Math.Max(0, Length) * Math.Max(0, Height) / 1_000_000.0;

                return segments.Sum(s => s.LengthMm * s.HeightMm) / 1_000_000.0;
            }
        }
        public double OpeningAreaM2 => Openings.Sum(o => o.TotalAreaM2);
        public double NetAreaM2 => Math.Max(0, WallAreaM2 - OpeningAreaM2);
        public double TotalOpeningWidth => Openings.Sum(o => o.TotalWidth);
        public double TotalOpeningPerimeter => Openings.Sum(o => o.TotalPerimeter);
        public double TotalOpeningPerimeterTwoFaces => Openings.Sum(o => o.TotalPerimeterTwoFaces);
        public int TotalOpeningCount => Openings.Sum(o => o.Quantity);
        public double TotalDoorOpeningPerimeter => Openings.Where(o => o.IsDoor).Sum(o => o.TotalPerimeter);
        public double TotalNonDoorOpeningPerimeter => Openings.Where(o => o.IsNonDoor).Sum(o => o.TotalPerimeter);
        public int TotalDoorOpeningCount => Openings.Where(o => o.IsDoor).Sum(o => o.Quantity);
        public int TotalNonDoorOpeningCount => Openings.Where(o => o.IsNonDoor).Sum(o => o.Quantity);
        // Cap opening edge dimensions to wall bounds.
        // Prevents over-counting sealant when user inputs opening height/width > wall size.
        public double TotalOpeningVerticalEdges =>
            Openings.Sum(o => Math.Min(o.Height, RepresentativeHeightMm) * 2 * o.Quantity);
        public double TotalOpeningHorizontalTopLength =>
            Openings.Sum(o => Math.Min(o.Width, Length) * o.Quantity);
        public double TotalOpeningSillLength =>
            Openings.Where(o => o.IsNonDoor).Sum(o => Math.Min(o.Width, Length) * o.Quantity);
        public bool IsColdStorageWall =>
            string.Equals(Category, "Vách", StringComparison.OrdinalIgnoreCase)
            && string.Equals(Application, "Kho lạnh", StringComparison.OrdinalIgnoreCase);
        public string ResolvedTopPanelTreatment => NormalizeTopPanelTreatment(TopPanelTreatment, TopEdgeExposed);
        public bool HasTopPanelTreatment => !string.Equals(ResolvedTopPanelTreatment, TopPanelTreatmentNone, StringComparison.OrdinalIgnoreCase);
        public string ResolvedEndPanelTreatment => NormalizeEndPanelTreatment(EndPanelTreatment, StartEdgeExposed || EndEdgeExposed);
        public bool HasEndPanelTreatment => !string.Equals(ResolvedEndPanelTreatment, EndPanelTreatmentNone, StringComparison.OrdinalIgnoreCase);
        public string ResolvedBottomPanelTreatment =>
            IsColdStorageWall
                ? NormalizeBottomPanelTreatment(BottomPanelTreatment, BottomEdgeExposed)
                : (BottomEdgeExposed ? BottomPanelTreatmentCurb : BottomPanelTreatmentNone);
        public bool HasBottomPanelTreatment =>
            IsColdStorageWall
                ? !string.Equals(ResolvedBottomPanelTreatment, BottomPanelTreatmentNone, StringComparison.OrdinalIgnoreCase)
                : BottomEdgeExposed;
        public double TopEdgeLength => HasTopPanelTreatment ? Length : 0;
        public double TopPanelCeilingCenterLength =>
            string.Equals(ResolvedTopPanelTreatment, TopPanelTreatmentCeilingCenter, StringComparison.OrdinalIgnoreCase) ? Length : 0;
        public double TopPanelCeilingPerimeterLength =>
            string.Equals(ResolvedTopPanelTreatment, TopPanelTreatmentCeilingPerimeter, StringComparison.OrdinalIgnoreCase) ? Length : 0;
        public double TopPanelFreeLength =>
            string.Equals(ResolvedTopPanelTreatment, TopPanelTreatmentFree, StringComparison.OrdinalIgnoreCase) ? Length : 0;
        public double BottomEdgeLength => HasBottomPanelTreatment ? Length : 0;
        public double EndPanelCenterLength =>
            string.Equals(ResolvedEndPanelTreatment, EndPanelTreatmentCenter, StringComparison.OrdinalIgnoreCase)
                ? ExposedEndLength
                : 0;
        public double EndPanelPerimeterLength =>
            string.Equals(ResolvedEndPanelTreatment, EndPanelTreatmentPerimeter, StringComparison.OrdinalIgnoreCase)
                ? ExposedEndLength
                : 0;
        public double EndPanelFreeLength =>
            string.Equals(ResolvedEndPanelTreatment, EndPanelTreatmentFree, StringComparison.OrdinalIgnoreCase)
                ? ExposedEndLength
                : 0;
        public double ExposedEndLength => (StartEdgeExposed ? StartEdgeHeightMm : 0) + (EndEdgeExposed ? EndEdgeHeightMm : 0);
        public double TotalExposedEdgeLength => TopEdgeLength + BottomEdgeLength + ExposedEndLength;
        public double OutsideCornerHeight => Math.Max(0, OutsideCornerCount) * RepresentativeHeightMm;
        public double InsideCornerHeight => Math.Max(0, InsideCornerCount) * RepresentativeHeightMm;

        /// <summary>
        /// Tổng chiều dài khe nối đứng (mm). = VerticalJointCount × Height.
        /// Bằng 0 khi LayoutDirection != "Ngang".
        /// </summary>
        public double VerticalJointTotalLength =>
            string.Equals(LayoutDirection, "Ngang", StringComparison.OrdinalIgnoreCase)
                ? Math.Max(0, VerticalJointCount) * RepresentativeHeightMm
                : 0.0;

        /// <summary>
        /// Chieu chia tam (mm) - chieu duoc chia boi PanelWidth.
        /// Doc: chia theo chieu dai vach.
        /// Ngang: chia theo chieu cao vach.
        /// </summary>
        public double DivisionSpan => LayoutDirection == "Ngang" ? RepresentativeHeightMm : Length;

        /// <summary>
        /// Chieu span cua tam (mm) - chieu con lai.
        /// Doc: span = chieu cao vach.
        /// Ngang: span = chieu dai vach.
        /// </summary>
        public double PanelSpan => LayoutDirection == "Ngang" ? Length : RepresentativeHeightMm;
        public int EstimatedPanelCount
        {
            get
            {
                if (PanelWidth <= 0 || DivisionSpan <= 0)
                    return 0;

                if (PolygonVertices != null && PolygonVertices.Count >= 3)
                {
                    var breakdown = GetPanelBreakdown();
                    return breakdown
                        .Where(e => !IsWasteLabel(e.Label))
                        .Sum(e => e.Count);
                }

                return (int)Math.Ceiling(DivisionSpan / PanelWidth);
            }
        }

        public List<TenderPanelEntry> GetPanelBreakdown()
        {
            if (PolygonVertices != null && PolygonVertices.Count >= 3)
            {
                bool isHorizontal = LayoutDirection == "Ngang";
                return ScanLineAnalyzer.Analyze(PolygonVertices, PanelWidth, isHorizontal);
            }

            // Vách đa cao độ + lắp dọc: trải tấm liên tục toàn tuyến (không reset theo từng nhịp).
            // Mỗi dải tấm lấy cao độ cấp theo max(H) trong dải; phần chênh lệch + phần vướng lỗ mở là hao hụt.
            if (LayoutDirection == "Dọc")
            {
                var effectiveSegments = GetEffectiveHeightSegments();
                if (effectiveSegments.Count > 1)
                {
                    bool hasResolvableOpenings = Openings.Count == 0
                        || Openings.All(o => o != null
                            && o.Width > 0
                            && o.Height > 0
                            && o.CenterStationMm >= 0);
                    if (hasResolvableOpenings)
                        return BuildContinuousVerticalBreakdown(effectiveSegments, Openings);
                }
            }
            else if (LayoutDirection == "Ngang")
            {
                bool hasResolvableOpenings = Openings.Count == 0
                    || Openings.All(o => o != null
                        && o.Width > 0
                        && o.Height > 0
                        && o.CenterStationMm >= 0);
                if (hasResolvableOpenings)
                    return BuildContinuousHorizontalBreakdown(Openings);
            }

            var entries = new List<TenderPanelEntry>();
            if (PanelWidth <= 0 || DivisionSpan <= 0 || PanelSpan <= 0)
                return entries;

            int totalPanels = EstimatedPanelCount;
            int totalReducedPanels = 0;
            int remainingReduciblePanels = totalPanels;
            var reducedGroups = new Dictionary<double, int>();
            var openingWasteGroups = new Dictionary<(double Width, double Length), int>();

            foreach (var op in Openings)
            {
                if (remainingReduciblePanels <= 0)
                    break;

                double opDivDim = LayoutDirection == "Ngang" ? op.Height : op.Width;
                double opSpanDim = LayoutDirection == "Ngang" ? op.Width : op.Height;

                if (opDivDim >= 2.0 * PanelWidth)
                {
                    int panelsInOp = Math.Max(0, (int)Math.Floor(opDivDim / PanelWidth) - 1);
                    int totalForThisOp = panelsInOp * op.Quantity;
                    double reducedSpan = Math.Max(0, PanelSpan - opSpanDim);
                    int cappedForThisOp = Math.Min(totalForThisOp, remainingReduciblePanels);

                    if (cappedForThisOp > 0 && reducedSpan > 0)
                    {
                        var overlapBands = ResolveOpeningDivisionOverlapBands(op).ToList();
                        bool hasFullBandCut = overlapBands.Any(w => w >= PanelWidth - 1.0);
                        bool appliedFullBandSplit = false;

                        if (hasFullBandCut
                            && TryResolveFullBandSplitLengths(op, out var seg1Length, out var seg2Length))
                        {
                            AddReducedPiece(seg1Length, cappedForThisOp);
                            AddReducedPiece(seg2Length, cappedForThisOp);
                            appliedFullBandSplit = true;
                        }

                        if (!appliedFullBandSplit)
                        {
                            AddReducedPiece(reducedSpan, cappedForThisOp);
                        }

                        totalReducedPanels += cappedForThisOp;
                        remainingReduciblePanels -= cappedForThisOp;

                        if (opSpanDim > 1)
                        {
                            int openingQty = Math.Max(1, op.Quantity);
                            if (overlapBands.Count == 0)
                            {
                                double fallbackWidth = Math.Round(Math.Min(PanelWidth, Math.Max(1, opDivDim)));
                                if (fallbackWidth < PanelWidth - 1.0)
                                {
                                    var wasteKey = (Width: fallbackWidth, Length: Math.Round(opSpanDim));
                                    if (openingWasteGroups.ContainsKey(wasteKey))
                                        openingWasteGroups[wasteKey] += cappedForThisOp;
                                    else
                                        openingWasteGroups[wasteKey] = cappedForThisOp;
                                }
                            }
                            else
                            {
                                foreach (var bandWidth in overlapBands)
                                {
                                    // Cắt trọn bề rộng dải panel => không tính là hao hụt.
                                    if (bandWidth >= PanelWidth - 1.0)
                                        continue;

                                    var wasteKey = (Width: bandWidth, Length: Math.Round(opSpanDim));
                                    int addCount = openingQty;
                                    if (openingWasteGroups.ContainsKey(wasteKey))
                                        openingWasteGroups[wasteKey] += addCount;
                                    else
                                        openingWasteGroups[wasteKey] = addCount;
                                }
                            }
                        }

                        void AddReducedPiece(double pieceLength, int count)
                        {
                            double roundedLength = Math.Round(pieceLength);
                            if (roundedLength <= 1.0 || count <= 0)
                                return;

                            if (reducedGroups.ContainsKey(roundedLength))
                                reducedGroups[roundedLength] += count;
                            else
                                reducedGroups[roundedLength] = count;
                        }
                    }
                }
            }

            totalReducedPanels = Math.Min(totalReducedPanels, totalPanels);

            double remnantW = DivisionSpan - (totalPanels - 1) * PanelWidth;
            bool hasRemnant = remnantW > 0 && remnantW < PanelWidth - 1;
            double wasteW = hasRemnant ? PanelWidth - remnantW : 0;

            int normalPanels = totalPanels - totalReducedPanels;
            if (normalPanels > 0)
            {
                entries.Add(new TenderPanelEntry
                {
                    WidthMm = PanelWidth,
                    LengthMm = PanelSpan,
                    Count = normalPanels,
                    Label = "Nguyên"
                });
            }

            foreach (var kv in reducedGroups.OrderByDescending(x => x.Key))
            {
                entries.Add(new TenderPanelEntry
                {
                    WidthMm = PanelWidth,
                    LengthMm = kv.Key,
                    Count = kv.Value,
                    Label = "Giảm (lỗ mở)"
                });
            }

            if (hasRemnant && wasteW > 1)
            {
                entries.Add(new TenderPanelEntry
                {
                    WidthMm = Math.Round(wasteW),
                    LengthMm = PanelSpan,
                    Count = 1,
                    Label = "Hao hụt"
                });
            }

            foreach (var kv in openingWasteGroups.OrderByDescending(x => x.Key.Length).ThenByDescending(x => x.Key.Width))
            {
                entries.Add(new TenderPanelEntry
                {
                    WidthMm = kv.Key.Width,
                    LengthMm = kv.Key.Length,
                    Count = kv.Value,
                    Label = WasteLabelOpening
                });
            }

            return entries;
        }

        private List<TenderPanelEntry> BuildContinuousHorizontalBreakdown(
            IReadOnlyList<TenderOpening>? openings)
        {
            var entries = new List<TenderPanelEntry>();
            if (PanelWidth <= 0 || DivisionSpan <= 0 || PanelSpan <= 0)
                return entries;

            int totalPanels = (int)Math.Ceiling(DivisionSpan / PanelWidth);
            if (totalPanels <= 0)
                return entries;

            var reducedGroups = new Dictionary<double, int>();
            var wasteGroups = new Dictionary<(double Width, double Length), int>();
            var openingWasteGroups = new Dictionary<(double Width, double Length), int>();
            int normalPanels = 0;

            var openingRanges = BuildOpeningRanges(openings)
                .Select(o =>
                {
                    double start = Math.Max(0, Math.Min(PanelSpan, o.Start));
                    double end = Math.Max(start, Math.Min(PanelSpan, o.End));
                    double bottom = Math.Max(0, Math.Min(DivisionSpan, o.Bottom));
                    double top = Math.Max(bottom, Math.Min(DivisionSpan, o.Top));
                    return (Start: start, End: end, Bottom: bottom, Top: top);
                })
                .Where(o => o.End - o.Start > 1.0 && o.Top - o.Bottom > 1.0)
                .ToList();

            for (int panelIndex = 0; panelIndex < totalPanels; panelIndex++)
            {
                double divStart = panelIndex * PanelWidth;
                double divEnd = Math.Min((panelIndex + 1) * PanelWidth, DivisionSpan);
                double installedDiv = Math.Max(0, divEnd - divStart);
                if (installedDiv <= 0.5)
                    continue;

                double removedArea = 0;
                double remnantDiv = Math.Max(0, PanelWidth - installedDiv);
                if (remnantDiv > 1.0)
                {
                    removedArea += remnantDiv * PanelSpan;
                    AddWaste(remnantDiv, PanelSpan, wasteGroups);
                }

                foreach (var opening in openingRanges)
                {
                    double overlapDiv = Math.Max(0, Math.Min(divEnd, opening.Top) - Math.Max(divStart, opening.Bottom));
                    if (overlapDiv <= 1.0)
                        continue;

                    double overlapSpan = Math.Max(0, Math.Min(PanelSpan, opening.End) - Math.Max(0, opening.Start));
                    if (overlapSpan <= 1.0)
                        continue;

                    removedArea += overlapDiv * overlapSpan;
                    // Cắt trọn bề rộng dải panel (kể cả dải cuối không đủ khổ) không tính hao hụt.
                    if (overlapDiv < installedDiv - 1.0)
                        AddWaste(overlapDiv, overlapSpan, openingWasteGroups);
                }

                double nominalArea = PanelWidth * PanelSpan;
                removedArea = Math.Max(0, Math.Min(nominalArea, removedArea));
                if (removedArea <= 1.0)
                {
                    normalPanels++;
                    continue;
                }

                double remainingLength = (nominalArea - removedArea) / PanelWidth;
                if (remainingLength > 1.0)
                {
                    double key = Math.Round(remainingLength);
                    if (reducedGroups.ContainsKey(key))
                        reducedGroups[key]++;
                    else
                        reducedGroups[key] = 1;
                }
            }

            if (normalPanels > 0)
            {
                entries.Add(new TenderPanelEntry
                {
                    WidthMm = PanelWidth,
                    LengthMm = PanelSpan,
                    Count = normalPanels,
                    Label = "Nguyên"
                });
            }

            foreach (var kv in reducedGroups.OrderByDescending(x => x.Key))
            {
                entries.Add(new TenderPanelEntry
                {
                    WidthMm = PanelWidth,
                    LengthMm = kv.Key,
                    Count = kv.Value,
                    Label = "Giảm (lỗ mở)"
                });
            }

            foreach (var kv in openingWasteGroups.OrderByDescending(x => x.Key.Length).ThenByDescending(x => x.Key.Width))
            {
                entries.Add(new TenderPanelEntry
                {
                    WidthMm = kv.Key.Width,
                    LengthMm = kv.Key.Length,
                    Count = kv.Value,
                    Label = WasteLabelOpening
                });
            }

            foreach (var kv in wasteGroups.OrderByDescending(x => x.Key.Length).ThenByDescending(x => x.Key.Width))
            {
                entries.Add(new TenderPanelEntry
                {
                    WidthMm = kv.Key.Width,
                    LengthMm = kv.Key.Length,
                    Count = kv.Value,
                    Label = "Hao hụt"
                });
            }

            return entries;

            static void AddWaste(
                double wasteWidth,
                double wasteLength,
                Dictionary<(double Width, double Length), int> dict)
            {
                double widthKey = Math.Round(wasteWidth);
                double lengthKey = Math.Round(wasteLength);
                if (widthKey <= 0 || lengthKey <= 0)
                    return;

                var key = (Width: widthKey, Length: lengthKey);
                if (dict.ContainsKey(key))
                    dict[key]++;
                else
                    dict[key] = 1;
            }
        }

        private List<TenderPanelEntry> BuildContinuousVerticalBreakdown(
            IReadOnlyList<TenderHeightSegment> segments,
            IReadOnlyList<TenderOpening>? openings)
        {
            var entries = new List<TenderPanelEntry>();
            if (PanelWidth <= 0 || Length <= 0 || segments.Count == 0)
                return entries;

            int stripCount = (int)Math.Ceiling(Length / PanelWidth);
            if (stripCount <= 0)
                return entries;

            var ranges = BuildSegmentRanges(segments);
            var openingRanges = BuildOpeningRanges(openings);
            var orderedGroups = new Dictionary<(double Width, double Height), int>();
            var wasteGroups = new Dictionary<(double Width, double WasteHeight), int>();
            var openingWasteGroups = new Dictionary<(double Width, double WasteHeight), int>();

            for (int strip = 0; strip < stripCount; strip++)
            {
                double stripStart = strip * PanelWidth;
                double stripEnd = Math.Min((strip + 1) * PanelWidth, Length);
                double stripWidth = Math.Max(0, stripEnd - stripStart);
                if (stripWidth <= 0)
                    continue;

                double maxHeight = 0;
                double netArea = 0;

                foreach (var range in ranges)
                {
                    double overlap = Math.Max(0, Math.Min(stripEnd, range.End) - Math.Max(stripStart, range.Start));
                    if (overlap <= 0)
                        continue;

                    maxHeight = Math.Max(maxHeight, range.HeightMm);
                    netArea += overlap * range.HeightMm;
                }

                if (maxHeight <= 0)
                    continue;

                // Khối lượng cấp: mỗi dải lấy theo cao độ max trong dải.
                var orderedKey = (Width: Math.Round((double)PanelWidth), Height: Math.Round(maxHeight));
                if (orderedGroups.ContainsKey(orderedKey))
                    orderedGroups[orderedKey]++;
                else
                    orderedGroups[orderedKey] = 1;

                // Hao hụt giao bậc trong dải (không tính lỗ mở).
                double orderedArea = PanelWidth * maxHeight;
                double stepWasteArea = Math.Max(0, orderedArea - netArea);
                if (stepWasteArea > 1.0)
                {
                    double wasteHeight = stepWasteArea / PanelWidth;
                    var wasteKey = (Width: Math.Round((double)PanelWidth), WasteHeight: Math.Round(wasteHeight));
                    if (wasteGroups.ContainsKey(wasteKey))
                        wasteGroups[wasteKey]++;
                    else
                        wasteGroups[wasteKey] = 1;
                }

                // Hao hụt do lỗ mở: lưu theo kích thước cắt thực tế trên từng dải tấm.
                foreach (var opening in openingRanges)
                {
                    double overlapWidth = Math.Max(0, Math.Min(stripEnd, opening.End) - Math.Max(stripStart, opening.Start));
                    if (overlapWidth <= 0)
                        continue;

                    double cutHeight = Math.Max(0, Math.Min(maxHeight, opening.Top) - Math.Max(0, opening.Bottom));
                    if (cutHeight <= 0)
                        continue;

                    // Cắt trọn bề rộng dải panel không tính hao hụt.
                    if (overlapWidth < stripWidth - 1.0)
                    {
                        var wasteKey = (Width: Math.Round(overlapWidth), WasteHeight: Math.Round(cutHeight));
                        if (openingWasteGroups.ContainsKey(wasteKey))
                            openingWasteGroups[wasteKey]++;
                        else
                            openingWasteGroups[wasteKey] = 1;
                    }
                }

            }

            foreach (var pair in orderedGroups.OrderByDescending(p => p.Key.Height).ThenByDescending(p => p.Key.Width))
            {
                entries.Add(new TenderPanelEntry
                {
                    WidthMm = pair.Key.Width,
                    LengthMm = pair.Key.Height,
                    Count = pair.Value,
                    Label = "Nguyên"
                });
            }

            foreach (var pair in openingWasteGroups.OrderByDescending(p => p.Key.WasteHeight).ThenByDescending(p => p.Key.Width))
            {
                entries.Add(new TenderPanelEntry
                {
                    WidthMm = pair.Key.Width,
                    LengthMm = pair.Key.WasteHeight,
                    Count = pair.Value,
                    Label = WasteLabelOpening
                });
            }

            foreach (var pair in wasteGroups.OrderByDescending(p => p.Key.WasteHeight).ThenByDescending(p => p.Key.Width))
            {
                entries.Add(new TenderPanelEntry
                {
                    WidthMm = pair.Key.Width,
                    LengthMm = pair.Key.WasteHeight,
                    Count = pair.Value,
                    Label = "Hao hụt"
                });
            }

            return entries;
        }

        private static IReadOnlyList<(double Start, double End, double HeightMm)> BuildSegmentRanges(
            IReadOnlyList<TenderHeightSegment> segments)
        {
            var ranges = new List<(double Start, double End, double HeightMm)>(segments.Count);
            double cursor = 0;
            foreach (var segment in segments)
            {
                double end = cursor + Math.Max(0, segment.LengthMm);
                ranges.Add((cursor, end, Math.Max(0, segment.HeightMm)));
                cursor = end;
            }

            return ranges;
        }

        private static IReadOnlyList<(double Start, double End, double Bottom, double Top)> BuildOpeningRanges(
            IReadOnlyList<TenderOpening>? openings)
        {
            var ranges = new List<(double Start, double End, double Bottom, double Top)>();
            if (openings == null || openings.Count == 0)
                return ranges;

            foreach (var opening in openings)
            {
                if (opening == null
                    || opening.Width <= 0
                    || opening.Height <= 0
                    || opening.CenterStationMm < 0)
                {
                    continue;
                }

                double start = opening.CenterStationMm - opening.Width / 2.0;
                double end = start + opening.Width;
                double bottom = Math.Max(0, opening.BottomElevationMm);
                double top = bottom + Math.Max(0, opening.Height);
                int qty = Math.Max(1, opening.Quantity);

                for (int i = 0; i < qty; i++)
                    ranges.Add((start, end, bottom, top));
            }

            return ranges;
        }

        private IEnumerable<double> ResolveOpeningDivisionOverlapBands(TenderOpening opening)
        {
            if (opening == null || PanelWidth <= 0 || DivisionSpan <= 0)
                yield break;

            if (LayoutDirection == "Ngang")
            {
                double start = Math.Max(0, opening.BottomElevationMm);
                double end = Math.Max(start, Math.Min(DivisionSpan, start + Math.Max(0, opening.Height)));
                foreach (var width in BuildOverlapBands(start, end))
                    yield return width;
                yield break;
            }

            if (opening.CenterStationMm < 0)
                yield break;

            double startAlongLength = Math.Max(0, opening.CenterStationMm - opening.Width / 2.0);
            double endAlongLength = Math.Max(startAlongLength, Math.Min(DivisionSpan, startAlongLength + Math.Max(0, opening.Width)));
            foreach (var width in BuildOverlapBands(startAlongLength, endAlongLength))
                yield return width;
        }

        private bool TryResolveFullBandSplitLengths(
            TenderOpening opening,
            out double firstPieceLengthMm,
            out double secondPieceLengthMm)
        {
            firstPieceLengthMm = 0;
            secondPieceLengthMm = 0;

            if (opening == null || PanelSpan <= 0)
                return false;

            double spanStart;
            double spanEnd;

            if (LayoutDirection == "Ngang")
            {
                if (opening.CenterStationMm < 0 || opening.Width <= 0)
                    return false;

                double rawStart = opening.CenterStationMm - opening.Width / 2.0;
                spanStart = Math.Max(0, Math.Min(PanelSpan, rawStart));
                spanEnd = Math.Max(spanStart, Math.Min(PanelSpan, rawStart + opening.Width));
            }
            else
            {
                if (opening.Height <= 0)
                    return false;

                spanStart = Math.Max(0, Math.Min(PanelSpan, opening.BottomElevationMm));
                spanEnd = Math.Max(spanStart, Math.Min(PanelSpan, spanStart + opening.Height));
            }

            if (spanEnd - spanStart <= 1.0)
                return false;

            firstPieceLengthMm = Math.Max(0, spanStart);
            secondPieceLengthMm = Math.Max(0, PanelSpan - spanEnd);
            return true;
        }

        private IEnumerable<double> BuildOverlapBands(double start, double end)
        {
            if (end - start <= 1.0)
                yield break;

            int firstStrip = Math.Max(0, (int)Math.Floor(start / PanelWidth));
            int lastStrip = Math.Max(firstStrip, (int)Math.Ceiling(end / PanelWidth) - 1);
            for (int strip = firstStrip; strip <= lastStrip; strip++)
            {
                double stripStart = strip * PanelWidth;
                double stripEnd = Math.Min((strip + 1) * PanelWidth, DivisionSpan);
                double overlap = Math.Max(0, Math.Min(stripEnd, end) - Math.Max(stripStart, start));
                double rounded = Math.Round(overlap);
                if (rounded > 0)
                    yield return rounded;
            }
        }

        public double OrderedAreaM2
        {
            get
            {
                var breakdown = GetPanelBreakdown();
                if (breakdown.Count == 0)
                    return EstimatedPanelCount * PanelWidth * PanelSpan / 1_000_000.0;

                return breakdown.Sum(e => e.AreaM2);
            }
        }

        public static readonly string[] CategoryOptions = { "Vách", "Trần", "Nền", "Ốp cột", "Mái" };
        public static readonly string[] LayoutDirectionOptions = { "Dọc", "Ngang" };

        public static string DefaultLayoutDirection(string category) => category switch
        {
            "Trần" => "Ngang",
            "Nền" => "Ngang",
            "Mái" => "Ngang",
            "Ốp cột" => "Dọc",
            _ => "Dọc"
        };
    }
}
