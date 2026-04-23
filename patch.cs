using System;
using System.IO;
using System.Text.RegularExpressions;

class Program {
    static void Main() {
        string path = @""c:\my_project\shopdrawing-app\ShopDrawing.Plugin\ShopDrawingApp.cs"";
        string code = File.ReadAllText(path);
        
        string hookStr = @""
        private static void HookAutoUpdateForDocument(Document? doc)
        {
            if (doc == null) return;
            doc.CommandEnded -= OnDocumentCommandEnded;
            doc.CommandEnded += OnDocumentCommandEnded;
        }

        private static void OnDocumentCommandEnded(object sender, CommandEventArgs e)
        {
            string cmd = e.GlobalCommandName.ToUpperInvariant();
            if (cmd == """"ERASE"""" || cmd == """"U"""" || cmd == """"UNDO"""" || cmd == """"REDO"""" || cmd == """"GRIP_STRETCH"""" || cmd == """"MOVE"""" || cmd == """"STRETCH"""" || cmd == """"SCALE"""" || cmd == """"ROTATE"""" || cmd == """"COPY"""")
            {
                ShopDrawingRuntimeServices.Settings.NotifyWasteUpdated();
            }
        }
"";

        code = code.Replace(""HookAnnotationScaleForDocument(Application.DocumentManager.MdiActiveDocument);"", ""HookAnnotationScaleForDocument(Application.DocumentManager.MdiActiveDocument);\n            HookAutoUpdateForDocument(Application.DocumentManager.MdiActiveDocument);"");
        
        code = code.Replace(""HookAnnotationScaleForDocument(e.Document);"", ""HookAnnotationScaleForDocument(e.Document);\n                HookAutoUpdateForDocument(e.Document);"");
        
        code = code.Replace(""public void Terminate() { }"", ""public void Terminate() { }\n"" + hookStr);
        
        File.WriteAllText(path, code);
        Console.WriteLine(""Done patching ShopDrawingApp.cs"");
    }
}
