using System;
using System.Collections.Generic;
using System.Text;

namespace LeoQ.Core.Models
{
    public static class LeoPathGeometry
    {
        // Very simple, explainable model:
        // - Uplink + downlink path length grows with altitude
        // - Crosslink path length approximated by fraction of ground distance
        //
        // This is not a full orbital mechanics model; it's a parameterized approximation
        // suitable for reproducible evaluation and sensitivity analysis.
        public static double EstimatePathKm(double groundDistanceKm, double altitudeKm, int islHops)
        {
            // Uplink + downlink: assume slant path ~= altitude * 2.2 each leg
            // (factor > 1 accounts for non-vertical geometry)
            double upDownKm = 2 * (2.2 * altitudeKm);

            // Crosslink: approximate as groundDistance scaled + mild inflation
            // distributed across isl hops
            double crosslinkKm = groundDistanceKm * 1.05;

            return upDownKm + crosslinkKm;
        }
    }

}
