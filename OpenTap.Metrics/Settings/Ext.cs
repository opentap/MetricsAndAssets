namespace OpenTap.Metrics.Settings;

internal static class Ext
{
    public static int IndexOf<T>(this T[] arr, T elem)
    {
        for (int i = 0; i < arr.Length; i++)
            if (arr[i]?.Equals(elem) == true)
                return i;
        return -1;
    }
}