# Changelog - ShopDrawing AutoCAD Plugin

## [2026-04-20] - v0.2.27 Tender Polygon Waste & Preview UI ≡ƒÜÇ
### Changed
- **Tender Waste Logic**: Tß╗æi ╞░u logic t├¡nh to├ín khß╗æi l╞░ß╗úng hao hß╗Ñt cho v├ích "Pick V├╣ng" (Polygon) ─æß╗ông nhß║Ñt vß╗¢i Pick v├ích. Bß║»t ch├¡nh x├íc l╞░ß╗úng "Hao hß╗Ñt (Lß╗ù mß╗ƒ)" (Grazing Waste) do lß╗ù cß║»t lß║╣m 1 phß║ºn, v├á sß╗¡a lß╗ùi t├¡nh mß║⌐u vß╗Ñn tß║Ñm cuß╗æi kh├┤ng ch├¡nh x├íc khi dß║úi tß║Ñm bß╗ï chia cß║»t.
- **Tender CAD Preview**: Tß╗æi ╞░u ─æ╞░ß╗¥ng gh├⌐p tß║Ñm tr├¬n l╞░ß╗¢i CAD/WPF, tß╗▒ ─æß╗Öng loß║íi bß╗Å c├íc ─æoß║ín thß║│ng ─æi xuy├¬n qua kh├┤ng gian lß╗ù mß╗ƒ, gi├║p bß║ún vß║╜ m├┤ phß╗Ång trß╗▒c quan h╞ín.

## [2026-04-20] - v0.2.26 Tender Excel Export Synchronization ≡ƒôè
### Fixed
- **Excel Reference Bug**: Cß║¡p nhß║¡t lß║íi logic xuß║Ñt b├ío c├ío Excel cho chß╗⌐c n─âng Tender, ─æß║úm bß║úo tham chiß║┐u chß╗ë sß╗æ ch├¡nh x├íc ß╗ƒ phß║ºn tß╗òng (Tß╗òng diß╗çn t├¡ch dß╗▒ kiß║┐n cß║Ñp v├á Khß╗æi l╞░ß╗úng hao hß╗Ñt) ─æß║┐n ─æ├║ng c├íc cß╗Öt dß╗» liß╗çu thay v├¼ cß╗Öt sß╗æ l╞░ß╗úng tß║Ñm.


## [2026-04-20] - v0.2.25 Tender Opening CAD Projection Fix ≡ƒöº
### Fixed
- **CAD Projection**: Sß╗¡a lß╗ùi sai tß╗ìa ─æß╗Ö lß╗ù mß╗ƒ khi bß║Ñm "Vß║╜ CAD" trong chß║┐ ─æß╗Ö "Pick D├ái" bß║▒ng c├ích bß║»t tu├ón thß╗º tß╗ìa ─æß╗Ö chuß║⌐n Unroll thay v├¼ tß╗ìa ─æß╗Ö chuß╗Öt tuyß╗çt ─æß╗æi.

## [2026-04-20] - v0.2.24 Tender Project Folder Structure ≡ƒôü
### Changed
- Cß║¡p nhß║¡t cß║Ñu tr├║c th╞░ mß╗Ñc dß╗» liß╗çu dß╗▒ ├ín tß╗½ `ShopDrawingData` sang `Project Data` ─æß╗â bao qu├ít h╞ín (chß╗⌐a Tender, Shopdrawing, Production).
- Tß╗▒ ─æß╗Öng gom c├íc file Excel xuß║Ñt BOM v├áo th╞░ mß╗Ñc `BOQ` / `Tender` t╞░╞íng ß╗⌐ng.
- Khß║»c phß╗Ñc lß╗ùi test case v├á ─æß║úm bß║úo file `Project Data` marker ─æ╞░ß╗úc tß║ío th├ánh c├┤ng trong m├┤i tr╞░ß╗¥ng runtime.

