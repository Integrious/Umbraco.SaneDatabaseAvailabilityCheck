using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Infrastructure.Persistence;
using Xunit;

namespace Integrious.Umbraco.SaneDatabaseAvailabilityCheck.Tests;

public class SaneDatabaseAvailabilityCheckComposerTests
{
    [Fact]
    public void Compose_RegistersTheCheckAgainstTheUmbracoInterface()
    {
        ServiceProvider provider = Compose();

        Assert.IsType<SaneDatabaseAvailabilityCheck>(provider.GetRequiredService<IDatabaseAvailabilityCheck>());
    }

    [Fact]
    public void Compose_ReplacesAnyExistingRegistration()
    {
        var services = NewServiceCollection();
        services.AddSingleton<IDatabaseAvailabilityCheck>(Substitute.For<IDatabaseAvailabilityCheck>());

        ServiceProvider provider = Compose(services);

        Assert.IsType<SaneDatabaseAvailabilityCheck>(provider.GetRequiredService<IDatabaseAvailabilityCheck>());
        Assert.Single(provider.GetServices<IDatabaseAvailabilityCheck>());
    }

    [Fact]
    public void Compose_WithNoConfiguration_LeavesTheCheckWaitingIndefinitely()
    {
        ServiceProvider provider = Compose();

        SaneDatabaseAvailabilityCheckOptions options =
            provider.GetRequiredService<IOptions<SaneDatabaseAvailabilityCheckOptions>>().Value;

        Assert.Null(options.TimeoutSeconds);
        Assert.Equal(1, options.RetryIntervalSeconds);
        Assert.Equal(30, options.ProgressLogIntervalSeconds);
        Assert.Equal(60, options.EscalateToErrorAfterSeconds);
    }

    // This is the contract documented in the README; if the section path changes, this breaks.
    [Fact]
    public void Compose_BindsFromTheDocumentedAppSettingsSection()
    {
        ServiceProvider provider = Compose(configuration: new Dictionary<string, string?>
        {
            ["Umbraco:DatabaseAvailabilityCheck:TimeoutSeconds"] = "600",
            ["Umbraco:DatabaseAvailabilityCheck:RetryIntervalSeconds"] = "5",
            ["Umbraco:DatabaseAvailabilityCheck:ProgressLogIntervalSeconds"] = "15",
            ["Umbraco:DatabaseAvailabilityCheck:EscalateToErrorAfterSeconds"] = "120",
        });

        SaneDatabaseAvailabilityCheckOptions options =
            provider.GetRequiredService<IOptions<SaneDatabaseAvailabilityCheckOptions>>().Value;

        Assert.Equal(600, options.TimeoutSeconds);
        Assert.Equal(5, options.RetryIntervalSeconds);
        Assert.Equal(15, options.ProgressLogIntervalSeconds);
        Assert.Equal(120, options.EscalateToErrorAfterSeconds);
    }

    [Fact]
    public void Compose_WiresUpValidation_SoBadConfigurationFailsTheBoot()
    {
        ServiceProvider provider = Compose(configuration: new Dictionary<string, string?>
        {
            ["Umbraco:DatabaseAvailabilityCheck:TimeoutSeconds"] = "0",
        });

        OptionsValidationException exception = Assert.Throws<OptionsValidationException>(
            provider.GetRequiredService<IDatabaseAvailabilityCheck>);

        Assert.Contains(
            "Umbraco:DatabaseAvailabilityCheck:TimeoutSeconds",
            string.Join(' ', exception.Failures),
            StringComparison.Ordinal);
    }

    [Fact]
    public void Compose_WithoutABuilder_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new SaneDatabaseAvailabilityCheckComposer().Compose(null!));
    }

    private static ServiceCollection NewServiceCollection()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        return services;
    }

    private static ServiceProvider Compose(
        ServiceCollection? services = null,
        Dictionary<string, string?>? configuration = null)
    {
        services ??= NewServiceCollection();

        IConfigurationRoot configurationRoot = new ConfigurationBuilder()
            .AddInMemoryCollection(configuration ?? [])
            .Build();

        IUmbracoBuilder builder = Substitute.For<IUmbracoBuilder>();
        builder.Services.Returns(services);
        builder.Config.Returns(configurationRoot);

        new SaneDatabaseAvailabilityCheckComposer().Compose(builder);

        return services.BuildServiceProvider();
    }
}
