using Microsoft.EntityFrameworkCore;

namespace MoneyBot.Data
{
    public class AppDbContext : DbContext 
    {
        public DbSet<Item> Items => Set<Item>();
        public DbSet<Transaction> Transactions => Set<Transaction>();






    }
}
