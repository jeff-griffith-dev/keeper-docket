# Keeper / Docket

> *"Don't trust me — look at the evidence for yourself."*

---

## What This Is

**Keeper** is a meeting accountability agent for Microsoft Teams and Slack.  
**Docket** is the structured commitment store Keeper writes to — available as a standalone API.

After every meeting, Keeper scans the transcript, extracts who committed to what and by when, files those commitments into Docket, and follows up until each item is closed. No one has to type anything. Nothing gets lost.

Docket is intentionally separate. It has no opinion about where your meetings happen, what tools your clients use, or whether anyone has a Copilot license. It is a structured, queryable, auditable record of commitments — a REST API that any tool can write to and read from.

---

## Why This Exists

In Professional Services, the most expensive moment is the one right after a meeting where someone says *"what did I just agree to doing?"* 

Microsoft Copilot is excellent at summarizing what happened in a meeting. It cannot follow up on what was promised, it cannot reach across organizational boundaries into client meetings, and it cannot give you an auditable record of commitments over time. Keeper and Docket were built for that gap — the space between *"AI took good notes"* and *"everyone actually did what they said they would."*

This project is also a revival and modernization of [4Minitz](https://github.com/4minitz/4minitz), an open source meeting minutes tool that went inactive circa 2022. It had the right data model and the right instincts. It died because the framework aged out and no one had the tools to rebuild it quickly. We do now.

The full story — motivation, decisions, architecture, and implementation — is documented as it happens:

📝 **Blog series:** [kdblog.jeff-griffith.dev](https://kdblog.jeff-griffith.dev)

---

## Architecture Overview

```
┌─────────────────────────────────────────────────────┐
│                      KEEPER                         │
│         The agent. You talk to Keeper.              │
│                                                     │
│  ┌─────────────┐          ┌─────────────────────┐  │
│  │ Teams Stack │          │   Slack Stack        │  │
│  │ .NET 9      │          │   Python / FastAPI   │  │
│  │ Azure Bot   │          │   Slack Bolt SDK     │  │
│  │ Framework   │          │                      │  │
│  │ Azure OpenAI│          │   OpenAI / Ollama    │  │
│  └──────┬──────┘          └──────────┬───────────┘  │
│         │                            │              │
└─────────┼────────────────────────────┼──────────────┘
          │                            │
          └──────────────┬─────────────┘
                         ▼
┌─────────────────────────────────────────────────────┐
│                      DOCKET                         │
│       The record. Docket remembers everything.      │
│                                                     │
│   REST API — MeetingSeries / Minutes /              │
│              Topics / ActionItems / Owners          │
│                                                     │
│   Azure Stack: Azure SQL + EF Core                  │
│   Portable Stack: PostgreSQL                        │
└─────────────────────────────────────────────────────┘
```

Keeper is what you talk to. Docket is what gets written. They are deployed and versioned independently.

---

## Repository Structure

```
keeper-docket/
├── README.md                        ← you are here
├── CHANGELOG.md                     ← what changed and when
├── docs/
│   ├── 00-archaeology/              ← 4Minitz codebase analysis
│   ├── 01-spec/                     ← feature spec derived from archaeology
│   ├── 02-architecture/             ← Architecture Decision Records (ADRs)
│   └── 03-blog-drafts/              ← markdown source for each blog post
├── src/
│   ├── core/                        ← shared domain model
│   ├── stack-azure/                 ← .NET 9 / Azure Bot Framework / Teams
│   └── stack-portable/              ← Python / FastAPI / Slack Bolt
└── archive/
    └── 4minitz-reference/           ← preserved reference from original project
```

---

## Current Status

| Component | Status |
|---|---|
| Repo structure | ✅ Complete |
| 4Minitz archaeology | 🏗️ In progress |
| Architecture Decision Records | 🏗️ In progress |
| Docket domain model | 🔜 Next |
| Docket REST API | 🔜 Pending |
| Keeper — Azure Stack | 🔜 Pending |
| Keeper — Portable Stack | 🔜 Pending |

This table reflects what actually exists in this repository right now. It is updated with each commit. If something says pending, it does not exist yet.

---

## The Two Stacks

This project is intentionally implemented twice — not to demonstrate that one stack is better, but to demonstrate that the constraint is no longer implementation. Modern AI-assisted development tools generated both implementations from the same spec. The bottleneck is thinking, not typing.

| | Azure Stack | Portable Stack |
|---|---|---|
| Language | C# / .NET 9 | Python 3.12 |
| Bot interface | Azure Bot Framework → Teams | Slack Bolt SDK |
| AI layer | Azure OpenAI + Semantic Kernel | OpenAI API / Ollama (swappable) |
| Database | Azure SQL + Entity Framework Core | PostgreSQL |
| Scheduling | Azure Durable Functions | Celery + Redis |
| Deployment | Azure Container Apps | Docker Compose |
| Auth | Azure AD / Entra ID | OAuth2 via Slack |

See [ADR-001](docs/02-architecture/ADR-001-two-stack.md) for the reasoning behind this decision.

---

## Relationship to Copilot and Existing Tools

Docket is designed to receive data from any source — including Copilot. If your organization uses Microsoft Copilot for meeting summaries, Keeper can consume that output and give it a backbone: structured records, ownership, due dates, and follow-up. Organizations that later want deeper integration can migrate to Keeper's native transcript processing without losing their Docket history.

This is not a competitor to Copilot. It is the accountability layer that Copilot does not provide.

---

## Following Along

The blog series documents every decision, dead end, and working implementation as it happens. Each post links to the specific commit that represents its "as of" state — you can check out that commit and see exactly what existed when each post was written.

📝 [kdblog.jeff-griffith.dev](https://kdblog.jeff-griffith.dev)  
🐙 [github.com/jeff-griffith-dev/keeper-docket](https://github.com/jeff-griffith-dev/keeper-docket)

---

## License

MIT — fork it, learn from it, build on it.
