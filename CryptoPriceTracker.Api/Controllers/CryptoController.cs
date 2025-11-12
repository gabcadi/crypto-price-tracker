using Microsoft.AspNetCore.Mvc;
using CryptoPriceTracker.Api.Services;
using CryptoPriceTracker.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace CryptoPriceTracker.Api.Controllers
{
    [ApiController]
    [Route("api/crypto")]
    public class CryptoController : ControllerBase
    {
        private readonly CryptoPriceService _service;
        private readonly ApplicationDbContext _dbContext;

        // Constructor with dependency injection of the service
        public CryptoController(CryptoPriceService service, ApplicationDbContext dbContext)
        {
            _service = service;
            _dbContext = dbContext;
        }

        /// <summary>
        /// This endpoint triggers a price update by fetching prices from the CoinGecko API
        /// and saving them in the database through the service logic.
        /// </summary>
        /// <returns>200 OK with a confirmation message once done</returns>
        [HttpPost("update-prices")]
        public async Task<IActionResult> UpdatePrices()
        {
            await _service.UpdatePricesAsync();
            return Ok("Prices updated.");
        }

        /// <summary>
        /// This endpoint will allow the frontend to display the most recent data saved in the database.
        /// </summary>
        /// <returns>A list of assets and their latestPrice recorded price</returns>
        [HttpGet("latestPrice-prices")]
        public async Task<IActionResult> GetLatestPrices([FromServices] ApplicationDbContext db)
        {
            var latestPrice = await db.CryptoAssets
                .Select(coin => new
                {
                    coin.Name,
                    coin.Symbol,
                    coin.ExternalId,
                    coin.IconUrl,
                    // We get the latestPrice price from the price history
                    Last = coin.PriceHistory
                        .OrderByDescending(p => p.Date)
                        .FirstOrDefault(),
                    Previous = coin.PriceHistory
                        .OrderByDescending(p => p.Date)
                        .Skip(1)
                        .FirstOrDefault()
                })
                .ToListAsync();

            // The result to include only necessary fields
            var result = latestPrice.Select(a => new
            {
                a.Name,
                a.Symbol,
                a.ExternalId,
                a.IconUrl,
                LatestPrice = a.Last?.Price,
                LastUpdated = a.Last?.Date,
                PreviousPrice = a.Previous?.Price
            });

            return Ok(result);
        }
    }
}