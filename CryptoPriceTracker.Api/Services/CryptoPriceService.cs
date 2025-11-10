using System.Text.Json;
using CryptoPriceTracker.Api.Data;
using CryptoPriceTracker.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace CryptoPriceTracker.Api.Services
{
    public class CryptoPriceService
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly HttpClient _httpClient;
        private readonly ILogger<CryptoPriceService> _logger;

        public CryptoPriceService(ApplicationDbContext dbContext, HttpClient httpClient, ILogger<CryptoPriceService> logger)
        {
            _dbContext = dbContext;
            _httpClient = httpClient;
            _logger = logger;
        }

        // This method fetches the latestPrice crypto prices from CoinGecko API and updates the database
        public async Task UpdatePricesAsync()
        {
            var cryptoAssets = await _dbContext.CryptoAssets.ToListAsync();
            if (cryptoAssets.Count == 0) return;

            var cryptoNames = string.Join(",", cryptoAssets.Select(a => a.ExternalId));
            var url = $"https://api.coingecko.com/api/v3/simple/price?ids={cryptoNames}&vs_currencies=usd";

            await UpdateAssetIconsAsync(cryptoAssets, cryptoNames);

            try
            {
                var response = await _httpClient.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("CoinGecko API request failed. StatusCode: {StatusCode}", response.StatusCode);
                    return;
                }

                var json = await response.Content.ReadAsStringAsync();

                // Deserialize the JSON response into a dictionary. Due to the structure is: { "bitcoin": { "usd": 50000 } }
                var prices = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, decimal>>>(json);

                foreach (var coin in cryptoAssets)
                {
                    var today = DateTime.UtcNow;
                    var lastPrice = _dbContext.CryptoPriceHistories
                        .Where(p => p.CryptoAssetId == coin.Id)
                        .OrderByDescending(p => p.Date)
                        .FirstOrDefault();
                    var history = _dbContext.CryptoPriceHistories
                        .Where(p => p.CryptoAssetId == coin.Id)
                        .ToList();

                    // Avoid updating prices if it's already updated today
                    if (lastPrice != null && lastPrice.Date.Date == today)
                    {
                        continue;
                    }

                    // Check if the price for this coin exists in the API response
                    if (prices == null || !prices.TryGetValue(coin.ExternalId, out var priceDict) ||
                        priceDict == null || !priceDict.TryGetValue("usd", out var newPrice))
                    {
                        _logger.LogWarning("No price found for {Asset}.", coin.ExternalId);
                        continue;
                    }

                    // Handle duplicate entries or API errors:
                    var priceValidator = new PriceValidator();
                    // - If the price is found multiple times, or the value is not valid (like being 0 or negative), we log it and skip it
                    if (!priceValidator.ShouldSavePrice(newPrice, today, history))
                    {
                        _logger.LogWarning("Price for {Asset} is invalid or already saved for today.", coin.ExternalId);
                        continue; //Skip invalid prices to avoid saving incorrect data
                    }

                    var priceHistory = new CryptoPriceHistory
                    {
                        CryptoAssetId = coin.Id,
                        Price = newPrice,
                        Date = DateTime.UtcNow
                    };
                    _dbContext.CryptoPriceHistories.Add(priceHistory);
                }

                await _dbContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating crypto prices from CoinGecko.");
            }
        }

        private async Task UpdateAssetIconsAsync(List<CryptoAsset> cryptoAssets, string cryptoNames)
        {
            var marketsUrl = $"https://api.coingecko.com/api/v3/coins/markets?vs_currency=usd&ids={cryptoNames}";

            try
            {
                // User-Agent header is required to avoid 403 responses
                _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("CryptoPriceTracker/1.0 (+https://gabcadi.dev)");
                
                var marketsResponse = await _httpClient.GetAsync(marketsUrl);
                if (marketsResponse.IsSuccessStatusCode)
                {
                    var marketsJson = await marketsResponse.Content.ReadAsStringAsync();
                    var marketData = JsonSerializer.Deserialize<List<CoinGeckoMarketDto>>(marketsJson);

                    foreach (var coin in cryptoAssets)
                    {
                        var market = marketData?.FirstOrDefault(m => m.id == coin.ExternalId);
                        if (market != null && !string.IsNullOrEmpty(market.image) && coin.IconUrl != market.image)
                        {
                            coin.IconUrl = market.image;
                            _dbContext.CryptoAssets.Update(coin);
                        }
                    }
                    await _dbContext.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not update icons from CoinGecko.");
            }
        }

        private class CoinGeckoMarketDto
        {
            public string id { get; set; }
            public string image { get; set; }
        }
    }
}