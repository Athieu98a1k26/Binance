using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MLTrain.Models;

namespace MLTrain.Core
{
    public static class RegimeDetector
    {
        public static bool IsTrending(ModelInput f)
        {
            return Math.Abs(f.EmaSlope) > 0.05f && f.Atr > 0.5f;
        }
    }
}
