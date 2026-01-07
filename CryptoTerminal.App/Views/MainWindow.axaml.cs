using System;
using Avalonia.Controls;
using CryptoTerminal.Core.Models;
using CryptoTerminal.Core.ViewModels;
using ScottPlot;
using System.Collections.Generic;
using System.Linq;
using CryptoTerminal.App.Components;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace CryptoTerminal.App.Views;

public partial class MainWindow : Window
{
    private SmartTradeOverlay? _tradeOverlay;
    private RealPositionOverlay? _realOverlay;
    private bool _isDragging = false;

    private enum DragMode
    {
        None,
        Entry,
        Tp,
        Sl
    }

    // 拖拽状态
    private DragMode _previewDragMode = DragMode.None;
    private long? _draggingRealOrderId = null; // 正在拖拽的实盘单ID

    public MainWindow()
    {
        InitializeComponent();

        // 监听 ViewModel 的数据加载事件
        // 当 DataContext 变化时订阅事件
        DataContextChanged += OnDataContextChanged;
        // 手动挂载事件
        ChartPlot.PointerPressed += OnPlotPointerPressed;
        ChartPlot.PointerMoved += OnPlotPointerMoved;
        ChartPlot.PointerReleased += OnPlotPointerReleased; // 补上这行
        // 🔥 核心修改：强制监听 Released 事件
        // 第三个参数 true 意味着：即使 ScottPlot 说它已经处理完了，我也要收到通知！
        //ChartPlot.AddHandler(PointerReleasedEvent, OnPlotPointerReleased, handledEventsToo: true);
        // 新增：监听窗口加载完成事件
        Loaded += OnWindowLoaded;
    }

