using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Controls;

namespace EnvFlow.Controls;

/// <summary>
/// A Grid control that allows setting the cursor.
/// </summary>
public class CursorGrid : Grid
{
    public InputCursor? Cursor
    {
        get => ProtectedCursor;
        set => ProtectedCursor = value;
    }
}
