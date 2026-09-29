using Going.Plaid;
using Going.Plaid.Entity;
using Going.Plaid.Link;
using Going.Plaid.Transactions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using MoneyBot.Data;
using System.Globalization;

namespace MoneyBot.Services
{


    public class PlaidService
    {
        /// <summary>
        /// Wraps all interaction with the Plaid API: creating Link tokens, exchanging
        /// public tokens for access tokens, and syncing transactions into Postgres
       ///This class interacts with plaid directly 
        /// </summary>

        private readonly PlaidClient _plaid;

        private readonly AppDbContext _db;

        private readonly IDataProtector _protector;
        public PlaidService (PlaidClient plaid , AppDbContext db, IDataProtectionProvider dataProtection)
        {
        
            _db = db;
            _plaid = plaid;
            _protector = dataProtection.CreateProtector("PlaidAccessToken");
        }


        public async Task<string> CreateLinkTokenAsync(string userId)
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



        /// <summary>
        ///  Runs once, right after the user finishes the Plaid Link flow in the browser.
        /// Swaps the short-lived public token for a permanent access token, saves a new

        /// </summary>

        public async Task ExchangePublicTokenAsync(string userId, string publicToken)
        {
            var exchange = await _plaid.ItemPublicTokenExchangeAsync(
                new Going.Plaid.Item.ItemPublicTokenExchangeRequest
                {
                    PublicToken = publicToken
                });

            _db.Items.Add(new Item
            {
                UserId = userId,
                PlaidAccessTokenEnc = _protector.Protect(exchange.AccessToken),
                Cursor = null,
                BankName = "Unknown",
            });

            await _db.SaveChangesAsync();

            await SyncTransactionsAsync(userId);
        }

        /// <summary>
        /// Pulls new, changed, and removed transactions for every bank account (Item)
        /// this user has connected, using Plaid's cursor-based sync endpoint so only
        /// what's changed since last time is fetched — not the full history every call.
        /// Called after linking a new account, on a manual "refresh" from the UI,
        /// and (ideally) from a Plaid webhook when new transactions land.
        /// </summary>


        public async Task SyncTransactionsAsync(string userId)
        {
            var items = await _db.Items.Where(i => i.UserId == userId).ToListAsync();

            foreach (var item in items)
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
                        await UpsertTransaction(userId, t);

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

            await _db.SaveChangesAsync();
        }



        private void UpsertTransaction( int UserID , Going.Plaid.Entity.Transaction t)
        {
            var existing = _db.Transactions.Find(t.TransactionId);
            var row = existing ?? new Models.Transaction { TransactionId = t.TransactionId };

            row.userID = UserID;
            row.Date = t.Date;
            row.Amount = t.Amount ?? 0m;
            row.Merchant = t.MerchantName ?? t.Name;
            row.Category = t.PersonalFinanceCategory?.Primary;
            row.Pending = t.Pending ?? false;
            row.Source = "plaid"; 

            if (existing == null)
                _db.Transactions.Add(row); 
        }

    }
}