## [2026-04-20] - v0.2.23 Tender Net Area Relabeling ≡ƒÅ╖∩╕Å
### Changed
- **UI Label Update**: ─Éß╗òi t├¬n cß╗Öt ti├¬u ─æß╗ü `DT net (m┬▓)` th├ánh `DT nghiß╗çm thu (m┬▓)` trong form xuß║Ñt b├ío c├ío Excel BOM ─æß╗â trß╗ƒ n├¬n trß╗▒c quan v├á dß╗à hiß╗âu h╞ín ─æß╗æi vß╗¢i nghiß╗çp vß╗Ñ nghiß╗çm thu c├┤ng tr╞░ß╗¥ng, giß╗» nguy├¬n t├¡nh ─æ├║ng ─æß║»n cß╗ºa logic t├¡nh to├ín.

## [2026-04-18] - v0.2.22 Panel Splitting Optimization Γ£é∩╕Å
### Added
- **T├¡nh n─âng mß╗¢i**: Tß╗æi ╞░u tß╗▒ ─æß╗Öng chia tß║Ñm (splitting panel) khi ─æi qua lß╗ù mß╗ƒ "nguy├¬n khß╗ò". `ScanLineAnalyzer` hiß╗çn tß║íi ─æ├ú t├¡nh to├ín ch├¡nh x├íc ─æß╗â ph├ón t├ích ─æoß║ín tr├¬n v├á d╞░ß╗¢i, v├á nhß║úy nhß╗ïp (skip) ß╗ƒ v├╣ng ─æi qua lß╗ù mß╗ƒ.
- **Cß║úi thiß╗çn UI**: ─É╞░ß╗¥ng chia nhß╗ïp tr├¬n giao diß╗çn xem tr╞░ß╗¢c (CAD preview) tß╗▒ ─æß╗Öng kh├┤ng ─æi xuy├¬n qua kh├┤ng gian lß╗ù mß╗ƒ.

## [2026-04-18] - v0.2.21 Tender Excel Geometric Area Export ≡ƒôè
### Changed
- **Tender UI Optimization** - ─Éß╗ông bß╗Ö h├│a gi├í trß╗ï diß╗çn t├¡ch h├¼nh hß╗ìc thß╗▒c tß║┐ (tß╗½ Pick V├╣ng/Pick D├ái) hiß╗ân thß╗ï trß╗▒c tiß║┐p v├áo file Excel xuß║Ñt BOM thay v├¼ ├íp dß╗Ñng c├┤ng thß╗⌐c D├ái x Rß╗Öng c┼⌐, gi├║p ─æß╗ông nhß║Ñt dß╗» liß╗çu hiß╗ân thß╗ï (source of truth) giß╗»a AutoCAD, App v├á Excel. Ghi ch├║ tß║íi Excel c┼⌐ng cß║¡p nhß║¡t th├┤ng b├ío r├╡ gß╗æc tr├¡ch xuß║Ñt tß╗½ m├┤ h├¼nh CAD.
- **Tender Opening Logic** - Cß╗Öt diß╗çn t├¡ch lß╗ù mß╗ƒ trong Excel ─æ╞░ß╗úc trß║ú lß║íi form c├┤ng thß╗⌐c gß╗æc `Rß╗Öng * Cao * SL / 1000000` ─æß╗â ng╞░ß╗¥i d├╣ng c├│ thß╗â linh hoß║ít nhß║¡p tay hoß║╖c tinh chß╗ënh c├íc tham sß╗æ nß║┐u cß║ºn thiß║┐t m├á kh├┤ng bß╗ï ß║únh h╞░ß╗ƒng.

## [2026-04-18] - v0.2.20 Tender UI Dimension Locking ≡ƒöÆ
### Changed
- **Tender UI Optimization**: C├íc cß╗Öt k├¡ch th╞░ß╗¢c trong bß║úng Quß║ún l├╜ khß╗æi l╞░ß╗úng ch├áo gi├í bao gß╗ôm: D├ái, Cao (cß╗ºa v├ích) v├á Rß╗Öng, Cao, Cao ─æß╗Ö ─æ├íy (cß╗ºa lß╗ù mß╗ƒ) ─æ├ú ─æ╞░ß╗úc kh├│a lß║íi (read-only). Dß╗» liß╗çu n├áy ─æ╞░ß╗úc lß║Ñy trß╗▒c tiß║┐p tß╗½ viß╗çc bß║»t ─æiß╗âm/dß╗▒ng h├¼nh tr├¡ch xuß║Ñt tß╗½ CAD. Viß╗çc kh├│a lß║íi ─æß╗â ─æß║úm bß║úo t├¡nh ─æß╗ông nhß║Ñt dß╗» liß╗çu v├á bß║úo to├án "nguß╗ôn sß╗▒ thß║¡t" tß╗½ m├┤ h├¼nh (tr├ính viß╗çc sß╗¡a tay nhß║ºm tr├¬n DataGrid).

