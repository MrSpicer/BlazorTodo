using Blazored.LocalStorage;
using TodoList.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

builder.Services.AddHsts(options =>
{
	options.MaxAge = TimeSpan.FromDays(365);
});

// Third party
builder.Services.AddBlazoredLocalStorage();

// Application services
builder.Services.AddMemoryCache();
builder.Services.AddTodoServices();

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

app.UseRouting();

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();
