using Microsoft.EntityFrameworkCore;

namespace MoneyBot.Data
{
    public class AppDbContext : DbContext 
    {
        public AppDbContext (DbContextOptions<AppDbContext>options) : base(options) { }


        public DbSet <Item> Items => Set <Item>();
        public DbSet< Transaction> Transactions =>  Set< Transactions>(); 
    
    
    
    }
}
