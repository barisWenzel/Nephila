using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Drawing;

namespace Nephila
{
    public class LineGraphComponent : GH_Component
    {
        public LineGraphComponent()
            : base(
                "Line Graph",
                "LGraph",
                "Builds a graph topology from lines:\ndeduplicated vertices, edges, adjacency trees.",
                "Nephilia",
                "Graph")
        {
        }

        protected override Bitmap Icon => null;

        public override Guid ComponentGuid =>
            new Guid("A1B2C3D4-E5F6-7890-ABCD-EF1234567890");

        // ─────────────────────────────────────────────
        //  Inputs
        // ─────────────────────────────────────────────
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddLineParameter(
                "Lines", "L",
                "Input lines to build the graph from.",
                GH_ParamAccess.list);

            pManager.AddBooleanParameter(
                "Show Edge Numbers", "SE",
                "Show edge index labels at midpoints.",
                GH_ParamAccess.item, false);

            pManager.AddBooleanParameter(
                "Show Vertex Numbers", "SV",
                "Show vertex index labels at points.",
                GH_ParamAccess.item, false);
        }

        // ─────────────────────────────────────────────
        //  Outputs
        // ─────────────────────────────────────────────
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddPointParameter(
                "Points", "P",
                "Deduplicated vertex positions.",
                GH_ParamAccess.list);

            pManager.AddLineParameter(
                "Edges", "E",
                "Deduplicated edges.",
                GH_ParamAccess.list);

            pManager.AddIntegerParameter(
                "PP", "PP",
                "Point-to-Point adjacency. Path = vertex index, values = neighbour indices.",
                GH_ParamAccess.tree);

            pManager.AddIntegerParameter(
                "PL", "PL",
                "Point-to-Line adjacency. Path = vertex index, values = edge indices.",
                GH_ParamAccess.tree);

            pManager.AddIntegerParameter(
                "LP", "LP",
                "Line-to-Point adjacency. Path = edge index, values = [start, end] vertex indices.",
                GH_ParamAccess.tree);

            pManager.AddGenericParameter(
                "Labels", "Labels",
                "TextDot labels for vertices and/or edges.",
                GH_ParamAccess.list);
        }

        // ─────────────────────────────────────────────
        //  Solve
        // ─────────────────────────────────────────────
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            var lines = new List<Line>();
            bool showEdgeNums = false;
            bool showVertexNums = false;

            if (!DA.GetDataList(0, lines)) return;
            DA.GetData(1, ref showEdgeNums);
            DA.GetData(2, ref showVertexNums);

            double tol = Rhino.RhinoDoc.ActiveDoc.ModelAbsoluteTolerance;

            // ── Datenstrukturen ────────────────────────
            var pointIndex = new Dictionary<Point3d, int>(new Point3dComparer(tol));
            var points = new List<Point3d>();
            var edges = new List<Line>();
            var edgeIndexSet = new HashSet<(int, int)>();

            var lpTree = new DataTree<int>();
            var ppDict = new Dictionary<int, HashSet<int>>();
            var plDict = new Dictionary<int, HashSet<int>>();

            int currentIndex = 0;

            // ── Linien verarbeiten ─────────────────────
            foreach (var line in lines)
            {
                int a = RegisterPoint(line.From, pointIndex, points, ppDict, plDict, ref currentIndex);
                int b = RegisterPoint(line.To, pointIndex, points, ppDict, plDict, ref currentIndex);

                if (a == b) continue;

                var key = a < b ? (a, b) : (b, a);
                if (!edgeIndexSet.Add(key)) continue;

                int edgeIdx = edges.Count;
                edges.Add(line);

                // LP
                var lpPath = new GH_Path(edgeIdx);
                lpTree.Add(a, lpPath);
                lpTree.Add(b, lpPath);

                // PP
                ppDict[a].Add(b);
                ppDict[b].Add(a);

                // PL
                plDict[a].Add(edgeIdx);
                plDict[b].Add(edgeIdx);
            }

            // ── PP Tree ────────────────────────────────
            var ppTree = new DataTree<int>();
            foreach (var kvp in ppDict)
            {
                var path = new GH_Path(kvp.Key);
                foreach (int n in kvp.Value)
                    ppTree.Add(n, path);
            }

            // ── PL Tree ────────────────────────────────
            var plTree = new DataTree<int>();
            foreach (var kvp in plDict)
            {
                var path = new GH_Path(kvp.Key);
                foreach (int n in kvp.Value)
                    plTree.Add(n, path);
            }

            // ── Labels ────────────────────────────────
            var dots = new List<TextDot>();

            if (showVertexNums)
                for (int i = 0; i < points.Count; i++)
                    dots.Add(new TextDot(i.ToString(), points[i]));

            if (showEdgeNums)
                for (int i = 0; i < edges.Count; i++)
                    dots.Add(new TextDot($"e{i}", edges[i].PointAt(0.5)));

            // ── Output ────────────────────────────────
            DA.SetDataList(0, points);
            DA.SetDataList(1, edges);
            DA.SetDataTree(2, ppTree);
            DA.SetDataTree(3, plTree);
            DA.SetDataTree(4, lpTree);
            DA.SetDataList(5, dots);
        }

        // ─────────────────────────────────────────────
        //  Hilfsmethoden
        // ─────────────────────────────────────────────
        private static int RegisterPoint(
            Point3d pt,
            Dictionary<Point3d, int> pointIndex,
            List<Point3d> points,
            Dictionary<int, HashSet<int>> ppDict,
            Dictionary<int, HashSet<int>> plDict,
            ref int currentIndex)
        {
            if (!pointIndex.TryGetValue(pt, out int idx))
            {
                idx = currentIndex++;
                pointIndex[pt] = idx;
                points.Add(pt);
                ppDict[idx] = new HashSet<int>();
                plDict[idx] = new HashSet<int>();
            }
            return idx;
        }
    }

    // ─────────────────────────────────────────────────
    //  Point3d Comparer
    // ─────────────────────────────────────────────────
    internal class Point3dComparer : IEqualityComparer<Point3d>
    {
        private readonly double _tol;
        public Point3dComparer(double tol) { _tol = tol; }

        public bool Equals(Point3d a, Point3d b)
            => a.DistanceToSquared(b) < _tol * _tol;

        public int GetHashCode(Point3d p)
            => ((int)(p.X / _tol))
             ^ ((int)(p.Y / _tol))
             ^ ((int)(p.Z / _tol));
    }
}
