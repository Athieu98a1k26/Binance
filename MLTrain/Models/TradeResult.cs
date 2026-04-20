using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MLTrain.Models
{
    public class TradeResult
    {
        public string Type { get; set; } // LONG/SHORT
        public decimal EntryPrice { get; set; }
        public decimal ExitPrice { get; set; }
        public bool IsWin { get; set; }
        public DateTime EntryTime { get; set; }
    }
}
