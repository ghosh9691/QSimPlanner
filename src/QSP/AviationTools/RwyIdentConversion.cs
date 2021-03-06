﻿using System;
using System.Collections.Generic;
using static QSP.Utilities.ExceptionHelpers;

namespace QSP.AviationTools
{
    public static class RwyIdentConversion
    {
        /// <summary>
        /// Input examples: "05", "26", "14R", "25C", "36L".
        /// Note that 3, 3L are not valid.
        /// Returns null is input is invalid.
        /// </summary>
        public static string RwyIdentOppositeDir(string rwy)
        {
            try
            {
                var numPart = int.Parse(rwy.Substring(0, 2));
                var charPart = rwy.Substring(2);
                return OppositeNum(numPart) + oppositeDirection[charPart];
            }
            catch
            {
                return null;
            }
        }

        private static string OppositeNum(int numPart)
        {
            Ensure<ArgumentException>(0 < numPart && numPart <= 36);
            var opposite = numPart >= 19 ? (numPart - 18) : (numPart + 18);
            return opposite.ToString().PadLeft(2, '0');
        }

        private static readonly IReadOnlyDictionary<string, string> oppositeDirection =
            new Dictionary<string, string>()
            {
                [""] = "",
                ["R"] = "L",
                ["L"] = "R",
                ["C"] = "C"
            };
    }
}
