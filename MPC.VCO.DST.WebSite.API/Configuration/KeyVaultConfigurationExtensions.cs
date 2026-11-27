using Azure.Extensions.AspNetCore.Configuration.Secrets;
using Azure.Identity;

namespace MPC.VCO.DST.WebSite.API.Configuration
{
    public static class KeyVaultConfigurationExtensions
    {
        public static IConfigurationBuilder AddOptimizerKeyVaultConfiguration(
    this IConfigurationBuilder configurationBuilder,
    IConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(configurationBuilder);
            ArgumentNullException.ThrowIfNull(configuration);

            string? keyVaultUri = configuration["KEY_VAULT_URI"];
            if (string.IsNullOrWhiteSpace(keyVaultUri))
            {
                return configurationBuilder;
            }

            if (!Uri.TryCreate(keyVaultUri, UriKind.Absolute, out Uri? vaultUri)
                || vaultUri.Scheme != Uri.UriSchemeHttps)
            {
                throw new InvalidOperationException("KEY_VAULT_URI must be an absolute HTTPS URI.");
            }

            return configurationBuilder.AddAzureKeyVault(
                vaultUri,
                new DefaultAzureCredential(),
                new AzureKeyVaultConfigurationOptions
                {
                    Manager = new OptimizerKeyVaultSecretManager()
                });
        }
    }
}
