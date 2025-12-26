
namespace CryptoTerminal.Core.Models;

/// <summary>
/// 通用 K 线数据模型 (不可变数据，适合高频处理)
/// </summary>
/// <param name="OpenTime">开盘时间</param>
/// <param name="Open">开盘价</param>
/// <param name="High">最高价</param>
/// <param name="Low">最低价</param>
/// <param name="Close">收盘价</param>
/// <param name="Volume">成交量</param>
public record UnifiedKline(
    DateTime OpenTime, 
    double Open, 
    double High, 
    double Low, 
    double Close, 
    double Volume
);