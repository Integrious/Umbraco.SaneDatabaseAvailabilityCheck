using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Umbraco.Cms.Infrastructure.Persistence;
using Xunit;

namespace Integrious.Umbraco.SaneDatabaseAvailabilityCheck.Tests;

public class SaneDatabaseAvailabilityCheckTests
{
    [Fact]
    public void WhenDatabaseIsImmediatelyAvailable_ReturnsTrueWithoutWaiting()
    {
        var harness = Harness.DatabaseAvailableAfter(TimeSpan.Zero, new SaneDatabaseAvailabilityCheckOptions());

        Assert.True(harness.Run());

        Assert.Empty(harness.Sleeps);
        Assert.Equal(TimeSpan.Zero, harness.Elapsed);
    }

    [Fact]
    public void WhenDatabaseBecomesAvailableShortly_ReturnsTrueAndSaysSo()
    {
        var harness = Harness.DatabaseAvailableAfter(
            TimeSpan.FromSeconds(10),
            new SaneDatabaseAvailabilityCheckOptions());

        Assert.True(harness.Run());

        Assert.Equal(TimeSpan.FromSeconds(10), harness.Elapsed);
        Assert.Contains(
            harness.Logger.MessagesAt(LogLevel.Information),
            m => m.Contains("became available", StringComparison.Ordinal));
    }

    // The whole point of the package: Umbraco's default gives up after four seconds.
    [Fact]
    public void WithNoTimeoutConfigured_KeepsWaitingFarBeyondTheUmbracoDefault()
    {
        var options = new SaneDatabaseAvailabilityCheckOptions
        {
            RetryIntervalSeconds = 5,
        };

        var harness = Harness.DatabaseAvailableAfter(TimeSpan.FromHours(2), options);

        Assert.Null(options.TimeoutSeconds);
        Assert.True(harness.Run());
        Assert.Equal(TimeSpan.FromHours(2), harness.Elapsed);
    }

    [Fact]
    public void WhenTimeoutElapses_ReturnsFalseAtExactlyTheTimeout()
    {
        var options = new SaneDatabaseAvailabilityCheckOptions
        {
            TimeoutSeconds = 30,
        };

        var harness = Harness.DatabaseNeverAvailable(options);

        Assert.False(harness.Run());

        Assert.Equal(TimeSpan.FromSeconds(30), harness.Elapsed);
        Assert.Contains(
            harness.Logger.MessagesAt(LogLevel.Error),
            m => m.Contains("Giving up", StringComparison.Ordinal));
    }

    [Fact]
    public void WhenDatabaseBecomesAvailableBeforeTheTimeout_ReturnsTrue()
    {
        var options = new SaneDatabaseAvailabilityCheckOptions
        {
            TimeoutSeconds = 60,
        };

        var harness = Harness.DatabaseAvailableAfter(TimeSpan.FromSeconds(10), options);

        Assert.True(harness.Run());
        Assert.Equal(TimeSpan.FromSeconds(10), harness.Elapsed);
    }

    // A retry interval longer than the timeout must not extend the timeout: the timeout is a promise
    // about total boot delay, not a lower bound on it.
    [Fact]
    public void WithRetryIntervalLongerThanTheTimeout_DoesNotWaitPastTheTimeout()
    {
        var options = new SaneDatabaseAvailabilityCheckOptions
        {
            TimeoutSeconds = 5,
            RetryIntervalSeconds = 60,
        };

        var harness = Harness.DatabaseNeverAvailable(options);

        Assert.False(harness.Run());

        Assert.Equal(TimeSpan.FromSeconds(5), harness.Elapsed);
        Assert.Equal([TimeSpan.FromSeconds(5)], harness.Sleeps);
    }

    [Fact]
    public void RespectsTheConfiguredRetryInterval()
    {
        var options = new SaneDatabaseAvailabilityCheckOptions
        {
            TimeoutSeconds = 20,
            RetryIntervalSeconds = 5,
        };

        var harness = Harness.DatabaseNeverAvailable(options);

        Assert.False(harness.Run());

        // Attempts at 0, 5, 10, 15 and 20 seconds; the last one trips the timeout.
        Assert.Equal(4, harness.Sleeps.Count);
        Assert.All(harness.Sleeps, s => Assert.Equal(TimeSpan.FromSeconds(5), s));
    }

