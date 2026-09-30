namespace MoneyBot.Services.CSV
{
    // FNB :
    // negative = money out, flip to our convention
    public class FNB : IBankCSVMapper
    {
        public string BankName => "FNB";

        public (DateOnly, decimal, string) Map(IDictionary<string, string> row) => (
            DateOnly.Parse(row["Date"]),
            -decimal.Parse(row["Amount"]), 
            row["Description"]
        );
    }
}
