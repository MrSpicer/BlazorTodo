using System.Threading.RateLimiting;
using Blazored.LocalStorage;
using Microsoft.AspNetCore.RateLimiting;
using TodoList.Components;
using TodoList.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
	.AddInteractiveServerComponents();

builder.Services.AddHsts(options =>
{
	options.MaxAge = TimeSpan.FromDays(365);
});

// Third party
builder.Services.AddBlazoredLocalStorage();

// Application services
builder.Services.AddMemoryCache(options =>
{
	options.SizeLimit = 200;
});
builder.Services.AddTodoServices();

var rateLimitConfig = builder.Configuration.GetSection("RateLimiting");

builder.Services.AddRateLimiter(options =>
{
	options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
	{
		var ip = context.Request.Headers["CF-Connecting-IP"].FirstOrDefault()
			?? context.Request.Headers["X-Forwarded-For"].FirstOrDefault()?.Split(',')[0].Trim()
			?? context.Connection.RemoteIpAddress?.ToString()
			?? "unknown";

		var permitLimit = rateLimitConfig.GetValue<int>("PermitLimit", 100);
		var windowSeconds = rateLimitConfig.GetValue<int>("WindowSeconds", 60);
		var queueLimit = rateLimitConfig.GetValue<int>("QueueLimit", 0);

		return RateLimitPartition.GetFixedWindowLimiter(ip, _ => new FixedWindowRateLimiterOptions
		{
			PermitLimit = permitLimit,
			Window = TimeSpan.FromSeconds(windowSeconds),
			QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
			QueueLimit = queueLimit,
		});
	});

	options.AddFixedWindowLimiter("strict", limiterOptions =>
	{
		limiterOptions.PermitLimit = rateLimitConfig.GetValue<int>("StrictPermitLimit", 10);
		limiterOptions.Window = TimeSpan.FromSeconds(rateLimitConfig.GetValue<int>("StrictWindowSeconds", 60));
		limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
		limiterOptions.QueueLimit = 0;
	});

	options.OnRejected = async (context, token) =>
	{
		context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
		if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
		{
			context.HttpContext.Response.Headers.RetryAfter =
				((int)retryAfter.TotalSeconds).ToString();
		}
		context.HttpContext.Response.ContentType = "text/plain";
		await context.HttpContext.Response.WriteAsync("Too many requests. Please try again later.", token);
	};
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

// Only redirect to HTTPS when not disabled (e.g., in container environments)
if (!builder.Configuration.GetValue<bool>("DisableHttpsRedirect"))
{
	app.UseHttpsRedirection();
}

app.UseStaticFiles();

app.Use(async (context, next) =>
{
	context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
	context.Response.Headers.Append("X-Frame-Options", "SAMEORIGIN");
	context.Response.Headers.Append("X-XSS-Protection", "1; mode=block");
	context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
	await next();
});

app.UseRateLimiter();

app.UseRouting();
app.UseAntiforgery();

app.MapRazorComponents<App>()
	.AddInteractiveServerRenderMode();

app.Run();
