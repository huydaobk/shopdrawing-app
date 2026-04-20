using System;
using System.IO;
using ShopDrawing.Plugin.Core;

namespace ShopDrawing.Tests
{
    public class ProjectInitializationStructureTests
    {
        [Fact]
        public void InitializeProjectStructure_ShouldCreateExpectedFolders()
        {
            string tempRoot = Path.Combine(Path.GetTempPath(), "shopdrawing-init", Guid.NewGuid().ToString("N"));

            try
            {
                string initializedRoot = ProjectDataPathResolver.InitializeProjectStructure(tempRoot);

                Assert.Equal(Path.GetFullPath(tempRoot), initializedRoot);
                Assert.True(Directory.Exists(Path.Combine(initializedRoot, "Drawings")));
                Assert.True(Directory.Exists(Path.Combine(initializedRoot, "Project Data")));
                Assert.True(Directory.Exists(Path.Combine(initializedRoot, "Project Data", "Log")));
                Assert.True(Directory.Exists(Path.Combine(initializedRoot, "Project Data", "tender_project")));
                Assert.True(Directory.Exists(Path.Combine(initializedRoot, "Project Data", "shopdrawing_project")));
                Assert.True(Directory.Exists(Path.Combine(initializedRoot, "Project Data", "production_project")));
                Assert.True(Directory.Exists(Path.Combine(initializedRoot, "Project Data", "BOQ")));
                Assert.True(Directory.Exists(Path.Combine(initializedRoot, "Project Data", "BOQ", "Tender")));
                Assert.True(Directory.Exists(Path.Combine(initializedRoot, "Project Data", "BOQ", "Shopdrawing")));
                Assert.True(Directory.Exists(Path.Combine(initializedRoot, "Project Data", "BOQ", "Production")));
                Assert.True(File.Exists(Path.Combine(initializedRoot, ".shopdrawing-project.json")));
            }
            finally
            {
                if (Directory.Exists(tempRoot))
                {
                    Directory.Delete(tempRoot, recursive: true);
                }
            }
        }
    }
}
