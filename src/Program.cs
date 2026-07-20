using System.Net;
using System.Threading.RateLimiting;
using Blazored.LocalStorage;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using TodoList.Components;
using TodoList.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
	.AddInteractiveServerComponents();

builder.Services.AddRazorPages();
builder.Services.AddCascadingAuthenticationState();

builder.Services.AddHsts(options =>
{
	options.MaxAge = TimeSpan.FromDays(365);
});

// Data Protection key ring. The keys sign auth cookies and antiforgery tokens. Without a
// stable, persisted ring the keys regenerate on every container restart — logging everyone
// out and breaking in-flight antiforgery tokens (and preventing multi-replica scale-out).
// Persist to a mounted volume when DataProtection:KeysDirectory is configured (see
// docker-stack.yml); fall back to the ephemeral default for local `dotnet run`.
var keysDirectory = builder.Configuration["DataProtection:KeysDirectory"];
var dataProtection = builder.Services.AddDataProtection().SetApplicationName("BlazorTodo");
if (!string.IsNullOrWhiteSpace(keysDirectory))
{
	Directory.CreateDirectory(keysDirectory);
	dataProtection.PersistKeysToFileSystem(new DirectoryInfo(keysDirectory));
}

// TLS terminates at Cloudflare; the app receives plain HTTP from the tunnel over the Docker
// overlay network. Honor X-Forwarded-Proto/For so Request.Scheme is https and the client IP
// is the real caller — required for Secure cookies and accurate rate-limiter partitioning.
// Only private-range peers are trusted: the app publishes no inbound ports, so only in-network
// proxies (cloudflared) can reach it — a public client cannot spoof these headers.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
	options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
	options.KnownIPNetworks.Clear();
	options.KnownProxies.Clear();
	options.KnownIPNetworks.Add(new System.Net.IPNetwork(IPAddress.Parse("10.0.0.0"), 8));
	options.KnownIPNetworks.Add(new System.Net.IPNetwork(IPAddress.Parse("172.16.0.0"), 12));
	options.KnownIPNetworks.Add(new System.Net.IPNetwork(IPAddress.Parse("192.168.0.0"), 16));
});

// Force the Secure flag on the Identity auth cookie in non-dev environments (the browser↔
// Cloudflare hop is always HTTPS). Local `dotnet run` serves plain HTTP, so relax to
// SameAsRequest there or login cookies would never be sent back.
builder.Services.ConfigureApplicationCookie(options =>
{
	options.Cookie.HttpOnly = true;
	options.Cookie.SameSite = SameSiteMode.Lax;
	options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
		? CookieSecurePolicy.SameAsRequest
		: CookieSecurePolicy.Always;
});

// Third party
builder.Services.AddBlazoredLocalStorage();

// Application services
builder.Services.AddTodoServices(builder.Configuration);

var rateLimitConfig = builder.Configuration.GetSection("RateLimiting");

builder.Services.AddRateLimiter(options =>
{
	options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
	{
		// Cloudflare sets CF-Connecting-IP to the real client; it's authoritative because only
		// the tunnel can reach the app. Fall back to the forwarded-headers-resolved connection IP
		// (see UseForwardedHeaders) rather than trusting a raw X-Forwarded-For, which any caller
		// reaching the app directly could spoof.
		var ip = context.Request.Headers["CF-Connecting-IP"].FirstOrDefault()
			?? context.Connection.RemoteIpAddress?.ToString()
			?? "unknown";

		var permitLimit = rateLimitConfig.GetValue<int>("PermitLimit", 500);
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

	// Partition the strict limiter per-IP; a single shared bucket would let one abuser lock
	// every user out of login. Keyed by the same forwarded client IP as the global limiter.
	options.AddPolicy("strict", context =>
	{
		var ip = context.Request.Headers["CF-Connecting-IP"].FirstOrDefault()
			?? context.Connection.RemoteIpAddress?.ToString()
			?? "unknown";

		return RateLimitPartition.GetFixedWindowLimiter($"strict:{ip}", _ => new FixedWindowRateLimiterOptions
		{
			PermitLimit = rateLimitConfig.GetValue<int>("StrictPermitLimit", 100),
			Window = TimeSpan.FromSeconds(rateLimitConfig.GetValue<int>("StrictWindowSeconds", 60)),
			QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
			QueueLimit = 0,
		});
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

// Apply pending EF Core migrations and seed the bootstrap admin account before serving traffic,
// so requests never hit an unmigrated schema and an operator can sign in on first deploy.
using (var scope = app.Services.CreateScope())
{
	await scope.ServiceProvider.GetRequiredService<TodoList.Data.DatabaseInitializer>().RunAsync();
}

// Must run before anything that reads the scheme or client IP (HTTPS redirect, security
// headers, rate limiter, auth), so those see the Cloudflare-forwarded values.
app.UseForwardedHeaders();

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

// Content-Security-Policy: Bootstrap CSS is served from cdn.jsdelivr.net (with SRI), and Blazor
// Server ships an inline bootstrap <script> plus inline component styles, so script/style-src
// allow 'unsafe-inline'. connect-src permits the SignalR websocket. This is defense-in-depth
// layered on top of Razor's automatic output encoding; tightening script-src to nonces/hashes
// is a future hardening step.
const string ContentSecurityPolicy =
	"default-src 'self'; " +
	"base-uri 'self'; " +
	"object-src 'none'; " +
	"frame-ancestors 'self'; " +
	"img-src 'self' data:; " +
	"font-src 'self'; " +
	"style-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net; " +
	"script-src 'self' 'unsafe-inline'; " +
	"connect-src 'self' ws: wss:";

app.Use(async (context, next) =>
{
	context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
	context.Response.Headers.Append("X-Frame-Options", "SAMEORIGIN");
	// X-XSS-Protection is deprecated; 0 disables the legacy auditor (which can itself be abused).
	context.Response.Headers.Append("X-XSS-Protection", "0");
	context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
	context.Response.Headers.Append("Content-Security-Policy", ContentSecurityPolicy);
	await next();
});

app.UseRouting();

// Must run after UseRouting so per-endpoint policies (e.g. "strict" on the Identity pages) can
// read the selected endpoint's metadata; before auth so load is shed prior to credential work.
app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

// After auth (needs HttpContext.User) and on the HTTP page response (a cookie can't be set over
// the SignalR circuit): give anonymous visitors a stable anon_sid cookie and track their activity.
app.UseMiddleware<TodoList.Middleware.AnonymousSessionMiddleware>();

app.UseAntiforgery();

app.MapRazorComponents<App>()
	.AddInteractiveServerRenderMode();

// Identity UI lives in Razor Pages (login, register, forgot-password, account manage). Apply the
// tighter "strict" limiter here to blunt credential stuffing, account/email enumeration, and
// password-reset email flooding — the global limiter alone leaves too much headroom.
app.MapRazorPages().RequireRateLimiting("strict");

app.Run();
