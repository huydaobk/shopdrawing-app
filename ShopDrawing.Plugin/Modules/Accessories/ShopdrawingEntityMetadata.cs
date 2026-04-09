using System;
using System.Collections.Generic;
using System.Globalization;
using Autodesk.AutoCAD.DatabaseServices;
namespace ShopDrawing.Plugin.Modules.Accessories
{
    internal static class ShopdrawingEntityMetadata
    {
        private const string RegAppName = "SHOPDRAWING";

        public static void ApplyPanelMetadata(Database db, Transaction tr, Entity entity, ShopDrawing.Plugin.Models.Panel panel)
        {
            EnsureRegApp(db, tr);
            entity.XData = BuildResultBuffer(new Dictionary<string, string>
            {
                ["PANEL_ID"] = panel.PanelId,
                ["WALL_CODE"] = panel.WallCode,
                ["SPEC"] = panel.Spec,
                ["WIDTH_MM"] = FormatNumber(panel.WidthMm),
                ["LENGTH_MM"] = FormatNumber(panel.LengthMm),
                ["PANEL_X"] = FormatNumber(panel.X),
                ["PANEL_Y"] = FormatNumber(panel.Y),
                ["STEP_W"] = FormatNumber(panel.StepWasteWidth),
                ["STEP_H"] = FormatNumber(panel.StepWasteHeight),
                ["IS_CUT"] = panel.IsCutPanel ? "1" : "0",
                ["IS_REUSED"] = panel.IsReused ? "1" : "0",
                ["SOURCE_ID"] = panel.SourceId ?? string.Empty,
                ["APP"] = panel.Application,
                ["TOP"] = panel.TopPanelTreatment,
                ["START"] = panel.StartPanelTreatment,
                ["END"] = panel.EndPanelTreatment,
                ["BOTTOM"] = panel.BottomEdgeEnabled ? "1" : "0"
            });
        }

        public static void ApplyOpeningMetadata(Database db, Transaction tr, Entity entity, ShopDrawing.Plugin.Models.Opening opening)
        {
            EnsureRegApp(db, tr);
            entity.XData = BuildResultBuffer(new Dictionary<string, string>
            {
                ["OPENING_TYPE"] = opening.OpeningType
            });
        }

        public static IReadOnlyDictionary<string, string> Read(Entity entity)
        {
            using ResultBuffer? buffer = entity.GetXDataForApplication(RegAppName);
            if (buffer == null)
            {
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }

            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (TypedValue typedValue in buffer)
            {
                if (typedValue.TypeCode != (int)DxfCode.ExtendedDataAsciiString || typedValue.Value is not string raw)
                {
                    continue;
                }

                int separatorIndex = raw.IndexOf('=');
                if (separatorIndex <= 0 || separatorIndex >= raw.Length - 1)
                {
                    continue;
                }

                string key = raw.Substring(0, separatorIndex).Trim();
                string value = raw.Substring(separatorIndex + 1).Trim();
                values[key] = value;
            }

            return values;
        }

        private static ResultBuffer BuildResultBuffer(IReadOnlyDictionary<string, string> values)
        {
            var typedValues = new List<TypedValue>
            {
                new TypedValue((int)DxfCode.ExtendedDataRegAppName, RegAppName)
            };

            foreach (var entry in values)
            {
                typedValues.Add(new TypedValue((int)DxfCode.ExtendedDataAsciiString, $"{entry.Key}={entry.Value}"));
            }

            return new ResultBuffer(typedValues.ToArray());
        }

        private static string FormatNumber(double value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static void EnsureRegApp(Database db, Transaction tr)
        {
            RegAppTable regAppTable = (RegAppTable)tr.GetObject(db.RegAppTableId, OpenMode.ForRead);
            if (regAppTable.Has(RegAppName))
            {
                return;
            }

            regAppTable.UpgradeOpen();
            var regApp = new RegAppTableRecord { Name = RegAppName };
            regAppTable.Add(regApp);
            tr.AddNewlyCreatedDBObject(regApp, true);
        }
    }
}
