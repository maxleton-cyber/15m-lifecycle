using Lifecycle.Api.Configuration;
using Lifecycle.Api.Models;
using Lifecycle.Api.Services;
using Lifecycle.Api.Workers;
using StackExchange.Redis;

EnvFileLoader.LoadFromProjectRoot(Directory.GetCurrentDirectory());

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<BaiduMapSettings>(builder.Configuration.GetSection("BaiduMap"));
builder.Services.Configure<LlmSettings>(builder.Configuration.GetSection("Llm"));
builder.Services.Configure<AgentSettings>(builder.Configuration.GetSection("Agent"));
builder.Services.Configure<RedisSettings>(builder.Configuration.GetSection("Redis"));

var redisSettings = builder.Configuration.GetSection("Redis").Get<RedisSettings>() ?? new RedisSettings();

try
{
    var redis = ConnectionMultiplexer.Connect(redisSettings.ConnectionString);
    builder.Services.AddSingleton<IConnectionMultiplexer>(redis);
}
catch
{
    builder.Services.AddSingleton<IConnectionMultiplexer>(_ => null!);
}

builder.Services.AddSingleton<IBaiduApiRateLimiter, BaiduApiRateLimiter>();
builder.Services.AddSingleton<IRedisCacheService, RedisCacheService>();
builder.Services.AddSingleton<IQuotaGuardService, QuotaGuardService>();
builder.Services.AddSingleton<IAgentTaskQueue, AgentTaskQueue>();
builder.Services.AddSingleton<IAgentTaskStore, AgentTaskStore>();

builder.Services.AddHttpClient<IBaiduMapClient, BaiduMapClient>();
builder.Services.AddHttpClient<IAgentService, AgentService>();

builder.Services.AddSingleton<IIsochroneService, IsochroneService>();
builder.Services.AddSingleton<IPoiCoverageService, PoiCoverageService>();
builder.Services.AddSingleton<IRegionService, RegionService>();

builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));

builder.Services.AddHostedService<AgentTaskWorker>();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
});

var app = builder.Build();

app.UseCors();
app.MapControllers();

app.Run();
