using System;
using System.Text;
class Program {
    static void Main() {
        string s = ""Tr?n"";
        foreach(byte b in Encoding.UTF8.GetBytes(s)) Console.Write(b.ToString(""X2"") + "" "");
        Console.WriteLine();
    }
}
