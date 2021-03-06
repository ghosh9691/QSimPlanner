﻿using QSP.LibraryExtension;
using QSP.RouteFinding.Containers;
using QSP.RouteFinding.Data;
using QSP.RouteFinding.Data.Interfaces;
using System.Collections.Generic;
using System.Linq;
using QSP.MathTools;
using QSP.MathTools.Vectors;
using static QSP.AviationTools.Constants;
using static QSP.MathTools.Vectors.Vector3DExtension;
using static System.Math;

namespace QSP.RouteFinding.RandomRoutes
{
    public class RandomRouteFinder
    {
        public static readonly double MaxLegDis = 700.0;
        private static readonly double MaxAngleRadian = MaxLegDis / EarthRadiusNm;

        private readonly LatLonSearcher<Waypoint> searcher;

        public RandomRouteFinder(IEnumerable<Waypoint> candidates, int gridSize, int polarRegSize)
        {
            searcher = new LatLonSearcher<Waypoint>(gridSize, polarRegSize);

            foreach (var i in candidates)
            {
                searcher.Add(i);
            }
        }

        public List<Waypoint> Find(Waypoint start, Waypoint end)
        {
            var route = new List<Waypoint> { start };
            var current = start;

            while (current.Distance(end) > MaxLegDis)
            {
                var list = GetCandidates(current, end);
                var optimal = ChooseCandidates(list, current, end);
                route.Add(optimal);
                current = optimal;
            }

            route.Add(end);
            return RemoveRedundentWpts(route);
        }

        // Prevents the route output like this:
        // ... 160E20N 160E21N 160E22N 160E23N ...
        private static List<Waypoint> RemoveRedundentWpts(
            IEnumerable<Waypoint> items)
        {
            var list = new LinkedList<Waypoint>(items);
            var node = list.First;

            while (node != null)
            {
                var prev = node.Previous;
                var next = node.Next;

                if (prev != null &&
                    next != null &&
                    prev.Value.Lon == node.Value.Lon &&
                    node.Value.Lon == next.Value.Lon &&
                    prev.Value.Distance(next.Value) <= MaxLegDis)
                {
                    list.Remove(node);
                }

                node = next;
            }

            return list.ToList();
        }

        private Waypoint ChooseCandidates(List<Waypoint> candidates, Waypoint start, Waypoint end)
        {
            return candidates
                .Where(w => w.Equals(start) == false)
                .MinBy(i => i.Distance(start) + i.Distance(end));
        }

        private List<Waypoint> GetCandidates(Waypoint start, Waypoint end)
        {
            var startVector = start.ToVector3D();
            var endVector = end.ToVector3D();
            var tangent = EarthGeometry.GetW(startVector, endVector);
            var maxDisVector = (startVector + Tan(MaxAngleRadian) * tangent)
                .Normalize();

            var midPoint = (startVector + maxDisVector) * 0.5;

            var pt = midPoint.ToLatLon();
            var smallRegion = searcher.Find(pt.Lat, pt.Lon, MaxLegDis * 0.5);

            if (smallRegion.Count > 0) return smallRegion;

            return searcher.Find(start.Lat, start.Lon, MaxLegDis);
        }
    }
}
