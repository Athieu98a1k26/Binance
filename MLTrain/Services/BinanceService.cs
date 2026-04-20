using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Binance.Net.Clients;
using Binance.Net.Enums;
using MLTrain.Models;

namespace MLTrain.Services
{
    public class BinanceService
    {
        private BinanceSocketClient _socket = new();

        public async Task Start(string symbol, Action<Candle> onCandle)
        {
            await _socket.UsdFuturesApi.ExchangeData.SubscribeToKlineUpdatesAsync(
                symbol,
                KlineInterval.FifteenMinutes,
                data =>
                {
                    var k = data.Data.Data;

                    // chỉ lấy nến đã đóng
                    if (!k.Final) return;

                    var candle = new Candle
                    {
                        Time = k.OpenTime,
                        Open = (decimal)k.OpenPrice,
                        High = (decimal)k.HighPrice,
                        Low = (decimal)k.LowPrice,
                        Close = (decimal)k.ClosePrice,
                        Volume = (decimal)k.Volume
                    };

                    onCandle(candle);
                });
        }
    }
}
