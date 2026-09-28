using Going.Plaid;
using Going.Plaid.Entity;
using MoneyBot.Data;
using Microsoft.EntityFrameworkCore;
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

        private readonly AppDBContext _db; 

        public PlaidService (PlaidClient plaid , AppDBContext db)
        {
        
            _db = db;
            _plaid = plaid;

        }
      


        public async Task <string> CreateLinkTokenAsync (int UserID)
        {
            var response = await _plaid.LinkTokenCreateAsync(new LinkTokenCreateRequest
            {
                User = new LinkTokenCreateRequestUser { ClientUserIdentity = UserID.ToString() },
                ClientName = "MoneyBot",
                Products = new[] { Products.Transactions },
                CountryCode = new[] { CountryCode.Us },
                Language = "en",

            });

            return response.LinkToken;   
         }



        /// <summary>
        ///  Runs once, right after the user finishes the Plaid Link flow in the browser.
        /// Swaps the short-lived public token for a permanent access token, saves a new
     
        /// </summary>

        public async Task ExchangePublicTokenAsync (int UserID , string publicToken)
        {
            var exchange = await _plaid.ItemPublicTokenExchangeAsync(
                new Going.Plaid.Item.ItemPublicTokenExchangeRequest
                {
                    PublicToken = publicToken
                });


            _db.Items.Add(new Item
            {
                UserId = UserID,
                PlaidAccessTokenEnc = Encrypt(exchange.AccessToken),
                Cursor = null,
                BankName = "Unknown",
            });

            await _db.SaveChangesAsync();

            await SyncTransactionsAsync(UserID);
        }

        /// <summary>
        /// Pulls new, changed, and removed transactions for every bank account (Item)
        /// this user has connected, using Plaid's cursor-based sync endpoint so only
        /// what's changed since last time is fetched — not the full history every call.
        /// Called after linking a new account, on a manual "refresh" from the UI,
        /// and (ideally) from a Plaid webhook when new transactions land.
        /// </summary>

        public async Task SyncTransactionAsync(int UserID)
        {
            var items = await _db.Items.Where(i => i.UserId == UserID).ToListAsync();

            foreach (var item in items)
            {
                var accessToken = Decrypt(item.PlaidAcessTokenEnc);
                bool hasMore = true;
                string? cursor = item.Cursor;


                while (hasMore)
                {
                    var response = await _plaid.TransactionsSyncAsync(new TransactionsSyncRequest
                    {
                        AcessToken = accessToken,
                        Cursor = cursor,
                    });

                    foreach (var t in response.Added.Concat(response.Modified))
                        UpsertTransaction(UserID, t);  

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
            var existing = _db.Transactions.Find(t.TransactionId)
            var row = existing ?? new Models.Transaction { TransactionId = t.TransactionId };

            row.userID = UserID;
            row.Date = DateOnly.FromDateTime(t.Date);
            row.Amount = t.Amount ?? 0m;
            row.Merchant = t.MerchantName ?? t.Name;
            row.Category = t.PersonalFinanceCategory?.Primary;
            row.Pending = t.Pending ?? false;
            row.Source = "plaid";

            if (existing == null)
                _db.Transactions.Add(row); 
        }

        private string Encrpyt(string value) => value;
        private string Decrypt(string value) => value;



    }
}
