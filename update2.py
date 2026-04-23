import sys

path = 'ShopDrawing.Plugin/Core/BlockManager.cs'
with open(path, 'r', encoding='utf-8') as f:
    lines = f.readlines()

new_lines = []
i = 0
while i < len(lines):
    line = lines[i]
    
    if 'public void DrawAllPanels(List<Panel> panels, Transaction tr)' in line:
        new_lines.append(line.replace('void', 'List<ObjectId>'))
    elif 'var allHatchIds = new ObjectIdCollection();' in line:
        new_lines.append(line)
        new_lines.append(line.replace('allHatchIds = new ObjectIdCollection', 'allCreatedIds = new List<ObjectId>'))
    elif 'ObjectId hatchId = DrawPanel(panel, tr, ms);' in line:
        new_lines.append(line.replace('ObjectId hatchId =', 'var result ='))
    elif 'if (!hatchId.IsNull) allHatchIds.Add(hatchId);' in line:
        new_lines.append(line.replace('hatchId', 'result.HatchId'))
        new_lines.append('                allCreatedIds.AddRange(result.AllIds);\n')
    elif 'dot.MoveToBottom(allHatchIds);' in line:
        new_lines.append(line)
        if '}' in lines[i+1]:
            new_lines.append('            }\n\n            return allCreatedIds;\n')
            i += 1
            
    elif 'public void DrawCeilingHardware(' in line:
        new_lines.append(line.replace('void', 'List<ObjectId>'))
    elif 'if (panels == null || panels.Count == 0 || boundary == null || tSpacingMm <= 0)' in line:
        new_lines.append('            var result = new List<ObjectId>();\n')
        new_lines.append(line)
    elif 'return;' in line and 'DrawCeilingHardware' in ''.join(lines[i-20:i]):
        new_lines.append(line.replace('return;', 'return result;'))
    elif 'ms.AppendEntity(line);' in line and ('SD_CEILING_T' in lines[i-1] or 'SD_CEILING_MUSHROOM' in lines[i-1]):
        new_lines.append(line.replace('ms.AppendEntity', 'result.Add(ms.AppendEntity'))
        new_lines[-1] = new_lines[-1].replace(';', ');')
    elif 'InsertMushroomBoltMarker(db, tr, ms, center);' in line:
        new_lines.append(line.replace('InsertMushroomBoltMarker', 'result.Add(InsertMushroomBoltMarker'))
        new_lines[-1] = new_lines[-1].replace(';', ');')
    elif 'return result;' not in line and 'private ObjectId DrawPanel' in line:
        # Add return result to previous method (DrawCeilingHardware)
        if 'return result;' not in new_lines[-2]:
            new_lines.insert(-1, '            return result;\n')
        new_lines.append(line.replace('private ObjectId DrawPanel', 'private (ObjectId HatchId, List<ObjectId> AllIds) DrawPanel'))
    elif 'var allIds = new ObjectIdCollection { outlineId, hatchId, tagId }.JoinWith(jointIds).JoinWith(signIds);' in line:
        new_lines.append('            var allIdsList = new List<ObjectId> { outlineId, hatchId, tagId };\n')
        new_lines.append('            allIdsList.AddRange(jointIds);\n')
        new_lines.append('            allIdsList.AddRange(signIds);\n')
        new_lines.append('\n')
        new_lines.append('            var allIds = new ObjectIdCollection();\n')
        new_lines.append('            foreach(var id in allIdsList) allIds.Add(id);\n')
    elif 'return hatchId;' in line:
        new_lines.append(line.replace('hatchId', '(hatchId, allIdsList)'))
        
    elif 'private void InsertMushroomBoltMarker(' in line:
        new_lines.append(line.replace('void', 'ObjectId'))
    elif 'ms.AppendEntity(blockRef);' in line and 'InsertMushroomBoltMarker' in ''.join(lines[i-15:i]):
        new_lines.append(line.replace('ms.AppendEntity', 'ObjectId id = ms.AppendEntity'))
    elif 'tr.AddNewlyCreatedDBObject(blockRef, true);' in line and 'InsertMushroomBoltMarker' in ''.join(lines[i-16:i]):
        new_lines.append(line)
        new_lines.append('            return id;\n')
        
    elif 'private void EntityIdsToGroup(' in line:
        new_lines.append(line.replace('private', 'public'))
        
    elif 'public void DrawOpenings(' in line:
        new_lines.append(line.replace('void', 'List<ObjectId>'))
    elif 'if (openings == null || openings.Count == 0) return;' in line:
        new_lines.append('            var result = new List<ObjectId>();\n')
        new_lines.append(line.replace('return;', 'return result;'))
    elif 'DrawOpeningBoundary(opening, tr, ms);' in line:
        new_lines.append(line.replace('DrawOpeningBoundary', 'result.AddRange(DrawOpeningBoundary'))
        new_lines[-1] = new_lines[-1].replace(';', ');')
    elif 'private void DrawOpeningBoundary(' in line:
        if 'return result;' not in new_lines[-2]:
            new_lines.insert(-1, '            return result;\n')
        new_lines.append(line.replace('void', 'List<ObjectId>'))
    elif 'double cx = o.X + o.Width / 2;' in line:
        new_lines.append('            var result = new List<ObjectId>();\n')
        new_lines.append(line)
    elif 'ms.AppendEntity(pl);' in line and 'SD_OPENING' in lines[i-5]:
        new_lines.append(line.replace('ms.AppendEntity', 'result.Add(ms.AppendEntity'))
        new_lines[-1] = new_lines[-1].replace(';', ');')
    elif 'ms.AppendEntity(hatch);' in line and 'Hatch hatch = new Hatch();' in lines[i-1]:
        new_lines.append(line.replace('ms.AppendEntity', 'result.Add(ms.AppendEntity'))
        new_lines[-1] = new_lines[-1].replace(';', ');')
    elif 'ms.AppendEntity(txt);' in line and 'SD_OPENING' in ''.join(lines[i-10:i]):
        new_lines.append(line.replace('ms.AppendEntity', 'result.Add(ms.AppendEntity'))
        new_lines[-1] = new_lines[-1].replace(';', ');')
    elif '        }' in line and i > 910 and 'public static class' in lines[min(i+3, len(lines)-1)]:
        new_lines.append(line)
        new_lines.insert(-1, '            return result;\n')
    else:
        new_lines.append(line)
        
    i += 1

with open(path, 'w', encoding='utf-8') as f:
    f.writelines(new_lines)

