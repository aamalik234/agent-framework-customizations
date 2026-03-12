# Agent Framework Customizations

Samples and tools for the [Microsoft Agent Framework](https://github.com/microsoft/agents) — declarative multi-agent workflows deployed to Azure AI Foundry.

## Repository Structure

```
workflow-samples/          Declarative YAML workflow definitions
dotnet/samples/            .NET sample applications
  CustomerSupport.Hosted/  Deploys agents + workflow to Azure AI Foundry
  CustomerSupport.McpServer/ MCP server exposing ticketing tools
tools/
  yaml-docx-roundtrip/     Convert between workflow YAML and Word documents
```

## Projects

### Workflow Samples

[`workflow-samples/CustomerSupport.yaml`](workflow-samples/CustomerSupport.yaml) — A declarative multi-agent workflow for customer support, featuring self-service troubleshooting, ticket creation, routing, specialized support, resolution, and escalation.

### CustomerSupport.Hosted

[README](dotnet/samples/CustomerSupport.Hosted/README.md) — A .NET console app that registers agents and the workflow in Azure AI Foundry, then runs an interactive session. All agents execute server-side with tools provided via a Hosted MCP Server.

> **Note:** This sample requires the [Microsoft Agent Framework](https://github.com/microsoft/agents) parent repository for `Microsoft.Agents.AI.Workflows.Declarative` project references. See its [README](dotnet/samples/CustomerSupport.Hosted/README.md) for setup instructions.

### CustomerSupport.McpServer

[README](dotnet/samples/CustomerSupport.McpServer/README.md) — A self-contained ASP.NET Core MCP server exposing mock ticketing tools (CreateTicket, GetTicket, ResolveTicket, SendNotification) over HTTP Streamable transport. Can be deployed to Azure Container Apps, App Service, or run locally.

### YAML ↔ Word Document Roundtrip Tools

[README](tools/yaml-docx-roundtrip/README.md) — Two .NET console apps that convert between workflow YAML and plain-English Word knowledge base documents using GPT-5.1 via Azure AI Foundry.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download) (10.0.100+)
- [Azure AI Foundry](https://ai.azure.com) project with a GPT-5.1 model deployment
- Azure CLI authenticated (`az login`) for `DefaultAzureCredential`

## License

This project is licensed under the [MIT License](LICENSE).
