# YAML ↔ Word Document Roundtrip Tools

Two .NET console applications that enable roundtripping between Microsoft Agent Framework workflow YAML files and plain-English Word knowledge base documents, using GPT-5.1 via an Azure AI Foundry project.

## Scenario

**Customer Support Demo**: Generate a human-readable Word document from an existing workflow YAML (stripping all agent names and technical syntax), then re-generate identical YAML from that Word document by providing agent names as context.

## Prerequisites

- **.NET 10 SDK** (10.0.100+)
- **Azure AI Foundry project** with a GPT-5.1 model deployment
- **Azure credentials** configured for `DefaultAzureCredential` (e.g., `az login`)

## Environment Variables

| Variable | Description | Example |
|----------|-------------|---------|
| `AZURE_AI_PROJECT_ENDPOINT` | Azure AI Foundry project endpoint | `https://<your-resource>.services.ai.azure.com/api/projects/<your-project>` |
| `AZURE_AI_MODEL_DEPLOYMENT_NAME` | Model deployment name (default: `gpt-5.1`) | `gpt-5.1` |

## Build

```bash
cd tools/yaml-docx-roundtrip
dotnet build
```

## Program 1: YamlToWord — Generate Knowledge Base from YAML

Reads a workflow YAML and produces a plain-English `.docx` knowledge base document with no agent names or technical syntax.

```bash
# Using defaults (reads ../../workflow-samples/CustomerSupport.yaml)
dotnet run --project YamlToWord

# Custom paths
dotnet run --project YamlToWord -- --input path/to/workflow.yaml --output MyKnowledgeBase.docx --title "My Process"
```

### Arguments

| Argument | Default | Description |
|----------|---------|-------------|
| `--input` | `../../workflow-samples/CustomerSupport.yaml` | Path to the workflow YAML file |
| `--output` | `CustomerSupport_KnowledgeBase.docx` | Output Word document path |
| `--title` | `Customer Support Process — Knowledge Base` | Document title |

## Program 2: WordToYaml — Generate YAML from Knowledge Base

Reads a Word document and produces a workflow YAML file. Agent names are injected via the `--agents` argument.

```bash
# Using defaults (reads CustomerSupport_KnowledgeBase.docx, uses built-in agent mapping)
dotnet run --project WordToYaml

# Custom paths and agent mapping
dotnet run --project WordToYaml -- \
  --input MyKnowledgeBase.docx \
  --output MyWorkflow.yaml \
  --agents "self-service=SelfServiceAgent,ticketing=TicketingAgent,routing=TicketRoutingAgent,support=WindowsSupportAgent,resolution=TicketResolutionAgent,escalation=TicketEscalationAgent"
```

### Arguments

| Argument | Default | Description |
|----------|---------|-------------|
| `--input` | `CustomerSupport_KnowledgeBase.docx` | Path to the Word knowledge base document |
| `--output` | `CustomerSupport_Generated.yaml` | Output YAML file path |
| `--agents` | *(built-in CustomerSupport mapping)* | Agent name mapping (comma-separated `role=AgentName` pairs) |

### Default Agent Mapping (CustomerSupport)

| Business Function | Agent Name |
|-------------------|------------|
| Self-service troubleshooting | `SelfServiceAgent` |
| Ticket creation | `TicketingAgent` |
| Ticket routing | `TicketRoutingAgent` |
| Windows-specific support | `WindowsSupportAgent` |
| Ticket resolution/closure | `TicketResolutionAgent` |
| Ticket escalation | `TicketEscalationAgent` |

## Full Roundtrip Example

```bash
cd tools/yaml-docx-roundtrip

# Set environment
$env:AZURE_AI_PROJECT_ENDPOINT = "https://<your-resource>.services.ai.azure.com/api/projects/<your-project>"
$env:AZURE_AI_MODEL_DEPLOYMENT_NAME = "gpt-5.1"

# Step 1: YAML → Word
dotnet run --project YamlToWord

# Step 2: Word → YAML
dotnet run --project WordToYaml

# Step 3: Compare
diff ../../workflow-samples/CustomerSupport.yaml CustomerSupport_Generated.yaml
```

## Architecture

```
┌─────────────────────┐     GPT-5.1      ┌──────────────────────┐
│  CustomerSupport     │ ───────────────► │  Knowledge Base      │
│  .yaml               │   (strip agent   │  .docx               │
│                      │    names, convert │  (plain English,     │
│  (technical workflow)│    to prose)      │   no agent names)    │
└─────────────────────┘                   └──────────────────────┘
                                                    │
                                                    │  GPT-5.1
                                                    │  (+ agent name
                                                    │   mapping)
                                                    ▼
                                          ┌──────────────────────┐
                                          │  CustomerSupport     │
                                          │  _Generated.yaml     │
                                          │  (identical workflow)│
                                          └──────────────────────┘
```

## Project Structure

```
yaml-docx-roundtrip/
├── yaml-docx-roundtrip.sln
├── README.md
├── Common/                    # Shared library
│   ├── Common.csproj
│   ├── FoundryClientFactory.cs   # Azure AI Foundry ChatClient setup
│   ├── WordDocumentHelper.cs     # .docx read/write via OpenXml
│   └── PromptTemplates.cs        # GPT-5.1 prompt templates
├── YamlToWord/                # Program 1
│   ├── YamlToWord.csproj
│   └── Program.cs
└── WordToYaml/                # Program 2
    ├── WordToYaml.csproj
    └── Program.cs
```
