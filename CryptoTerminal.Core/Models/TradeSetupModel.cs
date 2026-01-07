using CommunityToolkit.Mvvm.ComponentModel;

namespace CryptoTerminal.Core.Models;

/// <summary>
/// 交易计划模型 (包含入场、止盈、止损信息)
/// 继承 ObservableObject 以便支持双向绑定
/// </summary>
public partial class TradeSetupModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(OrderTypeLabel))]
    [NotifyPropertyChangedFor(nameof(IsValid))]
    private double _entryPrice;

    
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RiskRewardLabel))]
    [NotifyPropertyChangedFor(nameof(EntryToTpPercent))]
    [NotifyPropertyChangedFor(nameof(IsValid))]
    private double _tpPrice; // 暂时不用 nullable，简化逻辑，0表示未设置

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RiskRewardLabel))]
    [NotifyPropertyChangedFor(nameof(EntryToSlPercent))]
    [NotifyPropertyChangedFor(nameof(IsValid))]
    private double _slPrice;

    // 市场现价 (用于计算是 Limit 还是 Stop 单)
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(OrderTypeLabel))]
    private double _marketPrice;
    
    // 方向: true=Long, false=Short
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(OrderTypeLabel))]
    [NotifyPropertyChangedFor(nameof(IsValid))]
    private bool _isLong = true;
    
    [ObservableProperty]
    private double _quantity = 0.002; // 给一个默认值，防止新手填0报错 (BTC 最小通常是 0.001)
    
    // 盈亏比 (Risk/Reward Ratio) 文本
    public string RiskRewardLabel
    {
        get
        {
            if (TpPrice <= 0 || SlPrice <= 0 || EntryPrice <= 0) return "R/R: --";
            
            // 计算距离绝对值
            double rewardDist = Math.Abs(TpPrice - EntryPrice);
            double riskDist = Math.Abs(EntryPrice - SlPrice);
            
            if (riskDist < 0.000001) return "R/R: ∞";
            
            double ratio = rewardDist / riskDist;
            return $"R/R: {ratio:F2}";
        }
    }
    
    public bool IsValid
    {
        get
        {
            if (EntryPrice <= 0) return false;

            // 验证 TP
            if (TpPrice > 0)
            {
                if (IsLong && TpPrice <= EntryPrice) return false;
                if (!IsLong && TpPrice >= EntryPrice) return false;
            }

            // 验证 SL
            if (SlPrice > 0)
            {
                if (IsLong && SlPrice >= EntryPrice) return false;
                if (!IsLong && SlPrice <= EntryPrice) return false;
            }

            return true;
        }
    }
    
    // 辅助属性：显示百分比
    public double EntryToTpPercent => EntryPrice > 0 ? (Math.Abs(TpPrice - EntryPrice) / EntryPrice) * 100 : 0;
    public double EntryToSlPercent => EntryPrice > 0 ? (Math.Abs(EntryPrice - SlPrice) / EntryPrice) * 100 : 0;

    // 辅助属性：判断当前是 Limit 还是 Stop
    public string OrderTypeLabel
    {
        get
        {
            if (IsLong)
                return EntryPrice < MarketPrice ? "Limit Buy" : "Stop Buy";
            else
                return EntryPrice > MarketPrice ? "Limit Sell" : "Stop Sell";
        }
    }
}