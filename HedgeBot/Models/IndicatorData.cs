using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HedgeBot.Models
{
    public class IndicatorData
    {
        // ==========================================
        //  THÔNG TIN GIÁ (Check Price Action/Wicks)
        // ==========================================
        public decimal Open { get; set; }
        public decimal High { get; set; }
        public decimal Low { get; set; }
        public decimal Close { get; set; }

        // ==========================================
        //  CÁC ĐƯỜNG TRUNG BÌNH (Moving Averages)
        // ==========================================
        public decimal EMA9 { get; set; }
        public decimal EMA9Prev { get; set; }
        public decimal EMA21 { get; set; }
        public decimal EMA200 { get; set; }
        public decimal EMA200Prev { get; set; }
        public decimal EMA200SlopeSmooth { get; set; }

        // ==========================================
        //  CHỈ BÁO DAO ĐỘNG & ĐỘNG LƯỢNG
        // ==========================================
        public decimal RSI { get; set; }
        public decimal MacdHist { get; set; }
        public decimal MacdHistPrev { get; set; }

        // ==========================================
        //  CHỈ BÁO BIẾN ĐỘNG (Volatility)
        // ==========================================
        // Quan trọng nhất cho bộ lọc Sideway và FOMO
        public decimal ATR { get; set; }
    }
}
