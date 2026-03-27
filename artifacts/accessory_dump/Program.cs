using System;
using System.Linq;
using ShopDrawing.Plugin.Core;

var defaults = AccessoryDataManager.GetDefaults();
foreach (var app in defaults.Select(x => x.Application).Distinct())
    Console.WriteLine($"APP=[{app}]");
Console.WriteLine("--ROWS--");
foreach (var row in defaults.Where(x => x.Name.Contains("MC-202") || x.Name.Contains("SN-505") || x.Name.Contains("MS-617")))
    Console.WriteLine($"{row.Application} | {row.Name} | {row.CalcRule} | {row.Factor}");
