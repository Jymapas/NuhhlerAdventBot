using System.Threading.RateLimiting;
using Application.Abstractions;
using Application.Validation;
using FluentValidation;
using Infrastructure.Persistence;
using Infrastructure.Repositories;
using Infrastructure.Telegram;
using Microsoft.AspNetCore.RateLimiting;
using Serilog;
using WebApi.Background;
using WebApi.Endpoints;
using WebApi.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Serilog
Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File(
        path: builder.Configuration["LOG_PATH"] ?? "./logs/log-.ndjson",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 7,
        shared: true)
    .CreateLogger();
builder.Host.UseSerilog();

// CORS
var allowed = (builder.Configuration["ALLOWED_ORIGINS"] ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.WithOrigins(allowed).AllowAnyMethod().AllowAnyHeader()));

// EF Core Sqlite
builder.Services.AddSqlite(builder.Configuration);

// DI
builder.Services.AddScoped<INoteRepository, NoteRepository>();
builder.Services.AddSingleton<ITelegramAuth, TelegramAuth>();
builder.Services.AddValidatorsFromAssemblyContaining<NoteInputValidator>();

// Swagger (dev)
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Rate limit
builder.Services.AddRateLimiter(o =>
{
    o.AddPolicy("default", http => RateLimitPartition.GetFixedWindowLimiter("all",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 60, Window = TimeSpan.FromMinutes(1) }));
    o.AddPolicy("post-strict", http => RateLimitPartition.GetFixedWindowLimiter("post",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) }));
});

// BackgroundService
builder.Services.AddHostedService<StartupWarmupService>();

var app = builder.Build();

app.UseSerilogRequestLogging();
app.UseMiddleware<RequestIdMiddleware>();
app.UseCors();
app.UseRateLimiter();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapHealth();
app.MapTextPad();

app.Run();
