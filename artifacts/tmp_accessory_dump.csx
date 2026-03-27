using System;
using System.Linq;
using ShopDrawing.Plugin.Core;
using ShopDrawing.Plugin.Models;

var defaults = AccessoryDataManager.GetDefaults().Where(x => x.Application == "Kho l?nh").ToList();
Console.WriteLine("DEFAULTS");
foreach (var x in defaults)
    Console.WriteLine($"{x.Application} | {x.Name} | {x.Material} | {x.Position} | {x.CalcRule}");

var legacy = new[]
{
    new TenderAccessory { CategoryScope = "Vách", Application = "Ngoài nhà", SpecKey = "T?t c?", Name = "Vi?n l? m?", Position = "C?a di", Unit = "md", CalcRule = AccessoryCalcRule.PER_DOOR_OPENING_PERIMETER, Factor = 1 },
    new TenderAccessory { CategoryScope = "Vách", Application = "Phòng s?ch", SpecKey = "T?t c?", Name = "Vi?n l? m? 2 m?t", Position = "L? m?", Unit = "md", CalcRule = AccessoryCalcRule.PER_OPENING_PERIMETER_TWO_FACES, Factor = 1 },
    new TenderAccessory { CategoryScope = "Vách", Application = "Kho l?nh", SpecKey = "T?t c?", Name = "Vi?n l? m? 2 m?t", Position = "L? m?", Unit = "md", CalcRule = AccessoryCalcRule.PER_OPENING_PERIMETER_TWO_FACES, Factor = 1 }
};

var normalized = AccessoryDataManager.NormalizeConfiguredAccessories(legacy).ToList();
Console.WriteLine("NORMALIZED");
foreach (var x in normalized)
    Console.WriteLine($"{x.Application} | {x.Name} | {x.Material} | {x.Position} | {x.CalcRule}");
