using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Infrastructure.Persistence;
using Umbraco.Extensions;

namespace Integrious.Umbraco.SaneDatabaseAvailabilityCheck;

/// <summary>
/// Replaces Umbraco's default database availability check with
/// <see cref="SaneDatabaseAvailabilityCheck"/>.
/// </summary>
/// <remarks>
/// Composers run after Umbraco has registered its own core services, and <c>AddUnique</c> replaces
/// any existing registration, so installing the package is enough to take effect.
/// </remarks>
public sealed class SaneDatabaseAvailabilityCheckComposer : IComposer
{
    /// <inheritdoc/>
    public void Compose(IUmbracoBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddOptions<SaneDatabaseAvailabilityCheckOptions>()
            .Bind(builder.Config.GetSection(SaneDatabaseAvailabilityCheckOptions.SectionName));

        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<
                IValidateOptions<SaneDatabaseAvailabilityCheckOptions>,
                SaneDatabaseAvailabilityCheckOptionsValidator>());

        builder.Services.AddUnique<IDatabaseAvailabilityCheck, SaneDatabaseAvailabilityCheck>();
    }
}
