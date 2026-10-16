using Going.Plaid;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MoneyBot.Data;
using MoneyBot.Models;
using MoneyBot.Services;
using MoneyBot.Services.CSV;
using Supabase.Extensions.DependencyInjection;
using PlaidEnv = Going.Plaid.Environment;

DotNetEnv.Env.Load();

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection")
        ?? System.Environment.GetEnvironmentVariable("DB_CONNECTION_STRING"),
        npgsqlOptions =>
        {
            npgsqlOptions.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(10),
                errorCodesToAdd: null);
        }));

builder.Services.AddDataProtection();

// Register framework services
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

// Configure PlaidOptions with Environment, ClientId, and Secret
var plaidEnvStr = (System.Environment.GetEnvironmentVariable("PLAID_ENV")
    ?? builder.Configuration["Plaid:Environment"])?.Trim().ToLowerInvariant();

var targetPlaidEnv = plaidEnvStr switch
{
    "production" => PlaidEnv.Production,
    "development" => PlaidEnv.Development,
    _ => PlaidEnv.Sandbox
};

builder.Services.Configure<PlaidOptions>(options =>
{
    options.Environment = targetPlaidEnv;
    options.ClientId = System.Environment.GetEnvironmentVariable("PLAID_CLIENT_ID")
        ?? builder.Configuration["Plaid:ClientId"]
        ?? string.Empty;
    options.Secret = System.Environment.GetEnvironmentVariable("PLAID_SECRET")
        ?? builder.Configuration["Plaid:Secret"]
        ?? string.Empty;
});

// PlaidClient consumes IOptions<PlaidOptions> directly
builder.Services.AddSingleton<PlaidClient>();

builder.Services.AddScoped<PlaidService>();

// Safely register Supabase Client if URL and Key are provided
var supabaseUrl = builder.Configuration["Supabase:Url"];
var supabaseKey = builder.Configuration["Supabase:Key"];
if (!string.IsNullOrWhiteSpace(supabaseUrl) && !string.IsNullOrWhiteSpace(supabaseKey))
{
    builder.Services.AddSupabase(supabaseUrl, supabaseKey);
}

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