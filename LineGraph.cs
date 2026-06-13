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
                "Nephila",
                "Graph")
        {
        }

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

            pManager.AddNumberParameter(
                "Tolerance", "Tol",
                "Maximum number of iterations.",
                GH_ParamAccess.item, Rhino.RhinoDoc.ActiveDoc.ModelAbsoluteTolerance);
        }

        // ─────────────────────────────────────────────
        //  Outputs
        // ─────────────────────────────────────────────
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddPointParameter(
                "Vertices", "V",
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
                "Dots", "D",
                "Text dots for visualisation.",
                GH_ParamAccess.list);
        }

        // ─────────────────────────────────────────────
        //  Solve
        // ─────────────────────────────────────────────
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // ── Input ─────────────────────────────────
            var lines = new List<Line>();
            if (!DA.GetDataList(0, lines)) return;

            bool showEdgeNums = false;
            DA.GetData(1, ref showEdgeNums);

            bool showVertexNums = false;
            DA.GetData(2, ref showVertexNums);

            double tolerance = 0;
            DA.GetData(3, ref tolerance);

            // ── Setup ─────────────────────────────────
            var pointIndex = new Dictionary<Point3d, int>(new Point3dComparer(tolerance));
            var vertices = new List<Point3d>();
            var edges = new List<Line>();

            var ppDict = new Dictionary<int, HashSet<int>>();
            var plDict = new Dictionary<int, HashSet<int>>();
            var lpDict = new Dictionary<int, int[]>();

            int currentIndex = 0;

            // ── Build topology ────────────────────────
            for (int e = 0; e < lines.Count; e++)
            {
                Line line = lines[e];

                int a = GraphHelpers.RegisterPoint(line.From, pointIndex, vertices, ppDict, plDict, ref currentIndex);
                int b = GraphHelpers.RegisterPoint(line.To, pointIndex, vertices, ppDict, plDict, ref currentIndex);

                if (a == b) continue; // degenerate line

                int edgeIndex = edges.Count;
                edges.Add(line);

                // PP adjacency
                ppDict[a].Add(b);
                ppDict[b].Add(a);

                // PL adjacency
                plDict[a].Add(edgeIndex);
                plDict[b].Add(edgeIndex);

                // LP adjacency
                lpDict[edgeIndex] = new[] { a, b };
            }

            // ── Build trees ───────────────────────────
            var ppTree = new GH_Structure<GH_Integer>();
            var plTree = new GH_Structure<GH_Integer>();
            var lpTree = new GH_Structure<GH_Integer>();

            for (int i = 0; i < vertices.Count; i++)
            {
                var ppPath = new GH_Path(i);
                foreach (int n in ppDict[i])
                    ppTree.Append(new GH_Integer(n), ppPath);

                var plPath = new GH_Path(i);
                foreach (int n in plDict[i])
                    plTree.Append(new GH_Integer(n), plPath);
            }

            for (int i = 0; i < edges.Count; i++)
            {
                var lpPath = new GH_Path(i);
                lpTree.Append(new GH_Integer(lpDict[i][0]), lpPath);
                lpTree.Append(new GH_Integer(lpDict[i][1]), lpPath);
            }

            // ── Text dots ─────────────────────────────
            var dots = new List<TextDot>();

            if (showVertexNums)
                for (int i = 0; i < vertices.Count; i++)
                    dots.Add(new TextDot($"v{i}", vertices[i]));

            if (showEdgeNums)
                for (int i = 0; i < edges.Count; i++)
                    dots.Add(new TextDot($"e{i}", edges[i].PointAt(0.5)));

            // ── Output ────────────────────────────────
            Message = $"{edges.Count} Edges \n {vertices.Count} Vertices";

            DA.SetDataList(0, vertices);
            DA.SetDataList(1, edges);
            DA.SetDataTree(2, ppTree);
            DA.SetDataTree(3, plTree);
            DA.SetDataTree(4, lpTree);
            DA.SetDataList(5, dots);
        }

        // ─────────────────────────────────────────────
        //  Icon & Guid
        // ─────────────────────────────────────────────
        protected override System.Drawing.Bitmap Icon
        {
            get { return Nephila.Properties.Resources.NephilaIconTopo; }
        }
        
        public override Guid ComponentGuid =>
            new Guid("A1B2C3D4-E5F6-7890-ABCD-EF1234567890");
    }
}
