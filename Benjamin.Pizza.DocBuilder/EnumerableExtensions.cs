namespace Benjamin.Pizza.DocBuilder;

internal static class EnumerableExtensions
{
    public static IEnumerable<T> PrependIfNotEmpty<T>(this IEnumerable<T> enumerable, T item)
        => enumerable.Any()
            ? enumerable.Prepend(item)
            : enumerable;
}
