using Microsoft.Extensions.Caching.Memory;

namespace MaintenanceLab;

public sealed class WorkItemCache(IMemoryCache cache)
{
    public string GetTitle(string id) => cache.GetOrCreate($"work-item:{id}", entry =>
    {
        entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
        return $"Maintenance item {id}";
    })!;
}
