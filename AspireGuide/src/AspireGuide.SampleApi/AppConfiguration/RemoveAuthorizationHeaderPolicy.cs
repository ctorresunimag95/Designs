using Azure.Core;
using Azure.Core.Pipeline;

namespace AspireGuide.SampleApi.AppConfiguration;

public class RemoveAuthorizationHeaderPolicy : HttpPipelinePolicy
{
    public override void Process(HttpMessage message, ReadOnlyMemory<HttpPipelinePolicy> pipeline)
    {
        if (IsLocalEndpoint(message.Request.Uri))
        {
            message.Request.Headers.Remove("Authorization");
        }
        ProcessNext(message, pipeline);
    }

    public override ValueTask ProcessAsync(HttpMessage message, ReadOnlyMemory<HttpPipelinePolicy> pipeline)
    {
        if (IsLocalEndpoint(message.Request.Uri))
        {
            message.Request.Headers.Remove("Authorization");
        }
        return ProcessNextAsync(message, pipeline);
    }

    // FIXED: Accept RequestUriBuilder directly instead of System.Uri
    private static bool IsLocalEndpoint(RequestUriBuilder uriBuilder)
    {
        if (uriBuilder is null)
        {
            return false;
        }
        return uriBuilder.Host!.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            || uriBuilder.Host.Equals("127.0.0.1")
            || !uriBuilder.Host.EndsWith(".azconfig.io", StringComparison.OrdinalIgnoreCase);
    }
}
