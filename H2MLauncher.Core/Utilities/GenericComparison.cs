namespace H2MLauncher.Core.Utilities;

public static class GenericComparison
{
    /// <summary>
    /// Returns the smaller of the two provided values.
    /// </summary>
    public static T Min<T>(T value1, T value2)
    {        
        return Comparer<T>.Default.Compare(value1, value2) < 0 ? value1 : value2;
    }

    /// <summary>
    /// Returns the smaller of the two provided values.
    /// </summary>
    public static T Max<T>(T value1, T value2)
    {
        return Comparer<T>.Default.Compare(value1, value2) > 0 ? value1 : value2;
    }
}
