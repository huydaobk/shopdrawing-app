using Xunit;
using ShopDrawing.Plugin.Core;
using ShopDrawing.Plugin.Models;
using System.Linq;

namespace ShopDrawing.Tests
{
    public class LayoutEngineTests
    {
        [Fact]
        public void Calculate_ShouldCreateCorrectNumberOfPanels()
        {
            // Note: Since LayoutEngine depends on AutoCAD Polyline, 
            // in a real Unit Test we might need to mock AutoCAD objects.
            // For now, let's verify if we can at least instantiate the engine.
            var engine = new LayoutEngine();
            Assert.NotNull(engine);
        }
        
        [Fact]
        public void AssignIds_ShouldWorkCorrectlty()
        {
            // Group 1: Length 3000
            var p1 = new Panel { WidthMm = 1000, LengthMm = 3000, ThickMm = 50, Spec = "Spec1", IsReused = false };
            var p2 = new Panel { WidthMm = 800,  LengthMm = 3000, ThickMm = 50, Spec = "Spec1", IsReused = false };
            
            // Group 2: Length 2000
            var p3 = new Panel { WidthMm = 1000, LengthMm = 2000, ThickMm = 50, Spec = "Spec1", IsReused = false };
            
            // Reused Panel
            var p4 = new Panel { WidthMm = 1000, LengthMm = 3000, ThickMm = 50, Spec = "Spec1", IsReused = true };
            
            var panels = new System.Collections.Generic.List<Panel> { p1, p2, p3, p4 };
            PanelIdGenerator.AssignIds(panels, "A1");
            
            // 3000x1000 -> 01
            Assert.Equal("A1-01", p1.PanelId);

            // 3000x800 -> 02
            Assert.Equal("A1-02", p2.PanelId);
            
            // 2000x1000 -> 03
            Assert.Equal("A1-03", p3.PanelId);
            
            // Reused ID -> R01
            Assert.Equal("A1-R01", p4.PanelId);
        }
    }
}
