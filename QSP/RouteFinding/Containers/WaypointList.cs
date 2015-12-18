using QSP.AviationTools;
using QSP.LibraryExtension;
using QSP.RouteFinding.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using static QSP.LibraryExtension.StringParser.Utilities;
using static QSP.MathTools.MathTools;
using static QSP.Utilities.ErrorLogger;

namespace QSP.RouteFinding.Containers
{
    /// <summary>
    /// Representation of the airways and waypoints. 
    /// This is implemented with a hash table, so searching is by waypoint ident is O(1).
    /// This class is NOT thread safe.
    /// </summary>
    public class WaypointList
    {
        #region Fields

        private WaypointContainer content;

        private List<ChangeTracker> trackerCollection;
        private ChangeTracker currentTracker;
        private TrackChangesOption _trackChanges;

        #endregion

        public WaypointList()
        {
            _trackChanges = TrackChangesOption.No;
            content = new WaypointContainer(); 
            trackerCollection = new List<ChangeTracker>();
            currentTracker = null;
        }

        public TrackChangesOption TrackChanges
        {
            get { return _trackChanges; }

            set
            {
                //First, stop current tracking session.
                endCurrentSession();

                switch (value)
                {
                    case TrackChangesOption.Yes:
                        currentTracker = new ChangeTracker(Count, ChangeCategory.Normal);
                        break;

                    case TrackChangesOption.AddingNATs:
                        currentTracker = new ChangeTracker(Count, ChangeCategory.Nats);
                        break;

                    case TrackChangesOption.AddingPacots:
                        currentTracker = new ChangeTracker(Count, ChangeCategory.Pacots);
                        break;

                    case TrackChangesOption.AddingAusots:
                        currentTracker = new ChangeTracker(Count, ChangeCategory.Ausots);
                        break;

                }
                _trackChanges = value;
            }
        }

        private void endCurrentSession()
        {
            if (currentTracker != null)
            {
                currentTracker.RegionEnd = Count - 1;
                trackerCollection.Add(currentTracker);
                currentTracker = null;
            }
        }
               
        /// <summary>
        /// Loads all waypoints in waypoints.txt.
        /// </summary>
        /// <param name="filepath">Location of waypoints.txt</param>
        /// <exception cref="LoadWaypointFileException"></exception>
        public void ReadFixesFromFile(string filepath)
        {
            TrackChanges = TrackChangesOption.No;

            string[] allLines = File.ReadAllLines(filepath);

            foreach (var i in allLines)
            {
                try
                {
                    if (i.Length == 0 || i[0] == ' ')
                    {
                        continue;
                    }
                    int pos = 0;

                    string id = ReadString(i, ref pos, ',');
                    double lat = ParseDouble(i, ref pos, ',');
                    double lon = ParseDouble(i, ref pos, ',');

                    AddWpt(id, lat, lon);
                }
                catch (Exception ex)
                {
                    WriteToLog(ex);
                    //TODO: Write to log file. Show to user, etc.
                    throw new LoadWaypointFileException("Failed to load waypoints.txt.", ex);
                }
            }
            TrackChanges = TrackChangesOption.Yes;
        }

        public void AddWpt(string ID, double Lat, double Lon)
        {
            content.AddWpt(ID, Lat, Lon);
        }

        public void AddWpt(Waypoint item)
        {
            content.AddWpt(item);
        }

        public void AddWpt(WptNeighbor item)
        {
            content.AddWpt(item);
        }

        public void AddNeighbor(int index, Neighbor item)
        {
            if (currentTracker != null)
            {
                currentTracker.AddNeighborRecord(index);
            }
            content.AddNeighbor(index, item);
        }

        public void Clear()
        {
            content.Clear();
            trackerCollection.Clear();
            currentTracker = null;
        }

        public WptNeighbor this[int index]
        {
            get
            {
                return ElementAt(index);
            }
        }

        public WptNeighbor ElementAt(int index)
        {
            return content[index];
        }

        public LatLon LatLonAt(int index)
        {
            return this[index].LatLon;
        }

