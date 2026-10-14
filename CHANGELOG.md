# Changelog

All notable changes to this project are documented here. The format is based on
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [1.0.0] - 2026-10-14

Initial release, targeting Umbraco 17.

### Added

- `SaneDatabaseAvailabilityCheck`, replacing Umbraco's default boot-time database availability check.
  It keeps checking until the database answers rather than giving up after five attempts.
- `Umbraco:DatabaseAvailabilityCheck:TimeoutSeconds` - total seconds to wait before giving up.
  Unset by default, meaning the check never times out.
- `Umbraco:DatabaseAvailabilityCheck:RetryIntervalSeconds` - seconds between checks, defaulting to 1.
- `Umbraco:DatabaseAvailabilityCheck:ProgressLogIntervalSeconds` - how often to report that it is
  still waiting, defaulting to 30.
- `Umbraco:DatabaseAvailabilityCheck:EscalateToErrorAfterSeconds` - how long to wait before those
  progress reports are logged at `Error` rather than `Information`, defaulting to 60.
- Validation of all four settings, so a mistake in `appsettings.json` fails the boot with a message
  naming the setting rather than behaving unexpectedly.
- `SaneDatabaseAvailabilityCheckComposer`, which registers the check automatically on install.

[Unreleased]: https://github.com/Integrious/Umbraco.SaneDatabaseAvailabilityCheck/compare/v1.0.0...HEAD
[1.0.0]: https://github.com/Integrious/Umbraco.SaneDatabaseAvailabilityCheck/releases/tag/v1.0.0
