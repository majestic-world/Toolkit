using System;

namespace L2Toolkit.Utilities;

public static class AppNavigator
{
    public static event Action<string>? NavigateTo;

    public static void RequestNavigateTo(string page) => NavigateTo?.Invoke(page);
}
