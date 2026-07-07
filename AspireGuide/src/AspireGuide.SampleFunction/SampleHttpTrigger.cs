using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace AspireGuide.SampleFunction;

public class SampleHttpTrigger
{
    private readonly ILogger<SampleHttpTrigger> _logger;

    public SampleHttpTrigger(ILogger<SampleHttpTrigger> logger)
    {
        _logger = logger;
    }

    [Function(nameof(SampleHttpTrigger))]
    public IActionResult Run([HttpTrigger(AuthorizationLevel.Function, "get", "post")] HttpRequest req)
    {
        _logger.LogInformation("C# HTTP trigger function processed a request.");
        return new OkObjectResult("Welcome to Azure Functions!");
    }
}