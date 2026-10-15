# ZGott.Crestron.Logging

A `Microsoft.Extensions.Logging` provider for Crestron SIMPL# applications.
Write `ILogger` messages to `CrestronConsole` and the processor's `ErrorLog`,
with category names, scopes, event IDs, thread IDs, and exception details.

## Requirements and installation

- An application running on a compatible Crestron 4-Series or VC-4 runtime.
  Verify that the processor firmware/runtime supports .NET 8 or newer before deploying.
- Crestron's runtime is required to emit output; this is not a desktop console logger.
- The package depends on `Crestron.SimplSharp.SDK.Library` and the
  Microsoft logging packages. ASP.NET Core is **not** required.
  SIMPL# Pro applications should still reference `Crestron.SimplSharp.SDK.ProgramLibrary`
  directly for their control-system APIs and deployment tooling.

After the first release is published:

```powershell
dotnet add package ZGott.Crestron.Logging
```

The library is MIT-licensed. The Crestron SDK has its own license terms.
This project is not affiliated with or endorsed by Crestron Electronics.

## Quick start

Create the logging factory once during application startup and dispose of it when
your application shuts down:

```csharp
using Microsoft.Extensions.Logging;
using ZGott.Crestron.Logging;

using var loggerFactory = CrestronLoggerFactory.Create();

ILogger logger = loggerFactory.CreateLogger("MyControlSystem");

using (logger.BeginScope("Room-101"))
{
    logger.LogInformation(new EventId(42), "Connected to {Device}", "Display");
}
```

Example console output (the managed thread ID varies):

```text
@t.0001 [ Room-101 ] [ info ] #42 < MyControlSystem > | Connected to Display
```

For dependency injection, register the provider directly on your services, then inject
`ILogger<T>` into your services:

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ZGott.Crestron.Logging;

var services = new ServiceCollection();
services.AddCrestron();

using var serviceProvider = services.BuildServiceProvider();
var logger = serviceProvider.GetRequiredService<ILogger<MyControlSystem>>();
logger.LogInformation("System started");
```

`MyControlSystem` is your application's class. If you already use a .NET host,
call `builder.Services.AddCrestron()` or `builder.Logging.AddCrestron()` rather than
creating a second service provider. Other logging providers remain registered;
call `builder.Logging.ClearProviders()` first if Crestron should be the only provider.

`services.AddCrestron()` adds the required logging services automatically and returns
the service collection for chaining. An options callback is also supported:

```csharp
services.AddCrestron(options => options.ErrorLogMinimumLevel = LogLevel.Warning);
```

The existing `services.AddLogging(logging => logging.AddCrestron())` approach remains
supported. Repeated or mixed registration does not add duplicate Crestron providers.

`CrestronLoggerFactory.Create()` registers Crestron output automatically and defaults
the Microsoft logging minimum to `Trace`. Pass an `Action<ILoggingBuilder>` to
customize logging, or use `Create(configuration, builder => ...)` to combine
configuration with code. The callback runs after the factory defaults and binding
registrations. The standard `LoggerFactory.Create(builder => builder.AddCrestron())`
API remains supported.

### ASP.NET Core web app

Register the provider directly on `builder.Services`:

```csharp
using ZGott.Crestron.Logging;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddCrestron();

