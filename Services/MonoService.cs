using Microsoft.AspNetCore.DataProtection;
using MoneyBot.Data;
using System.Net.Http.Headers;
using System.Text.Json;

namespace MoneyBot.Services
{
    public class MonoService
    {
        private readonly HttpClient _http;
        private readonly AppDbContext _db;
        private readonly IDataProtector _protector;


        private const string BaseUrl = "https://api.withmono.com/v2";




        public MonoService(HttpClient http, AppDbContext db, IDataProtectionProvider dataProtection, IConfiguration config)
        {
            _http = http;
            _db = db;
            _protector = dataProtection.CreateProtector("MonoAccountId");
            _http.DefaultRequestHeaders.Add("mono-sec-key", config["Mono:SecretKey"]);
            _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        /// <summary>
        /// Pulls transactions for every bank this user has connected and
        /// upserts them. Respects IsCategoryManual so a user's correction
        /// isn't overwritten by the keyword guess on the next sync.
        /// </summary>
        public async Task SyncTransactionsAsync(string userId)
        {
            var items = await _db.Items.Where(i => i.UserId == userId).ToListAsync();

            foreach (var item in items)
            {
                var accountId = _protector.Unprotect(item.MonoAccountIdEnc);

                var response = await _http.GetAsync($"{BaseUrl}/accounts/{accountId}/transactions");
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadFromJsonAsync<JsonDocument>();
                var transactions = json!.RootElement.GetProperty("data").EnumerateArray();

                foreach (var t in transactions)
                {
                    var txnId = t.GetProperty("id").GetString()!;
                    var existing = await _db.Transactions.FindAsync(txnId);
                    var row = existing ?? new Transaction { TransactionId = txnId };

                    row.UserId = userId;
                    row.Date = DateOnly.Parse(t.GetProperty("date").GetString()!);
                    row.Amount = t.GetProperty("amount").GetDecimal();
                    row.Merchant = t.GetProperty("narration").GetString();
                    row.Source = "mono";

                    if (t.GetProperty("type").GetString() == "credit")
                        row.Amount = -row.Amount;

                    if (!row.IsCategoryManual)
                        row.Category = TransactionCategorizer.Categorize(row.Merchant ?? "");

                    if (existing == null)
                        _db.Transactions.Add(row);
                }
            }

            await _db.SaveChangesAsync();
        }



/// <summary>
/// Trades the short-lived code from React's Mono Connect widget for a
/// permanent account ID, saves it as a new Item, then runs the first sync.
/// </summary>
public async Task ExchangeCodeAsync(string userId, string code)
        {
            var response = await _http.PostAsJsonAsync($"{BaseUrl}/accounts/auth", new { code });
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadFromJsonAsync<JsonDocument>();
            var accountId = json!.RootElement.GetProperty("id").GetString()!;

            _db.Items.Add(new Item
            {
                UserId = userId,
                MonoAccountIdEnc = _protector.Protect(accountId),
                BankName = "Unknown",
            });

            await _db.SaveChangesAsync();
            await SyncTransactionsAsync(userId);
        } 







    }
}
