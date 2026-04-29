## To dos

    # Additional AI Enhancement Instructions — RAG + Lightweight MCP Integration

You already understands the project architecture, application structure, modules, and historical implementation context.

DO NOT regenerate or redesign the existing AI architecture.

This instruction ONLY introduces:

1. Retrieval-Augmented Generation (RAG)
2. Lightweight MCP-style internal tool orchestration (ONLY if beneficial)

These enhancements must integrate cleanly into the existing modular monolith architecture.

---

# 🎯 OBJECTIVE

Enhance the AI Copilot so it can:

- Retrieve operational/network knowledge dynamically
- Use internal tools intelligently
- Produce context-aware telco operational insights
- Maintain modular monolith boundaries

The goal is NOT to create a generic chatbot.

The goal is to create an AI-native telco operations assistant.

---

# 🧠 RAG REQUIREMENTS (MANDATORY)

Implement Retrieval-Augmented Generation (RAG) inside the existing AI module.

Use RAG for:

- Historical outage analysis
- Network incident retrieval
- Engineering SOP lookup
- Operational diagnostics
- Tower performance history
- Connectivity recommendations

---

# 🗄️ VECTOR STORAGE

Use:

- PostgreSQL + pgvector

DO NOT introduce external vector databases unless absolutely necessary.

---

# 📦 REQUIRED RAG STRUCTURE

Extend the existing AI module with:

/AI/Rag
/Chunking
/Embeddings
/Indexing
/Retrievers
/Stores

---

# 📚 RAG DATA SOURCES

The RAG pipeline must support indexing:

1. Incident reports
2. Outage summaries
3. Network diagnostics logs
4. Engineering documentation
5. Tower performance summaries
6. Historical alert records

Use mock/demo data where necessary.

---

# 🔄 RAG PIPELINE

Implement the following retrieval flow:

Document/Data
↓
Chunking
↓
Embedding generation
↓
Store embeddings in pgvector
↓
Similarity search retrieval
↓
Inject retrieved context into Semantic Kernel workflows

---

# 🧠 EMBEDDING REQUIREMENTS

Use Azure OpenAI embeddings.

Requirements:

- Async embedding generation
- Clean embedding abstraction
- Reusable retrieval services

DO NOT hardcode embedding logic into controllers.

---

# 🧩 RAG USAGE FLOW

Example:

User:
"Why is Lagos West slow?"

RAG retrieves:

- Related outage incidents
- Congestion history
- Tower degradation reports
- Historical latency spikes

AI synthesizes:

- Root cause analysis
- Suggested action
- Context-aware explanation

---

# 🛠️ LIGHTWEIGHT MCP-STYLE TOOLING (OPTIONAL BUT RECOMMENDED)

Implement lightweight INTERNAL tool orchestration only if it improves AI capability orchestration.

IMPORTANT:

- DO NOT implement external MCP servers.
- DO NOT introduce distributed agents.
- DO NOT violate modular monolith boundaries.

This is NOT a microservices system.

---

# 🧠 PURPOSE OF MCP-STYLE TOOLING

Allow the AI Copilot to dynamically invoke internal operational capabilities.

Examples:

- GetNetworkMetrics
- GetRegionalOutages
- AnalyzeLatency
- FindBestConnectivity

---

# 🧩 TOOL ARCHITECTURE

Implement tools inside:

/AI/Tools

Example structure:

/AI/Tools
/GetNetworkMetricsTool.cs
/GetOutagesTool.cs
/AnalyzeLatencyTool.cs
/FindBestConnectivityTool.cs

---

# ⚠️ TOOL EXECUTION RULES

Each tool:

- MUST use MediatR internally
- MUST NOT use HTTP calls
- MUST return structured results
- MUST be independently testable

---

# 🔄 MCP-STYLE EXECUTION FLOW

User Query
↓
Intent Detection
↓
Determine Required Tools
↓
Execute Internal Tools
↓
Retrieve RAG Context
↓
Generate AI Response

---

# 🧠 SEMANTIC KERNEL INTEGRATION

Semantic Kernel remains the orchestration layer.

It must:

- Select tools dynamically
- Inject retrieved RAG context
- Coordinate workflows
- Generate final responses

DO NOT move orchestration logic into controllers.

---

# 🚫 DO NOT DO THESE

- NO external MCP infrastructure
- NO microservices
- NO HTTP-based internal tool calls
- NO giant monolithic AI service
- NO prompt-only orchestration
- NO embedding generation inside API controllers

---

# ✅ EXPECTED RESULT

The final AI system should:

- Understand telco operational queries
- Retrieve relevant historical context
- Dynamically invoke internal capabilities
- Generate intelligent operational insights
- Preserve modular monolith architecture integrity

---

# IMPLEMENTATION PRIORITY

1. pgvector setup
2. Embedding infrastructure
3. Retrieval pipeline
4. Semantic Kernel integration
5. Internal MCP-style tools (optional enhancement)
6. AI orchestration refinement

---

# FINAL INSTRUCTION

RAG is REQUIRED.

MCP-style tooling is OPTIONAL but recommended if it improves orchestration clarity and extensibility without overengineering the system.
