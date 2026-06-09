using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Rhino;
using Rhino.Geometry;

namespace Nephilia
{
    public class ForceDensitySolver : GH_Component
    {
        public ForceDensitySolver()
          : base("Force Density Solver", "FDSolver",
              "Löst Tragwerke mit Force Density Method (FDM)",
              "Nephilia", "Solver")
        {
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddIntegerParameter("Ankerpunkte", "Anchors",
                "Indizes der festgehaltenen Knoten", GH_ParamAccess.list);
            pManager.AddPointParameter("Knoten", "V",
                "Knotenpositionen", GH_ParamAccess.list);
            pManager.AddLineParameter("Kanten", "E",
                "Stabverbindungen", GH_ParamAccess.list);
            pManager.AddIntegerParameter("Nachbarn", "PP",
                "Nachbarknoten je Knoten (Tree)", GH_ParamAccess.tree);
            pManager.AddIntegerParameter("Kanten-Index", "PL",
                "Kantenindizes je Knoten (Tree)", GH_ParamAccess.tree);
            pManager.AddVectorParameter("Knotenlasten", "P",
                "Externe Lasten je Knoten", GH_ParamAccess.list);
            pManager.AddNumberParameter("Kraftdichte", "q",
                "Kraftdichte je Kante (q>0=Zug, q<0=Druck)", GH_ParamAccess.list);
            pManager.AddIntegerParameter("Fixierte Kanten", "FixedE",
                "Indizes für Ziellängen-Constraint", GH_ParamAccess.list);
            pManager.AddNumberParameter("Ziellängen", "TargetL",
                "Ziellängen für fixierte Kanten", GH_ParamAccess.list);
            pManager.AddIntegerParameter("Max Iterationen", "MaxIt",
                "Maximale Iterationen", GH_ParamAccess.item, 1000);

            // Optional Parameter
            pManager[3].Optional = true;
            pManager[4].Optional = true;
            pManager[7].Optional = true;
            pManager[8].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddPointParameter("Knoten Output", "V_out",
                "Optimierte Knotenpositionen", GH_ParamAccess.list);
            pManager.AddLineParameter("Kanten Output", "E_out",
                "Optimierte Stabverbindungen", GH_ParamAccess.list);
            pManager.AddNumberParameter("Stabkräfte", "Forces",
                "Normalkräfte N = q * L", GH_ParamAccess.list);
            pManager.AddNumberParameter("Stablängen", "Lengths",
                "Berechnete Stablängen", GH_ParamAccess.list);
            pManager.AddTextParameter("Info", "Info",
                "Iterations- und Timinginfo", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // ========== EINGABEN ==========
            var anchorIndices = new List<int>();
            var points = new List<Point3d>();
            var edges = new List<Line>();
            var PP = new GH_Structure<GH_Integer>();
            var PL = new GH_Structure<GH_Integer>();
            var P = new List<Vector3d>();
            var q = new List<double>();
            var fixedEdges = new List<int>();
            var targetLengths = new List<double>();
            int maxIterations = 1000;

            if (!DA.GetDataList(0, anchorIndices)) return;
            if (!DA.GetDataList(1, points)) return;
            if (!DA.GetDataList(2, edges)) return;
            if (!DA.GetDataTree(3, out PP)) { PP = new GH_Structure<GH_Integer>(); }
            if (!DA.GetDataTree(4, out PL)) { PL = new GH_Structure<GH_Integer>(); }
            if (!DA.GetDataList(5, P)) { P = new List<Vector3d>(); }
            if (!DA.GetDataList(6, q)) return;
            if (!DA.GetDataList(7, fixedEdges)) { fixedEdges = new List<int>(); }
            if (!DA.GetDataList(8, targetLengths)) { targetLengths = new List<double>(); }
            if (!DA.GetData(9, ref maxIterations)) return;

            // ========== SOLVER HAUPTTEIL ==========
            var (outPositions, outEdges, forces, lengths, info) =
                SolveForceDensity(
                    anchorIndices, points, edges, PP, PL,
                    P, q, fixedEdges, targetLengths, maxIterations);

            // ========== AUSGABEN ==========
            DA.SetDataList(0, outPositions);
            DA.SetDataList(1, outEdges);
            DA.SetDataList(2, forces);
            DA.SetDataList(3, lengths);
            DA.SetData(4, info);
        }

        // ========== HAUPTSOLVER-METHODE ==========
        private (List<Point3d>, List<Line>, List<double>, List<double>, string)
            SolveForceDensity(
                List<int> anchorIndices,
                List<Point3d> points,
                List<Line> edges,
                GH_Structure<GH_Integer> PP,
                GH_Structure<GH_Integer> PL,
                List<Vector3d> P,
                List<double> q,
                List<int> fixedEdges,
                List<double> targetLengths,
                int maxIterations)
        {
            int vCount = points.Count;
            double tol = RhinoDoc.ActiveDoc.ModelAbsoluteTolerance;

            if (maxIterations > 10000)
            {
                maxIterations = 10000;
            }

            // ===== SETUP: Ankerpunkte =====
            bool[] naked = new bool[vCount];
            foreach (int idx in anchorIndices)
                if (idx >= 0 && idx < vCount)
                    naked[idx] = true;

            // ===== SETUP: Lastvektoren auffüllen =====
            while (P.Count < vCount)
                P.Add(P.Count > 0 ? P[0] : Vector3d.Zero);

            // ===== SETUP: Cache für Nachbarn & Kraftdichten =====
            int[][] neighborCache = new int[vCount][];
            double[][] qCache = new double[vCount][];

            for (int v = 0; v < vCount; v++)
            {
                var neighbors = PP.Branches[v];
                var lineIndices = PL.Branches[v];
                neighborCache[v] = neighbors.Select(x => x.Value).ToArray();
                qCache[v] = new double[neighbors.Count];

                for (int i = 0; i < neighbors.Count; i++)
                {
                    int edgeIdx = lineIndices[i].Value;
                    qCache[v][i] = (edgeIdx >= 0 && edgeIdx < q.Count) ? q[edgeIdx] : 1.0;
                }
            }

            // ===== SETUP: Ziellängen-Constraint =====
            bool hasFixed = (fixedEdges != null && fixedEdges.Count > 0 &&
                           targetLengths != null && targetLengths.Count == fixedEdges.Count);

            // ===== SETUP: Endpunkte der Kanten =====
            int[] edgeA = new int[edges.Count];
            int[] edgeB = new int[edges.Count];

            for (int e = 0; e < edges.Count; e++)
            {
                edgeA[e] = -1;
                edgeB[e] = -1;

                for (int i = 0; i < vCount; i++)
                {
                    if (points[i].DistanceTo(edges[e].From) < tol)
                        edgeA[e] = i;
                    if (points[i].DistanceTo(edges[e].To) < tol)
                        edgeB[e] = i;
                }
            }

            // ===== ITERATIONEN =====
            Point3d[] positions = points.ToArray();
            Point3d[] prevPositions = (Point3d[])positions.Clone();

            int lastIt = maxIterations - 1;
            var sw = Stopwatch.StartNew();

            for (int it = 0; it < maxIterations; it++)
            {
                Point3d[] result = (Point3d[])positions.Clone();

                // --- Force Density Schritt ---
                for (int v = 0; v < vCount; v++)
                {
                    if (naked[v]) continue;

                    int[] neighbours = neighborCache[v];
                    Vector3d sum = Vector3d.Zero;
                    double sumQ = 0.0;

                    for (int i = 0; i < neighbours.Length; i++)
                    {
                        sum += new Vector3d(positions[neighbours[i]]) * qCache[v][i];
                        sumQ += qCache[v][i];
                    }

                    sum += P[v];

                    if (Math.Abs(sumQ) > 1e-10)
                        result[v] = new Point3d(sum / sumQ);
                }

                positions = result;

                // --- Ziellängen-Korrektur ---
                if (hasFixed)
                {
                    for (int k = 0; k < fixedEdges.Count; k++)
                    {
                        int e = fixedEdges[k];
                        if (e < 0 || e >= edges.Count) continue;

                        int a = edgeA[e];
                        int b = edgeB[e];
                        if (a < 0 || b < 0) continue;

                        double target = targetLengths[k];
                        Vector3d dir = positions[b] - positions[a];
                        double len = dir.Length;

                        if (len < 1e-10) continue;

                        dir /= len;
                        double diff = len - target;

                        if (!naked[a] && !naked[b])
                        {
                            positions[a] += dir * (diff * 0.5);
                            positions[b] -= dir * (diff * 0.5);
                        }
                        else if (!naked[a])
                        {
                            positions[a] += dir * diff;
                        }
                        else if (!naked[b])
                        {
                            positions[b] -= dir * diff;
                        }
                    }
                }

                // --- Konvergenzcheck ---
                double maxDelta = 0.0;
                for (int v = 0; v < vCount; v++)
                {
                    double d = positions[v].DistanceTo(prevPositions[v]);
                    if (d > maxDelta) maxDelta = d;
                }

                if (maxDelta < tol)
                {
                    lastIt = it;
                    break;
                }

                prevPositions = (Point3d[])positions.Clone();
            }

            sw.Stop();

            // ===== STABKRÄFTE & LÄNGEN =====
            var forces = new List<double>();
            var lengths = new List<double>();

            for (int e = 0; e < edges.Count; e++)
            {
                int a = edgeA[e];
                int b = edgeB[e];

                if (a >= 0 && b >= 0)
                {
                    double L = positions[a].DistanceTo(positions[b]);
                    double qi = (e < q.Count) ? q[e] : 1.0;
                    double F = qi * L;

                    lengths.Add(L);
                    forces.Add(F);
                }
            }

            // ===== OUTPUT EDGES =====
            var outEdges = new List<Line>();
            for (int e = 0; e < edges.Count; e++)
            {
                int a = edgeA[e];
                int b = edgeB[e];

                if (a >= 0 && b >= 0)
                    outEdges.Add(new Line(positions[a], positions[b]));
            }

            // ===== INFO STRING =====
            string info = $"Iterations: {lastIt + 1} | Time: {sw.ElapsedMilliseconds}ms | " +
                         $"Vertices: {vCount} | Edges: {edges.Count}";

            return (positions.ToList(), outEdges, forces, lengths, info);
        }

        public override Guid ComponentGuid
        {
            get { return new Guid("87654321-4321-8765-4321-876543218765"); }
        }

        protected override System.Drawing.Bitmap Icon
        {
            get { return null; }
        }
    }
}
