using Azure.AI.OpenAI;
using Azure.Identity;
using OpenAI.Chat;

namespace Common;

/// <summary>
/// Factory for creating a ChatClient backed by an Azure AI Foundry project endpoint.
/// Supports both Foundry project endpoints (https://x.services.ai.azure.com/api/projects/y)
/// and direct Azure OpenAI endpoints (https://x.openai.azure.com/).
/// </summary>
public static class FoundryClientFactory
{
    /// <summary>
    /// Creates a <see cref="ChatClient"/> using the Foundry project endpoint and model deployment.
    /// </summary>
    /// <param name="endpoint">
    /// The Azure AI Foundry project endpoint. If null, reads from AZURE_AI_PROJECT_ENDPOINT env var.
    /// For Foundry endpoints (services.ai.azure.com), the /api/projects/... suffix is automatically
    /// stripped since AzureOpenAIClient needs the base resource URL.
    /// </param>
    /// <param name="modelDeployment">
    /// The model deployment name. If null, reads from AZURE_AI_MODEL_DEPLOYMENT_NAME env var (default: gpt-5.1).
    /// </param>
    public static ChatClient CreateChatClient(string? endpoint = null, string? modelDeployment = null)
    {
        endpoint ??= Environment.GetEnvironmentVariable("AZURE_AI_PROJECT_ENDPOINT")
            ?? throw new InvalidOperationException(
                "AZURE_AI_PROJECT_ENDPOINT environment variable is not set. " +
                "Set it to your Azure AI Foundry project endpoint, e.g. https://<resource>.services.ai.azure.com/api/projects/<project>");

        modelDeployment ??= Environment.GetEnvironmentVariable("AZURE_AI_MODEL_DEPLOYMENT_NAME") ?? "gpt-5.1";

        var credential = new ChainedTokenCredential(
            new AzureCliCredential(),
            new DefaultAzureCredential());

        // For Foundry project endpoints, strip the /api/projects/... suffix.
        // AzureOpenAIClient needs the base resource URL (e.g., https://x.services.ai.azure.com/).
        var endpointUri = new Uri(endpoint);
        if (endpointUri.AbsolutePath.Contains("/api/projects/", StringComparison.OrdinalIgnoreCase))
        {
            var baseUrl = $"{endpointUri.Scheme}://{endpointUri.Host}";
            endpointUri = new Uri(baseUrl);
        }

        var client = new AzureOpenAIClient(endpointUri, credential);
        return client.GetChatClient(modelDeployment);
    }
}
