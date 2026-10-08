using Microsoft.AI.Foundry.Local;
using FlRequest = Microsoft.AI.Foundry.Local.Request;

namespace Microsoft.AI.Local.Foundry.Runtime;

/// <summary>Creates Foundry Local requests.</summary>
internal static class FoundryLocalRequests
{
    public static FlRequest CreateRequest(IReadOnlyDictionary<string, string>? options, IEnumerable<Item> items)
    {
        var request = new FlRequest();
        try
        {
            foreach (var item in items)
            {
                request.AddItem(item);
            }

            if (options is { Count: > 0 })
            {
                request.SetOptions(new RequestOptions { AdditionalOptions = options.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal) });
            }

            return request;
        }
        catch
        {
            request.Dispose();
            throw;
        }
    }
}
