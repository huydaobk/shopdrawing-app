using ShopDrawing.Plugin.Core;

namespace ShopDrawing.Tests
{
    public class LayoutPackingServiceTests
    {
        [Fact]
        public void TryPackViewport_ShouldPlaceNextViewportToTheRight_WhenTopRowHasSpace()
        {
            var usedRegions = new List<(double MinX, double MinY, double MaxX, double MaxY)>
            {
                (10, 102, 90, 200)
            };

            bool packed = LayoutPackingMath.TryPackViewportBounds(
                usedRegions,
                usableMinX: 10,
                usableMaxX: 220,
                usableMinY: 20,
                usableMaxY: 200,
                reqWidth: 80,
                reqHeight: 70,
                spacing: 10,
                titleHeight: 18,
                out var topLeft);

            Assert.True(packed);
            Assert.Equal(100, topLeft.LeftX, 3);
            Assert.Equal(200, topLeft.TopY, 3);
        }

        [Fact]
        public void TryPackViewport_ShouldUseLowerFreeBand_WhenTopRowCannotFit()
        {
            var usedRegions = new List<(double MinX, double MinY, double MaxX, double MaxY)>
            {
                (10, 132, 150, 220)
            };

            bool packed = LayoutPackingMath.TryPackViewportBounds(
                usedRegions,
                usableMinX: 10,
                usableMaxX: 220,
                usableMinY: 20,
                usableMaxY: 220,
                reqWidth: 100,
                reqHeight: 70,
                spacing: 10,
                titleHeight: 18,
                out var topLeft);

            Assert.True(packed);
            Assert.Equal(10, topLeft.LeftX, 3);
            Assert.Equal(122, topLeft.TopY, 3);
        }
    }
}
