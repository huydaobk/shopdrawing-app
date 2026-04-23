import sys

path = 'ShopDrawing.Plugin/Modules/Panel/QuickPlanWallCommandService.cs'
with open(path, 'r', encoding='utf-8') as f:
    lines = f.readlines()

new_lines = []
i = 0
while i < len(lines):
    line = lines[i]
    
    if 'var boundaryId = PromptPlanInputs(doc, ed, settings, out var openings);' in line:
        new_lines.append('                var promptResult = PromptPlanInputs(doc, ed, settings, request, out var openings);\n')
        new_lines.append('                var boundaryId = promptResult.BoundaryId;\n')
        
    elif 'DrawLayout(doc, ed, blockManager, layout, openings, request, scope, settings, boundaryId);' in line:
        new_lines.append('                DrawLayout(doc, ed, blockManager, layout, openings, request, scope, settings, boundaryId, promptResult.PlanEntityIds);\n')
        
    elif 'private static ObjectId PromptPlanInputs(Document doc, Editor ed, ShopDrawingRuntimeSettings settings, out List<Opening> openings)' in line:
        new_lines.append('        private static (ObjectId BoundaryId, List<ObjectId> PlanEntityIds) PromptPlanInputs(Document doc, Editor ed, ShopDrawingRuntimeSettings settings, LayoutRequest request, out List<Opening> openings)\n')
        
    elif 'return ObjectId.Null;' in line and i < 330:
        new_lines.append(line.replace('ObjectId.Null', '(ObjectId.Null, new List<ObjectId>())'))
        
    elif 'ObjectId boundaryId = ObjectId.Null;' in line and 'using (var tr = doc.Database.TransactionManager.StartTransaction())' in lines[i+1]:
        new_lines.append('            var planEntityIds = new List<ObjectId>();\n')
        new_lines.append(line)
        
    elif 'boundaryId = ms.AppendEntity(polyline);' in line:
        new_lines.append('''
                // Ve duong line tren mat bang
                var drawnPlanPoly = new Polyline();
                drawnPlanPoly.AddVertexAt(0, new Autodesk.AutoCAD.Geometry.Point2d(segments[0].Start.X, segments[0].Start.Y), 0, 0, 0);
                for (int j = 0; j < segments.Count; j++)
                {
                    drawnPlanPoly.AddVertexAt(j + 1, new Autodesk.AutoCAD.Geometry.Point2d(segments[j].End.X, segments[j].End.Y), 0, 0, 0);
                }
                drawnPlanPoly.Layer = "0";
                drawnPlanPoly.Color = Autodesk.AutoCAD.Colors.Color.FromColorIndex(Autodesk.AutoCAD.Colors.ColorMethod.ByAci, 3); // Green

                planEntityIds.Add(ms.AppendEntity(drawnPlanPoly));
                tr.AddNewlyCreatedDBObject(drawnPlanPoly, true);

                // Text tren mat bang
                var arialStyleId = BlockManager.EnsureArialStyle(doc.Database, tr);
                var midPt = drawnPlanPoly.GetPointAtDist(drawnPlanPoly.Length / 2.0);
                var txt = new DBText();
                txt.TextStyleId = arialStyleId;
                txt.TextString = $"{request.WallCode} - {request.Spec}";
                txt.Position = new Autodesk.AutoCAD.Geometry.Point3d(midPt.X, midPt.Y + 100, 0);
                txt.Height = 150;
                txt.Color = Autodesk.AutoCAD.Colors.Color.FromColorIndex(Autodesk.AutoCAD.Colors.ColorMethod.ByAci, 3);
                planEntityIds.Add(ms.AppendEntity(txt));
                tr.AddNewlyCreatedDBObject(txt, true);
''')
        new_lines.append(line)
        
    elif 'return boundaryId;' in line and i < 350:
        new_lines.append('            return (boundaryId, planEntityIds);\n')
        
    elif 'private static void DrawLayout(' in line:
        new_lines.append(line)
        new_lines.append(lines[i+1]) # doc
        new_lines.append(lines[i+2]) # ed
        new_lines.append(lines[i+3]) # blockManager
        new_lines.append(lines[i+4]) # layout
        new_lines.append(lines[i+5]) # openings
        new_lines.append(lines[i+6]) # request
        new_lines.append(lines[i+7]) # scope
        new_lines.append(lines[i+8]) # settings
        new_lines.append(lines[i+9].replace('ObjectId boundaryId)', 'ObjectId boundaryId,\n            List<ObjectId> planEntityIds)'))
        i += 9
        
    elif 'using var tr = doc.Database.TransactionManager.StartTransaction();' in line and 'blockManager.DrawAllPanels' in lines[i+1]:
        new_lines.append(line)
        new_lines.append('            var allDrawnIds = new ObjectIdCollection();\n')
        new_lines.append('            foreach(var id in planEntityIds) allDrawnIds.Add(id);\n')
        
    elif 'blockManager.DrawAllPanels(layout.AllPanels, tr);' in line:
        new_lines.append('            allDrawnIds.JoinWith(blockManager.DrawAllPanels(layout.AllPanels, tr));\n')
        
    elif 'blockManager.DrawCeilingHardware(' in line:
        new_lines.append('                allDrawnIds.JoinWith(blockManager.DrawCeilingHardware(\n')
        
    elif 'tr);' in line and 'request.CeilingMushroomDivisionCount,' in lines[i-1]:
        new_lines.append(line.replace('tr);', 'tr));'))
        
    elif 'blockManager.DrawOpenings(openings, tr);' in line:
        new_lines.append('                allDrawnIds.JoinWith(blockManager.DrawOpenings(openings, tr));\n')
        
    elif 'tr.Commit();' in line and 'DrawLayout' in ''.join(lines[i-40:i]) and 'DrawAllPanels' in ''.join(lines[i-30:i]):
        new_lines.append('            // Nhom mat bang va mat dung\n')
        new_lines.append('            blockManager.EntityIdsToGroup(allDrawnIds, request.WallCode, tr);\n')
        new_lines.append(line)
        
    else:
        new_lines.append(line)
        
    i += 1

with open(path, 'w', encoding='utf-8') as f:
    f.writelines(new_lines)

