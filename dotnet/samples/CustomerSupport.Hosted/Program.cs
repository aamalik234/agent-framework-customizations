// Licensed under the MIT License. See LICENSE file in the project root for full license information.

// This program deploys the CustomerSupport workflow and its agents to Azure AI Foundry.
// Agent tools are provided via a HostedMcpServerTool reference that points to the
// CustomerSupport.McpServer endpoint. Foundry calls the MCP server directly.
//
// Usage:
//   dotnet run -- [--mcp-url <url>] [input text]
//
// Configuration:
//   AZURE_AI_PROJECT_ENDPOINT  – Foundry project endpoint (user-secret or env var)
//   AZURE_AI_MODEL_DEPLOYMENT_NAME – Model deployment (default: gpt-5.1)
//   MCP_SERVER_URL – MCP server URL (env var, overridden by --mcp-url)

using Azure.AI.Agents.Persistent;
using Azure.AI.Projects;
using Azure.AI.Projects.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using OpenAI.Responses;
using Shared.Foundry;
using Shared.Workflows;

namespace Demo.Workflows.Declarative.CustomerSupport.Hosted;

internal sealed class Program
{
    private const string DefaultMcpServerUrl = "http://localhost:5200/mcp";
    private const string McpServerName = "customer_support_ticketing";

    public static async Task Main(string[] args)
    {
        // Initialize configuration
        IConfiguration configuration = Application.InitializeConfig();
        Uri foundryEndpoint = new(configuration.GetValue(Application.Settings.FoundryEndpoint));
        string model = configuration.GetValue(Application.Settings.FoundryModel);

        // Resolve the MCP server URL from CLI args, env var, or default.
        string mcpUrl = ResolveMcpUrl(args, configuration);
        Console.ForegroundColor = ConsoleColor.DarkYellow;
        Console.WriteLine($"MCP Server URL: {mcpUrl}");
        Console.ResetColor();

        // Create the Foundry project client.
        // WARNING: DefaultAzureCredential is convenient for development but requires careful consideration in production.
        AIProjectClient aiProjectClient = new(foundryEndpoint, new DefaultAzureCredential());

        // 1. Register the six agents in Foundry (with HostedMcpServerTool for tools).
        await CreateAgentsAsync(aiProjectClient, model, mcpUrl);

        // 2. Register the workflow in Foundry from the YAML definition.
        AgentVersion workflowVersion = await CreateWorkflowAsync(aiProjectClient);

        // 3. Get interactive input.
        string workflowInput = Application.GetInput(StripCustomArgs(args));

        // 4. Run the workflow through Foundry.
        AIAgent agent = aiProjectClient.AsAIAgent(workflowVersion);
        AgentSession session = await agent.CreateSessionAsync();

        ProjectConversation conversation =
            await aiProjectClient
                .GetProjectOpenAIClient()
                .GetProjectConversationsClient()
                .CreateProjectConversationAsync()
                .ConfigureAwait(false);

        Console.WriteLine($"CONVERSATION: {conversation.Id}");

        ChatOptions chatOptions = new() { ConversationId = conversation.Id };
        ChatClientAgentRunOptions runOptions = new(chatOptions);

        // Interactive loop: keep sending user input until the workflow completes
        // or the user types "exit".
        string? currentInput = workflowInput;
        while (true)
        {
            IAsyncEnumerable<AgentResponseUpdate> updates = agent.RunStreamingAsync(currentInput, session, runOptions);

            string? lastMessageId = null;
            await foreach (AgentResponseUpdate update in updates)
            {
                if (update.MessageId != lastMessageId)
                {
                    Console.WriteLine($"\n\n{update.AuthorName ?? update.AgentId}");
                }

                lastMessageId = update.MessageId;
                Console.Write(update.Text);
            }

            Console.WriteLine();

            // Prompt for follow-up input.
            Console.ForegroundColor = ConsoleColor.DarkGreen;
            Console.Write("\nINPUT (or 'exit'): ");
            Console.ForegroundColor = ConsoleColor.White;
            currentInput = Console.ReadLine()?.Trim();
            Console.ResetColor();

            if (string.IsNullOrWhiteSpace(currentInput) ||
                currentInput.Equals("exit", StringComparison.OrdinalIgnoreCase))
            {
                break;
            }
        }
    }

    // -----------------------------------------------------------------------
    // Workflow registration
    // -----------------------------------------------------------------------