## [2026-04-18] - v0.2.19 Tender Data Persistence Fix ≡ƒÆ╛
### Fixed
- **Tender Data Loss**: Khß║»c phß╗Ñc triß╗çt ─æß╗â lß╗ùi thß╗ënh thoß║úng mß║Ñt dß╗» liß╗çu Tender khi tß║»t bß║¡t Autocad. ─É├ú hook trß╗▒c tiß║┐p v├áo sß╗▒ kiß╗çn `Database.SaveComplete` nguy├¬n thß╗ºy cß╗ºa hß║í tß║ºng CAD. Mß╗ùi khi ng╞░ß╗¥i d├╣ng bß║Ñm Save hoß║╖c Save As, tß╗çp tin dß╗» liß╗çu dß╗▒ ├ín (`.json`) sß║╜ tß╗▒ ─æß╗Öng ─æ╞░ß╗úc ─æß╗ông bß╗Ö l╞░u ngay lß║¡p tß╗⌐c b├¬n cß║ính th╞░ mß╗Ñc chß╗⌐a tß╗çp `dwg` mß╗¢i nhß║Ñt, ─æß║úm bß║úo t├¡nh bß╗ün vß╗»ng (persistence) cß╗ºa dß╗» liß╗çu.

## [2026-04-18] - v0.2.18 Tender Preview Text Overlay Fix ≡ƒôÉ
### Changed
- **Tender UI Optimization** - T─âng k├¡ch th╞░ß╗¢c n├⌐t chß╗» (text size) trong Preview (Vß║╜ CAD) cho k├¡ch th╞░ß╗¢c panel v├á lß╗ù mß╗ƒ (vd: size t─âng tß╗½ 70 -> 150).
- C─ân chß╗ënh lß║íi `Justify` v├á `Offset` cho Text trong AutoCAD Preview ─æß╗â sß╗æ (dimension text) kh├┤ng bß╗ï ─æ├¿ d├¡nh gß║ích trß╗▒c tiß║┐p l├¬n c├íc n├⌐t red/green line cß╗ºa viß╗ün / m├¡ nß╗æi (joints). Text giß╗¥ ─æ├óy tß╗▒ ─æß╗Öng d├án s├íng ra m├⌐p ngo├ái dß╗▒a theo ph╞░╞íng ph├íp gi├│ng phß║úi ngang/tr├íi t╞░╞íng ß╗⌐ng.

## [2026-04-18] - v0.2.17 Immediate Preview on Pick Lß╗ù Mß╗ƒ ΓÜí
### Changed
- **Tender UI Optimization** - Khi user thß╗▒c hiß╗çn thao t├íc (Pick Lß╗ù mß╗ƒ), khung Preview v├á bß║úng khß╗æi l╞░ß╗úng (BOM) b├¬n d╞░ß╗¢i sß║╜ cß║¡p nhß║¡t giao diß╗çn ngay lß║¡p tß╗⌐c thay v├¼ phß║úi chß╗¥ ng╞░ß╗¥i d├╣ng bß║Ñm Enter ─æß╗â tho├ít thao t├íc.

## [2026-04-18] - v0.2.16 Tender CAD "Pick Khoß║úng C├ích ─É├íy" Revert ≡ƒÉ¢
### Fixed
- **Tender Geometry Error for Openings** - X├│a bß╗Å lß╗ùi dß╗ïch chuyß╗ân (shift) khoß║úng c├ích ─æ├íy lß║ºn 2 khi l╞░u `OpeningPolygon`. Khß║»c phß╗Ñc triß╗çt ─æß╗â lß╗ù mß╗ƒ Floating khi vß║╜ CAD sau khi pick. (C├íc ─æiß╗âm pick p1, p2 bß║▒ng chuß╗Öt tr├¬n m├án h├¼nh ─æ├ú ngß║ºm ─æß╗ïnh chß╗⌐a cao ─æß╗Ö thß╗▒c tß║┐ rß╗ôi, kh├┤ng cß║ºn cß╗Öng th├¬m tham sß╗æ BottomElevationMm v├áo tß╗ìa ─æß╗Ö `OpeningPolygon` nß╗»a).

