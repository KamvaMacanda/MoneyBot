using Going.Plaid;
using Going.Plaid.Entity;       
using Going.Plaid.Link;
using Going.Plaid.Transactions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MoneyBot.Data;
using MoneyBot.Models;
using MoneyBot.Services.CSV;
using PlaidTransaction = Going.Plaid.Entity.Transaction;
using AppTransaction = MoneyBot.Models.Transaction;

namespace MoneyBot.Services
{
    public class PlaidService
    {
        ///<summary>
        /// Wraps interactions with Plaid: creating link tokens, exchanging public tokens,
        /// and syncing transactions for later analysis.
        ///</summary>

        private readonly PlaidClient _plaid;
        private readonly AppDbContext _db;
        private readonly IDataProtector _protector;
        private readonly ILogger<PlaidService> _logger;

        public PlaidService(
            PlaidClient plaid, AppDbContext db,
            IDataProtectionProvider dataProtection, ILogger<PlaidService> logger)
        {
            _plaid = plaid;
            _db = db;
            _protector = dataProtection.CreateProtector("PlaidAccessToken");
            _logger = logger;
        }

        public async Task<string> CreateLinkTokenAsync(string userId)
        {
            try
            {
                var response = await _plaid.LinkTokenCreateAsync(new LinkTokenCreateRequest
                {
                    User = new LinkTokenCreateRequestUser { ClientUserId = userId },
                    ClientName = "MoneyBot",
                    Products = new[] { Products.Transactions },
                    CountryCodes = new[] { CountryCode.Us },
                    Language = Language.English,
                });
                return response.LinkToken;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Plaid link-token request failed for user {UserId}", userId);
                throw new InvalidOperationException("Couldn't start the bank connection. Please try again shortly.");
            }
        }

        public async Task ExchangePublicTokenAsync(string userId, string publicToken)
        {
            try
            {
                var exchange = await _plaid.ItemPublicTokenExchangeAsync(
                    new Going.Plaid.Item.ItemPublicTokenExchangeRequest { PublicToken = publicToken });

                // Explicitly use the DB model type to avoid ambiguity with Going.Plaid.Entity.Item
                _db.Items.Add(new MoneyBot.Models.Item
                {
                    UserId = userId,
                    PlaidAccessTokenEnc = _protector.Protect(exchange.AccessToken),
                    Cursor = null,
                    BankName = "Unknown",
                });

                await _db.SaveChangesAsync();
                await SyncTransactionsAsync(userId);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Plaid token exchange failed for user {UserId}", userId);
                throw new InvalidOperationException("Couldn't finish connecting that bank. Please try linking again.");
            }
        }

        public async Task SyncTransactionsAsync(string userId)
        {
            var items = await _db.Items.Where(i => i.UserId == userId).ToListAsync();

            foreach (var item in items)
            {
                try
                {
                    var accessToken = _protector.Unprotect(item.PlaidAccessTokenEnc);
                    bool hasMore = true;
                    string? cursor = item.Cursor;

                    while (hasMore)
                    {
                        var response = await _plaid.TransactionsSyncAsync(new TransactionsSyncRequest
                        {
                            AccessToken = accessToken,
                            Cursor = cursor,
                        });

                        foreach (var t in response.Added.Concat(response.Modified))
                            UpsertTransaction(userId, t);

                        foreach (var removed in response.Removed)
                        {
                            var existing = await _db.Transactions.FindAsync(removed.TransactionId);
                            if (existing != null)
                                _db.Transactions.Remove(existing);
                        }

                        cursor = response.NextCursor;
                        hasMore = response.HasMore;
                    }

                    item.Cursor = cursor;
                }
                catch (Exception ex) when (ex is HttpRequestException or System.Security.Cryptography.CryptographicException)
                {
                    // One bank failing to sync shouldn't stop the others, just log and continue.
                    _logger.LogError(ex, "Sync failed for item {ItemId}, user {UserId}", item.Id, userId);
                }
            }

            await _db.SaveChangesAsync();
        }

        private void UpsertTransaction(string userId, PlaidTransaction t)
        {
            var existing = _db.Transactions.Find(t.TransactionId!);
            var row = existing ?? new AppTransaction { TransactionId = t.TransactionId! };

            row.UserID = userId;
            row.Date = t.Date ?? DateOnly.FromDateTime(DateTime.UtcNow);
            row.Amount = t.Amount ?? 0m;

            // MerchantName is the cleaned name (may be null); OriginalDescription is the raw bank text.
            // Name is obsolete, so it's no longer used.
            row.Merchant = t.MerchantName ?? t.OriginalDescription ?? "Unknown";

            row.Pending = t.Pending ?? false;
            row.Source = "plaid";

            if (!row.IsCategoryManual)
            {
                row.Category = t.PersonalFinanceCategory?.Primary
                               ?? TransactionCategorizer.Categorize(row.Merchant);
            }

            if (existing == null)
                _db.Transactions.Add(row);
        }
    }
}