using System.Collections.Generic;
using System.Threading.Tasks;
using CryptoTerminal.Core.Models;

namespace CryptoTerminal.Core.Interfaces;

/// <summary>
/// 通用交易所服务接口
/// </summary>
public interface IExchangeService
{
    /// <summary>
    /// 交易所名称 (如 Binance, OKX)
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 获取历史 K 线数据
    /// </summary>
    /// <param name="symbol">交易对 (如 BTCUSDT)</param>
    /// <param name="interval">周期 (如 1m, 1h - 暂时用字符串代替枚举)</param>
    /// <param name="limit">获取数量</param>
    /// <returns>通用 K 线列表</returns>
    Task<List<UnifiedKline>> GetKlinesAsync(string symbol, string interval, int limit = 100);
}