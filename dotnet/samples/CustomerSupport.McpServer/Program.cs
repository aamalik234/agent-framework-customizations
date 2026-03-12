// Licensed under the MIT License. See LICENSE file in the project root for full license information.

// This MCP server exposes the CustomerSupport ticketing tools (CreateTicket, GetTicket,
// ResolveTicket, SendNotification) over HTTP Streamable transport. When deployed to a
// publicly-reachable URL, Azure AI Foundry agents can invoke these tools via
// HostedMcpServerTool references.

using System.ComponentModel;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Server;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Create the ticketing plugin (mock functionality – same implementation as the local sample).
TicketingPlugin plugin = new();

// Wrap each plugin method as an McpServerTool via AIFunction.
McpServerTool[] tools =
[
    McpServerTool.Create(AIFunctionFactory.Create(plugin.CreateTicket)),
    McpServerTool.Create(AIFunctionFactory.Create(plugin.GetTicket)),
    McpServerTool.Create(AIFunctionFactory.Create(plugin.ResolveTicket)),
    McpServerTool.Create(AIFunctionFactory.Create(plugin.SendNotification)),
];

// Register the MCP server with HTTP Streamable transport.
builder.Services
    .AddMcpServer()
    .WithHttpTransport()
    .WithTools(tools);

WebApplication app = builder.Build();

// Map the MCP endpoint at /mcp (default route).
app.MapMcp();

Console.ForegroundColor = ConsoleColor.Cyan;
Console.WriteLine("CustomerSupport MCP Server started.");
Console.WriteLine("Endpoint: /mcp");
Console.ResetColor();

await app.RunAsync();

// ---------------------------------------------------------------------------
// TicketingPlugin: identical mock implementation from the CustomerSupport sample
// ---------------------------------------------------------------------------

internal sealed class TicketingPlugin
{
    private readonly Dictionary<string, TicketItem> _ticketStore = [];

    [Description("Retrieve a ticket by identifier from Azure DevOps.")]
    public TicketItem? GetTicket(string id)
    {
        Trace(nameof(GetTicket));
        _ticketStore.TryGetValue(id, out TicketItem? ticket);
        return ticket;
    }

    [Description("Create a ticket in Azure DevOps and return its identifier.")]
    public string CreateTicket(string subject, string description, string notes)
    {
        Trace(nameof(CreateTicket));

        TicketItem ticket = new()
        {
            Subject = subject,
            Description = description,
            Notes = notes,
            Id = Guid.NewGuid().ToString("N"),
        };

        _ticketStore[ticket.Id] = ticket;
        return ticket.Id;
    }

    [Description("Resolve an existing ticket in Azure DevOps given its identifier.")]
    public void ResolveTicket(string id, string resolutionSummary)
    {
        Trace(nameof(ResolveTicket));

        if (_ticketStore.TryGetValue(id, out TicketItem? ticket))
        {
            ticket.Status = TicketStatus.Resolved;
        }
    }

    [Description("Send an email notification to escalate ticket engagement.")]
    public void SendNotification(string id, string email, string cc, string body)
    {
        Trace(nameof(SendNotification));
    }

    private static void Trace(string functionName)
    {
        Console.ForegroundColor = ConsoleColor.DarkMagenta;
        try
        {
            Console.WriteLine($"\nFUNCTION: {functionName}");
        }
        finally
        {
            Console.ResetColor();
        }
    }
}

internal enum TicketStatus
{
    Open,
    InProgress,
    Resolved,
    Closed,
}

internal sealed class TicketItem
{
    public TicketStatus Status { get; set; } = TicketStatus.Open;
    public string Subject { get; init; } = string.Empty;
    public string Id { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Notes { get; init; } = string.Empty;
}
