using System.Collections.Immutable;

namespace FlowerShop;

public static class QueryExtensions
{
    public static IOrderedEnumerable<T> RankBy<T>(
        this IEnumerable<T> source,
        Func<T, int> score,
        Func<T, string> tieBreaker)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(score);
        ArgumentNullException.ThrowIfNull(tieBreaker);

        return source
            .OrderByDescending(score)
            .ThenBy(tieBreaker, StringComparer.Ordinal);
    }

    public static Result<ImmutableArray<T>, TError> Collect<T, TError>(this IEnumerable<Result<T, TError>> results)
    {
        ArgumentNullException.ThrowIfNull(results);

        var values = ImmutableArray.CreateBuilder<T>();

        foreach (var result in results)
        {
            var failure = result.Match<Result<ImmutableArray<T>, TError>?>(
                value =>
                {
                    values.Add(value);
                    return null;
                },
                error => Result<ImmutableArray<T>, TError>.Fail(error));

            if (failure is not null)
            {
                return failure;
            }
        }

        return Result<ImmutableArray<T>, TError>.Ok(values.ToImmutable());
    }
}
