using ChatNeat.API.Database;
using ChatNeat.API.Services;
using Microsoft.Azure.Cosmos.Table;
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
            builder.Services.AddTransient<IChatService, ChatService>();
            builder.Services.AddScoped<IChatNeatTableClient, ChatNeatTableClient>(x =>
            {
                var logger = x.GetService<ILogger<ChatNeatTableClient>>();
                string connectionString = Environment.GetEnvironmentVariable("CHATNEAT_TABLE_STORAGE_CONNECTION_STRING", EnvironmentVariableTarget.Process);
                CloudStorageAccount storageAccount = CloudStorageAccount.Parse(connectionString);
                var tableClient = storageAccount.CreateCloudTableClient();
                return new ChatNeatTableClient(tableClient, logger);
            });
        }
    }
}
