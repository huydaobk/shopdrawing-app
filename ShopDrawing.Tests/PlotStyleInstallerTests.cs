using System.IO;
using ShopDrawing.Plugin.Core;

namespace ShopDrawing.Tests
{
    public class PlotStyleInstallerTests
    {
        [Fact]
        public void ResolvePreferredStyleName_ShouldPreferRequestedStyleWhenAvailable()
        {
            var styles = new[] { "monochrome.ctb", "SD_Black.ctb", "custom.ctb" };

            string resolved = PlotStyleInstaller.ResolvePreferredStyleName(styles, "custom.ctb");

            Assert.Equal("custom.ctb", resolved);
        }

        [Fact]
        public void ResolvePreferredStyleName_ShouldFallBackToSdBlackThenMonochrome()
        {
            Assert.Equal(
                PlotStyleInstaller.DefaultPlotStyleName,
                PlotStyleInstaller.ResolvePreferredStyleName(new[] { "monochrome.ctb", "SD_Black.ctb" }, null));

            Assert.Equal(
                PlotStyleInstaller.FallbackPlotStyleName,
                PlotStyleInstaller.ResolvePreferredStyleName(new[] { "monochrome.ctb" }, null));
        }

        [Fact]
        public void FindPlotStylesDirectoryFromAppData_ShouldFindAutoCadProfilePath()
        {
            string root = Path.Combine(Path.GetTempPath(), "sd-plotstyle-test-" + Guid.NewGuid().ToString("N"));
            try
            {
                string plotStylesDir = Path.Combine(
                    root,
                    "Autodesk",
                    "AutoCAD 2026",
                    "R25.1",
                    "enu",
                    "Plotters",
                    "Plot Styles");

                Directory.CreateDirectory(plotStylesDir);

                string? found = PlotStyleInstaller.FindPlotStylesDirectoryFromAppData(root);

                Assert.Equal(plotStylesDir, found);
            }
            finally
            {
                if (Directory.Exists(root))
                    Directory.Delete(root, true);
            }
        }
    }
}
