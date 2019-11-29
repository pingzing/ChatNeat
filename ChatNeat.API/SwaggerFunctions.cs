using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Aliencube.AzureFunctions.Extensions.OpenApi.Attributes;
using Aliencube.AzureFunctions.Extensions.OpenApi;
using Aliencube.AzureFunctions.Extensions.OpenApi.Configurations;
using Newtonsoft.Json.Serialization;
using Microsoft.OpenApi;
using System.Reflection;
using Microsoft.OpenApi.Models;

namespace ChatNeat.API
{
    public class SwaggerFunctions
    {
        private readonly ILogger<SwaggerFunctions> _logger;
        private OpenApiInfo _swaggerMetadata = new OpenApiInfo
        {
            Version = "2.0.0",
            Title = "ChatNeat API",
            License = new OpenApiLicense { Name = "MIT" }
        };

        public SwaggerFunctions(ILogger<SwaggerFunctions> logger)
        {
            _logger = logger;
        }

        [FunctionName("RenderOpenApiDocument")]
        [OpenApiIgnore]
        public async Task<IActionResult> RenderOpenApiDocument(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "swagger.json")] HttpRequest req)
        {
            var helper = new DocumentHelper(new RouteConstraintFilter());
            var document = new Document(helper);
            var result = await document.InitialiseDocument()
                .AddMetadata(_swaggerMetadata)
                .AddServer(req, "api")
                .Build(Assembly.GetExecutingAssembly(), new CamelCaseNamingStrategy())
                .RenderAsync(Microsoft.OpenApi.OpenApiSpecVersion.OpenApi2_0, OpenApiFormat.Json)
                .ConfigureAwait(false);

            return new ContentResult
            {
                Content = result,
                ContentType = "application/json",
                StatusCode = StatusCodes.Status200OK
            };
        }

        [FunctionName("RenderOpenApiUI")]
        [OpenApiIgnore]
        public async Task<IActionResult> RenderOpenApiUI(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "swagger/ui")]HttpRequest req)
        {
            var ui = new SwaggerUI();
            var result = await (await ui.AddMetadata(_swaggerMetadata)
                .AddServer(req, "api")
                .BuildAsync())
                .RenderAsync("swagger.json")
                .ConfigureAwait(false);

            return new ContentResult
            {
                Content = result,
                ContentType = "text/html",
                StatusCode = StatusCodes.Status200OK
            };
        }
    }
}
