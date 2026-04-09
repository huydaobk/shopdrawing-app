using System;
using System.IO;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;
using ShopDrawing.Plugin.Runtime;

namespace ShopDrawing.Plugin.Core
{
    internal sealed class ProjectInitializationService
    {
        public bool InitializeInteractive()
        {
            Document? document = Application.DocumentManager.MdiActiveDocument;
            Editor? editor = document?.Editor;

            if (editor == null || document == null)
            {
                return false;
            }

            string? projectRoot = ResolveProjectRoot(editor, document);
            if (string.IsNullOrWhiteSpace(projectRoot))
            {
                editor.WriteMessage("\n[ShopDrawing] Khởi tạo dự án đã bị hủy.");
                return false;
            }

            string initializedRoot = ProjectDataPathResolver.InitializeProjectStructure(projectRoot);
            ShopDrawingRuntimeServices.RefreshProjectScopedServices();

            editor.WriteMessage($"\n[ShopDrawing] Đã khởi tạo dự án tại: {initializedRoot}");
            editor.WriteMessage($"\n[ShopDrawing] Marker: {Path.Combine(initializedRoot, ProjectDataPathResolver.GetProjectMarkerFileName())}");
            editor.WriteMessage($"\n[ShopDrawing] Data:   {Path.Combine(initializedRoot, "ShopDrawingData")}");
            editor.WriteMessage("\n[ShopDrawing] Nếu có bản vẽ dự án, nên lưu chúng trong thư mục Drawings để team dùng thống nhất.");
            return true;
        }

        private static string? ResolveProjectRoot(Editor editor, Document document)
        {
            string? drawingPath = TryGetDrawingPath(document);
            if (!string.IsNullOrWhiteSpace(drawingPath) && Path.IsPathRooted(drawingPath))
            {
                string suggestedRoot = ProjectDataPathResolver.ResolveFromDrawingPath(drawingPath, ensureExists: false).RuntimeRoot;
                return PromptForProjectRoot(editor, suggestedRoot);
            }

            editor.WriteMessage("\n[ShopDrawing] Bản vẽ hiện tại chưa có đường dẫn lưu hợp lệ.");
            return PromptForProjectRoot(editor, null);
        }

        private static string? PromptForProjectRoot(Editor editor, string? suggestedRoot)
        {
            if (!string.IsNullOrWhiteSpace(suggestedRoot))
            {
                var confirmOptions = new PromptKeywordOptions($"\nKhởi tạo dự án tại [{suggestedRoot}]?")
                {
                    AllowNone = true
                };
                confirmOptions.Keywords.Add("Yes");
                confirmOptions.Keywords.Add("Change");
                confirmOptions.Keywords.Default = "Yes";

                PromptResult confirmResult = editor.GetKeywords(confirmOptions);
                if (confirmResult.Status == PromptStatus.Cancel)
                {
                    return null;
                }

                if (string.IsNullOrWhiteSpace(confirmResult.StringResult) ||
                    string.Equals(confirmResult.StringResult, "Yes", StringComparison.OrdinalIgnoreCase))
                {
                    return suggestedRoot;
                }
            }

            var pathOptions = new PromptStringOptions("\nNhập đường dẫn thư mục gốc dự án")
            {
                AllowSpaces = true
            };

            PromptResult pathResult = editor.GetString(pathOptions);
            if (pathResult.Status != PromptStatus.OK)
            {
                return null;
            }

            string projectRoot = pathResult.StringResult.Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(projectRoot))
            {
                editor.WriteMessage("\n[ShopDrawing] Đường dẫn dự án không hợp lệ.");
                return null;
            }

            return projectRoot;
        }

        private static string? TryGetDrawingPath(Document document)
        {
            string? drawingPath = document.Database?.Filename;
            if (string.IsNullOrWhiteSpace(drawingPath))
            {
                drawingPath = document.Name;
            }

            return drawingPath;
        }
    }
}
