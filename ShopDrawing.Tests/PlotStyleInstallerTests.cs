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

        [Fact]
        public void NeedsCopy_ShouldUseContentHashInsteadOfTimestampOnly()
        {
            string root = Path.Combine(Path.GetTempPath(), "sd-plotstyle-hash-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);

            string source = Path.Combine(root, "source.ctb");
            string destination = Path.Combine(root, "destination.ctb");
            try
            {
                File.WriteAllText(source, "A");
                File.WriteAllText(destination, "B");

                var sameTimestamp = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                File.SetLastWriteTimeUtc(source, sameTimestamp);
                File.SetLastWriteTimeUtc(destination, sameTimestamp);

                Assert.True(PlotStyleInstaller.NeedsCopy(source, destination));

                File.WriteAllText(destination, "A");
                File.SetLastWriteTimeUtc(destination, sameTimestamp);
                Assert.False(PlotStyleInstaller.NeedsCopy(source, destination));
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, true);
                }
            }
        }
    }
}
