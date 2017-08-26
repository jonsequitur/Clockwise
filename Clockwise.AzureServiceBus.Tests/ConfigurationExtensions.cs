using Microsoft.Extensions.Configuration;

namespace Clockwise.AzureServiceBus.Tests
{
    internal static class ConfigurationExtensions
    {
        public static TSettings For<TSettings>(this IConfiguration configuration) => configuration
            .GetSection(typeof(TSettings).Name)
            .Get<TSettings>();
    }
}