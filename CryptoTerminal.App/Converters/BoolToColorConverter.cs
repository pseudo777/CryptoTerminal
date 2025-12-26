using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Globalization;

namespace CryptoTerminal.App.Converters;

public class BoolToColorConverter : IValueConverter
{
    // 定义两种颜色：选中色和未选中色
    // Long 模式：选中=绿，未选中=灰
    // Short 模式：选中=红，未选中=灰
    // 我们通过 parameter 来区分是给 "Long" 按钮用还是给 "Short" 按钮用

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isLong && parameter is string buttonType)
        {
            // 默认灰色 (未选中)
            var defaultBrush = Brushes.Gray;

            if (buttonType == "Long")
            {
                // 如果当前是 Long 模式 (isLong == true)，则绿色
                return isLong ? Brushes.Green : defaultBrush;
            }
            else if (buttonType == "Short")
            {
                // 如果当前是 Short 模式 (isLong == false)，则红色
                return !isLong ? Brushes.Red : defaultBrush;
            }
        }
        return Brushes.Gray;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}