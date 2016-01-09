using QSP.Core;
using QSP.LibraryExtension;
using QSP.RouteFinding.Airports;
using QSP.RouteFinding.Containers;
using QSP.RouteFinding.AirwayStructure;
using System;
using System.Collections.Generic;
using System.Linq;
using static QSP.RouteFinding.Constants;
using static QSP.RouteFinding.RouteFindingCore;
using QSP.RouteFinding.TerminalProcedures.Sid;
using QSP.RouteFinding.TerminalProcedures.Star;
using QSP.RouteFinding.Routes;

namespace QSP.RouteFinding
{
    public class RouteFinder
    {
        private string navDBLoation;
        private WaypointList wptList;
        private AirportManager airportList;

        public RouteFinder() : this(QspCore.AppSettings.NavDBLocation, WptList, AirportList) { }

        public RouteFinder(string navDBLoation, WaypointList wptList, AirportManager airportList)
        {
            this.navDBLoation = navDBLoation;
            this.wptList = wptList;
            this.airportList = airportList;
        }

        /// <summary>
        /// Add SID to wptList and returns the index of origin rwy.
        /// </summary>
        private int addSid(string icao, string rwy, List<string> sid)
        {
            return new SidHandler(icao, navDBLoation, wptList, airportList).AddSidsToWptList(rwy, sid);
        }

        /// <summary>
        /// Add STAR to wptList and returns the index of destination rwy.
        /// </summary>
        private int addStar(string icao, string rwy, List<string> star)
        {
            var starAdder = new StarHandler(icao, navDBLoation, wptList, airportList);
            return starAdder.AddStarsToWptList(rwy, star);
        }

        /// <summary>
        /// Gets a route between two aiports, from ORIG to DEST.
        /// </summary>
        public Route FindRoute(string origIcao, string origRwy, List<string> origSid, string destIcao, string destRwy, List<string> destStar)
        {
            int origIndex = addSid(origIcao, origRwy, origSid);
            int destIndex = addStar(destIcao, destRwy, destStar);

            var result = getRoute(origIndex, destIndex);
            wptList.Restore();
            // TODO: Renable this functionality. 
            //            result.SetNat(NatsManager);
            return result;
        }

        /// <summary>
        /// Gets a route from an airport to a waypoint.
        /// </summary>
        public Route FindRoute(string icao, string rwy, List<string> sid, int wptIndex)
        {
            int origIndex = addSid(icao, rwy, sid);

            var result = getRoute(origIndex, wptIndex);
            wptList.Restore();
            // TODO: Renable this functionality. 
            //            result.SetNat(NatsManager);  //TODO: This need to be set as well.
            return result;
        }

        /// <summary>
        /// Gets a route from a waypoint to an airport.
        /// </summary>
        public Route FindRoute(int wptIndex, string icao, string rwy, List<string> star)
        {
            int endIndex = addStar(icao, rwy, star);
            var result = getRoute(wptIndex, endIndex);
            wptList.Restore();
            // TODO: Renable this functionality. 
            //            result.SetNat(NatsManager);
            return result;
        }

        /// <summary>
        /// Gets a route from a waypoint to a waypoint.
        /// </summary>
        public Route FindRoute(int wptIndex1, int wptIndex2)
        {
            var result = getRoute(wptIndex1, wptIndex2);
            wptList.Restore();
            // TODO: Renable this functionality. 
            //            result.SetNat(NatsManager);
            return result;
        }

        private Route extractRoute(routeFindingData FindRouteData, int startPtIndex, int endPtIndex)
        {
            Route result = new Route();

            var waypoints = new List<Waypoint>();
            var airways = new List<string>();
            var totalDistances = new List<double>();

            int index = endPtIndex;

            while (index != startPtIndex)
            {
                waypoints.Add(wptList[index]);
                airways.Add(FindRouteData.FromAirway[index]);
                totalDistances.Add(FindRouteData.CurrentDis[index]);

                index = FindRouteData.FromWptIndex[index];
            }

            waypoints.Add(wptList[startPtIndex]);
            ConvertToNeighborDistance(totalDistances);

            return BuildRoute(waypoints, airways, totalDistances);
        }

        private static void ConvertToNeighborDistance(List<double> totalDistances)
        {
            for (int i = 0; i < totalDistances.Count - 1; i++)
            {
                totalDistances[i] -= totalDistances[i + 1];
            }
        }

        private static Route BuildRoute(List<Waypoint> waypoints, List<string> airways, List<double> totalDistances)
        {
            var result = new Route();
            int edgeCount = airways.Count;

            result.AppendWaypoint(waypoints[edgeCount]);

            for (int i = edgeCount - 1; i >= 0; i--)
            {
                result.AppendWaypoint(waypoints[i],
                                      airways[i],
                                      totalDistances[i]);
            }
            return result;
        }

