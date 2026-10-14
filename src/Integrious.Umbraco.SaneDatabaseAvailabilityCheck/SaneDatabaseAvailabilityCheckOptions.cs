namespace Integrious.Umbraco.SaneDatabaseAvailabilityCheck;

/// <summary>
/// Configures how long, and how often, Umbraco waits for its database to become available on boot.
/// </summary>
/// <remarks>
/// Bound from the <c>Umbraco:DatabaseAvailabilityCheck</c> section of <c>appsettings.json</c>.
/// </remarks>
public sealed class SaneDatabaseAvailabilityCheckOptions
{
    /// <summary>
    /// The configuration section these options are bound from.
    /// </summary>
    public const string SectionName = "Umbraco:DatabaseAvailabilityCheck";

    /// <summary>
    /// Gets or sets the total number of seconds to keep checking for the database before giving up.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Defaults to <c>null</c>, meaning the check never times out and Umbraco waits indefinitely
    /// for the database to become available.
    /// </para>
    /// <para>
    /// The timeout is measured from the start of the first check, not from the last one, so it is a
    /// ceiling on total boot delay. When it is reached the check gives up and Umbraco raises a
    /// <c>BootFailedException</c>, exactly as it does when the default check fails.
    /// </para>
    /// <para>
    /// If set, must be <c>1</c> or greater.
    /// </para>
    /// </remarks>
    public int? TimeoutSeconds { get; set; }

    /// <summary>
    /// Gets or sets the number of seconds to wait between checks. Defaults to <c>1</c>, matching
    /// Umbraco's default check.
    /// </summary>
    /// <remarks>
    /// Umbraco logs a warning, with the underlying exception, on every failed connection attempt.
    /// Raising this value is the simplest way to reduce that noise while waiting for a slow database.
    /// </remarks>
    public int RetryIntervalSeconds { get; set; } = 1;

    /// <summary>
    /// Gets or sets how often, in seconds, to log a message reporting that the database is still
    /// unavailable and the check is still waiting. Defaults to <c>30</c>.
    /// </summary>
    /// <remarks>
    /// This is deliberately independent of <see cref="RetryIntervalSeconds"/>: the check can poll
    /// frequently while reporting infrequently.
    /// </remarks>
    public int ProgressLogIntervalSeconds { get; set; } = 30;

    /// <summary>
    /// Gets or sets the number of seconds after which the progress messages described by
    /// <see cref="ProgressLogIntervalSeconds"/> are logged at <c>Error</c> rather than
    /// <c>Information</c>. Defaults to <c>60</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A database that takes a few seconds to wake up is routine; one that has not answered after a
    /// minute is something an operator should be paged about. Escalating the level lets monitoring
    /// alert on a stalled boot even though the check itself is, by design, still waiting patiently.
    /// </para>
    /// <para>
    /// Set to <c>null</c> to never escalate. If set, must be <c>0</c> or greater.
    /// </para>
    /// </remarks>
    public int? EscalateToErrorAfterSeconds { get; set; } = 60;
}
