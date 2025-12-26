using ScottPlot;
using SkiaSharp;
using CryptoTerminal.Core.Models;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System;


namespace CryptoTerminal.App.Components;

public class RealPositionOverlay : IPlottable
{
    public bool IsVisible { get; set; } = true;
    public IAxes Axes { get; set; } = new ScottPlot.Axes();
    public IEnumerable<LegendItem> LegendItems => LegendItem.None;

    // 数据源
    public ObservableCollection<RealOrderModel> Orders { get; set; } = new();

    // 缓存：用于碰撞检测
    // Key: OrderId, Value: (LineY, CloseButtonRect)
    private Dictionary<long, (float Y, SKRect CloseRect)> _hitTargets = new();

    // 画笔
    private readonly SKPaint _buyLinePaint = new() { Color = SKColors.Green, StrokeWidth = 2, IsAntialias = true };
    private readonly SKPaint _sellLinePaint = new() { Color = SKColors.Red, StrokeWidth = 2, IsAntialias = true };
    private readonly SKPaint _textPaint = new() { Color = SKColors.White, TextSize = 11, IsAntialias = true };
    private readonly SKPaint _bgPaint = new() { IsAntialias = true };
    private readonly SKPaint _closeBtnPaint = new() { Color = SKColors.Gray, IsAntialias = true };

    public AxisLimits GetAxisLimits() => AxisLimits.NoLimits;

    public void Render(RenderPack rp)
    {
        if (!IsVisible) return;
        _hitTargets.Clear(); // 清空上一帧的碰撞缓存

        foreach (var order in Orders)
        {
            float y = Axes.GetPixelY(order.Price);
            if (!rp.DataRect.ContainsY(y)) continue;

            bool isBuy = order.Side.Equals("Buy", StringComparison.OrdinalIgnoreCase);
            var paint = isBuy ? _buyLinePaint : _sellLinePaint;
            var color = isBuy ? SKColors.Green : SKColors.Red;

            // 1. 画线
            rp.Canvas.DrawLine(rp.DataRect.Left, y, rp.DataRect.Right, y, paint);

            // 2. 画标签 (左侧)
            string label = $"#{order.Id} {order.Type}";
            float textWidth = _textPaint.MeasureText(label);
            float labelHeight = 20;
            
            // 标签背景
            var labelRect = new SKRect(rp.DataRect.Left, y - labelHeight/2, rp.DataRect.Left + textWidth + 20, y + labelHeight/2);
            _bgPaint.Color = color;
            rp.Canvas.DrawRect(labelRect, _bgPaint);
            rp.Canvas.DrawText(label, labelRect.Left + 5, y + labelHeight/2 - 4, _textPaint);

            // 3. 画关闭按钮 [X] (紧跟在标签右侧)
            float btnSize = 16;
            var closeRect = new SKRect(labelRect.Right + 5, y - btnSize/2, labelRect.Right + 5 + btnSize, y + btnSize/2);
            
            rp.Canvas.DrawRect(closeRect, _closeBtnPaint);
            // 画个简单的 "X"
            using var xPaint = new SKPaint { Color = SKColors.White, StrokeWidth = 2, IsAntialias = true };
            rp.Canvas.DrawLine(closeRect.Left + 3, closeRect.Top + 3, closeRect.Right - 3, closeRect.Bottom - 3, xPaint);
            rp.Canvas.DrawLine(closeRect.Left + 3, closeRect.Bottom - 3, closeRect.Right - 3, closeRect.Top + 3, xPaint);

            // 4. 记录碰撞区域
            _hitTargets[order.Id] = (y, closeRect);
        }
    }

    // --- 碰撞检测 API ---

    // 检测是否点中实盘线 (用于拖拽改单)
    public long? GetHitLine(float mouseY)
    {
        foreach (var kvp in _hitTargets)
        {
            if (Math.Abs(mouseY - kvp.Value.Y) < 10) return kvp.Key;
        }
        return null;
    }

    // 检测是否点中关闭按钮 (用于撤单)
    public long? GetHitCloseButton(float x, float y)
    {
        foreach (var kvp in _hitTargets)
        {
            if (kvp.Value.CloseRect.Contains(x, y)) return kvp.Key;
        }
        return null;
    }
}