    private static async Task<AgentVersion> CreateWorkflowAsync(AIProjectClient aiProjectClient)
    {
        string workflowYaml = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "CustomerSupport.yaml"));

        WorkflowAgentDefinition workflowDefinition = WorkflowAgentDefinition.FromYaml(workflowYaml);

        return await aiProjectClient.CreateAgentAsync(
            agentName: "CustomerSupportWorkflow",
            agentDefinition: workflowDefinition,
            agentDescription: "CustomerSupport multi-agent workflow hosted in Foundry");
    }

    // -----------------------------------------------------------------------
    // Agent registration (with HostedMcpServerTool references)
    // -----------------------------------------------------------------------

    private static async Task CreateAgentsAsync(AIProjectClient aiProjectClient, string model, string mcpUrl)
    {
        await aiProjectClient.CreateAgentAsync(
            agentName: "SelfServiceAgent",
            agentDefinition: DefineSelfServiceAgent(model),
            agentDescription: "Service agent for CustomerSupport workflow");

        await aiProjectClient.CreateAgentAsync(
            agentName: "TicketingAgent",
            agentDefinition: DefineTicketingAgent(model, mcpUrl),
            agentDescription: "Ticketing agent for CustomerSupport workflow");

        await aiProjectClient.CreateAgentAsync(
            agentName: "TicketRoutingAgent",
            agentDefinition: DefineTicketRoutingAgent(model, mcpUrl),
            agentDescription: "Routing agent for CustomerSupport workflow");

        await aiProjectClient.CreateAgentAsync(
            agentName: "WindowsSupportAgent",
            agentDefinition: DefineWindowsSupportAgent(model, mcpUrl),
            agentDescription: "Windows support agent for CustomerSupport workflow");

        await aiProjectClient.CreateAgentAsync(
            agentName: "TicketResolutionAgent",
            agentDefinition: DefineResolutionAgent(model, mcpUrl),
            agentDescription: "Resolution agent for CustomerSupport workflow");

        await aiProjectClient.CreateAgentAsync(
            agentName: "TicketEscalationAgent",
            agentDefinition: DefineEscalationAgent(model, mcpUrl),
            agentDescription: "Escalation agent for CustomerSupport workflow");
    }

    // -----------------------------------------------------------------------
    // Helper: build a HostedMcpServerTool converted to ResponseTool
    // -----------------------------------------------------------------------

    private static ResponseTool McpTool(string mcpUrl, params string[] allowedTools)
    {
        HostedMcpServerTool tool = new(serverName: McpServerName, serverAddress: mcpUrl)
        {
            AllowedTools = [.. allowedTools],
            ApprovalMode = HostedMcpServerToolApprovalMode.NeverRequire,
        };

        return tool.GetService<ResponseTool>()
            ?? tool.AsOpenAIResponseTool()
            ?? throw new InvalidOperationException("Could not convert HostedMcpServerTool to ResponseTool.");
    }

    // -----------------------------------------------------------------------
    // Agent definitions — mirror the local CustomerSupport/Program.cs but
    // replace AIFunctionFactory tools with HostedMcpServerTool references.
    // -----------------------------------------------------------------------

    private static PromptAgentDefinition DefineSelfServiceAgent(string model) =>
        new(model)
        {
            Instructions =
                """
                Use your knowledge to work with the user to provide the best possible troubleshooting steps.

                - If the user confirms that the issue is resolved, then the issue is resolved. 
                - If the user reports that the issue persists, then escalate.
                """,
            TextOptions =
                new OpenAI.Responses.ResponseTextOptions
                {
                    TextFormat =
                        OpenAI.Responses.ResponseTextFormat.CreateJsonSchemaFormat(
                            "TaskEvaluation",
                            BinaryData.FromString(
                                """
                                {
                                  "type": "object",
                                  "properties": {
                                    "IsResolved": {
                                      "type": "boolean",
                                      "description": "True if the user issue/ask has been resolved."
                                    },
                                    "NeedsTicket": {
                                      "type": "boolean",
                                      "description": "True if the user issue/ask requires that a ticket be filed."
                                    },
                                    "IssueDescription": {
                                      "type": "string",
                                      "description": "A concise description of the issue."
                                    },
                                    "AttemptedResolutionSteps": {
                                      "type": "string",
                                      "description": "An outline of the steps taken to attempt resolution."
                                    }                              
                                  },
                                  "required": ["IsResolved", "NeedsTicket", "IssueDescription", "AttemptedResolutionSteps"],
                                  "additionalProperties": false
                                }
                                """),
                            jsonSchemaFormatDescription: null,
                            jsonSchemaIsStrict: true),
                }
        };

    private static PromptAgentDefinition DefineTicketingAgent(string model, string mcpUrl) =>
        new(model)
        {
            Instructions =
                """
                Always create a ticket in Azure DevOps using the available tools.

                Include the following information in the TicketSummary.

                - Issue description: {{IssueDescription}}
                - Attempted resolution steps: {{AttemptedResolutionSteps}}

                After creating the ticket, provide the user with the ticket ID.
                """,
            Tools = { McpTool(mcpUrl, "CreateTicket") },
            StructuredInputs =
            {
                ["IssueDescription"] =
                    new StructuredInputDefinition
                    {
                        IsRequired = false,
                        DefaultValue = BinaryData.FromString(@"""unknown"""),
                        Description = "A concise description of the issue.",
                    },
                ["AttemptedResolutionSteps"] =
                    new StructuredInputDefinition
                    {
                        IsRequired = false,
                        DefaultValue = BinaryData.FromString(@"""unknown"""),
                        Description = "An outline of the steps taken to attempt resolution.",
                    }
            },
            TextOptions =
                new OpenAI.Responses.ResponseTextOptions
                {
                    TextFormat =
                        OpenAI.Responses.ResponseTextFormat.CreateJsonSchemaFormat(
                            "TaskEvaluation",
                            BinaryData.FromString(
                                """
                                {
                                  "type": "object",
                                  "properties": {
                                    "TicketId": {
                                      "type": "string",
                                      "description": "The identifier of the ticket created in response to the user issue."
                                    },
                                    "TicketSummary": {
                                      "type": "string",
                                      "description": "The summary of the ticket created in response to the user issue."
                                    }
                                  },
                                  "required": ["TicketId", "TicketSummary"],
                                  "additionalProperties": false
                                }
                                """),
                            jsonSchemaFormatDescription: null,
                            jsonSchemaIsStrict: true),
                }
        };

    private static PromptAgentDefinition DefineTicketRoutingAgent(string model, string mcpUrl) =>
        new(model)
        {
            Instructions =
                """
                Determine how to route the given issue to the appropriate support team. 

                Choose from the available teams and their functions:
                - Windows Activation Support: Windows license activation issues
                - Windows Support: Windows related issues
                - Azure Support: Azure related issues
                - Network Support: Network related issues
                - Hardware Support: Hardware related issues
                - Microsoft Office Support: Microsoft Office related issues
                - General Support: General issues not related to the above categories
                """,
            Tools = { McpTool(mcpUrl, "GetTicket") },
            TextOptions =
                new OpenAI.Responses.ResponseTextOptions
                {
                    TextFormat =
                        OpenAI.Responses.ResponseTextFormat.CreateJsonSchemaFormat(
                            "TaskEvaluation",
                            BinaryData.FromString(
                                """
                                {
                                  "type": "object",
                                  "properties": {
                                    "TeamName": {
                                      "type": "string",
                                      "description": "The name of the team to route the issue"
                                    }
                                  },
                                  "required": ["TeamName"],
                                  "additionalProperties": false
                                }
                                """),
                            jsonSchemaFormatDescription: null,
                            jsonSchemaIsStrict: true),
                }
        };

    private static PromptAgentDefinition DefineWindowsSupportAgent(string model, string mcpUrl) =>
        new(model)
        {
            Instructions =
                """
                Use your knowledge to work with the user to provide the best possible troubleshooting steps
                for issues related to Windows operating system.

                - Utilize the "Attempted Resolutions Steps" as a starting point for your troubleshooting.
                - Never escalate without troubleshooting with the user.                
                - If the user confirms that the issue is resolved, then the issue is resolved. 
                - If the user reports that the issue persists, then escalate.

                Issue: {{IssueDescription}}
                Attempted Resolution Steps: {{AttemptedResolutionSteps}}
                """,
            StructuredInputs =
            {
                ["IssueDescription"] =
                    new StructuredInputDefinition
                    {
                        IsRequired = false,
                        DefaultValue = BinaryData.FromString(@"""unknown"""),
                        Description = "A concise description of the issue.",
                    },
                ["AttemptedResolutionSteps"] =
                    new StructuredInputDefinition
                    {
                        IsRequired = false,
                        DefaultValue = BinaryData.FromString(@"""unknown"""),
                        Description = "An outline of the steps taken to attempt resolution.",
                    }
            },
            Tools = { McpTool(mcpUrl, "GetTicket") },
            TextOptions =
                new OpenAI.Responses.ResponseTextOptions
                {
                    TextFormat =
                        OpenAI.Responses.ResponseTextFormat.CreateJsonSchemaFormat(
                            "TaskEvaluation",
                            BinaryData.FromString(
                                """
                                {
                                  "type": "object",
                                  "properties": {
                                    "IsResolved": {
                                      "type": "boolean",
                                      "description": "True if the user issue/ask has been resolved."
                                    },
                                    "NeedsEscalation": {
                                      "type": "boolean",
                                      "description": "True resolution could not be achieved and the issue/ask requires escalation."
                                    },
                                    "ResolutionSummary": {
                                      "type": "string",
                                      "description": "The summary of the steps that led to resolution."
                                    }
                                  },
                                  "required": ["IsResolved", "NeedsEscalation", "ResolutionSummary"],
                                  "additionalProperties": false
                                }
                                """),
                            jsonSchemaFormatDescription: null,
                            jsonSchemaIsStrict: true),
                }
        };

    private static PromptAgentDefinition DefineResolutionAgent(string model, string mcpUrl) =>
        new(model)
        {
            Instructions =
                """
                Resolve the following ticket in Azure DevOps.
                Always include the resolution details.

                - Ticket ID: #{{TicketId}}
                - Resolution Summary: {{ResolutionSummary}}
                """,
            Tools = { McpTool(mcpUrl, "ResolveTicket") },
            StructuredInputs =
            {
                ["TicketId"] =
                    new StructuredInputDefinition
                    {
                        IsRequired = false,
                        DefaultValue = BinaryData.FromString(@"""unknown"""),
                        Description = "The identifier of the ticket being resolved.",
                    },
                ["ResolutionSummary"] =
                    new StructuredInputDefinition
                    {
                        IsRequired = false,
                        DefaultValue = BinaryData.FromString(@"""unknown"""),
                        Description = "The steps taken to resolve the issue.",
                    }
            }
        };

    private static PromptAgentDefinition DefineEscalationAgent(string model, string mcpUrl) =>
        new(model)
        {
            Instructions =
                """
                You escalate the provided issue to human support team by sending an email if the issue is not resolved.

                Here are some additional details that might help:
                - TicketId : {{TicketId}}
                - IssueDescription : {{IssueDescription}}
                - AttemptedResolutionSteps : {{AttemptedResolutionSteps}}

                Before escalating, gather the user's email address for follow-up.
                If not known, ask the user for their email address so that the support team can reach them when needed.

                When sending the email, include the following details:
                - To: support@contoso.com
                - Cc: user's email address
                - Subject of the email: "Support Ticket - {TicketId} - [Compact Issue Description]"
                - Body: 
                  - Issue description
                  - Attempted resolution steps
                  - User's email address
                  - Any other relevant information from the conversation history

                Assure the user that their issue will be resolved and provide them with a ticket ID for reference.
                """,
            Tools =
            {
                McpTool(mcpUrl, "GetTicket"),
                McpTool(mcpUrl, "SendNotification"),
            },
            StructuredInputs =
            {
                ["TicketId"] =
                    new StructuredInputDefinition
                    {
                        IsRequired = false,
                        DefaultValue = BinaryData.FromString(@"""unknown"""),
                        Description = "The identifier of the ticket being escalated.",
                    },
                ["IssueDescription"] =
                    new StructuredInputDefinition
                    {
                        IsRequired = false,
                        DefaultValue = BinaryData.FromString(@"""unknown"""),
                        Description = "A concise description of the issue.",
                    },
                ["ResolutionSummary"] =
                    new StructuredInputDefinition
                    {
                        IsRequired = false,
                        DefaultValue = BinaryData.FromString(@"""unknown"""),
                        Description = "An outline of the steps taken to attempt resolution.",
                    }
            },
            TextOptions =
                new OpenAI.Responses.ResponseTextOptions
                {
                    TextFormat =
                        OpenAI.Responses.ResponseTextFormat.CreateJsonSchemaFormat(
                            "TaskEvaluation",
                            BinaryData.FromString(
                                """
                                {
                                  "type": "object",
                                  "properties": {
                                    "IsComplete": {
                                      "type": "boolean",
                                      "description": "Has the email been sent and no more user input is required."
                                    },
                                    "UserMessage": {
                                      "type": "string",
                                      "description": "A natural language message to the user."
                                    }
                                  },
                                  "required": ["IsComplete", "UserMessage"],
                                  "additionalProperties": false
                                }
                                """),
                            jsonSchemaFormatDescription: null,
                            jsonSchemaIsStrict: true),
                }
        };

    // -----------------------------------------------------------------------
    // CLI helpers
    // -----------------------------------------------------------------------

    private static string ResolveMcpUrl(string[] args, IConfiguration configuration)
    {
        // Check for --mcp-url <value> in CLI args.
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i].Equals("--mcp-url", StringComparison.OrdinalIgnoreCase))
            {
                return args[i + 1];
            }
        }

        // Fallback to env var / config.
        string? url = configuration["MCP_SERVER_URL"];
        return url ?? DefaultMcpServerUrl;
    }

    /// <summary>
    /// Remove custom flags (--mcp-url) so the remaining args can be passed to <see cref="Application.GetInput"/>.
    /// </summary>
    private static string[] StripCustomArgs(string[] args)
    {
        List<string> result = [];
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i].Equals("--mcp-url", StringComparison.OrdinalIgnoreCase))
            {
                i++; // skip the value too
                continue;
            }

            result.Add(args[i]);
        }

        return [.. result];
    }
}
