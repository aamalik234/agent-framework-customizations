using Common;
using OpenAI.Chat;

// ──────────────────────────────────────────────────────────────────
// Program 2: WordToYaml
// Reads a plain-English Word knowledge base document and generates
// a workflow YAML file using GPT-5.1 via Azure AI Foundry.
// Agent names are provided via CLI argument or use defaults.
// ──────────────────────────────────────────────────────────────────

// Parse CLI arguments
string inputPath = GetArg(args, "--input", "CustomerSupport_KnowledgeBase.docx");
string outputPath = GetArg(args, "--output", "CustomerSupport_Generated.yaml");
string? agentMappingArg = GetArgOptional(args, "--agents");

// Build agent mapping: use CLI arg if provided, otherwise use default
string agentMapping = agentMappingArg ?? PromptTemplates.DefaultCustomerSupportAgentMapping;

Console.WriteLine("═══════════════════════════════════════════════════════");
Console.WriteLine("  Word Knowledge Base → YAML Generator");
Console.WriteLine("═══════════════════════════════════════════════════════");
Console.WriteLine();

// 1. Read the Word document
if (!File.Exists(inputPath))
{
    Console.Error.WriteLine($"Error: Input file not found: {inputPath}");
    Console.Error.WriteLine("Use --input <path> to specify the Word document.");
    return 1;
}

Console.WriteLine($"  Input:  {inputPath}");
Console.WriteLine($"  Output: {outputPath}");
Console.WriteLine();

Console.WriteLine("  Extracting text from Word document...");
string narrativeText = WordDocumentHelper.ExtractText(inputPath);

if (string.IsNullOrWhiteSpace(narrativeText))
{
    Console.Error.WriteLine("Error: No text content found in the Word document.");
    return 1;
}

Console.WriteLine($"  Extracted {narrativeText.Length} chars of narrative text.");
Console.WriteLine();

Console.WriteLine("  Agent mapping:");
foreach (var line in agentMapping.Split('\n', StringSplitOptions.RemoveEmptyEntries))
{
    var trimmed = line.Trim();
    if (!string.IsNullOrEmpty(trimmed))
    {
        Console.WriteLine($"    {trimmed}");
    }
}
Console.WriteLine();

// 2. Call GPT-5.1 to convert narrative → YAML
Console.WriteLine("  Connecting to Azure AI Foundry...");
ChatClient chatClient = FoundryClientFactory.CreateChatClient();

string systemPrompt = PromptTemplates.BuildNarrativeToYamlSystemPrompt(agentMapping);

Console.WriteLine("  Generating workflow YAML from knowledge base...");
Console.WriteLine();

var messages = new List<ChatMessage>
{
    new SystemChatMessage(systemPrompt),
    new UserChatMessage(
        "Convert the following customer support process knowledge base document into a " +
        "Microsoft Agent Framework workflow YAML file. Reproduce the complete workflow " +
        "with all steps, conditions, branching paths, and data flows described in the document.\n\n" +
        narrativeText)
};

ChatCompletion completion = await chatClient.CompleteChatAsync(messages, new ChatCompletionOptions
{
    Temperature = 0.1f, // Very low temperature for maximum fidelity to the schema
});

string yamlOutput = completion.Content[0].Text;

// Strip markdown code fences if the model wrapped the output
yamlOutput = StripCodeFences(yamlOutput);

// 3. Validate basic YAML structure
Console.WriteLine("  Validating YAML structure...");
bool isValid = ValidateYamlStructure(yamlOutput);

if (!isValid)
{
    Console.WriteLine("  ⚠ Warning: Generated YAML may have structural issues.");
    Console.WriteLine("  Writing output anyway — review manually.");
}
else
{
    Console.WriteLine("  YAML structure validated successfully.");
}
Console.WriteLine();

// 4. Write the YAML file
File.WriteAllText(outputPath, yamlOutput);

var fileInfo = new FileInfo(outputPath);
Console.WriteLine($"  YAML file created: {fileInfo.FullName}");
Console.WriteLine($"  File size: {fileInfo.Length:N0} bytes");
Console.WriteLine($"  Lines: {yamlOutput.Split('\n').Length}");
Console.WriteLine();

// 5. Show preview
Console.WriteLine("  ── Generated YAML Preview ──────────────────────────");
string preview = yamlOutput.Length > 800 ? yamlOutput[..800] + "\n  ..." : yamlOutput;
Console.WriteLine(preview);
Console.WriteLine("  ─────────────────────────────────────────────────────");
Console.WriteLine();

Console.WriteLine("  Tokens used: {0} prompt + {1} completion",
    completion.Usage.InputTokenCount,
    completion.Usage.OutputTokenCount);
Console.WriteLine();
Console.WriteLine("  Done!");
Console.WriteLine();
Console.WriteLine("  TIP: Compare with the original using:");
Console.WriteLine($"       diff \"{outputPath}\" \"<original-yaml-path>\"");

return 0;

// ──────────────────────────────────────────────────────────────────
// Helpers
// ──────────────────────────────────────────────────────────────────

static string GetArg(string[] args, string name, string defaultValue)
{
    for (int i = 0; i < args.Length - 1; i++)
    {
        if (args[i].Equals(name, StringComparison.OrdinalIgnoreCase))
        {
            return args[i + 1];
        }
    }
    return defaultValue;
}

static string? GetArgOptional(string[] args, string name)
{
    for (int i = 0; i < args.Length - 1; i++)
    {
        if (args[i].Equals(name, StringComparison.OrdinalIgnoreCase))
        {
            return args[i + 1];
        }
    }
    return null;
}

static string StripCodeFences(string text)
{
    var trimmed = text.Trim();

    // Remove ```yaml ... ``` wrapper
    if (trimmed.StartsWith("```"))
    {
        int firstNewline = trimmed.IndexOf('\n');
        if (firstNewline > 0)
        {
            trimmed = trimmed[(firstNewline + 1)..];
        }
    }

    if (trimmed.EndsWith("```"))
    {
        trimmed = trimmed[..^3].TrimEnd();
    }

    return trimmed;
}

static bool ValidateYamlStructure(string yaml)
{
    try
    {
        var deserializer = new YamlDotNet.Serialization.DeserializerBuilder()
            .WithAttemptingUnquotedStringTypeDeserialization()
            .Build();

        var parsed = deserializer.Deserialize<Dictionary<string, object>>(yaml);

        if (parsed is null)
        {
            Console.Error.WriteLine("    Error: YAML parsed as null.");
            return false;
        }

        bool hasKind = parsed.ContainsKey("kind");
        bool hasTrigger = parsed.ContainsKey("trigger");
        bool isWorkflow = hasKind && parsed["kind"]?.ToString() == "Workflow";

        if (!hasKind)
            Console.Error.WriteLine("    Error: Missing 'kind' field.");
        if (!isWorkflow)
            Console.Error.WriteLine("    Error: 'kind' is not 'Workflow'.");
        if (!hasTrigger)
            Console.Error.WriteLine("    Error: Missing 'trigger' field.");

        return hasKind && isWorkflow && hasTrigger;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"    YAML parse error: {ex.Message}");
        return false;
    }
}
