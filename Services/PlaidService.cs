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


                if (!response.IsSuccessStatusCode)
                    _logger.LogError("Plaid error: {Code} {Message}", response.Error?.ErrorCode, response.Error?.ErrorMessage);

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

                if (!exchange.IsSuccessStatusCode || string.IsNullOrEmpty(exchange.AccessToken))
                {
                    LogPlaidError("public token exchange", exchange.Error, userId);
                    throw new InvalidOperationException("Couldn't finish connecting that bank. Please try linking again.");
                }


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

                    // Call Plaid API: /accounts/balance/get
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
                        // Extract current or available balance directly from Plaid telemetry
                        var rawBalance = acc.Balances.Current ?? acc.Balances.Available ?? 0m;
                        var isCredit = acc.Type == AccountType.Credit;
                        var currency = acc.Balances.IsoCurrencyCode ?? "USD";

                        // Currency conversion , convert USD to South African Rand (ZAR)
                        decimal balanceInZar;
                        if (string.Equals(currency, "USD", StringComparison.OrdinalIgnoreCase))
                        {
                            balanceInZar = rawBalance * usdToZarExchangeRate;
                        }
                        else
                        {
                            // If Plaid already reports in ZAR or another currency, use as-is or adjust rate
                            balanceInZar = rawBalance;
                        }

                        // For credit cards, balance represents an outflow/debt
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

                    // Only advance the cursor if the whole sync succeeded, so a failed
                    // run is retried from the last good point next time.
                    if (!failed)
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