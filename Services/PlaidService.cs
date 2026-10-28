using Going.Plaid;
using Going.Plaid.Accounts;
using Going.Plaid.Entity;       
using Going.Plaid.Link;
using Going.Plaid.Transactions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MoneyBot.Data;
using MoneyBot.Models;
using MoneyBot.Services.CSV;
using AppTransaction = MoneyBot.Models.Transaction;
using PlaidTransaction = Going.Plaid.Entity.Transaction;

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

        private void LogPlaidError(string operation, PlaidError? error, string userId)
        {
            _logger.LogError(
                "Plaid {Operation} failed for user {UserId}: {ErrorCode} - {ErrorMessage}",
                operation, userId, error?.ErrorCode, error?.ErrorMessage);
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

                if (!response.IsSuccessStatusCode || string.IsNullOrEmpty(response.LinkToken))
                {
                    LogPlaidError("link token create", response.Error, userId);
                    throw new InvalidOperationException(response.Error?.ErrorMessage ?? "Plaid rejected link token creation.");
                }

                return response.LinkToken;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Plaid link-token request failed for user {UserId}", userId);
                throw new InvalidOperationException("Couldn't start the bank connection. Please verify your Plaid credentials and network.");
            }
        }

        public async Task ExchangePublicTokenAsync(string userId, string publicToken, string? institutionName = null)
        {
            try
            {
                var exchange = await _plaid.ItemPublicTokenExchangeAsync(
                    new Going.Plaid.Item.ItemPublicTokenExchangeRequest { PublicToken = publicToken });

                if (!exchange.IsSuccessStatusCode || string.IsNullOrEmpty(exchange.AccessToken))
                {
                    LogPlaidError("public token exchange", exchange.Error, userId);
                    var msg = exchange.Error?.ErrorMessage ?? "Plaid rejected public token exchange.";
                    throw new InvalidOperationException(msg);
                }

                var bankTitle = !string.IsNullOrWhiteSpace(institutionName) ? institutionName : "Connected Bank";

                // Save or update existing bank item
                var existingItem = await _db.Items.FirstOrDefaultAsync(i => i.UserId == userId && i.BankName == bankTitle);
                if (existingItem != null)
                {
                    existingItem.PlaidAccessTokenEnc = _protector.Protect(exchange.AccessToken);
                    existingItem.Cursor = null; // reset sync cursor on re-link
                }
                else
                {
                    _db.Items.Add(new MoneyBot.Models.Item
                    {
                        UserId = userId,
                        PlaidAccessTokenEnc = _protector.Protect(exchange.AccessToken),
                        Cursor = null,
                        BankName = bankTitle,
                    });
                }

                await _db.SaveChangesAsync();

                // Sync transactions immediately after exchange, but do not fail exchange if sync experiences minor initial delay
                try
                {
                    await SyncTransactionsAsync(userId);
                }
                catch (Exception syncEx)
                {
                    _logger.LogWarning(syncEx, "Initial sync for user {UserId} experienced an issue, but token exchange succeeded.", userId);
                }
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Plaid token exchange HTTP request failed for user {UserId}", userId);
                throw new InvalidOperationException("Network error connecting to Plaid. Please try again.");
            }
        }

        /// <summary>
        /// Fetches real, live account balances directly from Plaid's /accounts/balance/get endpoint
        /// for every bank connected by the user, and converts balances from USD to South African Rand (ZAR).
        /// </summary>
        public async Task<List<AccountDetail>> GetLiveAccountBalancesAsync(string userId, decimal usdToZarExchangeRate = 18.25m)
        {
            var results = new List<AccountDetail>();

            // Retrieve all connected bank items for this user from PostgreSQL
            var items = await _db.Items.Where(i => i.UserId == userId).ToListAsync();

            foreach (var item in items)
            {
                try
                {
                    // Decrypt the stored access token using ASP.NET Core Data Protection
                    var accessToken = _protector.Unprotect(item.PlaidAccessTokenEnc);

                    var balanceResponse = await _plaid.AccountsBalanceGetAsync(new AccountsBalanceGetRequest
                    {
                        AccessToken = accessToken
                    });

                    if (!balanceResponse.IsSuccessStatusCode || balanceResponse.Accounts == null)
                    {
                        LogPlaidError("accounts balance get", balanceResponse.Error, userId);
                        continue;
                    }

                    // Process each live account returned by Plaid
                    foreach (var acc in balanceResponse.Accounts)
                    {
                        var rawBalance = acc.Balances.Current ?? acc.Balances.Available ?? 0m;
                        var isCredit = acc.Type == AccountType.Credit;
                        var currency = acc.Balances.IsoCurrencyCode ?? "USD";

                        decimal balanceInZar = string.Equals(currency, "USD", StringComparison.OrdinalIgnoreCase)
                            ? rawBalance * usdToZarExchangeRate
                            : rawBalance;

                        if (isCredit)
                        {
                            balanceInZar = -Math.Abs(balanceInZar);
                        }

                        results.Add(new AccountDetail
                        {
                            AccountId = acc.AccountId,
                            Name = acc.Name,
                            Mask = !string.IsNullOrWhiteSpace(acc.Mask) ? $"•••• {acc.Mask}" : "••••",
                            Institution = !string.IsNullOrWhiteSpace(item.BankName) ? item.BankName : "Connected Bank",
                            Balance = rawBalance,
                            RandBalance = balanceInZar,
                            ConvertedBalance = balanceInZar,
                            Currency = "ZAR",
                            IsPositive = !isCredit && balanceInZar >= 0,
                            IsNegative = isCredit || balanceInZar < 0
                        });
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to fetch live Plaid balance for item {ItemId}, user {UserId}", item.Id, userId);
                }
            }

            return results;
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
                    bool failed = false;
                    string? cursor = item.Cursor;

                    while (hasMore)
                    {
                        var response = await _plaid.TransactionsSyncAsync(new TransactionsSyncRequest
                        {
                            AccessToken = accessToken,
                            Cursor = cursor,
                        });

                        if (!response.IsSuccessStatusCode)
                        {
                            LogPlaidError("transaction sync", response.Error, userId);
                            failed = true;
                            break;
                        }

                        var incomingTxns = response.Added.Concat(response.Modified).ToList();
                        var incomingIds = incomingTxns
                            .Where(t => !string.IsNullOrEmpty(t.TransactionId))
                            .Select(t => t.TransactionId!)
                            .Distinct()
                            .ToList();

                        // Batch query existing records in 1 trip instead of N individual queries
                        var existingMap = new Dictionary<string, AppTransaction>();
                        if (incomingIds.Count > 0)
                        {
                            var existingRows = await _db.Transactions
                                .Where(t => incomingIds.Contains(t.TransactionId))
                                .ToListAsync();
                            existingMap = existingRows.ToDictionary(t => t.TransactionId);
                        }

                        foreach (var t in incomingTxns)
                        {
                            if (string.IsNullOrEmpty(t.TransactionId)) continue;
                            existingMap.TryGetValue(t.TransactionId, out var existingRow);
                            UpsertTransactionBatched(userId, t, existingRow);
                        }

                        var removedIds = response.Removed
                            .Where(r => !string.IsNullOrEmpty(r.TransactionId))
                            .Select(r => r.TransactionId!)
                            .ToList();

                        if (removedIds.Count > 0)
                        {
                            var toRemove = await _db.Transactions
                                .Where(t => removedIds.Contains(t.TransactionId))
                                .ToListAsync();
                            _db.Transactions.RemoveRange(toRemove);
                        }

                        cursor = response.NextCursor;
                        hasMore = response.HasMore;
                    }

                    if (!failed)
                        item.Cursor = cursor;
                }
                catch (Exception ex) when (ex is HttpRequestException or System.Security.Cryptography.CryptographicException)
                {
                    _logger.LogError(ex, "Sync failed for item {ItemId}, user {UserId}", item.Id, userId);
                }
            }

            await _db.SaveChangesAsync();
        }

        private void UpsertTransactionBatched(string userId, PlaidTransaction t, AppTransaction? existing)
        {
            var row = existing ?? new AppTransaction { TransactionId = t.TransactionId! };

            row.UserID = userId;
            row.Date = t.Date ?? DateOnly.FromDateTime(DateTime.UtcNow);
            row.Amount = t.Amount ?? 0m;

            row.Merchant = t.MerchantName ?? t.OriginalDescription ?? "Unknown";
            row.Pending = t.Pending ?? false;
            row.Source = "plaid";

            if (!row.IsCategoryManual)
            {
                row.Category = t.PersonalFinanceCategory?.Primary
                               ?? TransactionCategorizer.Categorize(row.Merchant);
            }

            if (existing == null)
            {
                _db.Transactions.Add(row);
            }
        }
    }
}
