namespace FlowerShop;

public static class Scoring
{
    public static Func<int, Func<int, Func<int, Func<StockItem, int>>>> Urgency { get; } =
        targetStock => shortageWeight => priceWeight => CreateScore(targetStock, shortageWeight, priceWeight);

    private static Func<StockItem, int> CreateScore(int targetStock, int shortageWeight, int priceWeight)
    {
        if (targetStock < 0 || shortageWeight < 0 || priceWeight < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(targetStock),
                "The target stock and both weights must be 0 or more.");
        }

        return item =>
            Math.Max(0, targetStock - item.Stock) * shortageWeight
            + (int)FlowerCatalog.UnitPrice(item.Flower) * priceWeight;
    }
}
