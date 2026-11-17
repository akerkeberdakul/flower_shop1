namespace FlowerShop;

public static class LazyQuotes
{
    public static IEnumerable<Result<QuoteDisplay, AppError>> Generate(
        IEnumerable<Order> requests,
        Inventory inventory,
        ShopRules rules,
        Func<Order, decimal> deliveryPolicy)
    {
        ArgumentNullException.ThrowIfNull(requests);
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(deliveryPolicy);

        return Iterate(requests, inventory, rules, deliveryPolicy);
    }

    private static IEnumerable<Result<QuoteDisplay, AppError>> Iterate(
        IEnumerable<Order> requests,
        Inventory inventory,
        ShopRules rules,
        Func<Order, decimal> deliveryPolicy)
    {
        foreach (var request in requests)
        {
            yield return QuoteWorkflow.RequestQuote(request, inventory, rules, deliveryPolicy);
        }
    }
}
