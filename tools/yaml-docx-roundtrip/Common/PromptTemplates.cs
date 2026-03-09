namespace Common;

/// <summary>
/// Prompt templates for the YAML ↔ Word document roundtrip.
/// </summary>
public static class PromptTemplates
{
    /// <summary>
    /// System prompt for Program 1: converts workflow YAML into a plain-English knowledge base document.
    /// The output should be a natural-language process description with NO agent names, NO YAML syntax,
    /// and NO technical variable references.
    /// </summary>
    public const string YamlToNarrativeSystemPrompt = """
        You are a technical writer creating a plain-English customer support process knowledge base document.
        
        You will be given a workflow definition in YAML format that describes an automated customer support process.
        Your task is to convert it into a clear, flowing narrative document written in plain English that a
        non-technical business analyst or support manager could read and understand.
        
        CRITICAL RULES:
        1. Do NOT include any agent names, system identifiers, or technical component names.
           Instead, describe each step by its business function (e.g., "the self-service troubleshooting process",
           "the ticket creation step", "the routing decision", "the specialized support process").
        2. Do NOT include any YAML syntax, code, variable names, expressions, or technical notation.
        3. Do NOT reference technical concepts like "actions", "conditions", "loops", "variables", or "expressions".
        4. DO describe the complete workflow as a business process, covering:
           - What happens when a customer first reports an issue
           - How the system attempts to help the customer resolve the issue themselves
           - What information is gathered during self-service troubleshooting
           - Under what circumstances a support ticket is created
           - How the issue description and attempted resolution steps are captured
           - How the ticket is communicated to the customer
           - How the system decides which team should handle the ticket
           - What happens when the ticket is routed to a specific team (e.g., Windows Support)
           - How the specialized support process works, including the interactive troubleshooting loop
           - What happens when the issue is resolved (ticket closure)
           - What happens when the issue cannot be resolved and needs escalation
           - How escalation works (email notification, ticket details forwarded)
        5. Describe conditions and branching as business decisions (e.g., "If the issue is resolved during
           self-service, the process ends successfully" rather than "condition check on IsResolved").
        6. Describe iterative processes as natural interactions (e.g., "The system continues working with
           the customer until the issue is resolved or it is determined that a ticket is needed").
        7. Mention specific data that flows between steps in business terms:
           - Issue description
           - Attempted resolution steps
           - Ticket identifier/number
           - Team name for routing
           - Resolution summary
           - Whether the issue was resolved or needs escalation
        8. Write in present tense, using clear paragraph structure.
        9. Do NOT use bullet points, numbered lists, or markdown formatting. Write flowing prose paragraphs.
        10. Separate major phases of the process with paragraph breaks.
        11. The document should be detailed enough that someone could reconstruct the complete workflow logic
            from the description alone, including all branching paths and data dependencies.
        
        OUTPUT FORMAT:
        Return ONLY the narrative text. Do not wrap it in markdown code fences or add any preamble.
        Start directly with the process description.
        """;

