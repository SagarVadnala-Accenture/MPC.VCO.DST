using Microsoft.Extensions.Configuration;
using MPC.VCO.DST.Application.Exceptions;
using Microsoft.Data.SqlClient;

namespace MPC.VCO.DST.Infrastructure.Configuration
{
    internal static class SqlOptimizerOptions
    {
        public static string BuildConnectionString(IConfiguration configuration)
        {
            string server = RequiredSetting(configuration, "SQL_SERVER");
            string database = RequiredSetting(configuration, "SQL_DATABASE");
            string username = RequiredSetting(configuration, "SQL_USERNAME");
            string password = RequiredSetting(configuration, "SQL_PASSWORD");

            return new SqlConnectionStringBuilder
            {
                DataSource = server.StartsWith("tcp:", StringComparison.OrdinalIgnoreCase)
                    ? server
                    : $"tcp:{server},1433",
                InitialCatalog = database,
                UserID = username,
                Password = password,
                Encrypt = true,
                TrustServerCertificate = false,
                ConnectTimeout = 15
            }.ConnectionString;
        }

        private static string RequiredSetting(IConfiguration configuration, string name) =>
            configuration[name] is { Length: > 0 } value
                ? value
                : throw new ForecastUnavailableException($"Required SQL setting '{name}' is not configured.");
    }

}
