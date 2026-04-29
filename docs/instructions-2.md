# Additional Refinement Instructions — Persistence, RBAC, MCP Plugin Support, and RAG Storage Enhancements

You already have full historical context of the application, architecture, documentation, and existing implementation.

Before making changes:

- Review ALL existing documentation
- Review ALL generated modules and infrastructure
- Preserve architectural consistency
- Extend the current implementation WITHOUT redesigning the system

The following are REFINEMENTS and EXTENSIONS to the existing system.

DO NOT rebuild the architecture.

---

# 🎯 OBJECTIVE

Enhance the current implementation with:

1. Improved RBAC + user administration
2. Persistent infrastructure configuration
3. MCP plugin extensibility
4. Dedicated RAG document storage architecture
5. Cloud-drive-compatible ingestion support

The application flow remains intentionally simple.

Do NOT overengineer the UX or architecture.

---

# 🔐 1. RBAC + USER MANAGEMENT ENHANCEMENTS

Extend the existing Identity/User modules.

Requirements:

Roles:

- Admin
- Manager
- Engineer

Privileges:

- Admin:
  - Full CRUD operations for users
  - Role assignment
  - User activation/deactivation

- Manager:
  - Create users
  - Update users
  - View users
  - Limited role assignment
  - Cannot manage Admin accounts

- Engineer:
  - Read-only operational access

---

# 🧩 REQUIRED IMPLEMENTATION

Implement:

- Role-based authorization policies
- Secure CRUD endpoints
- Validation and auditing
- UI management screens for user administration

Frontend:

- Add user management pages
- Role-aware UI rendering
- Protected routes

Backend:

- Authorization handlers
- Permission policies
- Secure DTO validation

DO NOT bypass RBAC checks.

---

# 💾 2. PERSISTENCE + STABLE INFRASTRUCTURE CONFIGURATION

Current issue:

- Service URLs and persistence reset when the app shuts down.

This must be fixed.

---

# REQUIRED CHANGES

## Docker Persistence

Ensure persistent Docker volumes exist for:

- PostgreSQL
- Redis
- Document storage

Use named Docker volumes.

---

## Stable Service Configuration

Ensure:

- Stable container naming
- Stable service discovery
- Stable Aspire configuration
- Environment-based configuration management

Avoid runtime-generated transient URLs when unnecessary.

---

## Configuration Requirements

Implement:

- appsettings.\* separation
- .env support
- Persistent Aspire resource configuration

Ensure:

- Restarting the application does NOT reset operational state unnecessarily.

---

# 🧠 3. MCP PLUGIN EXTENSIBILITY (IMPORTANT)

The system must support external MCP integrations IF client organizations already have MCP servers or APIs.

This is an EXTENSIBILITY requirement.

DO NOT tightly couple the system to a single MCP implementation.

---

# REQUIRED MCP ARCHITECTURE

Create a dedicated integration layer:

/AI/Mcp
/Contracts
/Clients
/Plugins
/Registry
/Adapters

---

# MCP OBJECTIVE

Allow future integration with:

- External MCP servers
- Internal MCP plugins
- External operational APIs
- Third-party telco systems

---

# REQUIRED DESIGN PRINCIPLES

The MCP integration layer must:

- Be modular
- Be provider-agnostic
- Support plugin registration
- Support future extensibility

---

# MCP EXECUTION MODEL

AI Copilot
↓
MCP Registry
↓
Available MCP Plugins
↓
Plugin Execution
↓
Structured Response
↓
AI Synthesis

---

# MCP PLUGIN EXAMPLES

Potential future plugins:

- Network Monitoring MCP
- CRM MCP
- Billing MCP
- Ticketing MCP
- External Telco API MCP

---

# IMPORTANT CONSTRAINTS

- DO NOT build distributed agents
- DO NOT create microservices
- DO NOT violate modular monolith principles

This is an internal extensibility layer.

---

# 📚 4. DEDICATED RAG DOCUMENT STORAGE SYSTEM

The current RAG implementation must be extended.

Create a dedicated document ingestion + storage architecture.

---

# REQUIRED STRUCTURE

/AI/Rag
/Documents
/Ingestion
/Storage
/Providers
/Indexing
/Embeddings
/Chunking
/Retrieval

---

# DOCUMENT STORAGE REQUIREMENTS

The system must support:

- Local document storage
- Cloud-linked storage providers
- Future extensibility

---

# SUPPORTED DOCUMENT SOURCES

Design abstractions for:

- Local uploads
- Google Drive
- OneDrive
- SharePoint
- Azure Blob Storage

Even if all providers are not fully implemented immediately, the architecture must support them cleanly.

---

# REQUIRED DOCUMENT FLOW

Document Source
↓
Ingestion Pipeline
↓
Storage Provider
↓
Chunking
↓
Embedding Generation
↓
pgvector Indexing
↓
Retrieval

---

# STORAGE DESIGN REQUIREMENTS

Implement:

- Metadata tracking
- File versioning support
- Document categorization
- Retrieval indexing

---

# FRONTEND REQUIREMENTS

Add:

- Document management page
- Upload interface
- Cloud provider connection UI
- Indexed document status tracking

---

# ⚙️ 5. APPLICATION FLOW SIMPLICITY

The application flow is intentionally SIMPLE.

Avoid:

- Overly complex orchestration
- Excessive abstractions
- Unnecessary event chains
- Overengineered UX

Focus on:

- Clean architecture
- Clear workflows
- Practical functionality
- Demo readiness

---

# 🧠 IMPORTANT IMPLEMENTATION INSTRUCTION

Before implementing:

1. Read existing documentation
2. Review current modules
3. Review existing Aspire configuration
4. Review Docker setup
5. Review current AI orchestration
6. Extend the system consistently

DO NOT generate disconnected implementations.

All new functionality must feel native to the existing architecture.

---

# 🧪 EXPECTED OUTCOME

The enhanced system should now support:

- Enterprise-grade RBAC
- Persistent infrastructure
- MCP plugin extensibility
- Dedicated RAG document storage
- Cloud-drive-compatible ingestion
- Stable operational environments

WITHOUT compromising:

- Modular monolith architecture
- Simplicity
- Maintainability
- Demo-readiness

---

# IMPLEMENTATION PRIORITY

1. Persistence fixes
2. RBAC enhancements
3. RAG document storage
4. Cloud provider abstractions
5. MCP plugin architecture
6. UI integration

---

# FINAL INSTRUCTION

Do NOT redesign the system.

Extend the current implementation carefully, consistently, and incrementally while preserving the existing architectural direction and application simplicity.
