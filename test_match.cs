using System;
using ShopDrawing.Plugin.Core;

class Program {
    static void Main() {
        string def = ""Tr?n"";
        string snap = ""Tr?n"";
        
        string normDef = TenderAccessoryRules.NormalizeScope(def);
        string normSnap = TenderAccessoryRules.NormalizeScope(snap);
        
        Console.WriteLine($""Def: {normDef} ({BitConverter.ToString(System.Text.Encoding.UTF8.GetBytes(normDef))})"");
        Console.WriteLine($""Snap: {normSnap} ({BitConverter.ToString(System.Text.Encoding.UTF8.GetBytes(normSnap))})"");
        
        bool match = string.Equals(normDef, normSnap, StringComparison.OrdinalIgnoreCase);
        Console.WriteLine($""Match: {match}"");
    }
}