    private void OnWindowLoaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        // 窗口加载完了，这时候订阅肯定完成了。
        // 手动触发一次数据加载
        if (DataContext is MainViewModel vm)
        {
            // 确保没有正在加载中（可选判断）
            if (vm.LoadDataCommand.CanExecute(null))
            {
                vm.LoadDataCommand.Execute(null);
            }
        }
    }

    private void OnDataContextChanged(object? sender, System.EventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            // 订阅事件：当 ViewModel 拿到数据后，通知我们画图
            vm.OnKlinesLoaded += UpdateChart;
            // 监听属性变化
            //vm.TradeSetup.PropertyChanged += TradeSetup_PropertyChanged;

            // 监听：预览线属性变化 -> 刷新
            vm.TradeSetup.PropertyChanged += (s, args) => ChartPlot.Refresh();

            // 监听：实盘列表变化 (撤单后) -> 刷新
            vm.OnOrderListChanged += () => ChartPlot.Refresh();
            
            vm.OnRealtimeUpdate += UpdateLastCandle;
        }
    }
    
    private void UpdateLastCandle(UnifiedKline kline)
    {
        // 必须在 UI 线程执行
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            // 1. 更新 ScottPlot 的数据源
            // 我们需要访问 UpdateChart 里创建的 ohlcList 列表
            // 为了方便，建议把 ohlcList 提升为 MainWindow 的成员变量
        
            if (_ohlcList.Count > 0)
            {
                var last = _ohlcList.Last();
                if (last.DateTime == kline.OpenTime)
                {
                    // 更新当前这根
                    _ohlcList[_ohlcList.Count - 1] = new OHLC(kline.Open, kline.High, kline.Low, kline.Close, kline.OpenTime, TimeSpan.FromMinutes(1));
                }
                else
                {
                    // 新增一根
                    _ohlcList.Add(new OHLC(kline.Open, kline.High, kline.Low, kline.Close, kline.OpenTime, TimeSpan.FromMinutes(1)));
                }
            
                // 2. 顺便更新 TradeModel 的 MarketPrice，保证 Limit/Stop 逻辑实时正确
                if (DataContext is MainViewModel vm)
                {
                    vm.TradeSetup.MarketPrice = kline.Close;
                }

                // 3. 刷新图表
                ChartPlot.Refresh(); 
            }
        });
    }
    private List<OHLC> _ohlcList = new();


    private void UpdateChart(List<UnifiedKline> klines)
    {
        // 1. 清空旧数据
        ChartPlot.Plot.Clear();
        // 2. 转换数据格式为 ScottPlot OHLC
        // 注意：ScottPlot 5 的 OHLC 构造函数可能需要 TimeSpan 作为周期
        //var ohlcList = new List<OHLC>();
        
        foreach (var k in klines)
        {
            _ohlcList.Add(new OHLC(k.Open, k.High, k.Low, k.Close, k.OpenTime, System.TimeSpan.FromMinutes(1)));
        }

        // 3. 添加蜡烛图
        var candlePlot = ChartPlot.Plot.Add.Candlestick(_ohlcList);
        candlePlot.RisingColor = Colors.Green;
        candlePlot.FallingColor = Colors.Red;

        // 4. 设置坐标轴自动适配日期格式
        ChartPlot.Plot.Axes.DateTimeTicksBottom();
        // 1. 获取 VM
        if (DataContext is not MainViewModel vm) return;
        double currentClose = klines.Last().Close;
        vm.TradeSetup.MarketPrice = currentClose;
        if (vm.TradeSetup.EntryPrice <= 0)
        {
            vm.TradeSetup.EntryPrice = currentClose;
        }

        if (_realOverlay == null)
        {
            _realOverlay = new RealPositionOverlay();
            // 绑定数据源 (引用传递，VM 的集合变了，这里也会变)
            _realOverlay.Orders = vm.RealOrders;
        }

        ChartPlot.Plot.Add.Plottable(_realOverlay);

        // 2. 初始化 TradeModel 的价格 (吸附到最新收盘价)
        if (vm.TradeSetup.EntryPrice <= 0)
        {
            vm.TradeSetup.EntryPrice = klines.Last().Close;
            vm.TradeSetup.MarketPrice = klines.Last().Close;
        }

        // 3. 创建并添加 Overlay (如果还没创建)
        if (_tradeOverlay == null)
        {
            _tradeOverlay = new SmartTradeOverlay { Model = vm.TradeSetup };
            // 绑定鼠标事件 (ScottPlot 控件自带事件)
            //ChartPlot.PointerPressed += OnPlotPointerPressed;
            //ChartPlot.PointerMoved += OnPlotPointerMoved;
            //ChartPlot.PointerReleased += OnPlotPointerReleased;
        }
        else
        {
            // 确保 Model 是最新的
            _tradeOverlay.Model = vm.TradeSetup;
        }

        ChartPlot.Plot.Add.Plottable(_tradeOverlay);

        // 5. 刷新重绘
        ChartPlot.Plot.Axes.AutoScale();
        ChartPlot.Refresh();
    }

    private void TradeSetup_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        // 当 ViewModel 中的 Entry/TP/SL 价格变化时 (无论是拖拽引起的，还是TextBox输入引起的)
        // 我们都调用一次图表刷新，确保视图同步
        if (e.PropertyName == nameof(TradeSetupModel.EntryPrice) ||
            e.PropertyName == nameof(TradeSetupModel.TpPrice) ||
            e.PropertyName == nameof(TradeSetupModel.SlPrice) ||
            e.PropertyName == nameof(TradeSetupModel.IsLong)) // 切换方向也要刷新颜色
        {
            ChartPlot.Refresh();
        }
    }

    // --- 鼠标交互逻辑 ---
    private DragMode _currentDragMode = DragMode.None;

    private void OnPlotPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_tradeOverlay == null) return;

        var p = e.GetPosition(ChartPlot);
        float x = (float)p.X;
        float y = (float)p.Y;

        if (DataContext is not MainViewModel vm) return;

        // --- A. 优先检查实盘层 (Real Overlay) ---
        if (_realOverlay != null)
        {
            // A1. 检查是否点了关闭按钮 [X]
            var closeId = _realOverlay.GetHitCloseButton(x, y);
            if (closeId.HasValue)
            {
                // 调用 VM 撤单
                vm.CancelOrderById(closeId.Value);
                return; // 撤单操作不触发拖拽
            }

            // A2. 检查是否点了实盘线 (准备拖拽改单)
            var dragId = _realOverlay.GetHitLine(y);
            if (dragId.HasValue)
            {
                _draggingRealOrderId = dragId.Value;
                if (_draggingRealOrderId.HasValue)
                {
                    ChartPlot.UserInputProcessor.Disable();

                    // 🔥 关键代码：强制捕获鼠标
                    e.Pointer.Capture(ChartPlot);

                    e.Handled = true;
                }

                //ChartPlot.UserInputProcessor.Disable();
                // ✅ 新增这行：告诉 Avalonia "这个点击我处理了，别给别人"
                // 这通常能确保 Released 事件能正确回调给这个控件
                //e.Handled = true; 
                return;
            }
        }

        // 如果确定要开始拖拽预览线
        if (_currentDragMode != DragMode.None)
        {
            ChartPlot.UserInputProcessor.Disable();

            // 🔥 关键代码：强制捕获鼠标
            e.Pointer.Capture(ChartPlot);

            e.Handled = true;
            return;
        }

        // --- B. 检查预览层 (Preview Overlay) ---
        if (_tradeOverlay != null && vm.TradeSetup.EntryPrice > 0)
        {
            // 1. 检查是否点中了 [TP] 按钮
            if (_tradeOverlay.IsHitTpButton(x, y))
            {
                _currentDragMode = DragMode.Tp;
                _tradeOverlay.IsDraggingTp = true;
                // 如果 TP 还没设置，初始化为 Entry 价格
                if (vm.TradeSetup.TpPrice <= 0) vm.TradeSetup.TpPrice = vm.TradeSetup.EntryPrice;

                ChartPlot.UserInputProcessor.Disable();
                return;
            }

            // 2. 检查是否点中了 [SL] 按钮
            if (_tradeOverlay.IsHitSlButton(x, y))
            {
                _currentDragMode = DragMode.Sl;
                _tradeOverlay.IsDraggingSl = true;
                if (vm.TradeSetup.SlPrice <= 0) vm.TradeSetup.SlPrice = vm.TradeSetup.EntryPrice;

                ChartPlot.UserInputProcessor.Disable();
                return;
            }

            // 3. 检查是否点中了 TP 线 (已存在的)
            if (_tradeOverlay.IsHitTpLine(y))
            {
                _currentDragMode = DragMode.Tp;
                _tradeOverlay.IsDraggingTp = true;
                ChartPlot.UserInputProcessor.Disable();
                return;
            }

            // 4. 检查是否点中了 SL 线 (已存在的)
            if (_tradeOverlay.IsHitSlLine(y))
            {
                _currentDragMode = DragMode.Sl;
                _tradeOverlay.IsDraggingSl = true;
                ChartPlot.UserInputProcessor.Disable();
                return;
            }

            // 5. 最后检查 Entry 线
            if (_tradeOverlay.IsHitEntry(y))
            {
                _currentDragMode = DragMode.Entry;
                _tradeOverlay.IsDraggingEntry = true;
                ChartPlot.UserInputProcessor.Disable();
                return;
            }
        }
    }

    private void OnPlotPointerMoved(object? sender, PointerEventArgs e)
    {
        var p = e.GetPosition(ChartPlot);
        var coords = ChartPlot.Plot.GetCoordinates((float)p.X, (float)p.Y);

        if (DataContext is not MainViewModel vm) return;

        // A. 处理实盘拖拽 (改单预览)
        if (_draggingRealOrderId.HasValue)
        {
            var order = vm.RealOrders.FirstOrDefault(o => o.Id == _draggingRealOrderId.Value);
            if (order != null)
            {
                order.Price = coords.Y; // 直接修改对象，UI会重绘
                ChartPlot.Refresh();
            }

            return;
        }

        if (_currentDragMode != DragMode.None)
        {
            double newPrice = coords.Y;

            var setup = vm.TradeSetup;

            // 根据模式更新不同的价格
            switch (_currentDragMode)
            {
                case DragMode.Entry:
                    // 移动 Entry 时，通常 TP/SL 不需要限制，因为 Entry 变了 TP/SL 可能就变得不合法了
                    // 这里我们暂不限制 Entry 的移动，允许它“穿过”TP/SL，
                    // 但为了严谨，你可以在松开鼠标时检查有效性
                    setup.EntryPrice = newPrice;
                    break;

                case DragMode.Tp:
                    // --- 止盈防呆逻辑 ---
                    if (setup.IsLong)
                    {
                        // 做多：TP 必须 > Entry
                        if (newPrice <= setup.EntryPrice) newPrice = setup.EntryPrice;
                    }
                    else
                    {
                        // 做空：TP 必须 < Entry
                        if (newPrice >= setup.EntryPrice) newPrice = setup.EntryPrice;
                    }

                    setup.TpPrice = newPrice;
                    break;

                case DragMode.Sl:
                    // --- 止损防呆逻辑 ---
                    if (setup.IsLong)
                    {
                        // 做多：SL 必须 < Entry
                        if (newPrice >= setup.EntryPrice) newPrice = setup.EntryPrice;
                    }
                    else
                    {
                        // 做空：SL 必须 > Entry
                        if (newPrice <= setup.EntryPrice) newPrice = setup.EntryPrice;
                    }

                    setup.SlPrice = newPrice;
                    break;
            }

            // 刷新
            ChartPlot.Refresh();
        }
    }

    private void OnPlotPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        // 调试代码：看看 release 时这个值还在不在
        //System.Diagnostics.Debug.WriteLine($"Released. DraggingID: {_draggingRealOrderId}");
        //System.Diagnostics.Debug.WriteLine("Released Fired");
        //Console.WriteLine("Force Released!");
        // A. 实盘拖拽结束
        if (_draggingRealOrderId.HasValue)
        {
            // 可以在这里调用 API 确认改单
            System.Diagnostics.Debug.WriteLine($"[API] Order #{_draggingRealOrderId} Modified!");
            _draggingRealOrderId = null;
            // 🔥 关键代码：释放鼠标捕获
            e.Pointer.Capture(null);
            ChartPlot.UserInputProcessor.Enable();
            e.Handled = true;
            return;
        }

        // B. 预览拖拽结束
        if (_currentDragMode != DragMode.None)
        {
            _currentDragMode = DragMode.None;
            if (_tradeOverlay != null)
            {
                _tradeOverlay.IsDraggingEntry = false;
                _tradeOverlay.IsDraggingTp = false;
                _tradeOverlay.IsDraggingSl = false;
            }

            // 🔥 关键代码：释放鼠标捕获
            e.Pointer.Capture(null);

            ChartPlot.UserInputProcessor.Enable();
            ChartPlot.Refresh();
            e.Handled = true;
        }
    }
}