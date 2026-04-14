using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HedgeBot.Models
{
    public class Candle
    {
        public decimal Open { get; set; }   // <--- BẮT BUỘC PHẢI CÓ
        public decimal High { get; set; }
        public decimal Low { get; set; }
        public decimal Close { get; set; }
        public decimal Volume { get; set; }
        public DateTime Time { get; set; }  // Nên có để quản lý nến theo thời gian
    }
}
