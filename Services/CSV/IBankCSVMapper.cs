namespace MoneyBot.Services.CSV
{
    public interface IBankCSVMapper
    {

        string BankName { get; }
        (DateOnly Date, decimal Amount, string Description) Map(IDictionary<string, string> row);
    }
}