    /// <summary>
    /// Builds the system prompt for Program 2: converts a plain-English knowledge base document back into
    /// workflow YAML, using the provided agent-name mapping.
    /// </summary>
    /// <param name="agentMapping">
    /// A description of which business function maps to which agent name, e.g.:
    /// "self-service troubleshooting = SelfServiceAgent, ticket creation = TicketingAgent, ..."
    /// </param>
    public static string BuildNarrativeToYamlSystemPrompt(string agentMapping)
    {
        return $$"""
            You are an expert workflow engineer for the Microsoft Agent Framework.
            
            You will be given a plain-English knowledge base document describing a customer support process.
            Your task is to convert it into a valid workflow YAML file that follows the exact schema used by
            the Microsoft Agent Framework's declarative workflow engine.
            
            AGENT NAME MAPPING:
            The knowledge base document does NOT contain agent names. Use the following mapping to assign
            the correct agent name for each business function described in the document:
            {{agentMapping}}
            
            YAML SCHEMA REFERENCE:
            The workflow YAML must follow this exact structure and use these exact action kinds:
            
            ```
            kind: Workflow
            trigger:
              kind: OnConversationStart
              id: <workflow_id>
              actions:
                - <action list>
            ```
            
            AVAILABLE ACTION KINDS:
            
            1. InvokeAzureAgent — Invoke an AI agent
               Fields: id, agent.name, conversationId (optional), input (optional), output (optional)
               input can contain:
                 - arguments: key-value pairs passed to the agent (structured data, NOT messages)
                 - messages: PowerFx expression to construct messages (e.g., =UserMessage(...))
                 - externalLoop.when: PowerFx condition for repeating the agent call
               CRITICAL: "arguments" and "messages" are MUTUALLY EXCLUSIVE input modes.
                 - Use "arguments" when the agent needs structured key-value data (e.g., IssueDescription, TicketId).
                 - Use "messages" when the agent needs a conversational text input.
                 - NEVER combine both in the same agent invocation.
                 - NEVER add a "messages" field to an agent that receives "arguments".
               output can contain:
                 - responseObject: variable name to store structured response (e.g., Local.ServiceParameters)
                 - autoSend: true/false — whether to auto-send responses to user

            2. ConditionGroup — Conditional branching
               Fields: id, conditions (list of condition objects)
               Each condition: condition (PowerFx expression), id, actions (list of actions)
               CRITICAL: Nest the full action sequence for each branch INSIDE the condition's actions list.
               Do NOT use GotoAction to jump out to top-level actions for branch handling.
            
            3. SetVariable — Set a workflow variable
               Fields: id, variable (e.g., Local.ResolutionSteps), value (PowerFx expression)
            
            4. SendActivity — Send a message to the user
               Fields: id, activity (string with {variable} interpolation)
            
            5. GotoAction — Jump to another action by its id
               Fields: id, actionId (target action's id)
               CRITICAL: Use GotoAction SPARINGLY. Only use it to skip ahead to EndWorkflow (id: all_done)
               when the process is complete. Do NOT use GotoAction for normal sequential flow or to jump
               between different sections of the workflow. The workflow engine executes actions sequentially;
               rely on that natural flow.
            
            6. CreateConversation — Create a new conversation context
               Fields: id, conversationId (variable to store the new conversation id)
            
            7. EndWorkflow — Mark the end of the workflow
               Fields: id
            
            EXPRESSION LANGUAGE (PowerFx):
            - All expressions are prefixed with = (e.g., =Local.Variable, =Not(Local.X.IsResolved))
            - Variable scopes: Local.* (workflow-scoped), System.* (runtime-provided)
            - System.ConversationId — the main conversation id
            - Functions: Not(), And (keyword, not function), UserMessage()
            - String interpolation in SendActivity uses {Local.Variable} syntax (no = prefix)
            - Multi-line expressions use YAML block scalar |- syntax
            
            VARIABLE NAMING CONVENTIONS:
            - Use descriptive PascalCase names: Local.ServiceParameters, Local.TicketParameters, Local.RoutingParameters
            - Agent response objects should be named after what they contain: ServiceParameters, TicketParameters, etc.
            - Boolean fields in response objects: IsResolved, NeedsTicket, NeedsEscalation, IsComplete
            - Data fields: IssueDescription, AttemptedResolutionSteps, ResolutionSummary, TicketId, TeamName
            
            ACTION ID CONVENTIONS:
            - Use snake_case: service_agent, ticket_agent, routing_agent, check_if_resolved, log_ticket
            - IDs should be descriptive of the action's purpose

            CRITICAL STRUCTURAL RULES (follow these EXACTLY):

            1. LINEAR SEQUENTIAL FLOW: The workflow is a flat sequence of actions at the trigger level.
               Actions execute top-to-bottom. Do NOT create separate labeled sections connected by GotoAction jumps.
               Instead, let actions flow naturally one after another.

            2. SELF-SERVICE AGENT (first step):
               - Uses conversationId: =System.ConversationId (the main user conversation)
               - Has externalLoop.when checking Not(IsResolved) And Not(NeedsTicket)
               - Output: responseObject: Local.ServiceParameters
               - Does NOT have autoSend in output (it sends to the main conversation directly)

            3. RESOLUTION CHECK (immediately after self-service):
               - Use a ConditionGroup to check if Local.ServiceParameters.IsResolved
               - If resolved, the single nested action is GotoAction → all_done
               - This is the ONLY place GotoAction is used to skip ahead at the top level

            4. TICKET CREATION:
               - Uses input.arguments with IssueDescription and AttemptedResolutionSteps from Local.ServiceParameters
               - Does NOT use input.messages — ticket agents take structured arguments
               - Does NOT have autoSend in output
               - Output: responseObject: Local.TicketParameters

            5. CAPTURE RESOLUTION STEPS (after ticket creation):
               - SetVariable to capture Local.ResolutionSteps = Local.ServiceParameters.AttemptedResolutionSteps
               - This variable is used later for escalation fallback

            6. ROUTING AGENT:
               - Uses input.messages: =UserMessage(Local.ServiceParameters.IssueDescription)
               - This is one of the few agents that uses messages (it's a conversational classification)
               - Output: responseObject: Local.RoutingParameters

            7. TEAM-SPECIFIC ROUTING (ConditionGroup):
               - A ConditionGroup checks Local.RoutingParameters.TeamName = "Windows Support"
               - The ENTIRE support flow (CreateConversation, support agent invocation, SetVariable,
                 resolution check, ticket resolution agent, GotoAction to end) is NESTED INSIDE
                 this condition's actions list
               - Do NOT have a separate else/default branch for escalation. Instead, escalation
                 happens naturally as the NEXT sequential actions after the ConditionGroup.

            8. SPECIALIZED SUPPORT AGENT (nested inside routing condition):
               - CreateConversation first to create Local.SupportConversationId
               - Uses conversationId: =Local.SupportConversationId
               - Has input.arguments (IssueDescription, AttemptedResolutionSteps) AND input.externalLoop
               - Has output.autoSend: true AND output.responseObject: Local.SupportParameters
               - After the agent: SetVariable to capture Local.ResolutionSteps = Local.SupportParameters.ResolutionSummary
               - Then a NESTED ConditionGroup checking if Local.SupportParameters.IsResolved
               - If resolved: invoke TicketResolutionAgent (with arguments TicketId and ResolutionSummary),
                 then GotoAction → all_done

            9. ESCALATION (natural fallthrough after routing ConditionGroup):
               - This is NOT inside any condition — it's the next top-level action after the ConditionGroup
               - CreateConversation for Local.EscalationConversationId
               - Uses conversationId: =Local.EscalationConversationId
               - Has input.arguments (TicketId, IssueDescription, ResolutionSummary: =Local.ResolutionSteps)
               - Has input.externalLoop.when: =Not(Local.EscalationParameters.IsComplete)
               - Has output.autoSend: true AND output.responseObject: Local.EscalationParameters

            10. END: The last action is EndWorkflow with id: all_done

            AUTOSENД RULES:
            - autoSend: true is used ONLY on agents that have their OWN conversation (via CreateConversation)
              AND need to relay their responses back to the user. This includes the support agent and escalation agent.
            - The self-service agent does NOT use autoSend because it uses the main System.ConversationId.
            - The ticket agent, routing agent, and resolution agent do NOT use autoSend.
            
            CRITICAL FORMATTING RULES:
            1. Use exactly 2-space indentation throughout
            2. Add YAML comments (# ...) before major action blocks explaining the purpose
            3. Multi-line PowerFx expressions (e.g., Not(X) And Not(Y)) use block scalar |- with each
               condition fragment on its own line, with additional indentation:
               ```
               when: |-
                 =Not(Local.ServiceParameters.IsResolved)
                  And 
                  Not(Local.ServiceParameters.NeedsTicket)
               ```
               Note: the And keyword and second Not() are indented by one extra space.
            4. String values with special characters should be quoted
            5. The activity field in SendActivity can use {Local.Variable} interpolation
            6. Include the standard header comment block describing the workflow
            
            REFERENCE EXAMPLE:
            Here is the EXACT structure of a correct workflow (use this as your template):
            
            ```yaml
            kind: Workflow
            trigger:
              kind: OnConversationStart
              id: workflow_demo
              actions:
                - kind: InvokeAzureAgent
                  id: service_agent
                  conversationId: =System.ConversationId
                  agent:
                    name: SelfServiceAgent
                  input:
                    externalLoop:
                      when: |-
                        =Not(Local.ServiceParameters.IsResolved)
                         And 
                         Not(Local.ServiceParameters.NeedsTicket)
                  output:
                    responseObject: Local.ServiceParameters
                - kind: ConditionGroup
                  id: check_if_resolved
                  conditions:
                    - condition: =Local.ServiceParameters.IsResolved
                      id: test_if_resolved
                      actions:
                        - kind: GotoAction
                          id: end_when_resolved
                          actionId: all_done
                - kind: InvokeAzureAgent
                  id: ticket_agent
                  agent:
                    name: TicketingAgent
                  input:
                    arguments:
                      IssueDescription: =Local.ServiceParameters.IssueDescription
                      AttemptedResolutionSteps: =Local.ServiceParameters.AttemptedResolutionSteps
                  output:
                    responseObject: Local.TicketParameters
                - kind: SetVariable
                  id: capture_attempted_resolution
                  variable: Local.ResolutionSteps
                  value: =Local.ServiceParameters.AttemptedResolutionSteps
                - kind: SendActivity
                  id: log_ticket
                  activity: "Created ticket #{Local.TicketParameters.TicketId}"
                - kind: InvokeAzureAgent
                  id: routing_agent
                  agent:
                    name: TicketRoutingAgent
                  input:
                    messages: =UserMessage(Local.ServiceParameters.IssueDescription)
                  output:
                    responseObject: Local.RoutingParameters
                - kind: SendActivity
                  id: log_route
                  activity: Routing to {Local.RoutingParameters.TeamName}
                - kind: ConditionGroup
                  id: check_routing
                  conditions:
                    - condition: =Local.RoutingParameters.TeamName = "Windows Support"
                      id: route_to_support
                      actions:
                        - kind: CreateConversation
                          id: conversation_support
                          conversationId: Local.SupportConversationId
                        - kind: InvokeAzureAgent
                          id: support_agent
                          conversationId: =Local.SupportConversationId
                          agent:
                            name: WindowsSupportAgent
                          input:
                            arguments:
                              IssueDescription: =Local.ServiceParameters.IssueDescription
                              AttemptedResolutionSteps: =Local.ServiceParameters.AttemptedResolutionSteps
                            externalLoop:
                              when: |-
                                =Not(Local.SupportParameters.IsResolved)
                                 And 
                                 Not(Local.SupportParameters.NeedsEscalation)
                          output:
                            autoSend: true
                            responseObject: Local.SupportParameters
                        - kind: SetVariable
                          id: capture_support_resolution
                          variable: Local.ResolutionSteps
                          value: =Local.SupportParameters.ResolutionSummary
                        - kind: ConditionGroup
                          id: check_resolved
                          conditions:
                            - condition: =Local.SupportParameters.IsResolved
                              id: handle_if_resolved
                              actions:
                                - kind: InvokeAzureAgent
                                  id: resolution_agent
                                  agent:
                                    name: TicketResolutionAgent
                                  input:
                                    arguments:
                                      TicketId: =Local.TicketParameters.TicketId
                                      ResolutionSummary: =Local.SupportParameters.ResolutionSummary
                                - kind: GotoAction
                                  id: end_when_solved
                                  actionId: all_done
                - kind: CreateConversation
                  id: conversation_escalate
                  conversationId: Local.EscalationConversationId
                - kind: InvokeAzureAgent
                  id: escalate_agent
                  conversationId: =Local.EscalationConversationId
                  agent:
                    name: TicketEscalationAgent
                  input:
                    arguments:
                      TicketId: =Local.TicketParameters.TicketId
                      IssueDescription: =Local.ServiceParameters.IssueDescription
                      ResolutionSummary: =Local.ResolutionSteps
                    externalLoop:
                      when: =Not(Local.EscalationParameters.IsComplete)
                  output:
                    autoSend: true
                    responseObject: Local.EscalationParameters
                - kind: EndWorkflow
                  id: all_done
            ```
            
            OUTPUT FORMAT:
            Return ONLY the complete, valid YAML. Do not wrap it in markdown code fences.
            Do not add any explanation before or after the YAML.
            Start directly with the # comment header, then kind: Workflow.
            """;
    }

    /// <summary>
    /// Default agent mapping for the CustomerSupport workflow.
    /// </summary>
    public const string DefaultCustomerSupportAgentMapping = """
        - Self-service troubleshooting / initial customer interaction = SelfServiceAgent
        - Ticket creation = TicketingAgent
        - Ticket routing / determining which team handles the issue = TicketRoutingAgent
        - Windows-specific technical support / specialized troubleshooting = WindowsSupportAgent
        - Ticket resolution / closing a resolved ticket = TicketResolutionAgent
        - Ticket escalation / email notification for unresolved issues = TicketEscalationAgent
        """;
}
