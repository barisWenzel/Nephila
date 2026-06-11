using Rhino.Geometry;
using System.Collections.Generic;

namespace Nephila
{
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

    internal static class GraphHelpers
    {
        public static int RegisterPoint(
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
}
