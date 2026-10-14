using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Infrastructure.Persistence;

namespace Integrious.Umbraco.SaneDatabaseAvailabilityCheck;

/// <summary>
/// An <see cref="IDatabaseAvailabilityCheck"/> that keeps checking for the database until it answers,
/// or until a configured timeout elapses.
/// </summary>
/// <remarks>
/// <para>
/// Umbraco's default check makes five attempts a second apart and then gives up, which is too little
/// patience for a database that is slow to start: an Azure SQL serverless database resuming from
/// pause, or an application container that started before its database sidecar was listening. This
/// implementation waits instead, indefinitely by default, and reports its progress to the log so that
/// a slow boot is visible rather than mysterious.
/// </para>
/// <para>
/// Exceptions thrown while testing the connection are deliberately not caught. Umbraco already
/// treats a <see cref="System.Data.Common.DbException"/> as "not available yet" and returns
/// <c>false</c>, which is the transient case this check is built to ride out. Anything else - a
/// malformed connection string, an unknown provider - is a configuration fault that no amount of
/// waiting will fix, and failing the boot immediately is far kinder than hanging forever.
/// </para>
/// </remarks>
public sealed class SaneDatabaseAvailabilityCheck : IDatabaseAvailabilityCheck
{
    private readonly ILogger<SaneDatabaseAvailabilityCheck> _logger;
    private readonly SaneDatabaseAvailabilityCheckOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="SaneDatabaseAvailabilityCheck"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="options">
    /// The configured options. These are read eagerly, so that invalid configuration fails the boot
    /// here, with an explanation, rather than part way through waiting for the database.
    /// </param>
    public SaneDatabaseAvailabilityCheck(
        ILogger<SaneDatabaseAvailabilityCheck> logger,
        IOptions<SaneDatabaseAvailabilityCheckOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _logger = logger;
        _options = options.Value;
    }

    /// <summary>
    /// Gets or sets the source of the current time.
    /// </summary>
    /// <remarks>
    /// Exposed for testing purposes, hence settable only internally.
    /// </remarks>
    internal TimeProvider TimeProvider { get; set; } = TimeProvider.System;

    /// <summary>
    /// Gets or sets the means of pausing between attempts.
    /// </summary>
    /// <remarks>
    /// Exposed for testing purposes, hence settable only internally. The check runs on the boot
    /// thread before the application is serving requests, so blocking it is both safe and the
    /// behaviour the synchronous <see cref="IDatabaseAvailabilityCheck"/> contract asks for.
    /// </remarks>
    internal Action<TimeSpan> Sleep { get; set; } = Thread.Sleep;

    /// <inheritdoc/>
    public bool IsDatabaseAvailable(IUmbracoDatabaseFactory databaseFactory)
    {
        ArgumentNullException.ThrowIfNull(databaseFactory);

        TimeSpan? timeout = _options.TimeoutSeconds is { } timeoutSeconds
            ? TimeSpan.FromSeconds(timeoutSeconds)
            : null;
        TimeSpan retryInterval = TimeSpan.FromSeconds(_options.RetryIntervalSeconds);
        TimeSpan progressLogInterval = TimeSpan.FromSeconds(_options.ProgressLogIntervalSeconds);
        TimeSpan? escalateToErrorAfter = _options.EscalateToErrorAfterSeconds is { } escalateAfterSeconds
            ? TimeSpan.FromSeconds(escalateAfterSeconds)
            : null;

        long startedAt = TimeProvider.GetTimestamp();
        TimeSpan nextProgressLogAt = progressLogInterval;
        var attempts = 0;

        while (true)
        {
            attempts++;

            if (databaseFactory.CanConnect)
            {
                if (attempts > 1)
                {
                    _logger.LogInformation(
                        "Database became available after {Attempts} attempts over {Elapsed}.",
                        attempts,
                        TimeProvider.GetElapsedTime(startedAt));
                }

                return true;
            }

            TimeSpan elapsed = TimeProvider.GetElapsedTime(startedAt);

            if (attempts == 1)
            {
                _logger.LogDebug(
                    "Could not immediately connect to the database. Will keep checking every {RetryInterval}{TimeoutDescription}.",
                    retryInterval,
                    timeout is { } firstAttemptTimeout
                        ? $" until {firstAttemptTimeout} has elapsed"
                        : " for as long as it takes, as no timeout is configured");
            }

            if (elapsed >= nextProgressLogAt)
            {
                LogLevel level = escalateToErrorAfter is { } threshold && elapsed >= threshold
                    ? LogLevel.Error
                    : LogLevel.Information;

                // Not a LoggerMessage delegate, because the level is chosen at run time.
                _logger.Log(
                    level,
                    "Still waiting for the database to become available: {Attempts} attempts over {Elapsed}{TimeoutDescription}.",
                    attempts,
                    elapsed,
                    timeout is { } progressTimeout
                        ? $", giving up after {progressTimeout}"
                        : ", with no timeout configured");

                // Catch up rather than drift, so that a long pause between attempts does not produce
                // a burst of backdated messages.
                while (nextProgressLogAt <= elapsed)
                {
                    nextProgressLogAt += progressLogInterval;
                }
            }

            if (timeout is { } limit && elapsed >= limit)
            {
                _logger.LogError(
                    "Giving up on the database after {Attempts} attempts over {Elapsed}, having reached the configured timeout of {Timeout}. Umbraco will now fail to boot.",
                    attempts,
                    elapsed,
                    limit);

                return false;
            }

            TimeSpan delay = retryInterval;
            if (timeout is { } deadline)
            {
                // Never sleep past the deadline: the timeout is a promise about total boot delay.
                TimeSpan remaining = deadline - elapsed;
                if (remaining < delay)
                {
                    delay = remaining;
                }
            }

            if (delay > TimeSpan.Zero)
            {
                Sleep(delay);
            }
        }
    }
}
