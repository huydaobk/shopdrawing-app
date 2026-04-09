using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using ShopDrawing.Plugin.Models;

namespace ShopDrawing.Plugin.Core
{
    public class DetailPlacer
    {
        public int PlaceDetails(Polyline boundary, DetailType type, Transaction tr)
        {
            var edges = GetEdges(boundary);
            int count = 0;

            if (type == DetailType.All || type == DetailType.BaseU)
                count += PlaceBaseU(edges, tr);

            if (type == DetailType.All || type == DetailType.TopCap)
                count += PlaceTopCap(edges, tr);

            if (type == DetailType.All || type == DetailType.CornerExternal || type == DetailType.CornerInternal)
                count += PlaceCorners(edges, type, tr);

            return count;
        }

        private List<Edge> GetEdges(Polyline pl)
        {
            var edges = new List<Edge>();
            for (int i = 0; i < pl.NumberOfVertices; i++)
            {
                SegmentType segType = pl.GetSegmentType(i);
                if (segType == SegmentType.Line)
                {
                    LineSegment2d seg = pl.GetLineSegment2dAt(i);
                    var edge = new Edge
                    {
                        Start = new Point3d(seg.StartPoint.X, seg.StartPoint.Y, 0),
                        End = new Point3d(seg.EndPoint.X, seg.EndPoint.Y, 0)
                    };
                    
                    double dx = edge.End.X - edge.Start.X;
                    double dy = edge.End.Y - edge.Start.Y;
                    
                    if (Math.Abs(dx) > Math.Abs(dy)) // Horizontal
                    {
                        edge.Position = (edge.Start.X < edge.End.X) ? EdgePosition.Bottom : EdgePosition.Top; 
                    }
                    else // Vertical
                    {
                        edge.Position = (edge.Start.Y < edge.End.Y) ? EdgePosition.Right : EdgePosition.Left;
                    }
                    edges.Add(edge);
                }
            }
            return edges;
        }

        private int PlaceBaseU(List<Edge> edges, Transaction tr)
        {
            int count = 0;
            foreach (var edge in edges)
            {
                if (edge.Position == EdgePosition.Bottom)
                {
                    InsertBlock("SD_DETAIL_BASE_U", edge.Start, 0, tr);
                    count++;
                }
            }
            return count;
        }

        private int PlaceTopCap(List<Edge> edges, Transaction tr)
        {
            int count = 0;
            foreach (var edge in edges)
            {
                if (edge.Position == EdgePosition.Top)
                {
                    InsertBlock("SD_DETAIL_TOP_CAP", edge.End, Math.PI, tr);
                    count++;
                }
            }
            return count;
        }

        private int PlaceCorners(List<Edge> edges, DetailType type, Transaction tr)
        {
            int count = 0;
            foreach (var edge in edges)
            {
                if (edge.Position == EdgePosition.Left || edge.Position == EdgePosition.Right)
                {
                    string blockName = (type == DetailType.CornerInternal) ? "SD_DETAIL_CORNER_INT" : "SD_DETAIL_CORNER_EXT";
                    InsertBlock(blockName, edge.Start, Math.PI / 2, tr);
                    count++;
                }
            }
            return count;
        }

        private void InsertBlock(string blockName, Point3d position, double rotation, Transaction tr)
        {
            Database db = Application.DocumentManager.MdiActiveDocument.Database;
            BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            
            if (!bt.Has(blockName))
            {
                CreateDummyBlock(db, bt, tr, blockName);
            }

            BlockTableRecord btr = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
            ObjectId blockDefId = bt[blockName];

            using (BlockReference br = new BlockReference(position, blockDefId))
            {
                br.Rotation = rotation;
                br.Layer = "SD_DETAIL";
                btr.AppendEntity(br);
                tr.AddNewlyCreatedDBObject(br, true);
            }
        }

        private void CreateDummyBlock(Database db, BlockTable bt, Transaction tr, string blockName)
        {
            bt.UpgradeOpen();
            BlockTableRecord btr = new BlockTableRecord();
            btr.Name = blockName;
            bt.Add(btr);
            tr.AddNewlyCreatedDBObject(btr, true);

            Circle c = new Circle();
            c.Center = Point3d.Origin;
            c.Radius = 50;
            c.ColorIndex = 1; // Red
            btr.AppendEntity(c);
            tr.AddNewlyCreatedDBObject(c, true);

            DBText txt = new DBText();
            txt.Position = new Point3d(0, -20, 0);
            txt.TextString = blockName;
            txt.Height = 20;
            btr.AppendEntity(txt);
            tr.AddNewlyCreatedDBObject(txt, true);
        }
    }
}
