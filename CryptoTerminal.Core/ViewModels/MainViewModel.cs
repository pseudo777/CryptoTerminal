using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CryptoTerminal.Core.Interfaces;
using CryptoTerminal.Core.Models;

namespace CryptoTerminal.Core.ViewModels;

// [ObservableObject] 会自动让这个类支持属性通知
public partial class MainViewModel : ObservableObject
{
    private readonly IExchangeService _exchangeService;

    // 存储 UI 标题
    [ObservableProperty] 
    private string _title = "Crypto Terminal";

    // 存储当前价格 (用于显示在界面上)
    [ObservableProperty]
    private double _currentPrice;
    
    // 添加 TradeModel 属性
    [ObservableProperty]
    private TradeSetupModel _tradeSetup = new();
    
    // 实盘订单集合
    [ObservableProperty] private ObservableCollection<RealOrderModel> _realOrders = new();

    // 事件：通知 View 层需要重绘
    public event Action<List<UnifiedKline>>? OnKlinesLoaded;
    public event Action? OnOrderListChanged; // 订单列表变更通知

    // 构造函数注入服务
    public MainViewModel(IExchangeService exchangeService)
    {
        _exchangeService = exchangeService;
        
        // 启动时自动加载数据
        //LoadDataCommand.Execute(null);
        // 监听 TradeSetup 变化
        TradeSetup.PropertyChanged += (s, e) => 
        {
            // 这里可以触发一些逻辑，但通常由 View 层监听属性变化去 Refresh
        };
    }

    // 定义一个异步命令：加载数据
    // CommunityToolkit 会自动生成 LoadDataCommand 属性
    [RelayCommand]
    private async Task LoadData()
    {
        Title = "Loading data...";
        
        // 调用我们之前的 Mock 服务
        var klines = await _exchangeService.GetKlinesAsync("BTCUSDT", "1m", 100);
        
        if (klines.Count > 0)
        {
            // 更新当前价格为最后一根 K 线的收盘价
            CurrentPrice = klines[^1].Close;
            Title = $"Crypto Terminal - BTC/USDT: {CurrentPrice:F2}";
            
            // 通知外部：数据已加载 (这里我们暂时通过事件或直接回调处理，
            // 下一步我们会用 Messenger 或 ObservableCollection 传给图表)
            OnKlinesLoaded?.Invoke(klines);
        }
        
    }
    
    [RelayCommand]
    private void SetLongMode()
    {
        TradeSetup.IsLong = true;
        ResetTradeToCurrentPrice();
    }

    [RelayCommand]
    private void SetShortMode()
    {
        TradeSetup.IsLong = false;
        ResetTradeToCurrentPrice();
    }

    [RelayCommand]
    private void CancelPreview()
    {
        // 隐藏/重置逻辑
        // 我们约定：如果 EntryPrice 为 0，则不显示 Overlay
        TradeSetup.EntryPrice = 0;
        TradeSetup.TpPrice = 0;
        TradeSetup.SlPrice = 0;
        
        // 通知 UI 刷新 (通过属性变更通知，或者稍后在 View 层手动 Refresh)
    }

    // --- 辅助方法 ---
    private void ResetTradeToCurrentPrice()
    {
        if (CurrentPrice > 0)
        {
            TradeSetup.EntryPrice = CurrentPrice;
            TradeSetup.MarketPrice = CurrentPrice;
            
            // 切换方向时，TP/SL 逻辑变了，建议重置或智能翻转
            // 这里简单处理：重置为空
            TradeSetup.TpPrice = 0;
            TradeSetup.SlPrice = 0;
        }
    }

    
    
    
    // 下单命令 (模拟)
    [RelayCommand]
    private async Task PlaceOrder()
    {
        if (!TradeSetup.IsValid) return;

        // 模拟 API 调用延迟
        await Task.Delay(500);

        long mainId = DateTime.Now.Ticks % 10000; // 模拟 ID

        // 1. 主单
        RealOrders.Add(new RealOrderModel
        {
            Id = mainId,
            ParentId = null,
            Symbol = "BTCUSDT",
            Side = TradeSetup.IsLong ? "Buy" : "Sell",
            Type = TradeSetup.OrderTypeLabel.Split(' ')[0],
            Price = TradeSetup.EntryPrice,
            Quantity = 0.1
        });

        // 2. TP (子单)
        if (TradeSetup.TpPrice > 0)
        {
            RealOrders.Add(new RealOrderModel
            {
                Id = mainId + 1,
                ParentId = mainId, // 关联
                Symbol = "BTCUSDT",
                Side = TradeSetup.IsLong ? "Sell" : "Buy",
                Type = "TakeProfit",
                Price = TradeSetup.TpPrice,
                Quantity = 0.1
            });
        }

        // 3. SL (子单)
        if (TradeSetup.SlPrice > 0)
        {
            RealOrders.Add(new RealOrderModel
            {
                Id = mainId + 2,
                ParentId = mainId, // 关联
                Symbol = "BTCUSDT",
                Side = TradeSetup.IsLong ? "Sell" : "Buy",
                Type = "StopLoss",
                Price = TradeSetup.SlPrice,
                Quantity = 0.1
            });
        }
        //Console.WriteLine($"Count: {RealOrders.Count}"); 

        // 4. 下单成功后，重置预览
        CancelPreview(); 
        OnPropertyChanged(nameof(RealOrders));
    }
    
    // --- 撤单逻辑 (级联撤销) ---
    [RelayCommand]
    private async Task CancelOrder(RealOrderModel? order)
    {
        if (order == null) return;
        
        // 注意：如果是从 ID 查找过来的，可能需要先在集合里找到真实引用
        if (!RealOrders.Contains(order)) 
        {
            order = RealOrders.FirstOrDefault(o => o.Id == order.Id);
            if (order == null) return;
        }

        System.Diagnostics.Debug.WriteLine($"正在撤单 #{order.Id} (Parent: {order.ParentId})");
        await Task.Delay(100);

        // 如果是主单，撤销所有子单
        if (order.IsMainOrder)
        {
            var children = RealOrders.Where(o => o.ParentId == order.Id).ToList();
            foreach (var child in children) RealOrders.Remove(child);
            RealOrders.Remove(order);
        }
        else
        {
            // 只是子单，仅撤销自己
            RealOrders.Remove(order);
        }
        
        // 通知 View 刷新图表
        OnOrderListChanged?.Invoke();
    }
    // --- 辅助命令：通过 ID 撤单 (给图表点击用) ---
    public void CancelOrderById(long orderId)
    {
        var order = RealOrders.FirstOrDefault(x => x.Id == orderId);
        if (order != null)
        {
            CancelOrderCommand.Execute(order);
        }
    }
}