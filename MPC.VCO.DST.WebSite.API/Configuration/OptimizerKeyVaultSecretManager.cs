using Azure.Extensions.AspNetCore.Configuration.Secrets;
using Azure.Security.KeyVault.Secrets;

namespace MPC.VCO.DST.WebSite.API.Configuration;

internal sealed class OptimizerKeyVaultSecretManager : KeyVaultSecretManager
{
    private static readonly IReadOnlyDictionary<string, string> ConfigurationKeys =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["sql-server-name"] = "SQL_SERVER",
            ["sql-database-name"] = "SQL_DATABASE",
            ["sql-username"] = "SQL_USERNAME",
            ["sql-password"] = "SQL_PASSWORD"
        };

    public override bool Load(SecretProperties secret) =>
        ConfigurationKeys.ContainsKey(secret.Name);

    public override string GetKey(KeyVaultSecret secret) =>
        ConfigurationKeys[secret.Name];
}