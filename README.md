# Sane Database Availability Check for Umbraco

[![NuGet](https://img.shields.io/nuget/v/Integrious.Umbraco.SaneDatabaseAvailabilityCheck?logo=nuget)](https://www.nuget.org/packages/Integrious.Umbraco.SaneDatabaseAvailabilityCheck/)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

Umbraco checks that its database is reachable before it boots. If it is not, Umbraco tries **five
times, one second apart**, and then throws a `BootFailedException`. Four seconds of patience.

That is fine on a developer machine. It is not fine when the database is simply slow to arrive:

- an **Azure SQL serverless** database resuming from auto-pause, which routinely takes 30-60 seconds;
- a **container** that started before its database sidecar was listening;
- a database server restarting after patching, or a failover group switching over;
- a brief network or DNS blip during a deployment.

In all of those cases the database turns up shortly afterwards, and the site would have been
perfectly healthy - but Umbraco has already given up, and something has to restart the application
before anyone notices.

This package replaces that check with one that **keeps waiting**. By default it never gives up. If
you would rather it did, set a timeout in seconds in `appsettings.json`.

## Installation

```bash
dotnet add package Integrious.Umbraco.SaneDatabaseAvailabilityCheck
```

That is the whole installation. The package ships a composer that replaces Umbraco's registration of
`IDatabaseAvailabilityCheck`, so it takes effect on the next boot with no code and no configuration.

## Configuration

Every setting is optional. The defaults wait forever, check every second, report progress every
thirty seconds, and start reporting at `Error` after a minute.

```jsonc
{
  "Umbraco": {
    "DatabaseAvailabilityCheck": {
      "TimeoutSeconds": 300,
      "RetryIntervalSeconds": 5,
      "ProgressLogIntervalSeconds": 30,
      "EscalateToErrorAfterSeconds": 60
    }
  }
}
```

| Setting | Default | Meaning |
| --- | --- | --- |
| `TimeoutSeconds` | *(unset)* | Total seconds to keep checking before giving up. **Unset means never give up.** Measured from the first check, so it is a ceiling on total boot delay. Must be 1 or more. |
| `RetryIntervalSeconds` | `1` | Seconds between checks. Must be 1 or more. |
| `ProgressLogIntervalSeconds` | `30` | How often to log that it is still waiting. Independent of the retry interval, so the check can poll often and report rarely. Must be 1 or more. |
| `EscalateToErrorAfterSeconds` | `60` | After this much waiting, those progress messages are logged at `Error` instead of `Information`, so monitoring can alert on a stalled boot. Set to `null` to never escalate. Must be 0 or more. |

Invalid values fail the boot with a message naming the setting, rather than quietly doing something
unexpected.

## What you will see in the log

Everything below is logged by `Integrious.Umbraco.SaneDatabaseAvailabilityCheck.SaneDatabaseAvailabilityCheck`.

| When | Level | Message |
| --- | --- | --- |
| First failed check | `Debug` | Could not immediately connect to the database. Will keep checking every … |
| Every `ProgressLogIntervalSeconds` | `Information` | Still waiting for the database to become available: *n* attempts over … |
| …once past `EscalateToErrorAfterSeconds` | `Error` | *(the same message, escalated)* |
| Database answers | `Information` | Database became available after *n* attempts over … |
| Timeout reached | `Error` | Giving up on the database after *n* attempts over … |

### Umbraco's own per-attempt warnings

Separately from this package, Umbraco logs a warning **with the full exception** on every failed
connection attempt - `Configured database is reporting as not being available.` At the default
one-second retry interval and a ten-minute outage, that is 600 stack traces.

Umbraco writes that one through its *static* logger, so it arrives with a `SourceContext` of
`object` rather than a real category name. There is therefore no precise way to filter it: an
override on `object` would silence everything else Umbraco logs statically too.

You can work around this using SaneDatabaseAvailabilityCheck's **`RetryIntervalSeconds`** setting.
Five seconds is plenty for a database you expect back inside a minute, and cuts the noise fivefold; 
ten or fifteen is reasonable if you are waiting out a failover. This package's own progress messages
are unaffected, because they are paced by `ProgressLogIntervalSeconds` rather than by the retry 
interval - which is exactly why the two settings are separate.

## Before you set it to wait forever

Waiting indefinitely is the right default for *this check*, but it is not free, and the trade-off is
worth making deliberately.

- **The boot thread blocks.** Umbraco's `IDatabaseAvailabilityCheck` is synchronous and runs during
  startup, so while it waits, the application is not serving anything - not even a health endpoint.
  This is equally true of Umbraco's own check; this package just makes the wait longer.
- **Your platform may lose patience first.** Kubernetes startup probes, Azure App Service warm-up,
  and container health checks all have their own timeouts. A boot that waits an hour will be killed
  and restarted long before that, which is usually fine - the restart simply tries again - but it
  does mean the *effective* timeout is whichever is shorter. If you need the wait to actually
  complete, raise the platform's startup timeout to match.
- **A timeout is a useful signal.** Setting `TimeoutSeconds` to a little more than your worst
  expected outage gives you a crash with a clear reason, which some teams prefer to a process that
  hangs. The default `EscalateToErrorAfterSeconds` exists so that you can keep waiting *and* get
  alerted.

## What it deliberately does not do

**It does not retry configuration errors.** Umbraco treats a `DbException` - the database refusing,
timing out, or not resolving - as "not available yet", and that is exactly the transient case this
package is built to ride out. Anything else, such as a malformed connection string or an unknown
provider, is thrown. No amount of waiting fixes a typo, and failing immediately with the real
exception is far more useful than a boot that hangs forever for a reason nobody can see.

## Compatibility

| Package version | Umbraco | .NET |
| --- | --- | --- |
| 1.x | 17.x | 10.0 |

`IDatabaseAvailabilityCheck` was introduced in Umbraco 16.2.0, so there is no sensible way to back-port
this to 16.1 or earlier. I've made a deliberate choice to target Umbraco 17 as the earliest supported 
version because that is LTS.

## Building

```bash
dotnet build
dotnet test
dotnet pack src/Integrious.Umbraco.SaneDatabaseAvailabilityCheck -c Release
```

## Licence

[MIT](LICENSE). Copyright (c) 2026 Integrious Ltd.