    [Fact]
    public void LogsProgressAtTheConfiguredInterval()
    {
        var options = new SaneDatabaseAvailabilityCheckOptions
        {
            TimeoutSeconds = 60,
            ProgressLogIntervalSeconds = 30,
            EscalateToErrorAfterSeconds = null,
        };

        var harness = Harness.DatabaseNeverAvailable(options);

        Assert.False(harness.Run());

        // One at 30 seconds, one at 60; sixty one-second attempts, but only two reports.
        Assert.Equal(2, harness.Logger.CountAt(LogLevel.Information));
        Assert.All(
            harness.Logger.MessagesAt(LogLevel.Information),
            m => Assert.Contains("Still waiting", m, StringComparison.Ordinal));
    }

    [Fact]
    public void EscalatesProgressToErrorAfterTheConfiguredThreshold()
    {
        var options = new SaneDatabaseAvailabilityCheckOptions
        {
            TimeoutSeconds = 40,
            ProgressLogIntervalSeconds = 10,
            EscalateToErrorAfterSeconds = 25,
        };

        var harness = Harness.DatabaseNeverAvailable(options);

        Assert.False(harness.Run());

        // Progress at 10s and 20s stays informational; 30s and 40s are past the threshold.
        Assert.Equal(2, harness.Logger.CountAt(LogLevel.Information));
        Assert.Equal(
            2,
            harness.Logger.MessagesAt(LogLevel.Error)
                .Count(m => m.Contains("Still waiting", StringComparison.Ordinal)));
    }

    [Fact]
    public void WithEscalationDisabled_KeepsProgressAtInformationIndefinitely()
    {
        var options = new SaneDatabaseAvailabilityCheckOptions
        {
            TimeoutSeconds = 40,
            ProgressLogIntervalSeconds = 10,
            EscalateToErrorAfterSeconds = null,
        };

        var harness = Harness.DatabaseNeverAvailable(options);

        Assert.False(harness.Run());

        Assert.Equal(4, harness.Logger.CountAt(LogLevel.Information));

        // Only the final "giving up" message is an error.
        Assert.Equal(1, harness.Logger.CountAt(LogLevel.Error));
    }

    // Progress reporting is time-based, so a retry interval far longer than the reporting interval
    // must not emit one backdated message per interval missed.
    [Fact]
    public void WhenRetryIntervalExceedsProgressInterval_DoesNotEmitABurstOfBackdatedMessages()
    {
        var options = new SaneDatabaseAvailabilityCheckOptions
        {
            TimeoutSeconds = 300,
            RetryIntervalSeconds = 100,
            ProgressLogIntervalSeconds = 10,
            EscalateToErrorAfterSeconds = null,
        };

        var harness = Harness.DatabaseNeverAvailable(options);

        Assert.False(harness.Run());

        // Attempts at 0, 100, 200 and 300 seconds; the last three each report once, not ten times.
        Assert.Equal(3, harness.Logger.CountAt(LogLevel.Information));
    }

    [Fact]
    public void OnTheFirstFailureOnly_ExplainsWhatItIsAboutToDo()
    {
        var options = new SaneDatabaseAvailabilityCheckOptions
        {
            TimeoutSeconds = 10,
        };

        var harness = Harness.DatabaseNeverAvailable(options);

        Assert.False(harness.Run());

        Assert.Equal(1, harness.Logger.CountAt(LogLevel.Debug));
        Assert.Contains(
            harness.Logger.MessagesAt(LogLevel.Debug),
            m => m.Contains("Could not immediately connect", StringComparison.Ordinal));
    }

    // Umbraco returns false for a DbException, which is the transient case worth waiting out.
    // Anything else is a configuration fault, and waiting forever would hide it.
    [Fact]
    public void WhenTestingTheConnectionThrows_PropagatesRatherThanRetryingForever()
    {
        var databaseFactory = Substitute.For<IUmbracoDatabaseFactory>();
        databaseFactory.CanConnect.Returns(_ => throw new InvalidOperationException("Unknown provider."));

        var harness = Harness.For(databaseFactory, new SaneDatabaseAvailabilityCheckOptions());

        var exception = Assert.Throws<InvalidOperationException>(() => harness.Run());
        Assert.Equal("Unknown provider.", exception.Message);
    }

