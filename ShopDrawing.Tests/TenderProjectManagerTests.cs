using System;
using System.IO;
using ShopDrawing.Plugin.Core;
using ShopDrawing.Plugin.Models;

namespace ShopDrawing.Tests
{
    public class TenderProjectManagerTests
    {
        [Fact]
        public void AutoSave_ShouldPreservePrimarySavePath()
        {
            var manager = new TenderProjectManager();
            string manualPath = Path.Combine(Path.GetTempPath(), $"tender-manual-{Guid.NewGuid():N}.json");
            string dwgName = $"dwg-{Guid.NewGuid():N}.dwg";
            string autoPath = manager.GetAutoSavePath(dwgName);

            try
            {
                var project = new TenderProject
                {
                    ProjectName = "Test",
                    CustomerName = "Customer"
                };

                manager.Save(project, manualPath);
                manager.AutoSave(project, dwgName);

                Assert.Equal(manualPath, project.FilePath);

                var reloaded = manager.Load(autoPath);
                Assert.NotNull(reloaded);
                Assert.Equal(manualPath, reloaded!.FilePath);
            }
            finally
            {
                if (File.Exists(manualPath))
                    File.Delete(manualPath);

                if (File.Exists(autoPath))
                    File.Delete(autoPath);
            }
        }

        [Fact]
        public void Load_AutoSavedProjectWithoutPrimarySavePath_ShouldKeepFilePathNull()
        {
            var manager = new TenderProjectManager();
            string dwgName = $"dwg-{Guid.NewGuid():N}.dwg";
            string autoPath = manager.GetAutoSavePath(dwgName);

            try
            {
                var project = new TenderProject
                {
                    ProjectName = "Test",
                    CustomerName = "Customer"
                };

                manager.AutoSave(project, dwgName);

                var reloaded = manager.Load(autoPath);
                Assert.NotNull(reloaded);
                Assert.Null(reloaded!.FilePath);
            }
            finally
            {
                if (File.Exists(autoPath))
                    File.Delete(autoPath);
            }
        }

    }
}
