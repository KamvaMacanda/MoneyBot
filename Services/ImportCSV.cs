using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.Extensions.Logging;
using MoneyBot.Data;
using MoneyBot.Models;
using MoneyBot.Services.CSV;
using System.Globalization;

namespace MoneyBot.Services
{
    public class ImportCSV
    {
        private readonly AppDbContext _db;
        private readonly Dictionary<string, IBankCSVMapper> _mappers;
        private readonly ILogger<ImportCSV> _logger;

        public ImportCSV(AppDbContext db, IEnumerable<IBankCSVMapper> mappers, ILogger<ImportCSV> logger)
        {
            _db = db;
            _mappers = mappers.ToDictionary(m => m.BankName, StringComparer.OrdinalIgnoreCase);
            _logger = logger;
        }

        public async Task<int> ImportAsync(string userId, string bankName, Stream csvStream)
        {
            if (!_mappers.TryGetValue(bankName, out var mapper))
                throw new ArgumentException($"No CSV mapper registered for '{bankName}'");

            using var reader = new StreamReader(csvStream);
            using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture));

            try
            {
                csv.Read();
                csv.ReadHeader();
            }
            catch (CsvHelperException ex)
            {
                _logger.LogWarning(ex, "Could not read CSV header for bank {Bank}", bankName);
                throw new InvalidOperationException($"'{bankName}' file doesn't look like a valid CSV — check the format.");
            }

            int imported = 0;
            int rowNumber = 1;

            while (csv.Read())
            {
                rowNumber++;
                try
                {
                    var row = csv.HeaderRecord!.ToDictionary(h => h, h => csv.GetField(h) ?? "");
                    var (date, amount, description) = mapper.Map(row);

                    var transactionId = $"csv-{userId}-{date}-{description}-{amount}".GetHashCode().ToString();
                    var existing = await _db.Transactions.FindAsync(transactionId);
                    var txn = existing ?? new Transaction { TransactionId = transactionId };

                    txn.UserID = userId;
                    txn.Date = date;
                    txn.Amount = amount;
                    txn.Merchant = description;
                    txn.Source = "csv";

                    if (!txn.IsCategoryManual)
                        txn.Category = TransactionCategorizer.Categorize(description);

                    if (existing == null)
                    {
                        _db.Transactions.Add(txn);
                        imported++;
                    }
                }
                catch (Exception ex) when (ex is FormatException or KeyNotFoundException)
                {
                    // Skip a bad row instead of failing the whole import —
                    // log it so the user can see which rows didn't import.
                    _logger.LogWarning(ex, "Skipped row {Row} in {Bank} import for user {UserId}", rowNumber, bankName, userId);
                }
            }

            await _db.SaveChangesAsync();
            _logger.LogInformation("Imported {Count} transactions from {Bank} for user {UserId}", imported, bankName, userId);
            return imported;
        }
    }
}