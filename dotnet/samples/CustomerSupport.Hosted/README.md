# CustomerSupport Hosted Workflow

Deploys the CustomerSupport multi-agent workflow to Azure AI Foundry. This is
**Approach B** — all agents and the workflow run server-side in Foundry, with
tools provided via a [Hosted MCP Server](../CustomerSupport.McpServer/).

## Architecture

```
┌─────────────────────┐      ┌──────────────────────┐
│  This Program       │      │  Azure AI Foundry     │
│  (one-time setup)   │─────▶│  ┌────────────────┐  │
│  Registers agents   │      │  │ Workflow Agent  │  │
│  + workflow in      │      │  │ (YAML-based)   │  │
│  Foundry, then runs │      │  └───────┬────────┘  │
│  interactive session│      │          │            │
└─────────────────────┘      │  ┌───────▼────────┐  │
                             │  │ Child Agents   │  │
                             │  │ (6 agents)     │  │
                             │  └───────┬────────┘  │
                             │          │            │
                             │  ┌───────▼────────┐  │
                             │  │ MCP Tool Calls │  │
                             │  └───────┬────────┘  │
                             └──────────┼────────────┘
                                        │
                             ┌──────────▼────────────┐
                             │  CustomerSupport      │
                             │  MCP Server           │
                             │  (Ticketing Tools)    │
                             └───────────────────────┘
```

## Agents Registered

| Agent | Tools (via MCP) | Structured Inputs |
|-------|----------------|-------------------|
| SelfServiceAgent | — | — |
| TicketingAgent | CreateTicket | IssueDescription, AttemptedResolutionSteps |
| TicketRoutingAgent | GetTicket | — |
| WindowsSupportAgent | GetTicket | IssueDescription, AttemptedResolutionSteps |
| TicketResolutionAgent | ResolveTicket | TicketId, ResolutionSummary |
| TicketEscalationAgent | GetTicket, SendNotification | TicketId, IssueDescription, ResolutionSummary |

## Configuration

Set the following as user secrets or environment variables:

| Setting | Description |
|---------|-------------|
| `AZURE_AI_PROJECT_ENDPOINT` | Your Foundry project endpoint |
| `AZURE_AI_MODEL_DEPLOYMENT_NAME` | Model deployment name (e.g., `gpt-5.1`) |
| `MCP_SERVER_URL` | (Optional) MCP server URL, overridden by `--mcp-url` CLI arg |

## Running

**Prerequisites:**
- The [CustomerSupport.McpServer](../CustomerSupport.McpServer/) must be running
  and accessible from Azure AI Foundry (deployed or tunneled).
- Azure CLI authenticated (`az login`).

```bash
# Start the MCP server (see CustomerSupport.McpServer/README.md)
# Then run the hosted workflow:
cd dotnet/samples/03-workflows/Declarative/CustomerSupport.Hosted
dotnet run -- --mcp-url "https://<your-public-mcp-url>/mcp" "My laptop won't connect to WiFi"
```

Or interactively (prompted for input):

```bash
dotnet run -- --mcp-url "https://<your-public-mcp-url>/mcp"
```

## Differences from Local CustomerSupport Sample

| Aspect | Local (`CustomerSupport/`) | Hosted (`CustomerSupport.Hosted/`) |
|--------|---------------------------|-------------------------------------|
| Agent execution | Client-side via `AzureAgentProvider` | Server-side in Foundry |
| Tools | In-process `AIFunction` | `HostedMcpServerTool` → external MCP server |
| Workflow | Local `WorkflowRunner` | Foundry-hosted `WorkflowAgentDefinition` |
| State | Local memory | Foundry-managed conversations |
