using System;
using System.Collections.Generic;
using CryptoPriceTracker.Api.Models;
using Xunit;

public class PriceValidatorTests
{
    [Fact]
    public void ShouldSavePrice_ReturnsFalse_WhenPriceIsZeroOrNegative()
    {
        var validator = new PriceValidator();
        var history = new List<CryptoPriceHistory>();
        Assert.False(validator.ShouldSavePrice(0, DateTime.UtcNow, history));
        Assert.False(validator.ShouldSavePrice(-10, DateTime.UtcNow, history));
    }

    [Fact]
    public void ShouldSavePrice_ReturnsFalse_WhenDuplicateForToday()
    {
        var validator = new PriceValidator();
        var today = DateTime.UtcNow.Date;
        var history = new List<CryptoPriceHistory>
        {
            new CryptoPriceHistory { Price = 100, Date = today }
        };
        Assert.False(validator.ShouldSavePrice(100, today, history));
    }

    [Fact]
    public void ShouldSavePrice_ReturnsTrue_WhenValidAndNotDuplicate()
    {
        var validator = new PriceValidator();
        var today = DateTime.UtcNow.Date;
        var history = new List<CryptoPriceHistory>
        {
            new CryptoPriceHistory { Price = 90, Date = today }
        };
        Assert.True(validator.ShouldSavePrice(100, today, history));
    }
}