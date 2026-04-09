using System;
using System.Collections.Generic;
using System.Linq;
using ShopDrawing.Plugin.Core;

namespace ShopDrawing.Plugin.Models
{
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

        public double WallAreaM2 => Length * Height / 1_000_000.0;
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
            Openings.Sum(o => Math.Min(o.Height, Height) * 2 * o.Quantity);
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
        public double ExposedEndLength => (StartEdgeExposed ? Height : 0) + (EndEdgeExposed ? Height : 0);
        public double TotalExposedEdgeLength => TopEdgeLength + BottomEdgeLength + ExposedEndLength;
        public double OutsideCornerHeight => Math.Max(0, OutsideCornerCount) * Height;
        public double InsideCornerHeight => Math.Max(0, InsideCornerCount) * Height;

        /// <summary>
        /// Tổng chiều dài khe nối đứng (mm). = VerticalJointCount × Height.
        /// Bằng 0 khi LayoutDirection != "Ngang".
        /// </summary>
        public double VerticalJointTotalLength =>
            string.Equals(LayoutDirection, "Ngang", StringComparison.OrdinalIgnoreCase)
                ? Math.Max(0, VerticalJointCount) * Height
                : 0.0;

        /// <summary>
        /// Chieu chia tam (mm) - chieu duoc chia boi PanelWidth.
        /// Doc: chia theo chieu dai vach.
        /// Ngang: chia theo chieu cao vach.
        /// </summary>
        public double DivisionSpan => LayoutDirection == "Ngang" ? Height : Length;

        /// <summary>
        /// Chieu span cua tam (mm) - chieu con lai.
        /// Doc: span = chieu cao vach.
        /// Ngang: span = chieu dai vach.
        /// </summary>
        public double PanelSpan => LayoutDirection == "Ngang" ? Length : Height;

        public int EstimatedPanelCount
        {
            get
            {
                if (PanelWidth <= 0 || DivisionSpan <= 0)
                    return 0;

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

            var entries = new List<TenderPanelEntry>();
            if (PanelWidth <= 0 || DivisionSpan <= 0 || PanelSpan <= 0)
                return entries;

            int totalPanels = EstimatedPanelCount;
            int totalReducedPanels = 0;
            var reducedGroups = new Dictionary<double, int>();

            foreach (var op in Openings)
            {
                double opDivDim = LayoutDirection == "Ngang" ? op.Height : op.Width;
                double opSpanDim = LayoutDirection == "Ngang" ? op.Width : op.Height;

                if (opDivDim >= 2.0 * PanelWidth)
                {
                    int panelsInOp = Math.Max(0, (int)Math.Floor(opDivDim / PanelWidth) - 1);
                    int totalForThisOp = panelsInOp * op.Quantity;
                    double reducedSpan = Math.Max(0, PanelSpan - opSpanDim);

                    if (totalForThisOp > 0 && reducedSpan > 0)
                    {
                        double key = Math.Round(reducedSpan);
                        if (reducedGroups.ContainsKey(key))
                            reducedGroups[key] += totalForThisOp;
                        else
                            reducedGroups[key] = totalForThisOp;

                        totalReducedPanels += totalForThisOp;
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
                    Count = Math.Min(kv.Value, totalPanels),
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

            return entries;
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

        public static readonly string[] CategoryOptions = { "Vách", "Trần", "Nền", "Ốp cột" };
        public static readonly string[] LayoutDirectionOptions = { "Dọc", "Ngang" };

        public static string DefaultLayoutDirection(string category) => category switch
        {
            "Trần" => "Ngang",
            "Nền" => "Ngang",
            "Ốp cột" => "Dọc",
            _ => "Dọc"
        };
    }
}
