# FeatureFlags.Client

Minimal .NET client SDK for the Feature Flags Service. There is also a thin FeatureFlagsClientFactory layer to avoid
creating a new HttpClient and client instance every time you want to evaluate a flag, which is the recommended way to
use the client without the DependencyInjection companion package.

## Evaluation (runtime)

```csharp
using FeatureFlags.Client;

var http = new HttpClient();
var client = new FeatureFlagsClient(http, new FeatureFlagsClientOptions
{
    BaseAddress = new Uri("https://localhost:5001/api/"),
    ApiKey = "ffsk_..."
});

var result = await client.EvaluateAsync("my-flag", new EvaluationContext { UserId = "123" });
Console.WriteLine(result.Allowed);
```

## With factory:

```csharp
using FeatureFlags.Client;

var client = new FeatureFlagsClientFactory.Create(new FeatureFlagsClientOptions
{
    BaseAddress = new Uri("https://localhost:5001/api/"),
    ApiKey = "ffsk_..."
});

var result = await client.EvaluateAsync("my-flag", new EvaluationContext { UserId = "123" });
Console.WriteLine(result.Allowed);
```

## DI registration

Install/use the companion package `FeatureFlags.Client.DependencyInjection` and register:

```csharp
services.AddFeatureFlagsClient(options =>
{
    options.BaseAddress = new Uri("https://localhost:5001/api/");
    options.ApiKey = "ffsk_...";
    options.ApiVersion = new Version(1, 0); // optional
});
```

This is the recommended way to use the client. You can then inject `IFeatureFlagsClient` wherever you need it.
