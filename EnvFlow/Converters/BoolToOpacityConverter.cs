using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using System;
using Windows.UI;

namespace EnvFlow.Converters;

public class BoolToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is bool boolValue && parameter is string colorParam)
        {
            if (boolValue)
            {
                // Parse the color from parameter (e.g., "#107C10")
                return ParseColor(colorParam);
            }
            else
            {
                // Return gray when disabled
                return new SolidColorBrush(Color.FromArgb(255, 160, 160, 160));
            }
        }
        return new SolidColorBrush(Color.FromArgb(255, 0, 0, 0));
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }

    private SolidColorBrush ParseColor(string hex)
    {
        hex = hex.Replace("#", "");
        byte a = 255;
        byte r = System.Convert.ToByte(hex.Substring(0, 2), 16);
        byte g = System.Convert.ToByte(hex.Substring(2, 2), 16);
        byte b = System.Convert.ToByte(hex.Substring(4, 2), 16);
        return new SolidColorBrush(Color.FromArgb(a, r, g, b));
    }
}
