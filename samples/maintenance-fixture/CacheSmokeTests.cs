using Microsoft.Extensions.Caching.Memory;
using Xunit;

namespace DependencyMaintenanceFixture;

public sealed class CacheSmokeTests
{
    [Fact]
    public void Cache_returns_the_value_that_was_stored()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());

        cache.Set("maintenance-demo", "healthy");

        Assert.Equal("healthy", cache.Get<string>("maintenance-demo"));
    }
}
