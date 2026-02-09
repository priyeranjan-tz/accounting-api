# React Frontend – Coding Agent Instructions  
## Production-Grade, Non-Negotiable Guidelines

---

## 0. Prime Directive

You are generating **production-grade React frontend code**.

- Do **not** optimize for speed of writing.
- Optimize for **correctness, maintainability, observability, accessibility, performance, and security**.
- If requirements are ambiguous or conflicting, **ask for clarification instead of guessing**.

---

## 1. Architectural Constraints (Hard Rules)

### 1.1 Feature-Based Architecture ONLY

- Organize code by **feature/domain**, not by technical type.
- Each feature owns:
  - UI components
  - hooks
  - API calls
  - state
  - tests
- Each feature must expose a **single public entry point** (`index.ts`).

❌ Do NOT create global dumping folders like:
- `utils/`
- `helpers/`
- `common/`

unless explicitly justified and approved.

---

### 1.2 Explicit Boundaries

- Shared code is allowed only in:
  - `design-system/`
  - `shared/hooks/`
  - `shared/utils/`
- Shared code must be:
  - generic
  - stable
  - reused by **two or more features**

If reuse is speculative → keep the code local to the feature.

---

## 2. TypeScript Rules (Strict)

### 2.1 TypeScript Is Mandatory

- `strict: true` is required.
- `any` is **forbidden**.
- `unknown` is allowed only with explicit type narrowing.

---

### 2.2 API Contracts

- Every API response must have:
  - a TypeScript type
  - runtime validation (schema or defensive checks)
- Never assume backend response shape.

---

## 3. State Management Rules

### 3.1 Server State ≠ UI State

- Server state is:
  - async
  - cached
  - fallible
- Do NOT store server state in global UI stores.

---

### 3.2 State Placement Hierarchy

1. Local component state  
2. Feature-level state  
3. Shared/global state (last resort)

If global state is used, **explicitly justify why lifting is unavoidable**.

---

## 4. Data Fetching Rules

### 4.1 Every API Call MUST Support

- cancellation (abort / cleanup)
- timeout handling
- error classification:
  - network
  - authentication
  - validation
  - server

---

### 4.2 Mandatory UI States

Every data-driven screen MUST implement:

- Loading state (skeleton preferred)
- Empty state
- Error state (with retry)
- Success state

❌ Never leave a screen blank or spinner-only.

---

## 5. Component Design Rules

### 5.1 Component Quality Bar

Every component must be:

- Accessible by default
- Clear about controlled vs uncontrolled behavior
- Themeable via design tokens (no hardcoded styles)
- Predictable in re-renders

---

### 5.2 Composition Over Configuration

- Prefer composition over large prop matrices.
- If props exceed ~8, refactor.

---

## 6. Forms (Critical)

### 6.1 Standard Form Behavior

- Validation rules must be explicit.
- Backend errors must map consistently to fields.
- Disable submit during submission.
- Prevent double submission.
- Show inline field errors, not alert popups.

---

### 6.2 Optimistic UI Rules

- Allowed only when rollback is safe.
- Rollback logic must be explicit.

---

## 7. Routing & Navigation

### 7.1 Routing Rules

- Route definitions must be centralized or generated.
- No hardcoded route strings scattered across code.

---

### 7.2 Authorization

- Frontend guards are **UX only**.
- Authorization correctness must be enforced by backend APIs.

---

## 8. Error Handling & Observability

### 8.1 Error Boundaries

- Global error boundary is mandatory.
- Route-level error boundaries for critical flows.

---

### 8.2 Logging

- Log meaningful events:
  - failures
  - retries
  - user-impacting actions
- Do NOT log:
  - PII
  - secrets
  - tokens

---

### 8.3 Correlation

- Surface correlation/request IDs in error UI when available.

---

## 9. Security Rules

### 9.1 Secrets & Tokens

- Never embed secrets in frontend code.
- Token storage must follow an approved security policy.
- Do NOT leak sensitive data into:
  - URLs
  - logs
  - analytics
  - localStorage (unless explicitly approved)

---

### 9.2 XSS Safety

- Avoid `dangerouslySetInnerHTML`.
- Sanitize any externally provided HTML.

---

## 10. Performance Rules

### 10.1 Mandatory Optimizations

- Code split by route.
- Lazy load non-critical components.
- Virtualize large lists and tables.
- Optimize images (responsive + lazy loading).

---

### 10.2 Re-Render Control

- Avoid unnecessary re-renders.
- Memoization must be intentional and justified.

---

## 11. Testing Requirements

### 11.1 Minimum Coverage

- Unit tests for pure logic.
- Component tests covering:
  - loading
  - empty
  - error
  - success states
- E2E tests for critical user journeys.

---

### 11.2 Test Stability

- Use stable selectors.
- Avoid brittle DOM-structure assertions.

---

## 12. Definition of Done (Enforced)

A feature or screen is **NOT DONE** unless:

- TypeScript is strict and validated
- Loading, empty, and error states exist
- Accessibility basics are met
- Errors are handled and observable
- Performance is reasonable
- Tests cover critical paths

---

## 13. Agent Behavior Rules

### 13.1 When Unsure

- Ask a clarification question.
- Do NOT invent requirements.
- Do NOT silently choose patterns without explanation.

---

### 13.2 Output Expectations

Generated code must be:

- readable
- boring
- explicit
- production-ready

Clarity > cleverness.

---

## 14. Explicit Anti-Patterns (Never Generate)

❌ God components  
❌ Global mutable state by default  
❌ Inline business logic inside JSX  
❌ Magic numbers or strings  
❌ Swallowed errors  
❌ UI without loading or error states  
❌ Copy-paste components with tiny variations  

---

## Final Instruction to the Coding Agent

> If the generated solution violates **any** rule in this document,  
> **STOP and revise before producing output**.
