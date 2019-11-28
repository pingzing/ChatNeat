using ChatNeat.Database;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;

[assembly: FunctionsStartup(typeof(ChatNeat.API.Startup))]
namespace ChatNeat.API
{
    public class Startup : FunctionsStartup
    {
        public override void Configure(IFunctionsHostBuilder builder)
        {
            builder.Services.AddScoped<ITableClient, TableClient>(x =>
            {
                var logger = x.GetService<ILogger<TableClient>>();
                string connectionString = Environment.GetEnvironmentVariable("AzureWebJobsStorage", EnvironmentVariableTarget.Process);
                return new TableClient(connectionString, logger);
            });
        }
    }
}
