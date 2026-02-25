# The Meeting That Never Ends

*Post 1 of the Keeper/Docket series — reviving dead open source with modern AI tools*

---

I've been in Professional Services for a long time. Long enough to know that the most dangerous moment in any client engagement isn't the hard conversation or the missed deadline. It's the moment after a meeting ends and everyone files out with different memories of what just happened.

You agreed to something. You're not sure what. The client remembers it differently. Your project manager heard a third version. Nobody wrote it down — or if they did, it's in a notebook that won't be opened again until someone is already upset.

I got tired of it.

## The Tool That Already Existed (And Then Didn't)

A few years ago, a small team built something called [4Minitz](https://github.com/4minitz/4minitz). It was a collaborative meeting minutes web app — structured agendas, action items with owners and due dates, email distribution, a proper record of what was discussed and what was promised. Real companies used it. The data model was well thought out. The team cared.

Then it went quiet. Last commits around 2022. Open issues with no responses. The maintainers moved on, and the framework they'd built on — Meteor.js, which had its moment around 2015 and then slowly aged out — made it increasingly hard for anyone to pick up where they left off.

The idea was right. The stack killed it.

## Why This Is a Good Problem for AI Tools

Here's what I wanted to find out: can modern AI-assisted development tools take a dead codebase, extract what was valuable about it, and rebuild it faster and more flexibly than the original team could? And not just for one platform — for two completely different tech stacks from a single design?

The answer, as of this writing, is yes. But the more interesting finding is *how* it works and *where* the AI actually saves you time versus where it's just a faster way to make the same mistakes.

This blog series documents the whole thing, start to finish. No glossing over the friction. No skipping the parts where things didn't compile.

## What's Being Built

The project splits into two components:

**Docket** is the data layer — a clean REST API for structured commitment records. Who said they'd do what, by when, in which meeting, with a full audit trail from transcript to confirmed action item to completion. It doesn't care where the meeting happened or what tools you use. It's a standalone service you can deploy independently.

**Keeper** is the agent — the thing you talk to in Microsoft Teams or Slack. It reads meeting transcripts, extracts commitments, writes them to Docket, and follows up with the people who own them until the work is done. Keeper is the part that uses AI. Docket is the part that makes sure nothing gets lost.

The same Docket spec drives two implementations: one Azure-native (ASP.NET Core, Azure SQL, Azure Bot Framework, Azure OpenAI), and one portable (Python FastAPI, PostgreSQL, Slack Bolt, OpenAI API). Same design, different stacks, both generated with AI assistance from the same starting point. That's the productivity and flexibility claim — and by the end of this series you'll be able to evaluate it for yourself.

## What Exists Right Now

I don't promise things. I show evidence.

As of today, 2/24/26:

- The [GitHub repo](https://github.com/jeff-griffith-dev/keeper-docket) is live with architecture decision records documenting every significant design choice and why it was made
- The 4Minitz archaeology is complete — I ran the original app, captured it, and extracted what the data model got right
- The Docket domain model and full OpenAPI spec (38 operations) are written and in the repo
- The Azure stack has a running API: all 38 endpoints implemented, SQLite dev database, schema applied on startup, Scalar UI live at `/scalar/v1`

The next post covers the archaeology — what I found in the 4Minitz codebase, what it got right, what the screenshots revealed about behavior that wasn't obvious from reading the code, and how that fed directly into the Docket design.

If you want to follow along: the repo is at [github.com/jeff-griffith-dev/keeper-docket](https://github.com/jeff-griffith-dev/keeper-docket). Every design decision has a paper trail. Check the `/docs/02-architecture` folder — the ADRs are short, readable, and honest about the tradeoffs.

The meeting accountability problem is still unsolved for most teams. Let's fix that.

---

*Next: [Post 2 — Digging Up 4Minitz: What a Dead Codebase Teaches You]*
