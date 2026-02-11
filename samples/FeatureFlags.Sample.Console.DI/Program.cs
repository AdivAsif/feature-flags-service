using FeatureFlags.Client;
using FeatureFlags.Client.DependencyInjection;
using FeatureFlags.Client.Exceptions;
using Microsoft.Extensions.DependencyInjection;

static string? GetArg(string[] args, string name)
{
    var prefix = $"--{name}=";
    return args.FirstOrDefault(a => a.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))?.Substring(prefix.Length);
}

static void PrintUsage()
{
    Console.WriteLine("FeatureFlags.Sample.Console.DI");
    Console.WriteLine();
    Console.WriteLine("Usage:");
    Console.WriteLine(
        "  dotnet run --project samples/FeatureFlags.Sample.Console.DI -- --baseUrl=https://localhost:5001/api/ --apiKey=ffsk_... --flagKey=my-flag");
    Console.WriteLine();
    Console.WriteLine("Optional:");
    Console.WriteLine(
        "  --userId=123 --email=a@b.com --groups=beta,internal --tenantId=t1 --environment=dev --apiVersion=1.0");
    Console.WriteLine();
    Console.WriteLine("Env var equivalents:");
    Console.WriteLine(
        "  FEATUREFLAGS_BASE_URL, FEATUREFLAGS_API_KEY, FEATUREFLAGS_FLAG_KEY, FEATUREFLAGS_USER_ID, FEATUREFLAGS_EMAIL, FEATUREFLAGS_GROUPS, FEATUREFLAGS_TENANT_ID, FEATUREFLAGS_ENVIRONMENT, FEATUREFLAGS_API_VERSION");
}

var baseUrl = GetArg(args, "baseUrl") ?? Environment.GetEnvironmentVariable("FEATUREFLAGS_BASE_URL");
var apiKey = GetArg(args, "apiKey") ?? Environment.GetEnvironmentVariable("FEATUREFLAGS_API_KEY");
var flagKey = GetArg(args, "flagKey") ?? Environment.GetEnvironmentVariable("FEATUREFLAGS_FLAG_KEY");

if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(flagKey))
{
    PrintUsage();
    return;
}

var userId = GetArg(args, "userId") ?? Environment.GetEnvironmentVariable("FEATUREFLAGS_USER_ID") ?? "anonymous";
var email = GetArg(args, "email") ?? Environment.GetEnvironmentVariable("FEATUREFLAGS_EMAIL");
var groupsCsv = GetArg(args, "groups") ?? Environment.GetEnvironmentVariable("FEATUREFLAGS_GROUPS");
var tenantId = GetArg(args, "tenantId") ?? Environment.GetEnvironmentVariable("FEATUREFLAGS_TENANT_ID");
var environment = GetArg(args, "environment") ?? Environment.GetEnvironmentVariable("FEATUREFLAGS_ENVIRONMENT");
var apiVersionString = GetArg(args, "apiVersion") ?? Environment.GetEnvironmentVariable("FEATUREFLAGS_API_VERSION");

var groups = string.IsNullOrWhiteSpace(groupsCsv)
    ? null
    : groupsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

Version? apiVersion = null;
if (!string.IsNullOrWhiteSpace(apiVersionString))
    apiVersion = Version.Parse(apiVersionString);

var services = new ServiceCollection();
services.AddFeatureFlagsClient(options =>
{
    options.BaseAddress = new Uri(baseUrl, UriKind.Absolute);
    options.ApiKey = apiKey;
    options.ApiVersion = apiVersion;
});

await using var provider = services.BuildServiceProvider();
var client = provider.GetRequiredService<IFeatureFlagsClient>();

try
{
    Console.WriteLine($"Evaluating flag '{flagKey}'...");

    var result = await client.EvaluateAsync(flagKey, new EvaluationContext
    {
        UserId = userId,
        Email = email,
        Groups = groups,
        TenantId = tenantId,
        Environment = environment
    });

    Console.WriteLine($"Allowed: {result.Allowed}");
    if (!string.IsNullOrWhiteSpace(result.Reason))
        Console.WriteLine($"Reason:  {result.Reason}");
}
catch (FeatureFlagsApiException ex)
{
    Console.Error.WriteLine($"API error: {(int)ex.StatusCode} {ex.StatusCode}");
    if (!string.IsNullOrWhiteSpace(ex.ResponseBody))
        Console.Error.WriteLine(ex.ResponseBody);
}