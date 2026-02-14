using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();

var app = builder.Build();

RateLimiter limiter = new TokenBucketRateLimiter(
    new ConcurrencyLimiterOptions(permitLimit: 2, queueProcessingOrder: QueueProcessingOrder.OldestFirst, queueLimit: 2));

// thread 1:
using RateLimitLease lease1 = limiter.AttemptAcquire(permitCount: 2);
if (lease.IsAcquired) { }

// thread 2:
using RateLimitLease lease2 = await limiter.AttemptAcquire(permitCount: 2);
if (lease.IsAcquired) { }




// Configure the HTTP request pipeline.

app.UseAuthorization();

app.MapControllers();

app.Run();
