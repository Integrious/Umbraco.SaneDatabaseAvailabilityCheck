using Microsoft.Extensions.Options;

namespace Integrious.Umbraco.SaneDatabaseAvailabilityCheck;

/// <summary>
/// Validates <see cref="SaneDatabaseAvailabilityCheckOptions"/>, so that a mistake in
/// <c>appsettings.json</c> fails the boot with an explanation rather than producing a check that
/// silently misbehaves.
/// </summary>
internal sealed class SaneDatabaseAvailabilityCheckOptionsValidator
    : IValidateOptions<SaneDatabaseAvailabilityCheckOptions>
{
    public ValidateOptionsResult Validate(string? name, SaneDatabaseAvailabilityCheckOptions options)
    {
        List<string>? failures = null;

        if (options.TimeoutSeconds is { } timeout && timeout < 1)
        {
            Fail(
                ref failures,
                $"{SaneDatabaseAvailabilityCheckOptions.SectionName}:{nameof(options.TimeoutSeconds)} must be 1 or greater, but was {timeout}. Omit it, or set it to null, to wait indefinitely.");
        }

        if (options.RetryIntervalSeconds < 1)
        {
            Fail(
                ref failures,
                $"{SaneDatabaseAvailabilityCheckOptions.SectionName}:{nameof(options.RetryIntervalSeconds)} must be 1 or greater, but was {options.RetryIntervalSeconds}.");
        }

        if (options.ProgressLogIntervalSeconds < 1)
        {
            Fail(
                ref failures,
                $"{SaneDatabaseAvailabilityCheckOptions.SectionName}:{nameof(options.ProgressLogIntervalSeconds)} must be 1 or greater, but was {options.ProgressLogIntervalSeconds}.");
        }

        if (options.EscalateToErrorAfterSeconds is { } escalateAfter && escalateAfter < 0)
        {
            Fail(
                ref failures,
                $"{SaneDatabaseAvailabilityCheckOptions.SectionName}:{nameof(options.EscalateToErrorAfterSeconds)} must be 0 or greater, but was {escalateAfter}. Omit it, or set it to null, to never escalate the log level.");
        }

        return failures is null
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);

        static void Fail(ref List<string>? failures, string message) => (failures ??= []).Add(message);
    }
}
