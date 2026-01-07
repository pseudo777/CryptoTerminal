using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CryptoTerminal.Core.Interfaces;
using CryptoTerminal.Core.Models;

namespace CryptoTerminal.Core.Services;

public class MockExchangeService : IExchangeService
{
    public string Name => "MockExchange";

    public Task<List<UnifiedKline>> GetKlinesAsync(string symbol, string interval, int limit = 100)
    {
        var list = new List<UnifiedKline>();
        var random = new Random();
        double price = 50000; // 初始假价格
        DateTime time = DateTime.UtcNow.AddMinutes(-limit); // 从过去开始推

        for (int i = 0; i < limit; i++)
        {
            // 简单的随机游走算法生成假数据
            double change = (random.NextDouble() - 0.5) * 100; 
            double open = price;
            double close = price + change;
            double high = Math.Max(open, close) + random.NextDouble() * 10;
            double low = Math.Min(open, close) - random.NextDouble() * 10;
            double volume = random.NextDouble() * 1000;

            list.Add(new UnifiedKline(time, open, high, low, close, volume));

            price = close; // 下一根K线的开盘价等于这根的收盘价
            time = time.AddMinutes(1); // 假设是 1分钟 K线
        }

        return Task.FromResult(list);
    }

    public Task<Action> SubscribeToKlineAsync(string symbol, string interval, Action<UnifiedKline> onUpdate)
    {
        throw new NotImplementedException();
    }

    public Task<long> PlaceOrderAsync(string symbol, string side, string type, double quantity, double price, double? tpPrice,
        double? slPrice)
    {
        throw new NotImplementedException();
    }

    public Task CancelOrderAsync(string symbol, long orderId)
    {
        throw new NotImplementedException();
    }
}