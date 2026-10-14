using Microsoft.Extensions.Options;
using Xunit;

namespace Integrious.Umbraco.SaneDatabaseAvailabilityCheck.Tests;

public class SaneDatabaseAvailabilityCheckOptionsValidatorTests
{
    private readonly SaneDatabaseAvailabilityCheckOptionsValidator _validator = new();

    [Fact]
    public void TheDefaults_AreValid()
    {
        ValidateOptionsResult result = Validate(new SaneDatabaseAvailabilityCheckOptions());

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ATimeoutBelowOneSecond_IsRejectedWithAPointerToTheIndefiniteDefault(int timeoutSeconds)
    {
        ValidateOptionsResult result = Validate(new SaneDatabaseAvailabilityCheckOptions
        {
            TimeoutSeconds = timeoutSeconds,
        });

        Assert.True(result.Failed);
        Assert.Contains("TimeoutSeconds", result.FailureMessage, StringComparison.Ordinal);
        Assert.Contains("wait indefinitely", result.FailureMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void AnAbsentTimeout_IsValid()
    {
        ValidateOptionsResult result = Validate(new SaneDatabaseAvailabilityCheckOptions
        {
            TimeoutSeconds = null,
        });

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void ARetryIntervalBelowOneSecond_IsRejected(int retryIntervalSeconds)
    {
        ValidateOptionsResult result = Validate(new SaneDatabaseAvailabilityCheckOptions
        {
            RetryIntervalSeconds = retryIntervalSeconds,
        });

        Assert.True(result.Failed);
        Assert.Contains("RetryIntervalSeconds", result.FailureMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void AProgressLogIntervalBelowOneSecond_IsRejected()
    {
        ValidateOptionsResult result = Validate(new SaneDatabaseAvailabilityCheckOptions
        {
            ProgressLogIntervalSeconds = 0,
        });

        Assert.True(result.Failed);
        Assert.Contains("ProgressLogIntervalSeconds", result.FailureMessage, StringComparison.Ordinal);
    }

    // Zero is meaningful: escalate from the very first report.
    [Fact]
    public void AnEscalationThresholdOfZero_IsValid()
    {
        ValidateOptionsResult result = Validate(new SaneDatabaseAvailabilityCheckOptions
        {
            EscalateToErrorAfterSeconds = 0,
        });

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void ANegativeEscalationThreshold_IsRejected()
    {
        ValidateOptionsResult result = Validate(new SaneDatabaseAvailabilityCheckOptions
        {
            EscalateToErrorAfterSeconds = -1,
        });

        Assert.True(result.Failed);
        Assert.Contains("EscalateToErrorAfterSeconds", result.FailureMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void EveryProblem_IsReportedAtOnce()
    {
        ValidateOptionsResult result = Validate(new SaneDatabaseAvailabilityCheckOptions
        {
            TimeoutSeconds = 0,
            RetryIntervalSeconds = 0,
            ProgressLogIntervalSeconds = 0,
            EscalateToErrorAfterSeconds = -1,
        });

        Assert.True(result.Failed);
        Assert.Equal(4, result.Failures!.Count());
    }

    // The failure message has to name the section an operator must go and edit.
    [Fact]
    public void FailuresName_TheAppSettingsPath()
    {
        ValidateOptionsResult result = Validate(new SaneDatabaseAvailabilityCheckOptions
        {
            RetryIntervalSeconds = 0,
        });

        Assert.Contains(
            "Umbraco:DatabaseAvailabilityCheck:RetryIntervalSeconds",
            result.FailureMessage,
            StringComparison.Ordinal);
    }

    private ValidateOptionsResult Validate(SaneDatabaseAvailabilityCheckOptions options) =>
        _validator.Validate(Options.DefaultName, options);
}
