# Angular Frontend – Coding Agent Instructions  
## Production-Grade, Non-Negotiable Guidelines

---

## 0. Prime Directive

You are generating **production-grade Angular application** code.

- Optimize for **correctness, maintainability, performance, accessibility, security, and clarity**.
- If requirements are ambiguous or conflicting, **ask for clarification** instead of assuming.

---

## 1. Architectural & Structural Rules

### 1.1 Feature/Domain-First Architecture

- Organize code by **feature/domain**, not by technical kind (avoid dumping all components in one folder).
- Each feature should be:
  - self-contained (components, services, models, routing)
  - decoupled
  - owned by a clear folder boundary
- Shared modules for reusable UI, pipes, directives, types only if truly reused across ≥2 features.

👍 Recommended: **Standalone components** where possible in new Angular apps. :contentReference[oaicite:1]{index=1}

---

### 1.2 Modules & Isolation

- Use **Angular Modules** only for cross-cutting concerns, lazy loaded areas, or legacy needs.
- Avoid deep module hierarchies that add unnecessary boilerplate.

---

### 1.3 CLI & Workspace Conventions

- Generate code using **Angular CLI** to enforce style standards and correct file placement.  
- Maintain a predictable folder structure for apps vs libraries (`projects/`, `libs/`).  
- Configure `angular.json` defaults to enforce build boundaries and performance budgets. :contentReference[oaicite:2]{index=2}

---

## 2. TypeScript Discipline

### 2.1 Strict Typing

- `strict: true` is mandatory.
- Avoid `any`; use specific types and interfaces.
- Validate API response shapes at the boundary (guards/schemas).

---

### 2.2 Reactive Models

- Use signals or reactive patterns depending on state characteristics and team preference (RxJS or Signals).

---

## 3. State & Data Management

### 3.1 Server vs UI State

- Treat server state as ephemeral and fallible.
- Maintain server data through observables/signals with clear caching and invalidation.

---

### 3.2 Local vs Global

- Local signals/observables for component state.
- Global shared state only when multiple unrelated features depend on it.

---

## 4. Routing & Navigation

### 4.1 Centralized Route Definition

- Define routes in a centralized module/feature routing file.
- Use strong typing for route parameters and guards.

---

### 4.2 Lazy Loading

- All feature routes that can be lazy-loaded **must be lazy-loaded** to reduce initial bundle size.

---

## 5. Component Design Standards

### 5.1 Component Quality

- Components must be:
  - **Accessible (a11y)** by default
  - **Single responsibility**
  - Controlled reactivity (OnPush where beneficial)
  - Isolated styling and template logic

---

### 5.2 Template Best Practices

- Avoid complex logic in templates.
- Prefer `*ngFor trackBy` for large lists to avoid unnecessary DOM work.
- Use async pipes for observable values to manage subscriptions. :contentReference[oaicite:3]{index=3}

---

## 6. Performance Rules

### 6.1 Change Detection

- Prefer `ChangeDetectionStrategy.OnPush` for performance where state is immutable or tracked explicitly.  
- Avoid unnecessary change detection cycles. :contentReference[oaicite:4]{index=4}

---

### 6.2 Bundling & Load Optimization

- Lazy load modules and routes.
- Define size **budgets** in CLI configuration (`angular.json`). :contentReference[oaicite:5]{index=5}

---

### 6.3 Image & Asset Optimization

- Use `NgOptimizedImage` for images to improve loading and LCP performance. :contentReference[oaicite:6]{index=6}

---

## 7. Security Best Practices

### 7.1 Angular Security Rules

- Keep Angular and dependencies up-to-date (security patches). :contentReference[oaicite:7]{index=7}
- Validate and sanitize all inputs rigorously. :contentReference[oaicite:8]{index=8}

---

### 7.2 Template & DOM Safety

- Avoid direct use of `innerHTML` unless sanitized via Angular DomSanitizer. :contentReference[oaicite:9]{index=9}
- Never use unsafe bypass methods unless absolutely necessary.

---

### 7.3 Communication Security

- Use HTTPS for API calls.
- Implement secure token handling (no secrets in front end).
- Validate API inputs and outputs server-side; frontend checks are UI only.

---

## 8. Accessibility (a11y)

- All UI elements must be keyboard-navigable.
- Use appropriate ARIA roles and attributes for custom components. :contentReference[oaicite:10]{index=10}
- Validate accessibility with automated tooling (Lighthouse, aXe).

---

## 9. Forms & Validation

- Use Angular **Reactive Forms** for complex forms.
- All forms must:
  - have validation
  - debounced input events where relevant
  - clear error messaging
  - proper form state indicators (touched, dirty, etc.)

---

## 10. Error Handling & Observability

### 10.1 Global Error Boundary

- Intercept errors globally (`ErrorHandler`) and locally where appropriate.
- Provide meaningful error messages and recovery UI.

---

### 10.2 Logging

- Log UI errors and crucial events (not sensitive data).
- Integrate telemetry for failures and key transitions.

---

## 11. Testing Requirements

### 11.1 Testing Types

- Unit tests for services, components, pipes.
- Component tests with variant states.
- E2E tests for critical user paths (login, navigation, CRUD).  
- Use stable selectors and avoid brittle DOM checks.

---

## 12. Definition of Done

A feature/screen is **NOT DONE** unless:

- Type casting and validations are explicit.
- Lazy loading implemented.
- Loading/empty/error states handled.
- Performance optimizations applied.
- Accessibility verified.
- Tests implemented covering critical behavior.

---

## 13. Coding Anti-Patterns (Forbidden)

❌ Deep inheritance trees  
❌ Business logic in templates  
❌ Large monolithic modules  
❌ Silent error swallowing  
❌ Data fetching without loading/error states  
❌ Unsafe input insertions (avoid unsafe DOM APIs)

---

## Final Agent Directive

> If any generated output violates a rule in this document, the agent must **stop and revise before output**.