    [Fact]
    public void WithoutADatabaseFactory_Throws()
    {
        var harness = Harness.DatabaseNeverAvailable(new SaneDatabaseAvailabilityCheckOptions());

        Assert.Throws<ArgumentNullException>(() => harness.Check.IsDatabaseAvailable(null!));
    }

    [Fact]
    public void WithInvalidOptions_FailsOnConstructionRatherThanMidWait()
    {
        var options = new SaneDatabaseAvailabilityCheckOptions
        {
            TimeoutSeconds = 0,
        };

        var validator = new SaneDatabaseAvailabilityCheckOptionsValidator();
        IOptions<SaneDatabaseAvailabilityCheckOptions> wrapped =
            new ValidatingOptions<SaneDatabaseAvailabilityCheckOptions>(options, validator);

        Assert.Throws<OptionsValidationException>(() =>
            new SaneDatabaseAvailabilityCheck(
                new RecordingLogger<SaneDatabaseAvailabilityCheck>(),
                wrapped));
    }

    /// <summary>
    /// Drives the check against a fake clock. Sleeping advances that clock, so an hour of waiting
    /// costs a test nothing, and the recorded sleeps show exactly how the check paced itself.
    /// </summary>
    private sealed class Harness
    {
        private const int MaximumSleeps = 100_000;

        private readonly long _startedAt;

        private Harness(
            IUmbracoDatabaseFactory databaseFactory,
            SaneDatabaseAvailabilityCheckOptions options)
        {
            DatabaseFactory = databaseFactory;
            Clock = new FakeTimeProvider();
            Logger = new RecordingLogger<SaneDatabaseAvailabilityCheck>();

            Check = new SaneDatabaseAvailabilityCheck(Logger, Options.Create(options))
            {
                TimeProvider = Clock,
                Sleep = duration =>
                {
                    if (Sleeps.Count >= MaximumSleeps)
                    {
                        throw new InvalidOperationException(
                            $"The check slept {MaximumSleeps} times, which means the test would never finish.");
                    }

                    Sleeps.Add(duration);
                    Clock.Advance(duration);
                },
            };

            _startedAt = Clock.GetTimestamp();
        }

        // Only what the tests themselves reach for is public. The clock and the factory are
        // plumbing: the clock is read by Elapsed and by the DatabaseAvailableAfter factory below,
        // and the factory only by Run.
        private FakeTimeProvider Clock { get; }

        private IUmbracoDatabaseFactory DatabaseFactory { get; }

        public SaneDatabaseAvailabilityCheck Check { get; }

        public RecordingLogger<SaneDatabaseAvailabilityCheck> Logger { get; }

        public List<TimeSpan> Sleeps { get; } = [];

        public TimeSpan Elapsed => Clock.GetElapsedTime(_startedAt);

        public static Harness For(
            IUmbracoDatabaseFactory databaseFactory,
            SaneDatabaseAvailabilityCheckOptions options) => new(databaseFactory, options);

        public static Harness DatabaseNeverAvailable(SaneDatabaseAvailabilityCheckOptions options)
        {
            var databaseFactory = Substitute.For<IUmbracoDatabaseFactory>();
            databaseFactory.CanConnect.Returns(false);
            return new Harness(databaseFactory, options);
        }

        public static Harness DatabaseAvailableAfter(
            TimeSpan delay,
            SaneDatabaseAvailabilityCheckOptions options)
        {
            var databaseFactory = Substitute.For<IUmbracoDatabaseFactory>();
            var harness = new Harness(databaseFactory, options);
            var availableFrom = harness.Clock.GetUtcNow() + delay;
            databaseFactory.CanConnect.Returns(_ => harness.Clock.GetUtcNow() >= availableFrom);
            return harness;
        }

        public bool Run() => Check.IsDatabaseAvailable(DatabaseFactory);
    }

    /// <summary>
    /// The smallest thing that behaves like the container's validating options, so that the check's
    /// eager read of <see cref="IOptions{TOptions}.Value"/> can be tested without a container.
    /// </summary>
    private sealed class ValidatingOptions<TOptions>(TOptions value, IValidateOptions<TOptions> validator)
        : IOptions<TOptions>
        where TOptions : class
    {
        public TOptions Value
        {
            get
            {
                var result = validator.Validate(Options.DefaultName, value);
                return result.Failed
                    ? throw new OptionsValidationException(
                        Options.DefaultName,
                        typeof(TOptions),
                        result.Failures)
                    : value;
            }
        }
    }
}
