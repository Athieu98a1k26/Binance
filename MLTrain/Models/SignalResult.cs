using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MLTrain.Models
{
    public class SignalResult
    {
        public string Signal { get; set; }
        public float Confidence { get; set; }
        public DateTime Time { get; set; }
    }
}
