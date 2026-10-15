using Going.Plaid;
using Microsoft.EntityFrameworkCore;
using MoneyBot.Data;
using MoneyBot.Models;
using MoneyBot.Services;
using MoneyBot.Services.CSV;


DotNetEnv.Env.Load(); 

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddDataProtection();

// Register framework services required by the middleware you use
builder.Services.AddRazorPages(options =>
{
    options.RootDirectory = "/Views";
}); 

builder.Services.AddControllers(); 
builder.Services.AddAuthorization(); 

builder.Services.AddScoped<ChatBotLogic>(); 

builder.Services.AddScoped<ImportCSV>();

builder.Services.AddSingleton<IBankCSVMapper, FnbCsvMapper>();

builder.Services.AddSingleton<IBankCSVMapper, CapitecCsvMapper>();

var plaidEnv = builder.Configuration["Plaid:Environment"] switch
{
    "Production" => Going.Plaid.Environment.Production,
    _ => Going.Plaid.Environment.Sandbox
};
builder.Services.AddSingleton(sp => new PlaidClient(plaidEnv));

builder.Services.AddScoped<PlaidService>(); 

var app = builder.Build();

app.UseExceptionHandler(errApp =>
{
    errApp.Run(async context =>
    {
        context.Response.StatusCode = 500;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new { error = "Something went wrong. Please try again." });
    });
}); 


 


// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi(); 

}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseAuthorization();


app.MapRazorPages();
app.MapControllers();

app.Run();
