namespace System.Linq;

/// <summary>
/// Provides compatibility helpers for working with <see cref="IAsyncEnumerable{T}" /> sequences.
/// </summary>
public static class AsyncEnumerableExtensions
{
    /// <summary>
    /// Materializes an <see cref="IAsyncEnumerable{T}" /> into a <see cref="List{T}" /> while honoring cancellation during enumeration.
    /// </summary>
    /// <typeparam name="T">The type of items produced by the asynchronous sequence.</typeparam>
    /// <param name="items">The asynchronous sequence to enumerate.</param>
    /// <param name="cancellationToken">A token that can stop enumeration before all items have been read.</param>
    /// <returns>A list containing the items yielded by <paramref name="items" /> in enumeration order.</returns>
    public static async Task<List<T>> ToListAsync<T>(this IAsyncEnumerable<T> items, CancellationToken cancellationToken = default)
    {
        var results = new List<T>();
        await foreach (var item in items.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            results.Add(item);
        }

        return results;
    }
}
