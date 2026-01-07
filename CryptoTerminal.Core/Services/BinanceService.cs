using Binance.Net.Clients;
using Binance.Net.Enums;
using Binance.Net.Objects;
using CryptoExchange.Net.Authentication;
using CryptoExchange.Net.Objects;
using CryptoTerminal.Core.Interfaces;
using CryptoTerminal.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Binance.Net;
using Binance.Net.Objects.Options;

namespace CryptoTerminal.Core.Services;

public class BinanceService : IExchangeService
{
    public string Name => "Binance Futures";

    private readonly BinanceRestClient _restClient;
    private readonly BinanceSocketClient _socketClient;

    public BinanceService()
    {
        // 定义 API 凭证 (测试网)
        var credentials = new ApiCredentials("YOUR_TESTNET_API_KEY", "YOUR_TESTNET_SECRET_KEY");

        // 1. 初始化 RestClient (使用 Lambda 配置)
        _restClient = new BinanceRestClient(options =>
        {
            // 设置环境 (自动处理 Testnet URL)
            options.Environment = BinanceEnvironment.Testnet;
        
            // 设置凭证
            options.ApiCredentials = credentials;

            // 如果需要代理 (例如 Clash)
            // options.Proxy = new System.Net.WebProxy("http://127.0.0.1:7890");
        });

        // 2. 初始化 SocketClient (使用 Lambda 配置)
        _socketClient = new BinanceSocketClient(options =>
        {
            options.Environment = BinanceEnvironment.Testnet;
            options.ApiCredentials = credentials;
        
            // options.Proxy = new System.Net.WebProxy("http://127.0.0.1:7890");
        });
    }

    // 1. 获取历史 K 线
    public async Task<List<UnifiedKline>> GetKlinesAsync(string symbol, string intervalStr, int limit = 100)
    {
        var interval = (KlineInterval)Enum.Parse(typeof(KlineInterval), intervalStr, true); // 简单转换 1m -> OneMinute
        
        // 使用合约 API (UsdFuturesApi)
        var result = await _restClient.UsdFuturesApi.ExchangeData.GetKlinesAsync(symbol, interval, limit: limit);

        if (!result.Success) throw new Exception($"API Error: {result.Error?.Message}");

        return result.Data.Select(k => new UnifiedKline(
            k.OpenTime, 
            (double)k.OpenPrice, 
            (double)k.HighPrice, 
            (double)k.LowPrice, 
            (double)k.ClosePrice, 
            (double)k.Volume
        )).ToList();
    }

    // 2. 订阅实时 K 线
    public async Task<Action> SubscribeToKlineAsync(string symbol, string intervalStr, Action<UnifiedKline> onUpdate)
    {
        var interval = (KlineInterval)Enum.Parse(typeof(KlineInterval), intervalStr, true);

        var subResult = await _socketClient.UsdFuturesApi.ExchangeData.SubscribeToKlineUpdatesAsync(
            symbol, 
            interval, 
            data =>
            {
                // 转换数据格式
                var k = data.Data.Data;
                var unified = new UnifiedKline(
                    k.OpenTime,
                    (double)k.OpenPrice,
                    (double)k.HighPrice,
                    (double)k.LowPrice,
                    (double)k.ClosePrice,
                    (double)k.Volume
                );
                // 回调
                onUpdate(unified);
            });

        if (!subResult.Success) throw new Exception($"Socket Error: {subResult.Error?.Message}");

        // 返回一个 Action 用于取消订阅
        return async () => await _socketClient.UnsubscribeAsync(subResult.Data);
    }

    // 3. 真实下单 (带 TP/SL 的策略单比较复杂，这里先演示最基础的 Limit/Market)
    public async Task<long> PlaceOrderAsync(string symbol, string sideStr, string typeStr, double quantity, double price, double? tpPrice, double? slPrice)
    {
        // 1. 参数转换
        var side = sideStr.Equals("Buy", StringComparison.OrdinalIgnoreCase) ? OrderSide.Buy : OrderSide.Sell;
    
        // 处理订单类型 (包含 Market 和 Limit)
        var type = FuturesOrderType.Limit;
        if (typeStr.Contains("Market", StringComparison.OrdinalIgnoreCase)) 
            type = FuturesOrderType.Market;
        else if (typeStr.Contains("Stop", StringComparison.OrdinalIgnoreCase))
            type = FuturesOrderType.Stop; // 注意：Stop单通常还需要 TriggerPrice，这里简化处理

        // 2. 构建 API 请求
        // 注意：币安要求价格和数量必须符合精度规则 (StepSize/TickSize)
        // 这里我们假设前端已经做过初步处理，或者依赖 API 报错来调试
        var result = await _restClient.UsdFuturesApi.Trading.PlaceOrderAsync(
            symbol: symbol,
            side: side,
            type: type,
            quantity: (decimal)quantity,
            price: type == FuturesOrderType.Limit ? (decimal)price : null,
            timeInForce: type == FuturesOrderType.Limit ? TimeInForce.GoodTillCanceled : null
        );

        // 3. 处理结果
        if (!result.Success)
        {
            // 抛出异常，让 ViewModel 捕获并显示错误
            throw new Exception($"Binance API Error: {result.Error?.Message} (Code: {result.Error?.Code})");
        }

        // 返回主订单 ID
        long mainOrderId = result.Data.Id;

        // --- 高级：关于 TP/SL ---
        // 币安合约支持在下单时附带 TP/SL (作为触发单)，或者你需要单独下两个 ReduceOnly 的单子。
        // 为了降低复杂度，第一步我们先只发主单。
        // 如果你想发 TP/SL，需要再次调用 _restClient.UsdFuturesApi.Trading.PlaceOrderAsync
        // 类型为 TakeProfitMarket / StopMarket，且 reduceOnly = true
    
        return mainOrderId;
    }

    public async Task CancelOrderAsync(string symbol, long orderId)
    {
        await _restClient.UsdFuturesApi.Trading.CancelOrderAsync(symbol, orderId);
    }
}