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

using var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddCrestron();
});

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

For dependency injection, register the provider through `AddLogging`, then inject
`ILogger<T>` into your services:

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ZGott.Crestron.Logging;

var services = new ServiceCollection();
services.AddLogging(builder => builder.AddCrestron());

using var serviceProvider = services.BuildServiceProvider();
var logger = serviceProvider.GetRequiredService<ILogger<MyControlSystem>>();
logger.LogInformation("System started");
```

`MyControlSystem` is your application's class. If you already use a .NET host,
register `AddCrestron()` with that host's logging builder rather than creating
a second service provider. Other logging providers remain registered;
call `builder.ClearProviders()` first if Crestron should be the only provider.

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
The default Microsoft logging filter is `Information`, so lowering only the
provider's minimum does not enable `Debug` or `Trace`.
Use `SetMinimumLevel`, `AddFilter<CrestronLoggerProvider>`, or configuration to
set the Microsoft filter as appropriate.

| Option                 | Default       | Behavior                                                                           |
|------------------------|---------------|------------------------------------------------------------------------------------|
| `MinimumLogLevel`      | `Information` | Minimum severity for this provider; `None` disables it.                            |
| `LogToConsole`         | `true`        | Writes enabled messages to `CrestronConsole`.                                      |
| `LogToErrorLog`        | `true`        | Writes enabled messages meeting `ErrorLogMinimumLevel` to the processor error log. |
| `ErrorLogMinimumLevel` | `Error`       | Error-log threshold; `None` disables this destination.                             |
| `IncludeCategory`      | `true`        | Includes the logger category.                                                      |
| `IncludeScopes`        | `true`        | Includes active scope values, outermost first.                                     |
| `IncludeEventId`       | `true`        | Includes nonzero numeric event IDs.                                                |
| `IncludeThreadId`      | `true`        | Includes the managed thread ID.                                                    |

By default, `Information` and `Warning` go only to the console; `Error` and
`Critical` go to both destinations. Lowering `ErrorLogMinimumLevel` maps
`Trace`/`Debug`/`Information` to `ErrorLog.Notice`, `Warning` to `ErrorLog.Warn`,
and `Error`/`Critical` to `ErrorLog.Error`. The provider-wide minimum still applies.
Exceptions are appended as separate lines to every selected destination.
Messages are rendered text, not a structured-log storage format.
Undefined `LogLevel` option values are rejected. Setting both destinations to
`false` intentionally disables output.

### Configuration

The provider alias is `Crestron`. When your application loads `appsettings.json`,
register the logging section with `AddConfiguration` (a .NET host already does
this), then call `AddCrestron()`:

```csharp
builder.AddConfiguration(configuration.GetSection("Logging"));
builder.AddCrestron();
```

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    },
    "Crestron": {
      "LogLevel": {
        "Default": "Debug",
        "Microsoft": "Warning"
      },
      "MinimumLogLevel": "Debug",
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

Options registered through dependency injection use `IOptionsMonitor` and update
existing loggers when the configuration source supports reloads.
Without dependency injection, use
`new CrestronLoggerProvider(new CrestronLoggerOptions { ... })` and dispose of the
provider yourself.

### API naming

The public API uses PascalCase for types, methods, and properties, and camelCase
for parameters; `.editorconfig` supplies naming rules for future changes.
`AddCrestron()` follows the Microsoft logging-provider registration
pattern. Earlier `AddCrestronLogger()` and `MinLevel` names remain as obsolete
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
