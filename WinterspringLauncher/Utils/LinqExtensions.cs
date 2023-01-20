using System.Linq.Expressions;

namespace WinterspringLauncher.Utils;

public static class LinqExtensions
{
    /// <summary>
    /// Returns the only element of a sequence, or a default value if the sequence is empty or contains more than one element.
    /// </summary>
    public static TSource? SingleOrDefaultIfMultiple<TSource>(this IEnumerable<TSource> source)
    {
        var elements = source.Take(2).ToArray();

        return (elements.Length == 1) ? elements[0] : default;
    }

    /// <summary>
    /// Returns the only element of a sequence, or a default value if the sequence is empty or contains more than one element.
    /// </summary>
    public static TSource? SingleOrDefaultIfMultiple<TSource>(this IEnumerable<TSource> source, Func<TSource, bool> predicate)
    {
        return source.Where(predicate).SingleOrDefaultIfMultiple();
    }

    /// <summary>
    /// Returns the only element of a sequence, or a default value if the sequence is empty or contains more than one element.
    /// </summary>
    public static TSource? SingleOrDefaultIfMultiple<TSource>(this IQueryable<TSource> source)
    {
        var elements = source.Take(2).ToArray();

        return (elements.Length == 1) ? elements[0] : default;
    }

    /// <summary>
    /// Returns the only element of a sequence, or a default value if the sequence is empty or contains more than one element.
    /// </summary>
    public static TSource? SingleOrDefaultIfMultiple<TSource>(this IQueryable<TSource> source, Expression<Func<TSource, bool>> predicate)
    {
        return source.Where(predicate).SingleOrDefaultIfMultiple();
    }
}