var app = builder.Build();
app.MapGet("/", () => "Hello World!");
app.Run();
```

`WebApplication.CreateBuilder` already configures logging, dependency injection,
and the `Logging` section from `appsettings.json`, including JSON reloads by
default. No separate logger factory or `AddConfiguration` call is needed;
inject `ILogger<T>` into your services as usual. Existing logging providers remain
registered. To use only Crestron output, call `builder.Logging.ClearProviders()`
before `builder.Services.AddCrestron()`. Registering through
`builder.Logging.AddCrestron()` is also supported and has the same behavior.

Crestron's runtime is still required to emit output; registering this provider
does not make Crestron logging available on a regular desktop web server.

## Options and filtering

```csharp
builder.SetMinimumLevel(LogLevel.Debug);
builder.AddCrestron(options =>
{
    options.MinimumLogLevel = LogLevel.Debug;
    options.IncludeScopes = true;
    options.ErrorLogMinimumLevel = LogLevel.Warning;
});
```

Both Microsoft's logging filters and the provider's `MinimumLogLevel` apply.
The provider now defaults to `Trace`, allowing category levels from configuration
to control filtering without a second hard-coded minimum. An explicitly configured
`MinimumLogLevel` still acts as a provider-wide floor, even for category overrides.
Leave it unset when configuration category filters should have full control.

`CrestronLoggerFactory.Create` also defaults Microsoft's minimum to `Trace`.
`AddCrestron()` alone does not change Microsoft's filters: the standard Microsoft
default is still `Information` unless changed by the application, host, or
configuration. Use `SetMinimumLevel`, `AddFilter<CrestronLoggerProvider>`, or
configuration to set that filter as appropriate. Configuration rules can enable
`Debug` or `Trace` without calling `SetMinimumLevel` in code.

| Option                 | Default       | Behavior                                                                           |
|------------------------|---------------|------------------------------------------------------------------------------------|
| `MinimumLogLevel`      | `Trace`       | Minimum severity for this provider; `None` disables it.                            |
| `LogToConsole`         | `true`        | Writes enabled messages to `CrestronConsole`.                                      |
| `LogToErrorLog`        | `true`        | Writes enabled messages meeting `ErrorLogMinimumLevel` to the processor error log. |
| `ErrorLogMinimumLevel` | `Error`       | Error-log threshold; `None` disables this destination.                             |
| `IncludeCategory`      | `true`        | Includes the logger category.                                                      |
| `IncludeScopes`        | `true`        | Includes active scope values, outermost first.                                     |
| `IncludeEventId`       | `true`        | Includes nonzero numeric event IDs.                                                |
| `IncludeThreadId`      | `true`        | Includes the managed thread ID.                                                    |

With the standalone factory defaults, `Trace` through `Warning` go only to the
console; `Error` and `Critical` go to both destinations. Lowering `ErrorLogMinimumLevel` maps
`Trace`/`Debug`/`Information` to `ErrorLog.Notice`, `Warning` to `ErrorLog.Warn`,
and `Error`/`Critical` to `ErrorLog.Error`. The provider-wide minimum still applies.
Exceptions are appended as separate lines to every selected destination.
Messages are rendered text, not a structured-log storage format.
Undefined `LogLevel` option values are rejected. Setting both destinations to
`false` intentionally disables output.

### Configuration

For a standalone factory, pass the application's **root configuration** (not the
`Logging` section). To load JSON, reference `Microsoft.Extensions.Configuration.Json`
in your application and deploy `appsettings.json` alongside it:

```csharp
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ZGott.Crestron.Logging;

using var configuration = new ConfigurationManager();
configuration.AddJsonFile(
    Path.Combine(AppContext.BaseDirectory, "appsettings.json"),
    optional: false,
    reloadOnChange: true);

using var loggerFactory = CrestronLoggerFactory.Create(configuration);
var logger = loggerFactory.CreateLogger("MyControlSystem");
logger.LogDebug("Enabled when the configured category level allows it");
```

Keep both the configuration and factory alive until application shutdown; the
factory does not own or dispose the supplied configuration.

The provider alias is `Crestron`. When your application loads `appsettings.json`,
register the logging section with `AddConfiguration` (a .NET host already does
this), then call `AddCrestron()`:

```csharp
builder.AddConfiguration(configuration.GetSection("Logging"));
builder.AddCrestron();
```

**`Logging:Crestron:LogLevel` is optional.** Crestron uses the global
`Logging:LogLevel` rules unless you add provider-specific overrides. The following
example uses global log levels and keeps only output settings under `Crestron`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning"
    },
    "Crestron": {
      "ErrorLogMinimumLevel": "Warning",
      "LogToConsole": true,
      "LogToErrorLog": true,
      "IncludeCategory": true,
      "IncludeScopes": true,
      "IncludeEventId": true,
      "IncludeThreadId": true
    }
  }
}
```

Only add `Logging:Crestron:LogLevel` when Crestron should use different levels
from other providers. For example, this optional override enables `Debug` for
Crestron while leaving other providers on the global rules:

```json
{
  "Logging": {
    "Crestron": {
      "LogLevel": {
        "Default": "Debug",
        "Microsoft": "Warning"
      }
    }
  }
}
```

Merge this override into your existing configuration only if needed; omit it to
use the global rules. Leave `MinimumLogLevel` unset (it defaults to `Trace`) so
it does not impose a higher provider-wide floor on either set of rules.

