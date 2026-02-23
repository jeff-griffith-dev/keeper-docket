# ADR-004: AI Orchestration Layer — Azure Stack

**Date:** 2026-02-22  
**Status:** Accepted  
**Author:** Jeff Griffith

---

## Context

The Azure stack for Keeper requires an AI orchestration layer — the component responsible for taking a meeting transcript, reasoning about its content, extracting structured commitments (who said they would do what, by when), and filing those commitments into Docket. This is not a simple API call. It requires chaining prompts, managing context, handling ambiguous or incomplete commitments, and producing structured output reliably enough to write to a database.

Microsoft's AI tooling landscape has consolidated significantly through 2024-2025. Understanding the current hierarchy is necessary before making this decision:

```
Azure AI Foundry
└── The platform layer — model hosting, deployment, portal, governance
    │
    ├── Microsoft Agent Framework          ← unified SDK (new outer layer)
    │   ├── Semantic Kernel                ← still active, absorbed into Agent Framework
    │   └── AutoGen                        ← Microsoft Research, now merged in
    │
    └── Foundry Agent Service              ← managed/hosted agent runtime (no-code/low-code)
```

Semantic Kernel is not being retired. It has been absorbed upward into the Microsoft Agent Framework, which is the new recommended entry point for building agentic applications on Azure. Agent Framework uses Semantic Kernel as its orchestration core while adding multi-agent coordination patterns from AutoGen and a unified SDK surface.

---

## Decision

**Use Microsoft Agent Framework as the AI orchestration layer for the Azure stack.**

Agent Framework is the forward-looking choice. It is where Microsoft is placing active investment, it subsumes Semantic Kernel rather than replacing it, and it is designed explicitly for the agentic patterns Keeper needs: chaining reasoning steps, managing state across a conversation, producing structured output, and coordinating multiple specialized agents if the architecture evolves in that direction.

For this project — which is explicitly about demonstrating modern tooling — using the current recommended framework rather than its predecessor is the right call. The blog narrative is stronger: "I evaluated the Microsoft AI stack as it stands in 2026 and chose the current recommended path, not the tutorial-default path."

---

## Why Not Semantic Kernel Directly

Semantic Kernel is not wrong. It is mature, well-documented, and has years of community examples. If this project were targeting a production enterprise deployment with strict stability requirements and a team that knew SK deeply, it would be a defensible choice.

The reason to start with Agent Framework instead is forward positioning. Microsoft has been explicit that Agent Framework is the unified path. Starting with SK directly means either staying on SK (and explaining why you didn't adopt the current framework) or migrating later — which costs time and creates a discontinuity in the blog narrative.

**However — and this matters — Agent Framework is the entry point, not a ceiling.**

As this project progresses, there will likely be moments where the Agent Framework abstraction layer obscures something that needs to be controlled directly. Any time a platform protects you from the underlying service, something is lost in that protection. You may not know what you've lost until you need it. When that moment arrives — when the abstraction is in the way rather than in front — the right move is to drop down to Semantic Kernel directly for that component.

This is not a hedge. It is an architectural principle: *start at the highest useful abstraction, and descend deliberately when the abstraction costs more than it saves.* The descent path is known, documented, and will be recorded here as an amendment when it happens.

---

## Why Not Foundry Agent Service (Managed)

Foundry Agent Service is the managed, low-code/no-code agent runtime in the Azure AI Foundry portal. It requires exposing your application as an OpenAPI endpoint and delegating orchestration to Microsoft's hosted runtime.

Rejected for this project for a clear reason: this project is specifically about demonstrating architectural thinking and control. Delegating orchestration to a managed runtime means the most interesting decisions — how the agent reasons, how it handles ambiguous commitments, how it constructs its prompts — happen inside Microsoft's infrastructure, not in the repo. There is nothing to show and nothing to learn from.

Foundry Agent Service is the right choice for a team that wants agent capabilities without agent complexity. This project is explicitly about the complexity. That rules it out.

---

## Implementation Notes

When using Agent Framework in .NET:

- No `Kernel` object is the primary entry point (that was Semantic Kernel's pattern)
- Instead, create `IChatClient` as a singleton using `AzureOpenAIClient`
- Wrap it with `ChatClientAgent` for agent behavior
- Agent state and context management is handled by the framework's runtime

The Keeper agent for transcript processing will use a multi-step pattern:
1. **Extraction agent** — reads the raw transcript, identifies candidate commitments
2. **Validation agent** — evaluates each candidate: is this a firm commitment or a vague intention? Who is the actual owner?
3. **Structuring agent** — converts validated commitments into Docket-compatible ActionItem objects
4. **Review step** — surfaces low-confidence items to the human for confirmation before filing

This chain is where the Agent Framework's multi-agent coordination patterns earn their keep. Each step is a discrete agent with a discrete prompt and a discrete output schema. They are testable independently.

---

## Escape Hatch — Descending to Semantic Kernel

If, as this project progresses, the Agent Framework abstraction layer becomes an obstacle — if fine-grained control over prompt construction, context window management, memory handling, or token-level behavior is needed — the path is to adopt Semantic Kernel directly for the affected component.

Semantic Kernel lives inside Agent Framework. Dropping down to it is not a rewrite. It is a targeted decision to bypass the outer abstraction for a specific component that needs the control. When this happens, it will be recorded here as an amendment with the specific reason, what the abstraction was hiding, and what direct SK control made possible.

This is the known cost of working at a high abstraction level. It is accepted deliberately.

---

## Alternatives Considered

| Option | Decision | Reason |
|---|---|---|
| Microsoft Agent Framework | ✅ Accepted | Forward-looking, subsumes SK, right abstraction level for this project |
| Semantic Kernel directly | Available as descent path | Mature and valid, but Agent Framework is the current recommended entry point |
| Foundry Agent Service (managed) | ❌ Rejected | Removes visibility and control over the most interesting decisions |
| LangChain.NET | ❌ Rejected | Third-party, adds dependency risk, no advantage over first-party Microsoft tooling in this context |
| Direct Azure OpenAI SDK calls | ❌ Rejected for initial implementation | Too low-level for the orchestration complexity required; viable for specific components if Agent Framework overhead proves costly |

---

## Consequences

- The Azure stack implementation will be built against Microsoft Agent Framework, not Semantic Kernel's `Kernel` API directly
- If Semantic Kernel direct adoption becomes necessary for specific components, it will be recorded as an amendment here with full reasoning
- The blog series will include a dedicated section on this decision — the Microsoft AI tooling landscape, what changed in 2024-2025, and why Agent Framework was chosen over the tutorial-default path
- This ADR should be reviewed if Microsoft makes significant Agent Framework API changes before implementation is complete

---

## Amendments

*None yet. When the abstraction layer costs more than it saves, the descent to Semantic Kernel will be recorded here.*
