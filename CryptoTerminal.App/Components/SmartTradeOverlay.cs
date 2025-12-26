using ScottPlot;
using SkiaSharp;
using CryptoTerminal.Core.Models;
using System;
using System.Collections.Generic;

namespace CryptoTerminal.App.Components;

/// <summary>
/// 智能交易绘图层：负责画 Entry/TP/SL 线
/// </summary>
public class SmartTradeOverlay : IPlottable
{
    // 必须实现接口属性
    public bool IsVisible { get; set; } = true;
    public IAxes Axes { get; set; } = new Axes();
    public IEnumerable<LegendItem> LegendItems => LegendItem.None;

    // 核心数据源
    public TradeSetupModel Model { get; set; }

    // --- 新增拖拽状态 ---
    public bool IsDraggingTp { get; set; }
    public bool IsDraggingSl { get; set; }

    // 交互状态：是否正在拖拽？
    public bool IsDraggingEntry { get; set; } = false;

    // 绘图画笔 (预先创建，性能优化)
    private readonly SKPaint _entryLinePaint = new()
    {
        Color = SKColors.Orange,
        StrokeWidth = 2,
        IsAntialias = true,
        PathEffect = SKPathEffect.CreateDash(new float[] { 10, 5 }, 0) // 虚线
    };

    private readonly SKPaint _tpLinePaint = new()
    {
        Color = SKColors.Green, StrokeWidth = 2, IsAntialias = true,
        PathEffect = SKPathEffect.CreateDash(new float[] { 5, 5 }, 0)
    };

    private readonly SKPaint _slLinePaint = new()
    {
        Color = SKColors.Red, StrokeWidth = 2, IsAntialias = true,
        PathEffect = SKPathEffect.CreateDash(new float[] { 5, 5 }, 0)
    };

    // 2. 区域填充画笔
    private readonly SKPaint _profitFill = new()
        { Color = SKColors.Green.WithAlpha(30), Style = SKPaintStyle.Fill, IsAntialias = true };

    private readonly SKPaint _lossFill = new()
        { Color = SKColors.Red.WithAlpha(30), Style = SKPaintStyle.Fill, IsAntialias = true };

    // 3. 文字与背景画笔
    private readonly SKPaint _textPaint = new() { Color = SKColors.White, TextSize = 12, IsAntialias = true };
    private readonly SKPaint _entryBgPaint = new() { Color = SKColors.Orange, IsAntialias = true };
    private readonly SKPaint _tpBgPaint = new() { Color = SKColors.Green, IsAntialias = true };
    private readonly SKPaint _slBgPaint = new() { Color = SKColors.Red, IsAntialias = true };

    private readonly SKPaint _textBgPaint = new()
    {
        Color = SKColors.Orange,
        IsAntialias = true
    };


    // 缓存最后一次绘制的 Y 像素坐标，用于碰撞检测
    private float _lastEntryPixelY;

    public AxisLimits GetAxisLimits() => AxisLimits.NoLimits;

    public void Render(RenderPack rp)
    {
        if (Model == null || !IsVisible || Model.EntryPrice <= 0) return;

        // 1. 坐标转换
        float yEntry = Axes.GetPixelY(Model.EntryPrice);
        float xLeft = rp.DataRect.Left;
        float xRight = rp.DataRect.Right;
        _lastEntryPixelY = yEntry;

        // 2. 绘制色块区域 (Risk/Reward Zones)
        // 只有当 TP 或 SL 存在时才画
        if (Model.TpPrice > 0)
        {
            float yTp = Axes.GetPixelY(Model.TpPrice);
            // 绘制绿色矩形 (Entry 到 TP)
            var rect = new SKRect(xLeft, Math.Min(yEntry, yTp), xRight, Math.Max(yEntry, yTp));
            rp.Canvas.DrawRect(rect, _profitFill);
            _lastTpPixelY = yTp;
        }

        if (Model.SlPrice > 0)
        {
            float ySl = Axes.GetPixelY(Model.SlPrice);
            // 绘制红色矩形 (Entry 到 SL)
            var rect = new SKRect(xLeft, Math.Min(yEntry, ySl), xRight, Math.Max(yEntry, ySl));
            rp.Canvas.DrawRect(rect, _lossFill);
            _lastSlPixelY = ySl;
        }

        // 3. 绘制线条
        // Entry Line
        _entryLinePaint.StrokeWidth = IsDraggingEntry ? 4 : 2;
        rp.Canvas.DrawLine(xLeft, yEntry, xRight, yEntry, _entryLinePaint);

        // TP Line
        if (Model.TpPrice > 0)
        {
            _tpLinePaint.StrokeWidth = IsDraggingTp ? 4 : 2;
            float yTp = Axes.GetPixelY(Model.TpPrice);
            rp.Canvas.DrawLine(xLeft, yTp, xRight, yTp, _tpLinePaint);
            DrawLabel(rp, yTp, $"TP: {Model.TpPrice:F2} (+{Model.EntryToTpPercent:F2}%)", _tpBgPaint, false);
        }

        // SL Line
        if (Model.SlPrice > 0)
        {
            _slLinePaint.StrokeWidth = IsDraggingSl ? 4 : 2;
            float ySl = Axes.GetPixelY(Model.SlPrice);
            rp.Canvas.DrawLine(xLeft, ySl, xRight, ySl, _slLinePaint);
            DrawLabel(rp, ySl, $"SL: {Model.SlPrice:F2} (-{Model.EntryToSlPercent:F2}%)", _slBgPaint, false);
        }

        // 4. 绘制 Entry 标签和按钮 ([+TP] [+SL])
        DrawEntryLabelAndButtons(rp, yEntry);
    }

