# Quickstart

## Development Setup

To test the API locally, development secrets need to be configured.

1. Navigate to the `Web.Api` project:

```bash
cd src/Web.Api 
```

2. Initialise user secrets

```bash
dotnet user-secrets init
```

3. Set the secrets

```bash
dotnet user-secrets set "Auth:Authority" "https://dev-ynz4fwdg3j8k6cna.uk.auth0.com/"
dotnet user-secrets set "Auth:Audience" "https://featureflags.adiv.co.uk"
dotnet user-secrets set "Auth:RoleClaimType" "https://featureflags.adiv.co.uk/roles"
dotnet user-secrets set "ConnectionStrings:FeatureFlagsDatabase" "Host=localhost;Port=5432;Database=feature_flags_db;Username=postgres;Password=password;"
dotnet user-secrets set "ConnectionStrings:FeatureFlagsCache" "localhost:6379"
dotnet user-secrets set "EnableDevToken" "true"
dotnet user-secrets set "JwtSecretKey" "adivadivadivadivadivadivadivadiv"
```

(Note: these values are for example purposes only, the Auth0 values should be replaced with your own - my testing Auth0
setup is down now)

4. Run the API

```bash
dotnet run --project src/Web.Api
```

## Production Setup

Do not use user-secrets in production. Instead, use environment variables, for an example in Powershell (Windows):

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Production"
$env:ConnectionStrings__FeatureFlagsDatabase = "Host=localhost;Port=5432;Database=feature_flags_db;Username=postgres;Password=password;"
$env:ConnectionStrings__FeatureFlagsCache = "localhost:6379"
$env:Auth__Authority = "https://dev-ynz4fwdg3j8k6cna.uk.auth0.com/"
$env:Auth__Audience = "https://featureflags.adiv.co.uk"
$env:Auth__RoleClaimType = "https://featureflags.adiv.co.uk/roles"
```

If you want to use the dev token for testing in production environment, you can also set the following environment
variables:

```powershell
$env:JwtSecretKey=“adivadivadivadivadivadivadivadiv”
$env:EnableDevToken=“true”
```

This enables the /dev/token endpoint and JWT authentication to use the dev token instead of pre-authorized IdP tokens
from
services like Auth0 or Azure AD.

## Running the infrastructure

Run the infrastructure using docker compose:

```bash
docker compose -f .\infrastructure\compose.localhost.yaml up -d
```

## Running the load tests

Load testing is done using [k6](https://k6.io/). I also conducted my localhost load testing using this command:

```bash
cmd /c "cd src\Web.Api && start /affinity 3 dotnet bin\Release\net10.0\Web.Api.dll"
```

This starts the API with two CPU cores, refer to the affinity docs for more info. CPU pinning is useful in load testing
because it gives a consistent result across machines, even with a Ryzen 9 9950X3D CPU, it will only use cores 0-1 - this
service is assuming Windows is being used.

Running the load tests is as simple as running:

```bash
k6 run --out json=summary.json infrastructure/k6.evaluation.steady.js
```

or

```bash
k6 run --out json=summary.json infrastructure/k6.key-stress-test.js
```

This will run the load tests and output the results to summary.json. You can also set environment variables:

```bash
$env:K6_WEB_DASHBOARD_EXPORT="report.html"
$env:K6_WEB_DASHBOARD="true"
```

This allows viewing the results in real-time in the browser using k6's web dashboard. After the test concludes, it will
create an HTML report as well.

## Running the unit tests

Running ``dotnet test`` at the project root will run all the unit tests.