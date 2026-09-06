using Microsoft.Extensions.Caching.Memory;
using Xunit;

namespace MaintenanceLab.Tests;

public sealed class CacheSmokeTests
{
    [Fact]
    public void Returns_a_stable_cached_title()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var sut = new WorkItemCache(cache);

        Assert.Equal("Maintenance item 42", sut.GetTitle("42"));
        Assert.Equal("Maintenance item 42", sut.GetTitle("42"));
    }
}