    /// <summary>
    /// 碰撞检测：判断鼠标是否按在了线上
    /// </summary>
    // --- 碰撞检测区域缓存 ---
    private SKRect _btnTpRect;
    private SKRect _btnSlRect;
    
    private float _lastTpPixelY;
    private float _lastSlPixelY;
    
    
    private void DrawLabel(RenderPack rp, float y, string text, SKPaint bgPaint, bool isEntry)
    {
        float textWidth = _textPaint.MeasureText(text);
        float padding = 8;
        float height = 20;
        
        // 标签靠右对齐
        var rect = new SKRect(rp.DataRect.Right - textWidth - padding * 2, y - height/2, rp.DataRect.Right, y + height/2);
        
        rp.Canvas.DrawRect(rect, bgPaint);
        rp.Canvas.DrawText(text, rect.Left + padding, y + height/2 - 4, _textPaint);
    }

    private void DrawEntryLabelAndButtons(RenderPack rp, float y)
    {
        // 先画基础标签
        string text = $"{Model.OrderTypeLabel}: {Model.EntryPrice:F2}";
        float textWidth = _textPaint.MeasureText(text);
        float padding = 8;
        float height = 24;
        
        var mainRect = new SKRect(rp.DataRect.Right - textWidth - padding * 2, y - height/2, rp.DataRect.Right, y + height/2);
        rp.Canvas.DrawRect(mainRect, _entryBgPaint);
        rp.Canvas.DrawText(text, mainRect.Left + padding, y + height/2 - 5, _textPaint);

        // --- 画按钮 [+TP] [+SL] ---
        // 按钮画在标签的左侧
        float btnWidth = 30;
        float gap = 5;

        // TP 按钮 (绿色)
        _btnTpRect = new SKRect(mainRect.Left - gap - btnWidth, mainRect.Top, mainRect.Left - gap, mainRect.Bottom);
        rp.Canvas.DrawRect(_btnTpRect, _tpBgPaint);
        rp.Canvas.DrawText("+TP", _btnTpRect.Left + 4, y + 4, _textPaint); // 简单居中

        // SL 按钮 (红色)
        _btnSlRect = new SKRect(_btnTpRect.Left - gap - btnWidth, mainRect.Top, _btnTpRect.Left - gap, mainRect.Bottom);
        rp.Canvas.DrawRect(_btnSlRect, _slBgPaint);
        rp.Canvas.DrawText("+SL", _btnSlRect.Left + 4, y + 4, _textPaint);
    }
    // --- 交互辅助方法 ---

    public bool IsHitEntry(float mouseY) => Math.Abs(mouseY - _lastEntryPixelY) < 10;
    
    // 如果 TP/SL 还没创建(价格为0)，则无法击中线身，只能通过按钮创建
    public bool IsHitTpLine(float mouseY) => Model.TpPrice > 0 && Math.Abs(mouseY - _lastTpPixelY) < 10;
    public bool IsHitSlLine(float mouseY) => Model.SlPrice > 0 && Math.Abs(mouseY - _lastSlPixelY) < 10;

    public bool IsHitTpButton(float x, float y) => _btnTpRect.Contains(x, y);
    public bool IsHitSlButton(float x, float y) => _btnSlRect.Contains(x, y);
}