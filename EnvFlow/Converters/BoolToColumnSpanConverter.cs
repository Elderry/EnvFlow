using Microsoft.UI.Xaml.Data;
using System;

namespace EnvFlow.Converters;

public class BoolToColumnSpanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is bool isComposite && isComposite)
        {
            return 4; // Span all 4 columns for composite variables
        }
        return 1; // Single column for leaf items
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