        /// <summary>
        /// Finds a route from the waypoint in wptList with index startPtIndex, to endPtIndex.
        /// </summary>
        /// <exception cref="RouteNotFoundException"></exception>
        private Route getRoute(int startPtIndex, int endPtIndex)
        {
            var FindRouteData = new routeFindingData(wptList.MaxSize);
            var regionPara = new routeSeachRegionPara(startPtIndex, endPtIndex, 0.0, wptList);
            bool routeFound = false;

            while (routeFound == false && regionPara.c <= 3000.0)
            {
                regionPara.c += 500.0;
                routeFound = findRouteAttempt(regionPara, FindRouteData);
            }

            if (routeFound)
            {
                return extractRoute(FindRouteData, startPtIndex, endPtIndex);
            }
            else
            {
                throw new RouteNotFoundException();
            }
        }

        private bool findRouteAttempt(routeSeachRegionPara regionPara, routeFindingData findRouteData)
        {
            findRouteData.InitializeCurrentDis(regionPara.StartPtIndex);

            var unvisited = new MinHeap<int, double>();
            unvisited.Insert(regionPara.StartPtIndex, 0.0);

            while (unvisited.Count > 0)
            {
                var current = unvisited.PopMin();

                if (current.Key == regionPara.EndPtIndex)
                {
                    return true;
                }
                updateNeighbors(current.Key, regionPara, findRouteData, unvisited, current.Value);
            }
            return false; //Route not found.            
        }

        private void updateNeighbors(int currentWptIndex, routeSeachRegionPara regionPara, routeFindingData FindRouteData,
                                     MinHeap<int, double> unvisited, double currentDis)
        {
            foreach (var edgeIndex in wptList.EdgesFrom(currentWptIndex))
            {
                var edge = wptList.GetEdge(edgeIndex);
                int index = edge.ToNodeIndex;

                if (wptWithinRange(index, regionPara))
                {
                    double newDis = currentDis + edge.value.Distance;

                    if (FindRouteData.CurrentDis[index] == MAX_DIS && newDis < MAX_DIS)
                    {
                        unvisited.Insert(index, newDis);
                        FindRouteData.SetValue(index, currentWptIndex, edge.value.Airway, newDis);
                    }
                    else if (unvisited.ItemExists(index) && newDis < unvisited.GetElement(index).Value)
                    {
                        unvisited.ReplaceValue(index, newDis);
                        FindRouteData.SetValue(index, currentWptIndex, edge.value.Airway, newDis);
                    }
                }
            }
        }

        private bool wptWithinRange(int wptIndex, routeSeachRegionPara regionPara)
        {
            //suppose the orig and dest rwys are already in the wptList
            if (wptList.Distance(regionPara.StartPtIndex, wptIndex) + wptList.Distance(regionPara.EndPtIndex, wptIndex) >
                2 * Math.Sqrt(regionPara.b * regionPara.b + regionPara.c * regionPara.c))
            {
                return false;
            }
            return true;
        }

        #region Helper Classes

        private class routeFindingData
        {
            public int[] FromWptIndex;
            public string[] FromAirway;
            public double[] CurrentDis;

            public routeFindingData()
            {
            }

            /// <param name="waypointCount">Total number of waypoints</param>
            public routeFindingData(int waypointCount)
            {
                FromWptIndex = new int[waypointCount];
                FromAirway = new string[waypointCount];
                CurrentDis = new double[waypointCount];
            }

            public void InitializeCurrentDis(int startPtIndex)
            {
                int len = CurrentDis.Count();
                for (int i = 0; i < len; i++)
                {
                    CurrentDis[i] = MAX_DIS;
                }
                CurrentDis[startPtIndex] = 0.0;
            }

            public void SetValue(int index, int fromIndex, string airway, double currentDistance)
            {
                FromWptIndex[index] = fromIndex;
                FromAirway[index] = airway;
                CurrentDis[index] = currentDistance;
            }
        }

        private class routeSeachRegionPara
        {
            public int StartPtIndex;
            public int EndPtIndex;
            public double b;
            public double c;

            public routeSeachRegionPara(int StartPtIndex, int EndPtIndex, double c, WaypointList wptList)
            {
                this.StartPtIndex = StartPtIndex;
                this.EndPtIndex = EndPtIndex;
                this.c = c;
                b = 0.5 * wptList.Distance(this.StartPtIndex, this.EndPtIndex);
            }
        }

        #endregion

    }
}
