using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AgenticCommerce.Core.Security;

namespace CrestApps.OrchardCore.AgenticCommerce.Core.Ap2;

/// <summary>
/// Evaluates the constraints of an open checkout mandate against a checkout, as the AP2 SDK does
/// (<c>checkout.allowed_merchants</c> and <c>checkout.line_items</c>). Unknown constraint types fail.
/// </summary>
internal static class Ap2CheckoutConstraints
{
    public const string AllowedMerchants = "checkout.allowed_merchants";

    public const string LineItems = "checkout.line_items";

    public static List<string> Evaluate(JsonObject openMandate, JsonObject checkout)
    {
        var violations = new List<string>();

        if (openMandate["constraints"] is not JsonArray constraints)
        {
            return violations;
        }

        foreach (var node in constraints)
        {
            if (node is not JsonObject constraint)
            {
                violations.Add("A constraint is not an object.");

                continue;
            }

            switch (JoseJson.GetString(constraint, "type"))
            {
                case AllowedMerchants:
                    EvaluateAllowedMerchants(constraint, checkout, violations);
                    break;

                case LineItems:
                    EvaluateLineItems(constraint, checkout, violations);
                    break;

                default:
                    violations.Add($"The constraint type '{JoseJson.GetString(constraint, "type")}' is not supported.");
                    break;
            }
        }

        return violations;
    }

    private static void EvaluateAllowedMerchants(JsonObject constraint, JsonObject checkout, List<string> violations)
    {
        if (checkout["merchant"] is not JsonObject merchant)
        {
            violations.Add("The checkout has no merchant.");

            return;
        }

        if (constraint["allowed"] is JsonArray allowed)
        {
            foreach (var candidate in allowed)
            {
                if (candidate is JsonObject allowedMerchant && MerchantMatches(allowedMerchant, merchant))
                {
                    return;
                }
            }
        }

        violations.Add("The checkout merchant is not in the allowed list.");
    }

    private static bool MerchantMatches(JsonObject candidate, JsonObject target)
    {
        var candidateId = JoseJson.GetString(candidate, "id");
        var targetId = JoseJson.GetString(target, "id");

        if (!string.IsNullOrEmpty(candidateId) && !string.IsNullOrEmpty(targetId))
        {
            return candidateId == targetId;
        }

        var name = JoseJson.GetString(candidate, "name");
        var website = JoseJson.GetString(candidate, "website");

        return !string.IsNullOrEmpty(name)
            && !string.IsNullOrEmpty(website)
            && name == JoseJson.GetString(target, "name")
            && website == JoseJson.GetString(target, "website");
    }

    private static void EvaluateLineItems(JsonObject constraint, JsonObject checkout, List<string> violations)
    {
        var cartQuantities = new Dictionary<string, long>(StringComparer.Ordinal);

        if (checkout["line_items"] is JsonArray lineItems)
        {
            foreach (var node in lineItems)
            {
                if (node is not JsonObject lineItem
                    || lineItem["item"] is not JsonObject item
                    || JoseJson.GetString(item, "id") is not { } itemId
                    || !JoseJson.TryGetNumber(lineItem["quantity"], out var quantity))
                {
                    violations.Add("A checkout line item has no item id or quantity.");

                    return;
                }

                cartQuantities[itemId] = cartQuantities.GetValueOrDefault(itemId) + (long)quantity;
            }
        }

        if (cartQuantities.Count == 0)
        {
            violations.Add("An empty checkout does not satisfy the line_items constraint.");

            return;
        }

        var requirements = new List<(HashSet<string> Acceptable, long Quantity)>();

        if (constraint["items"] is JsonArray items)
        {
            foreach (var node in items)
            {
                if (node is not JsonObject requirement || !JoseJson.TryGetNumber(requirement["quantity"], out var quantity))
                {
                    violations.Add("A line item requirement has no quantity.");

                    return;
                }

                var acceptable = new HashSet<string>(StringComparer.Ordinal);

                if (requirement["acceptable_items"] is JsonArray acceptableItems)
                {
                    foreach (var acceptableItem in acceptableItems)
                    {
                        if (acceptableItem is JsonObject acceptableObject && JoseJson.GetString(acceptableObject, "id") is { } id)
                        {
                            acceptable.Add(id);
                        }
                    }
                }

                requirements.Add((acceptable, (long)quantity));
            }
        }

        var hasWildcard = requirements.Exists(r => r.Acceptable.Count == 0);
        var skus = new List<string>();
        var total = 0L;

        foreach (var entry in cartQuantities)
        {
            if (entry.Value <= 0)
            {
                continue;
            }

            if (!hasWildcard && !requirements.Exists(r => r.Acceptable.Contains(entry.Key)))
            {
                violations.Add($"The item '{entry.Key}' is not acceptable under any requirement.");
            }

            skus.Add(entry.Key);
            total += entry.Value;
        }

        if (violations.Count > 0)
        {
            return;
        }

        if (MaxFlow(skus, cartQuantities, requirements) < total)
        {
            violations.Add("The checkout line items cannot all be assigned to the line item requirements.");
        }
    }

    private static long MaxFlow(
        List<string> skus,
        Dictionary<string, long> cartQuantities,
        List<(HashSet<string> Acceptable, long Quantity)> requirements)
    {
        var nodeCount = skus.Count + requirements.Count + 2;
        var source = 0;
        var sink = nodeCount - 1;
        var capacity = new long[nodeCount, nodeCount];

        for (var i = 0; i < skus.Count; i++)
        {
            capacity[source, 1 + i] = cartQuantities[skus[i]];

            for (var j = 0; j < requirements.Count; j++)
            {
                if (requirements[j].Acceptable.Count == 0 || requirements[j].Acceptable.Contains(skus[i]))
                {
                    capacity[1 + i, 1 + skus.Count + j] = long.MaxValue / 4;
                }
            }
        }

        for (var j = 0; j < requirements.Count; j++)
        {
            capacity[1 + skus.Count + j, sink] = Math.Max(0, requirements[j].Quantity);
        }

        var flow = 0L;
        var parent = new int[nodeCount];

        while (true)
        {
            Array.Fill(parent, -1);
            parent[source] = source;
            var queue = new Queue<int>();
            queue.Enqueue(source);

            while (queue.Count > 0 && parent[sink] == -1)
            {
                var u = queue.Dequeue();

                for (var v = 0; v < nodeCount; v++)
                {
                    if (parent[v] == -1 && capacity[u, v] > 0)
                    {
                        parent[v] = u;
                        queue.Enqueue(v);
                    }
                }
            }

            if (parent[sink] == -1)
            {
                return flow;
            }

            var bottleneck = long.MaxValue;

            for (var v = sink; v != source; v = parent[v])
            {
                bottleneck = Math.Min(bottleneck, capacity[parent[v], v]);
            }

            for (var v = sink; v != source; v = parent[v])
            {
                capacity[parent[v], v] -= bottleneck;
                capacity[v, parent[v]] += bottleneck;
            }

            flow += bottleneck;
        }
    }
}
