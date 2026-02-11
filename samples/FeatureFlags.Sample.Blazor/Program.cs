using FeatureFlags.Client.DependencyInjection;
using FeatureFlags.Sample.Blazor.Components;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddFeatureFlagsClient(options =>
{
    options.BaseAddress = new Uri(builder.Configuration["FeatureFlags:BaseUrl"] ?? string.Empty, UriKind.Absolute);
    options.ApiKey = builder.Configuration["FeatureFlags:ApiKey"];
    options.ApiVersion = Version.Parse(builder.Configuration["FeatureFlags:ApiVersion"] ?? "1.0");
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();