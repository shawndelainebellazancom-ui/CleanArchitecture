# PMCR-O Agent Framework - .NET 10 + Microsoft Agent Framework (MAF)

## Architecture Overview

This is a **Hybrid REST + gRPC** microservices architecture implementing the **PMCR-O** (Plan-Make-Check-Reflect-Orchestrate) cognitive framework using **Microsoft Agent Framework (MAF)** and Clean Architecture principles.

### Why Microsoft Agent Framework (MAF)?

This implementation uses **Microsoft.Agents.AI** instead of raw `Microsoft.Extensions.AI` because:

1. **Built-in Agent Patterns** - MAF provides `Agent` class with identity, instructions, and capabilities
2. **Context Management** - `AgentContext` for maintaining conversation state and session tracking
3. **Tool Integration** - Native support for function calling and tool orchestration
4. **Multi-Agent Coordination** - Framework for agent collaboration and handoff
5. **Enterprise Ready** - Production-grade abstractions for Teams, Azure, and enterprise scenarios

### Components

```
┌────────────────────────────────────────────────────┐
│         .NET Aspire AppHost                        │
│    (Orchestration + Service Discovery)             │
└─────────┬──────────────────────────────────────────┘
          │
    ┌─────┴──────┬─────────┬──────────┬───────────┐
    │            │         │          │           │
    ▼            ▼         ▼          ▼           ▼
┌────────┐  ┌────────┐ ┌──────┐ ┌─────────┐ ┌─────────┐
│ REST   │  │ gRPC   │ │Redis │ │Postgres │ │Playwright│
│ API    │──│Planner │ │Cache │ │Database │ │Browser  │
│Gateway │  │Service │ └──────┘ └─────────┘ └─────────┘
│:7269   │  │:7035   │
└────┬───┘  └────┬───┘
     │           │
     │           ▼
     │      ┌─────────┐
     │      │ Ollama  │
     │      │ LLM     │
     │      │:11434   │
     │      └─────────┘
     │
     ▼
┌─────────────┐
│ Playwright  │
│ MCP Tools   │
└─────────────┘
```

### Key Technologies

- **.NET Aspire** - Distributed application orchestration, service discovery, telemetry
- **Microsoft Agent Framework (MAF)** - Agent patterns, context management, tool integration
- **gRPC** - High-performance internal RPC
- **REST API** - External HTTP gateway with Swagger
- **MCP (Model Context Protocol)** - Tool server for browser automation
- **Playwright** - Headless browser automation
- **Redis** - Distributed caching and session state
- **PostgreSQL** - Cognitive trail persistence
- **Ollama** - Local LLM inference