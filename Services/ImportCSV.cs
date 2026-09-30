using MoneyBot.Data;
using MoneyBot.Services.CSV;
using System.Globalization;

namespace MoneyBot.Services
{
    public class ImportCSV
    {
        private readonly AppDbContext _db;
        private readonly Dictionary<string, IBankCSVMapper> _mappers;



        public ImportCSV(AppDbContext db , IEnumerable<IBankCSVMapper> mappers>)
        {
            _db = db;
            _mappers = mappers.ToDictionary(
                m => m.BankName,
                StringComparer.OrdinalIgnoreCase); 

        }

        public async Task<int> ImportAsync(string userId, string bankName, Stream csvStream)
        {
            if (!_mappers.TryGetValue(bankName, out var mapper))
                throw new ArgumentException($"No CSV mapper registered for '{bankName}'");

            using var reader = new StreamReader(csvStream);
            using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture));

            csv.Read();
            csv.ReadHeader();
            int imported = 0;

            while (csv.Read())
            {
                var row = csv.HeaderRecord!.ToDictionary(h => h,
                    h => csv.GetField(h) ?? "");

                var (date, amount, description) = mapper.Map(row);

                var transactionId = $"csv-{userId}-{date}-{description}-{amount}".GetHashCode().ToString();
                var existing = await _db.Transactions.FindAsync(transactionId);
                var txn = existing ?? new Transaction { TransactionId = transactionId };

                txn.UserId = userId;
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

            await _db.SaveChangesAsync();
            return imported;


        } 
}
