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
    
    // 保存取消订阅的 Token
    private Action? _unsubscribeAction;

    // 定义一个异步命令：加载数据
    // CommunityToolkit 会自动生成 LoadDataCommand 属性
    [RelayCommand]
    private async Task LoadData()
    {
        Title = "Connecting to Binance...";

        // 1. 加载历史数据
        var klines = await _exchangeService.GetKlinesAsync("BTCUSDT", "OneMinute", 100);
        
        if (klines.Count > 0)
        {
            CurrentPrice = klines[^1].Close;
            OnKlinesLoaded?.Invoke(klines);
            
            // 重置图表上的线
            ResetTradeToCurrentPrice();
        }

        // 2. 取消旧的订阅
        _unsubscribeAction?.Invoke();

        // 3. 订阅实时数据
        _unsubscribeAction = await _exchangeService.SubscribeToKlineAsync("BTCUSDT", "OneMinute", newKline =>
        {
            // 注意：这里是在后台线程回调，修改 UI 属性需要注意
            // ObservableProperty 通常支持，但 View 层的 Chart 更新需要 Dispatcher
            
            CurrentPrice = newKline.Close;
            
            // 通知 View 更新最后一根 K 线
            // 这里我们定义一个新事件 OnRealtimeUpdate
            OnRealtimeUpdate?.Invoke(newKline);
        });
        
        Title = "Binance Connected.";
        
    }
    // 新增实时更新事件
    public event Action<UnifiedKline>? OnRealtimeUpdate;
    
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

    try 
    {
        // 1. 设置状态
        // IsBusy = true; // 如果你有这个属性

        // 2. 调用真实 API
        System.Diagnostics.Debug.WriteLine("[API] Sending Order to Binance...");
        
        long mainOrderId = await _exchangeService.PlaceOrderAsync(
            symbol: "BTCUSDT",
            side: TradeSetup.IsLong ? "Buy" : "Sell",
            type: TradeSetup.OrderTypeLabel.Split(' ')[0], // "Limit" or "Market"
            quantity: TradeSetup.Quantity > 0 ? TradeSetup.Quantity : 0.001, // 缺省值保护
            price: TradeSetup.EntryPrice,
            tpPrice: TradeSetup.TpPrice > 0 ? TradeSetup.TpPrice : null,
            slPrice: TradeSetup.SlPrice > 0 ? TradeSetup.SlPrice : null
        );

        System.Diagnostics.Debug.WriteLine($"[API] Success! Order ID: {mainOrderId}");

        // 3. 更新本地 UI (添加到实盘列表)
        // 注意：真实场景下，我们应该通过 WebSocket 收到 OrderUpdate 推送来更新列表
        // 但为了交互即时性，我们先手动在本地加一条“预判”数据，等 WS 推送来了再更新状态
        
        var mainOrder = new RealOrderModel
        {
            Id = mainOrderId,
            ParentId = null,
            Symbol = "BTCUSDT",
            Side = TradeSetup.IsLong ? "Buy" : "Sell",
            Type = TradeSetup.OrderTypeLabel.Split(' ')[0],
            Price = TradeSetup.EntryPrice,
            Quantity = TradeSetup.Quantity,
            Status = "New" // 刚下的单
        };
        RealOrders.Add(mainOrder);

        // 如果有 TP/SL，虽然 API 可能没发，我们在图上先画出来作为“本地计划”
        // (真正完善的系统会在这里继续发 TP/SL 的 API 请求)
        if (TradeSetup.TpPrice > 0)
        {
            RealOrders.Add(new RealOrderModel { 
                Id = mainOrderId + 1, ParentId = mainOrderId, Symbol = "BTCUSDT", 
                Side = !TradeSetup.IsLong ? "Buy" : "Sell", Type = "TakeProfit", Price = TradeSetup.TpPrice 
            });
        }
        if (TradeSetup.SlPrice > 0)
        {
            RealOrders.Add(new RealOrderModel { 
                Id = mainOrderId + 2, ParentId = mainOrderId, Symbol = "BTCUSDT", 
                Side = !TradeSetup.IsLong ? "Buy" : "Sell", Type = "StopLoss", Price = TradeSetup.SlPrice 
            });
        }

        // 4. 重置预览
        CancelPreview();
    }
    catch (Exception ex)
    {
        // 这里很重要：把错误打印出来，看看 API 为什么拒绝
        System.Diagnostics.Debug.WriteLine($"下单失败: {ex.Message}");
        // 如果有 MessageBox 可以在这里弹窗
    }
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