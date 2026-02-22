# Keeper / Docket

**Keeper** is a meeting accountability agent for Microsoft Teams and Slack.  
**Docket** is the structured commitment store it writes to — available as a 
standalone API.

This project is a revival and modernization of [4Minitz](https://github.com/4minitz/4minitz),
an open source meeting minutes tool that went inactive circa 2022. The goal is 
to demonstrate how modern AI-assisted development tools change what's possible — 
and how fast.

## Status
🏗️ Infrastructure setup and archaeology phase. Follow along:  
📝 [Blog series — link when live]

## What's Here Now
- `/docs/00-archaeology` — Analysis of the 4Minitz codebase
- `/docs/02-architecture` — Architecture Decision Records

## What's Coming
- Docket: REST API for structured commitment records
- Keeper (Azure stack): .NET 9 + Azure Bot Framework + Azure OpenAI
- Keeper (Portable stack): Python FastAPI + Slack Bolt + OpenAI
