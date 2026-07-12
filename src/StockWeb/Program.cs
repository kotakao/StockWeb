using StockWeb.Api;
using StockWeb.Components;
using StockWeb.Data;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// 頁面透過 HttpClient 呼叫自身 API，打通 SQLite→Dapper→API→Blazor 全鏈路。
builder.Services.AddHttpClient();

// 資料層：連線工廠（DbPath 來自 appsettings.json）與 coverage 查詢。
var dbPath = builder.Configuration["DbPath"]
    ?? throw new InvalidOperationException("appsettings.json 缺少 DbPath 設定。");
builder.Services.AddSingleton<IDbConnectionFactory>(new SqliteConnectionFactory(dbPath));
builder.Services.AddScoped<ICoverageRepository, CoverageRepository>();
builder.Services.AddScoped<IMarketRepository, MarketRepository>();
builder.Services.AddScoped<IScreenerRepository, ScreenerRepository>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

app.UseStaticFiles();
app.UseAntiforgery();

app.MapCoverageEndpoints();
app.MapMarketEndpoints();
app.MapScreenerEndpoints();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