Options registered through dependency injection use `IOptionsMonitor` and update
existing loggers when the configuration source supports reloads. Microsoft's
`Logging:LogLevel` and `Logging:Crestron:LogLevel` category filters also update on
existing factory-created loggers, including `ILogger<T>`. Provider-specific rules
take precedence over global rules, and the longest matching category prefix wins.
For example, with no Crestron-specific overrides, changing
`Logging:LogLevel:MyControlSystem` to `Trace` enables trace output without recreating
the logger, provided `MinimumLogLevel` is not explicitly set higher.

For JSON files, `reloadOnChange: true` is required, and the runtime must deliver file
change notifications. If notifications are unavailable on the deployed filesystem,
use a supported polling file watcher or explicitly reload the configuration root.
Directly constructing a provider does not attach Microsoft's category filters;
use a logging factory or DI registration for configuration-driven categories.
Without dependency injection, use
`new CrestronLoggerProvider(new CrestronLoggerOptions { ... })` and dispose of the
provider yourself.

### API naming

The public API uses PascalCase for types, methods, and properties, and camelCase
for parameters; `.editorconfig` supplies naming rules for future changes.
`AddCrestron()` supports both direct service registration and the Microsoft
logging-builder registration pattern. Earlier `AddCrestronLogger()` and `MinLevel` names remain as obsolete
compatibility aliases; migrate to `AddCrestron()` and `MinimumLogLevel`.
For configuration, replace the `MinLevel` key with `MinimumLogLevel` and do not set both.

## Building and packaging

Use the .NET 8 SDK or a newer compatible SDK:

```powershell
dotnet build .\ZGott.Crestron.Logging.sln --configuration Release
dotnet test .\ZGott.Crestron.Logging.sln --configuration Release --no-build
.\.github\scripts\Test-ReleaseVersion.ps1
dotnet pack .\ZGott.Crestron.Logging\ZGott.Crestron.Logging.csproj --configuration Release --no-build --output .\artifacts
```

The local default version is `1.0.0`. Packages include this README, the MIT
license, XML API documentation, repository metadata, and Source Link information.
A `.snupkg` symbol package is produced alongside the `.nupkg`.
CI builds, tests, and packs pull requests and pushes to `main` without publishing.

## Publishing a release (maintainers)

**Publishing a GitHub release will publish to nuget.org.**

A tag push or draft release alone does not publish. Package versions
on nuget.org are immutable; review the package artifacts and release notes first.

### Versioning

Use [Semantic Versioning 2.0.0](https://semver.org/): `MAJOR.MINOR.PATCH`, optionally
followed by prerelease identifiers such as `-rc.1`. Increase the major version for
breaking public API changes, minor for backward-compatible features, and patch for
backward-compatible fixes. Start the official stable release at `1.0.0`.

Tags may be `v1.0.0` or `1.0.0`; the optional lowercase `v` is removed for the NuGet
version. Leading zeroes in numeric components/identifiers are rejected.
Build metadata (`+build.123`) is intentionally rejected because NuGet excludes it
from package identity, which would cause version collisions.
GitHub's prerelease checkbox must match whether the tag contains a prerelease suffix.
Changing the project file for each release is unnecessary: the tag supplies both
the package and assembly informational version during the release build.

### Create the release when ready

In GitHub, create a new release for `v1.0.0`, write the release notes, and publish it.
Leave the prerelease checkbox unchecked for a stable version. Alternatively:

```powershell
# This command publishes the GitHub release and triggers NuGet publication.
gh release create v1.0.0 --target main --title "1.0.0" --generate-notes

# For a preview, use a SemVer suffix and mark the release as a prerelease.
gh release create v1.1.0-rc.1 --target main --title "1.1.0-rc.1" --prerelease --generate-notes
```

Use `--draft` if you want to prepare notes without triggering publication.
When automating release creation from another workflow, GitHub's default
`GITHUB_TOKEN` does not trigger downstream workflows; use an appropriately scoped
GitHub App token or personal access token for that release-creation workflow.
An interactive `gh` session uses your own authentication.

The release workflow checks out the exact tag, validates its version, builds,
tests, and uploads the package and symbols as Actions artifacts. A separate job
waits for the `nuget` environment's approval (if configured), authenticates, and
pushes to nuget.org. NuGet push automatically includes the adjacent symbol package.
Duplicate versions are skipped to allow retries; changes require a new version.
No manual dispatch publishing path is provided.
