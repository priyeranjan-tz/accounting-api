# Technical Specification: NEMT Core EDI Engine

## 1. System Overview
The **Core EDI Engine** is a high-performance, stateless middleware designed to bridge the NEMT Dispatch Platform with the **Stedi** EDI ecosystem. It handles the translation of transport-specific business logic into HIPAA-compliant healthcare transactions.

---

## 2. Architectural Decisions

### 2.1 Tech Stack
* **Framework:** .NET 10 (C#) using Native AOT for optimized cold starts and memory efficiency.
* **Architecture Pattern:** Decoupled Queue-Worker Pattern (Producer-Consumer).
* **Database:** PostgreSQL 17 with JSONB support for EDI payload archiving.
* **Message Broker:** Amazon SQS (Standard).
* **EDI Provider:** Stedi Healthcare APIs (JSON-to-EDI).

### 2.2 Infrastructure Logic
* **Transportation Base Isolation:** All requests must be context-aware of the `Base_ID`.
* **Idempotency:** Every claim submission must use an `Idempotency-Key` (UUID) to prevent duplicate billing.
* **Concurrency Control:** The Worker Service implements a "Leaky Bucket" or "Semaphore" logic to stay within Stedi’s concurrency limits (default 100 for claims).

---

## 3. Container & Component Layering

| Layer | Responsibility | Tech Component |
| :--- | :--- | :--- |
| **Ingress API** | Request validation & Queueing | ASP.NET Core 10 (Minimal API) |
| **Message Broker** | Reliable async delivery | Amazon SQS / Azure Service Bus |
| **Worker Service** | Stedi Integration & Logic | .NET 10 BackgroundWorker |
| **Mapping Engine** | JSON Translation | Mapperly (Source Generated) |
| **Audit DB** | Transaction Logging | PostgreSQL |



---

## 4. Observability via OpenTelemetry (OTel)

To ensure full transparency of the claim lifecycle, the engine implements OpenTelemetry for Traces, Metrics, and Structured Logs.

### 4.1 Required OTel NuGet Libraries
To implement this in .NET 10, include the following packages:

```xml
<PackageReference Include="OpenTelemetry.Extensions.Hosting" Version="1.10.*" />
<PackageReference Include="OpenTelemetry.Exporter.OpenTelemetryProtocol" Version="1.10.*" />

<PackageReference Include="OpenTelemetry.Instrumentation.AspNetCore" Version="1.9.*" />
<PackageReference Include="OpenTelemetry.Instrumentation.Http" Version="1.9.*" />
<PackageReference Include="OpenTelemetry.Instrumentation.Runtime" Version="1.9.*" />
<PackageReference Include="Npgsql.OpenTelemetry" Version="8.0.*" /> <PackageReference Include="OpenTelemetry.Exporter.Console" Version="1.10.*" />