using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CryptoTerminal.Core.Models;

public  partial class RealOrderModel: ObservableObject
{
    public long Id { get; set; }           // 订单ID
    public long? ParentId { get; set; } 
    public string Symbol { get; set; } = string.Empty;
    public string Side { get; set; } = string.Empty; // "Buy" or "Sell"
    public string Type { get; set; } = string.Empty; // "Limit", "Market", "Stop"
    [ObservableProperty]
    private double _price;
    public double Quantity { get; set; }
    public string Status { get; set; } = "New"; // "New", "Filled", "Canceled"
    
    public bool IsMainOrder => ParentId == null || ParentId == 0;
    
    // 如果是 TP/SL 附属订单，可以加个标记
    public bool IsReduceOnly { get; set; } = false;
    
    public DateTime CreateTime { get; set; } = DateTime.UtcNow;
}