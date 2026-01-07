using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CryptoTerminal.Core.Models;

namespace CryptoTerminal.Core.Interfaces;

public interface IExchangeService
{
    string Name { get; }

    // 1. 获取历史 K 线 (REST)
    Task<List<UnifiedKline>> GetKlinesAsync(string symbol, string interval, int limit = 100);

    // 2. 订阅实时 K 线推送 (WebSocket)
    // 返回一个 Action 用于取消订阅，或者返回 IDisposable
    Task<Action> SubscribeToKlineAsync(string symbol, string interval, Action<UnifiedKline> onUpdate);

    // 3. 真实下单 (REST)
    // 返回 OrderId
    Task<long> PlaceOrderAsync(string symbol, string side, string type, double quantity, double price, double? tpPrice, double? slPrice);
    
    // 4. 撤单
    Task CancelOrderAsync(string symbol, long orderId);
}