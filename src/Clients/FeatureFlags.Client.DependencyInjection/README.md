# FeatureFlags.Client.DependencyInjection

DI helpers for `FeatureFlags.Client`. The recommended way to use this service.

## Register from code

```csharp
using FeatureFlags.Client.DependencyInjection;

services.AddFeatureFlagsClient(options =>
{
    options.BaseAddress = new Uri("https://localhost:5001/api/");
    options.ApiKey = "ffsk_...";
    options.ApiVersion = new Version(1, 0); // optional - more versions may be added in the future, but 1.0 is the current default if not specified
});
```

## Register from configuration

```csharp
services.AddFeatureFlagsClient(configuration, sectionName: "FeatureFlags");
```

Example config:

```json
{
  "FeatureFlags": {
    "BaseAddress": "https://localhost:5001/api/",
    "ApiKey": "ffsk_...",
    "ApiVersion": "1.0",
    "TimeoutSeconds": "2",
    "EnableRetries": "true",
    "MaxRetries": "3"
  }
}
```

