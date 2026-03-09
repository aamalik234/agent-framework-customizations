"""
Simplified side-by-side comparison for demo.
Plain boxes, no icons, with benefits callout.
"""

import graphviz

FONT = "Segoe UI,Helvetica,Arial,sans-serif"

g = graphviz.Digraph(
    "deployment_comparison",
    format="png",
    engine="dot",
    graph_attr={
        "label": "CustomerSupport — Deployment Comparison",
        "labelloc": "t",
        "fontsize": "24",
        "fontname": FONT,
        "bgcolor": "white",
        "pad": "0.5",
        "ranksep": "1.0",
        "nodesep": "0.7",
        "splines": "polyline",
        "rankdir": "TB",
        "compound": "true",
    },
    node_attr={
        "shape": "box",
        "style": "filled,rounded",
        "fontname": FONT,
        "fontsize": "12",
        "margin": "0.3,0.2",
        "penwidth": "1.5",
    },
    edge_attr={
        "fontname": FONT,
        "fontsize": "10",
        "penwidth": "1.2",
    },
)

# ── Users (same rank → side by side) ────────────────────────────────────
g.node("user1", "Customer", fillcolor="white", color="#424242")
g.node("user2", "Customer", fillcolor="white", color="#424242")
with g.subgraph() as s:
    s.attr(rank="same")
    s.node("user1")
    s.node("user2")

# =====================================================================
# LEFT — Approach 1: All in one .NET process
# =====================================================================
with g.subgraph(name="cluster_local") as c:
    c.attr(
        label="Approach 1 — Local\n(single .NET process)",
        style="rounded,filled", fillcolor="#E3F2FD",
        color="#0D47A1", penwidth="2.5",
        fontsize="15", fontcolor="#0D47A1",
    )
    c.node("wf1", "Workflow Engine\n(WorkflowRunner)",
           fillcolor="#BBDEFB", color="#0D47A1", penwidth="2")
    c.node("agents1",
           "6 Agents (in-process)\n─────────────────\n"
           "SelfService · Ticketing · Routing\n"
           "WindowsSupport · Resolution · Escalation",
           fillcolor="#EDE7F6", color="#7B1FA2")
    c.node("tools1",
           "TicketingPlugin (in-process)\n─────────────────\n"
           "CreateTicket · GetTicket\nResolveTicket · SendNotification",
           fillcolor="#E3F2FD", color="#1565C0")
    c.node("llm1", "Azure AI Foundry\n(LLM Inference)",
           fillcolor="#C8E6C9", color="#388E3C")

# Local edges
g.edge("user1", "wf1", label="input", color="#1565C0")
g.edge("wf1", "agents1", label="orchestrates", color="#7B1FA2")
g.edge("agents1", "tools1", label="direct call", color="#E65100")
g.edge("agents1", "llm1", label="LLM calls", color="#388E3C", style="dashed")

# =====================================================================
# RIGHT — Approach 2: Hosted in Foundry + MCP
# =====================================================================
with g.subgraph(name="cluster_hosted") as c:
    c.attr(
        label="Approach 2 — Hosted\n(Azure AI Foundry + MCP Server)",
        style="rounded,filled", fillcolor="#E8F5E9",
        color="#388E3C", penwidth="2.5",
        fontsize="15", fontcolor="#2E7D32",
    )

    with c.subgraph(name="cluster_foundry") as f:
        f.attr(
            label="Azure AI Foundry (cloud)",
            style="rounded,filled", fillcolor="#F3E5F5",
            color="#7B1FA2", penwidth="1.5",
            fontsize="13", fontcolor="#6A1B9A",
        )
        f.node("wf2", "Workflow Agent\n(YAML-based, deployed to Foundry)",
               fillcolor="#BBDEFB", color="#0D47A1", penwidth="2")
        f.node("agents2",
               "6 Hosted Agents\n─────────────────\n"
               "SelfService · Ticketing · Routing\n"
               "WindowsSupport · Resolution · Escalation",
               fillcolor="#EDE7F6", color="#7B1FA2")
        f.node("llm2", "Built-in LLM\n(managed by Foundry)",
               fillcolor="#C8E6C9", color="#388E3C")

    with c.subgraph(name="cluster_mcp") as m:
        m.attr(
            label="MCP Server (your infra)",
            style="rounded,filled", fillcolor="#FFF3E0",
            color="#E65100", penwidth="1.5",
            fontsize="13", fontcolor="#BF360C",
        )
        m.node("mcp",
               "ASP.NET Core + MCP HTTP Transport\n─────────────────\n"
               "CreateTicket · GetTicket\nResolveTicket · SendNotification",
               fillcolor="#FFE0B2", color="#E65100")

    # Benefits box
    c.node("benefits",
           "✦  Benefits of Approach 2  ✦\n"
           "━━━━━━━━━━━━━━━━━━━━━━━━━\n"
           "• Agents run serverless — no hosting cost\n"
           "• Scale to zero, scale to many\n"
           "• Workflow + agents managed by Foundry\n"
           "• Tools decoupled via MCP (swap anytime)\n"
           "• Enterprise auth, logging, tracing built-in\n"
           "• Deploy from YAML — no recompile",
           fillcolor="#FFFDE7", color="#F9A825",
           shape="note", fontsize="11")

# Hosted edges
g.edge("user2", "wf2", label="input", color="#1565C0")
g.edge("wf2", "agents2", label="orchestrates", color="#7B1FA2")
g.edge("agents2", "llm2", label="LLM calls", color="#388E3C", style="dashed")
g.edge("agents2", "mcp", label="HostedMcpServerTool\n(HTTP)", color="#E65100",
       penwidth="1.5")

# ── Render ───────────────────────────────────────────────────────────────
g.render("deployment_comparison", cleanup=True)
print("Diagram saved as deployment_comparison.png")