        public int NumberOfNodeFrom(int index)
        {
            return content.NumberOfNodeFrom(index);
        }

        public int Count
        {
            get { return content.Count; }
        }

        /// <summary>
        /// Find the index of WptNeighbor by ident of a waypoint.
        /// </summary>
        public int FindByID(string ident)
        {
            return content.FindByID(ident); 
        }

        /// <summary>
        /// Find all WptNeighbors by ident of a waypoint.
        /// </summary> 
        public List<int> FindAllByID(string ident)
        {
            return content .FindAllByID (ident);
        }

        /// <summary>
        /// Find the index of WptNeighbor matching the waypoint.
        /// </summary>
        public int FindByWaypoint(string ident, double lat, double lon)
        {
            return FindByWaypoint(new Waypoint(ident, lat, lon));
        }

        /// <summary>
        /// Find the index of WptNeighbor matching the waypoint.
        /// </summary>
        public int FindByWaypoint(Waypoint wpt)
        {
            return content.FindByWaypoint(wpt);
        }

        /// <summary>
        /// Find all occurences of WptNeighbor matching the waypoint.
        /// </summary>
        public List<int> FindAllByWaypoint(Waypoint wpt)
        {
            return content.FindAllByWaypoint(wpt);
        }
        
        private void revertChanges(ChangeCategory para)
        {
            //If _trackChanges is not set to "no" yet, we end current session.
            TrackChanges = TrackChangesOption.No;

            if (trackerCollection.Count > 0)
            {
                for (int i = trackerCollection.Count - 1; i >= 0; i--)
                {
                    if (trackerCollection[i].Category == para && trackerCollection[i].RegionEnd >= 0)
                    {
                        //remove neighbors first
                        var addedNeighbor = trackerCollection[i].AddedNeighbor;

                        for (int j = addedNeighbor.Count - 1; j >= 0; j--)
                        {
                            var neighbors = content[addedNeighbor[j]].NeighborList;
                            content[neighbors[neighbors.Count - 1].Index].NumNodeFrom--;
                            neighbors.RemoveAt(neighbors.Count - 1);
                        }

                        // Remove all wpts between regionStart and regionEnd.
                        int regionStart = trackerCollection[i].RegionStart;
                        int regionEnd = trackerCollection[i].RegionEnd;

                        if (regionEnd <= content.Count - 1)
                        {
                            for (int k = regionStart; k <= regionEnd; k++)
                            {
                                foreach (var m in content[k].NeighborList)
                                {
                                    content[m.Index].NumNodeFrom--;
                                }
                                searchHelper.Remove(this[k].ID, k, HashMap<string, int>.RemoveParameter.RemoveFirst);
                                RouteFindingCore.WptFinder.Remove(new WptSeachWrapper(k));
                            }
                            content.RemoveRange(regionStart, regionEnd - regionStart + 1);
                        }

                        trackerCollection.RemoveAt(i);
                    }
                }
            }

            TrackChanges = TrackChangesOption.Yes;
        }

        public void Restore()
        {
            revertChanges(ChangeCategory.Normal);
        }

        public void DisableNATs()
        {
            revertChanges(ChangeCategory.Nats);
        }

        public void DisablePacots()
        {
            revertChanges(ChangeCategory.Pacots);
        }

        public void DisableAusots()
        {
            revertChanges(ChangeCategory.Ausots);
        }

        public enum TrackChangesOption
        {
            Yes,
            No,
            AddingNATs,
            AddingPacots,
            AddingAusots
        }

        public LatLonSearchUtility<WptSeachWrapper> GenerateSearchGrids()
        {
            var searchGrid = new LatLonSearchUtility<WptSeachWrapper>(1, 5);

            for (int i = 0; i < content.Count; i++)
            {
                searchGrid.Add(new WptSeachWrapper(i, content[i].Lat, content[i].Lon));
            }

            return searchGrid;
        }

        public double Distance(int index1, int index2)
        {
            return GreatCircleDistance(this[index1].Lat, this[index1].Lon,
                                       this[index2].Lat, this[index2].Lon);
        }

    }
}