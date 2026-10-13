namespace MoneyBot.Services.CSV
{
    public class CapitecCsvMapper : IBankCSVMapper
    {
        public string BankName => "Capitec";

        public (DateOnly, decimal, string) Map(IDictionary<string, string> row) => (
            DateOnly.Parse(row["Transaction Date"]),
            decimal.Parse(row["Amount"]),
            row["Description"]
        );

    }
}
