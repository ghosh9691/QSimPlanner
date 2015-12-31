﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static QSP.RouteFinding.TerminalProcedures.Sid.Utilities;

namespace QSP.RouteFinding.TerminalProcedures.Sid
{
    //Find all SID available for the runway.
    public class SidSelector
    {
        private List<SidEntry> sidEntries;
        private string rwy;

        public SidSelector(List<SidEntry> sidEntries, string rwy)
        {
            this.sidEntries = sidEntries;
            this.rwy = rwy;
        }

        /// <summary>
        /// Find all SID available for the runway. Two SIDs only different in transitions are regarded as different. 
        /// If none is available an empty list is returned.
        /// </summary>
        public List<string> GetSidList()
        {
            var noTrans = new List<string>();
            var trans = new List<TerminalProcedureName>();

            classifySids(noTrans, trans);

            // Remove duplicates, from runway specific part and common part
            noTrans = noTrans.Distinct().ToList();

            // Remove transitions that are not available for the runway.
            removeTransitionWrongRunway(noTrans, trans);

            // SIDs which have transition(s), should not appear as one without transition.
            removeSidWithoutTransition(noTrans, trans);

            // Merge the two lists
            foreach (var k in trans)
            {
                noTrans.Add(k.ProcedureName + '.' + k.TransitionName);
            }
            return noTrans;
        }

        private void classifySids(List<string> noTrans, List<TerminalProcedureName> trans)
        {
            foreach (var i in sidEntries)
            {
                if (i.Type == EntryType.Transition) 
                {
                    trans.Add(new TerminalProcedureName(i.Name, i.RunwayOrTransition));     // All transitions
                }
                else if ((i.Type == EntryType.RwySpecific && i.RunwayOrTransition == rwy) ||
                         (i.Type == EntryType.Common && runwaySpecificPartExists(i.Name) == false))
                {
                    noTrans.Add(i.Name);    // RwySpecific or common parts which are available for the particular runway.
                }
            }
        }

        private bool runwaySpecificPartExists(string sidName)
        {
            foreach (var i in sidEntries)
            {
                if (i.Type==EntryType.RwySpecific && i.Name == sidName)
                {
                    return true;
                }
            }
            return false;
        }

        private static void removeTransitionWrongRunway(List<string> noTrans, List<TerminalProcedureName> trans)
        {
            for (int i = trans.Count - 1; i >= 0; i--)
            {
                bool removeEntry = true;

                foreach (var j in noTrans)
                {
                    if (j == trans[i].ProcedureName)
                    {
                        removeEntry = false;
                        break;
                    }
                }

                if (removeEntry)
                {
                    trans.RemoveAt(i);
                }
            }
        }

        private static void removeSidWithoutTransition(List<string> noTrans, List<TerminalProcedureName> trans)
        {
            for (int i = noTrans.Count - 1; i >= 0; i--)
            {
                foreach (var j in trans)
                {
                    if (noTrans[i] == j.ProcedureName)
                    {
                        noTrans.RemoveAt(i);
                        break;
                    }
                }
            }
        }
    }
}