## [2026-04-17] - v0.2.15 Tender CAD "Pick V├╣ng" Hole Fix ≡ƒÉ¢

## [2026-04-17] - v0.2.14 Tender CAD "Pick V├╣ng" Fix ≡ƒÉ¢
### Fixed
- **Tender Geometry Error for "Pick V├╣ng"** - Khß║»c phß╗Ñc lß╗ùi khi chß╗ìn v├ích bß║▒ng "Pick v├╣ng" (WallPolygon) vß╗¢i c├íc ─æ╞░ß╗¥ng bao h├¼nh ─æa gi├íc kh├┤ng vu├┤ng g├│c (v├¡ dß╗Ñ v├ích m├íi dß╗æc). Hß╗ç thß╗æng kh├┤ng c├▓n unroll tß╗▒ ─æß╗Öng (trß║úi phß║│ng) bi├¬n dß║íng tr├¬n bß║ún vß║╜ CAD m├á giß╗» nguy├¬n h├¼nh dß║íng nguy├¬n gß╗æc (literal geometry offset) gi├║p ─æß╗ông bß╗Ö tuyß╗çt ─æß╗æi vß╗¢i h├¼nh ß║únh tr├¬n Preview Canvas.
- **Tender CAD Openings Placement Offset** - ─É├ú sß╗¡a lß╗ùi tß╗ìa ─æß╗Ö lß╗ù mß╗ƒ khi dß╗▒ng bß║ún vß║╜ CAD cho v├ích "Pick v├╣ng", c├íc lß╗ù mß╗ƒ b├óy giß╗¥ ─æ╞░ß╗úc offset ho├án to├án chuß║⌐n x├íc theo bi├¬n dß║íng Polyline ch├¡nh.

## [2026-04-17] - v0.2.13 Tender Persistence Fix ≡ƒöº
### Fixed
- **Critical: Tender Wall AutoLoad bß╗ï chß║╖n bß╗ƒi marker file gate** ΓÇö Sau khi cß║¡p nhß║¡t plugin v├á restart CAD, v├ích ─æ├ú vß║╜ mß║Ñt khß╗Åi Bß║úng quß║ún l├╜ khß╗æi l╞░ß╗úng. Nguy├¬n nh├ón: `EnsureProject()` y├¬u cß║ºu file `.shopdrawing-project.json` tß╗ôn tß║íi tr╞░ß╗¢c khi AutoLoad, nh╞░ng AutoSave kh├┤ng tß║ío file n├áy. ─É├ú bß╗Å gate condition thß╗½a v├¼ `TryAutoLoad()` ─æ├ú c├│ guard `File.Exists` ri├¬ng.
- **CS8620 nullability warnings** ΓÇö Sß╗¡a 2 warning `IEnumerable<string?>` trong `TenderBomDialog.CadOps.cs` khi gom CAD handle ─æß╗â group entities.

