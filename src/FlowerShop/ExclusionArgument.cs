using System.Collections.Immutable;

namespace FlowerShop;

public static class ExclusionArgument
{
    public static Result<ImmutableArray<FlowerType>, AppError> Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Result<ImmutableArray<FlowerType>, AppError>.Ok(ImmutableArray<FlowerType>.Empty);
        }

        var flowers = ImmutableArray.CreateBuilder<FlowerType>();

        foreach (var name in text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!FlowerCatalog.TryParse(name, out var flower))
            {
                return Result<ImmutableArray<FlowerType>, AppError>.Fail(AppError.Domain(
                    "invalid_argument",
                    $"Unknown flower '{name}' in the exclusion list."));
            }

            if (!flowers.Contains(flower))
            {
                flowers.Add(flower);
            }
        }

        return Result<ImmutableArray<FlowerType>, AppError>.Ok(flowers.ToImmutable());
    }
}