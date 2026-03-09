# CustomerSupport MCP Server

An ASP.NET Core application that exposes the CustomerSupport ticketing tools as an
[MCP (Model Context Protocol)](https://modelcontextprotocol.io/) server over HTTP
Streamable transport.

## Tools Exposed

| Tool | Description |
|------|-------------|
| `CreateTicket` | Create a ticket in Azure DevOps and return its identifier |
| `GetTicket` | Retrieve a ticket by identifier from Azure DevOps |
| `ResolveTicket` | Resolve an existing ticket given its identifier |
| `SendNotification` | Send an email notification to escalate ticket engagement |

> **Note:** These are mock implementations using an in-memory store — identical
> to the `TicketingPlugin` used in the local `CustomerSupport` sample.

## Running Locally

```bash
cd dotnet/samples/03-workflows/Declarative/CustomerSupport.McpServer
dotnet run
```

By default the server listens on `http://localhost:5000/mcp`. You can configure
the URL via `launchSettings.json` or the `--urls` argument:

```bash
dotnet run --urls "http://localhost:5200"
```

The MCP endpoint will be at `http://localhost:5200/mcp`.

## Deploying to Azure

For the `CustomerSupport.Hosted` workflow to work end-to-end, the MCP server
must be reachable from Azure AI Foundry. Options include:

1. **Azure Container Apps** — containerize and deploy
2. **Azure App Service** — publish as a web app
3. **Dev tunnel / ngrok** — for local development, expose via tunnel

## Usage with CustomerSupport.Hosted

Start this server first, then run the hosted workflow deployer:

```bash
# Terminal 1: Start the MCP server
cd CustomerSupport.McpServer
dotnet run --urls "http://localhost:5200"

# Terminal 2: Run the hosted workflow (pass the MCP URL)
cd CustomerSupport.Hosted
dotnet run -- --mcp-url "https://<your-public-url>/mcp"
```