## [2026-04-17] - v0.2.5 Tender Grouping & Drawing Refinement ≡ƒÜÇ
### Added
- **AutoCAD CAD Grouping**: Tß╗▒ ─æß╗Öng gom nh├│m to├án bß╗Ö cß║Ñu kiß╗çn ─æ╞░ß╗úc sinh ra tß╗½ lß╗çnh vß║╜ Tender (─æ╞░ß╗¥ng bao, panel, lß╗ù mß╗ƒ, ghi ch├║, v├á ─æ╞░ß╗¥ng nß╗æi) v├áo mß╗Öt ─æß╗æi t╞░ß╗úng Group duy nhß║Ñt (`Tender Elevation Group`), gi├║p thao t├íc di chuyß╗ân/x├│a cß║ú cß╗Ñm bß║ún vß║╜ tr├¬n CAD chß╗ë qua 1 click.
- **TraceBoundary Picking (Pick V├╣ng)**: Giß║úi ph├íp linh hoß║ít cho viß╗çc nhß║¡n diß╗çn v├╣ng k├¡n, cho ph├⌐p click v├áo ─æiß╗âm bß║Ñt kß╗│ ─æß╗â tß╗▒ scan lß║Ñy boundary hoß║╖c click trß╗▒c tiß║┐p l├¬n Polyline. Giß║úi quyß║┐t vß║Ñn ─æß╗ü chß╗ìn c├íc v├╣ng dß╗ï dß║íng vu├┤ng hoß║╖c khuyß║┐t.
- **Link Line Rendering**: Tß╗▒ ─æß╗Öng vß║╜ c├íc dß║úi Line tham chiß║┐u m├áu x├ím nhß║ít (`SD_LINK`) ─æß╗â kß║┐t nß╗æi tß╗½ v├╣ng Floorplan ban ─æß║ºu vß╗¢i bß║ún vß║╜ Mß║╖t ─Éß╗⌐ng Panel ─æß╗â kiß╗âm so├ít nguß╗ôn gß╗æc chiß║┐t t├¡nh r├╡ r├áng.
- **Multi-select Opening/Lß╗ù Mß╗ƒ**: T├¡nh n─âng cho ph├⌐p giß╗» ph├¡m trß╗Å h├áng loß║ít lß╗ù mß╗ƒ khi th├¬m v├áo hß╗ç thß╗æng v├á bß╗ò sung khai b├ío Cao ─æß╗Ö ─æ├íy (Bottom Offset) thay v├¼ cß╗æ ─æß╗ïnh = 0.
- **Tender UI Improvements**: Cß║¡p nhß║¡t logic l├ám mß╗¢i UI m╞░ß╗út m├á, render ngay lß║¡p tß╗⌐c th├┤ng sß╗æ lß╗ù mß╗ƒ v├áo Footer cß╗ºa "Pick Nhß╗ïp" sau mß╗ùi lß║ºn chß╗ënh sß╗¡a. Th├¬m ph├¡m tß║»t Shift+Click cho mß╗ƒ nhanh.

### Fixed
- **BOM Deletion Sync Bug**: Khß║»c phß╗Ñc triß╗çt ─æß╗â lß╗ùi khi ng╞░ß╗¥i d├╣ng x├│a d├▓ng v├ích Tender m├á Canvas l╞░ß╗¢i kh├┤ng tß╗▒ ─æß╗Öng x├│a CAD block. T├¡nh n─âng Cleanup CAD Artifacts ─æ├ú dß╗ìn sß║ích c├íc handle c┼⌐ v├á clear Canvas ch├¡nh x├íc.
- **Pick D├ái Dimension Reset**: Sß╗¡a lß╗ùi Panel width/height t├¡nh sai khi dß╗▒ng "Pick d├ái" bß║▒ng c├ích chuß║⌐n ho├í Unit Coordinate v├á ├íp dß╗Ñng Vector quay (Rotate) ─æ├║ng ma trß║¡n ─æiß╗âm g├│c.

## [2026-02-27] - MVP Milestone ≡ƒÜÇ
### Added
- **Phase 05: AutoCAD Drawing**: `BlockManager` for drawing panels, hatches, and tags.
- **Phase 06: Waste Match**: `WasteMatcher` for finding remnants in SQLite DB.
- **Phase 07: UI Dialogs**: Professional WPF dialogs (`WallCreateDialog`, `WasteSuggestionDialog`, `SpecManagerDialog`) implemented in pure C# for max compatibility.
- **Phase 08: BOM & Commands**: 
    - `BomManager`: Live AutoCAD Table for panel statistics.
    - `ShopDrawingCommands`: `SD_WALL_CREATE`, `SD_SPEC`, `SD_BOM`, `SD_WASTE`.
- **Database**: Initialized SQLite schema for waste panels.

### Changed
- **UI Architecture**: Moved from XAML to Programmatic C# to bypass build issues in restricted environments.
- **Target Framework**: Verified .NET 8.0 support for AutoCAD 2026.

### Fixed
- Resolved ambiguity errors between `ShopDrawing.Models.Panel` and `System.Windows.Controls.Panel`.
- Fixed `AttributeReference` initialization bugs in `BlockManager`.
- Modernized `Table` API usage in `BomManager` (Cells vs SetTextString).

### Refactored
- Added robust error handling (try-catch) to all commands and static reactors.
- Improved null-safety and modernized coding patterns across the plugin.
