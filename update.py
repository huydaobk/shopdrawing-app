import re

path = 'ShopDrawing.Plugin/Core/BlockManager.cs'
with open(path, 'r', encoding='utf-8') as f:
    content = f.read()

# 1. DrawAllPanels
content = content.replace('public void DrawAllPanels(List<Panel> panels, Transaction tr)', 'public List<ObjectId> DrawAllPanels(List<Panel> panels, Transaction tr)')
content = re.sub(r'ObjectId hatchId = DrawPanel\(panel, tr, ms\);\s*if \(\!hatchId\.IsNull\) allHatchIds\.Add\(hatchId\);', 'var result = DrawPanel(panel, tr, ms);\n                if (!result.HatchId.IsNull) allHatchIds.Add(result.HatchId);\n                allCreatedIds.AddRange(result.AllIds);', content)
content = content.replace('var allHatchIds = new ObjectIdCollection();', 'var allHatchIds = new ObjectIdCollection();\n            var allCreatedIds = new List<ObjectId>();')
content = re.sub(r'dot\.MoveToBottom\(allHatchIds\);\s*\}', 'dot.MoveToBottom(allHatchIds);\n            }\n            return allCreatedIds;', content, count=1)

# 2. DrawPanel
content = content.replace('private ObjectId DrawPanel(Panel panel, Transaction tr, BlockTableRecord ms)', 'private (ObjectId HatchId, List<ObjectId> AllIds) DrawPanel(Panel panel, Transaction tr, BlockTableRecord ms)')
content = re.sub(r'var allIds = new ObjectIdCollection \{ outlineId, hatchId, tagId \}\.JoinWith\(jointIds\)\.JoinWith\(signIds\);', 'var allIdsList = new List<ObjectId> { outlineId, hatchId, tagId };\n            allIdsList.AddRange(jointIds);\n            allIdsList.AddRange(signIds);\n            var allIds = new ObjectIdCollection();\n            foreach(var id in allIdsList) allIds.Add(id);', content)
content = content.replace('return hatchId;', 'return (hatchId, allIdsList);')

# 3. DrawCeilingHardware
content = content.replace('public void DrawCeilingHardware(', 'public List<ObjectId> DrawCeilingHardware(')
content = content.replace('if (panels == null || panels.Count == 0 || boundary == null || tSpacingMm <= 0)\n            {\n                return;\n            }', 'var result = new List<ObjectId>();\n            if (panels == null || panels.Count == 0 || boundary == null || tSpacingMm <= 0)\n            {\n                return result;\n            }')
content = content.replace('if (layout == null)\n            {\n                return;\n            }', 'if (layout == null)\n            {\n                return result;\n            }')
content = content.replace('ms.AppendEntity(line);\n                    tr.AddNewlyCreatedDBObject(line, true);', 'result.Add(ms.AppendEntity(line));\n                    tr.AddNewlyCreatedDBObject(line, true);')
content = content.replace('InsertMushroomBoltMarker(db, tr, ms, center);', 'result.Add(InsertMushroomBoltMarker(db, tr, ms, center));')
content = content.replace('private void InsertMushroomBoltMarker(', 'private ObjectId InsertMushroomBoltMarker(')
content = content.replace('ms.AppendEntity(blockRef);\n            tr.AddNewlyCreatedDBObject(blockRef, true);', 'ObjectId id = ms.AppendEntity(blockRef);\n            tr.AddNewlyCreatedDBObject(blockRef, true);\n            return id;')
content = re.sub(r'return result;\s*\}\s*private ObjectId DrawPanel', 'return result;\n        }\n\n        private (ObjectId HatchId, List<ObjectId> AllIds) DrawPanel', content) # Ensure return result for DrawCeilingHardware
content = content.replace('return result;\n        }\n\n        private (ObjectId HatchId, List<ObjectId> AllIds) DrawPanel', 'return result;\n        }\n\n        private (ObjectId HatchId, List<ObjectId> AllIds) DrawPanel') # Wait, I didn't add return result at the end of DrawCeilingHardware

content = re.sub(r'(result\.Add\(InsertMushroomBoltMarker\(db, tr, ms, center\)\);\s*\}\s*\})\s*private \(ObjectId HatchId', r'\1\n            return result;\n        }\n\n        private (ObjectId HatchId', content)


# 4. EntityIdsToGroup
content = content.replace('private void EntityIdsToGroup(ObjectIdCollection ids, string name, Transaction tr)', 'public void EntityIdsToGroup(ObjectIdCollection ids, string name, Transaction tr)')

# 5. DrawOpenings
content = content.replace('public void DrawOpenings(List<Opening> openings, Transaction tr)', 'public List<ObjectId> DrawOpenings(List<Opening> openings, Transaction tr)')
content = content.replace('if (openings == null || openings.Count == 0) return;', 'var result = new List<ObjectId>();\n            if (openings == null || openings.Count == 0) return result;')
content = content.replace('DrawOpeningBoundary(opening, tr, ms);', 'result.AddRange(DrawOpeningBoundary(opening, tr, ms));')
content = re.sub(r'\}\s*\}\s*private List<ObjectId> DrawOpeningBoundary', r'}\n\n            return result;\n        }\n\n        private List<ObjectId> DrawOpeningBoundary', content)

# 6. DrawOpeningBoundary
content = content.replace('private void DrawOpeningBoundary(Opening o, Transaction tr, BlockTableRecord ms)', 'private List<ObjectId> DrawOpeningBoundary(Opening o, Transaction tr, BlockTableRecord ms)')
content = content.replace('double cx = o.X + o.Width / 2;', 'var result = new List<ObjectId>();\n            double cx = o.X + o.Width / 2;')
content = content.replace('ms.AppendEntity(pl);\n            tr.AddNewlyCreatedDBObject(pl, true);\n            ShopdrawingEntityMetadata.ApplyOpeningMetadata(ms.Database, tr, pl, o);', 'result.Add(ms.AppendEntity(pl));\n            tr.AddNewlyCreatedDBObject(pl, true);\n            ShopdrawingEntityMetadata.ApplyOpeningMetadata(ms.Database, tr, pl, o);')
content = content.replace('Hatch hatch = new Hatch();\n            ms.AppendEntity(hatch);\n            tr.AddNewlyCreatedDBObject(hatch, true);', 'Hatch hatch = new Hatch();\n            result.Add(ms.AppendEntity(hatch));\n            tr.AddNewlyCreatedDBObject(hatch, true);')
content = content.replace('ms.AppendEntity(txt);\n                tr.AddNewlyCreatedDBObject(txt, true);\n                startY -= lineSpacing;\n            }', 'result.Add(ms.AppendEntity(txt));\n                tr.AddNewlyCreatedDBObject(txt, true);\n                startY -= lineSpacing;\n            }\n            return result;')


with open(path, 'w', encoding='utf-8') as f:
    f.write(content)
