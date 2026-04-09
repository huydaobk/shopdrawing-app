using System;
using System.IO;
using ShopDrawing.Plugin.Core;

namespace ShopDrawing.Tests
{
    public class ProjectDataPathResolverTests
    {
        [Fact]
        public void ResolveFromDrawingPath_UsesMarkerRoot()
        {
            string tempRoot = CreateTempDirectory();

            try
            {
                string projectRoot = Path.Combine(tempRoot, "ProjectA");
                string nestedDrawingFolder = Path.Combine(projectRoot, "Drawings", "Sub");
                Directory.CreateDirectory(nestedDrawingFolder);
                File.WriteAllText(Path.Combine(projectRoot, ".shopdrawing-project.json"), "{}");

                string drawingPath = Path.Combine(nestedDrawingFolder, "A01.dwg");
                File.WriteAllText(drawingPath, string.Empty);

                var context = ProjectDataPathResolver.ResolveFromDrawingPath(drawingPath, ensureExists: false);

                Assert.Equal(projectRoot, context.RuntimeRoot);
                Assert.Equal(Path.Combine(projectRoot, "ShopDrawingData"), context.DataDirectory);
                Assert.Equal(Path.Combine(projectRoot, "ShopDrawingData", "logs", "shopdrawing_plugin.log"), context.LogPath);
            }
            finally
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }

        [Fact]
        public void ResolveFromDrawingPath_UsesParentOfDrawingsWhenNoMarker()
        {
            string tempRoot = CreateTempDirectory();

            try
            {
                string projectRoot = Path.Combine(tempRoot, "ProjectB");
                string drawingsFolder = Path.Combine(projectRoot, "Drawings");
                Directory.CreateDirectory(drawingsFolder);

                string drawingPath = Path.Combine(drawingsFolder, "B01.dwg");
                File.WriteAllText(drawingPath, string.Empty);

                var context = ProjectDataPathResolver.ResolveFromDrawingPath(drawingPath, ensureExists: false);

                Assert.Equal(projectRoot, context.RuntimeRoot);
                Assert.Equal(Path.Combine(projectRoot, "ShopDrawingData"), context.DataDirectory);
            }
            finally
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }

        [Fact]
        public void ResolveFromDrawingPath_UsesCurrentFolderWhenNoMarkerAndNotInDrawings()
        {
            string tempRoot = CreateTempDirectory();

            try
            {
                string drawingFolder = Path.Combine(tempRoot, "LooseFiles");
                Directory.CreateDirectory(drawingFolder);

                string drawingPath = Path.Combine(drawingFolder, "C01.dwg");
                File.WriteAllText(drawingPath, string.Empty);

                var context = ProjectDataPathResolver.ResolveFromDrawingPath(drawingPath, ensureExists: false);

                Assert.Equal(drawingFolder, context.RuntimeRoot);
                Assert.Equal(Path.Combine(drawingFolder, "ShopDrawingData"), context.DataDirectory);
            }
            finally
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }

        [Fact]
        public void ResolveFromDrawingPath_CreatesMarkerAndDataFolders_WhenEnsureExists()
        {
            string tempRoot = CreateTempDirectory();

            try
            {
                string projectRoot = Path.Combine(tempRoot, "ProjectC");
                string drawingsFolder = Path.Combine(projectRoot, "Drawings");
                Directory.CreateDirectory(drawingsFolder);

                string drawingPath = Path.Combine(drawingsFolder, "C01.dwg");
                File.WriteAllText(drawingPath, string.Empty);

                var context = ProjectDataPathResolver.ResolveFromDrawingPath(drawingPath, ensureExists: true);

                Assert.True(File.Exists(Path.Combine(projectRoot, ".shopdrawing-project.json")));
                Assert.True(Directory.Exists(context.DataDirectory));
                Assert.True(Directory.Exists(Path.Combine(context.DataDirectory, "logs")));
            }
            finally
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }

        private static string CreateTempDirectory()
        {
            string path = Path.Combine(Path.GetTempPath(), "shopdrawing-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return path;
        }
    }
}
