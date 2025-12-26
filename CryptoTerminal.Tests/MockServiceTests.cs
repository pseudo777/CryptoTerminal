using Xunit;
using CryptoTerminal.Core.Services;

namespace CryptoTerminal.Tests;

public class MockServiceTests
{
    [Fact]
    public async Task GetKlines_ShouldReturnCorrectCount()
    {
        // Arrange (准备)
        var service = new MockExchangeService();

        // Act (执行)
        var klines = await service.GetKlinesAsync("BTCUSDT", "1m", 50);

        // Assert (验证)
        Assert.Equal(50, klines.Count);
        Assert.Equal("MockExchange", service.Name);
        Assert.True(klines[0].Close > 0);
    }
}