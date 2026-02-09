# Frontend Service Tech Stack Standard (Angular + Tailwind + Tests)

This document defines the **non-negotiable tech stack**, **project layout**, **quality gates**, and **code style rules** for a production-grade frontend service built with **Angular**, styled with **Tailwind CSS**, with **unit tests** and **E2E tests using Playwright**.

> Vite is **not required** here. Use the **Angular CLI** build system.

---

## 1) Baseline Tech Stack (Required)

### Runtime & Build
- **Node.js:** 20.x LTS (pin via `.nvmrc` or `volta`)
- **Package manager:** `pnpm` (preferred) or `npm` (choose one; do not mix)
- **Framework:** **Angular** (latest stable)
- **Language:** **TypeScript** (strict)
- **Build tool:** **Angular CLI** (default builder)
- **Styling:** **Tailwind CSS**

### Quality & Tooling
- **Linting:** ESLint (Angular + TypeScript rules)
- **Formatting:** Prettier
- **Type checks:** `ng build` (AOT) + `tsc` as needed
- **Unit tests:** Angular Testing Utilities + **Jasmine/Karma** (default) **OR** Jest (choose one and standardize)
- **E2E tests:** **Playwright**
- **Git hooks:** Husky + lint-staged
- **CI:** run lint, typecheck/build, unit tests, E2E tests (headless)

---

## 2) Recommended Package Set

> Keep dependencies lean. Any new dependency requires PR justification.

**Core**
- `@angular/core`, `@angular/common`, `@angular/router`
- `@angular/cli`, `@angular/compiler-cli`
- `typescript`

**Tailwind**
- `tailwindcss`, `postcss`, `autoprefixer`
- Optional: `tailwind-merge` + `clsx` (use carefully; Angular often prefers `[ngClass]`)

**Lint/Format**
- `eslint`, `eslint-config-prettier`, `prettier`
- `@typescript-eslint/eslint-plugin`, `@typescript-eslint/parser`
- `@angular-eslint/eslint-plugin`, `@angular-eslint/eslint-plugin-template`, `@angular-eslint/template-parser`

**Unit Testing (choose one)**
- **Option A (default Angular):** Jasmine + Karma (`ng test`)
- **Option B (recommended modern):** Jest + `jest-preset-angular`

**E2E Testing**
- `@playwright/test`

**Git Hooks**
- `husky`, `lint-staged`

---

## 3) Project Structure (Standard)

Prefer a **feature-first** structure. Keep shared utilities and UI components in explicit shared areas.

```
.
├─ src/
│  ├─ app/
│  │  ├─ core/                 # singleton services, interceptors, guards, global providers
│  │  ├─ shared/               # shared UI components, pipes, directives (reusable)
│  │  ├─ features/             # feature modules / standalone feature areas
│  │  │  └─ <feature>/
│  │  │     ├─ pages/          # route components
│  │  │     ├─ components/     # feature-only components
│  │  │     ├─ services/       # feature services (facades, API adapters)
│  │  │     ├─ models/         # types and interfaces
│  │  │     └─ index.ts
│  │  ├─ app.routes.ts         # route config (prefer standalone + route-level providers)
│  │  └─ app.config.ts         # app-wide providers configuration
│  ├─ styles.css               # tailwind entry + global styles
│  └─ main.ts
├─ e2e/                        # Playwright tests
├─ .editorconfig
├─ .eslintrc.cjs
├─ .prettierrc
├─ tailwind.config.ts
└─ angular.json
```

**Rules**
- `core/` contains **singletons** only (provided in root or app config).
- `shared/` contains reusable UI building blocks; no feature-specific logic.
- `features/` contains business logic and feature UI.
- Avoid circular dependencies. Features should not import each other directly.

---

## 4) Angular Configuration Defaults (Non-Negotiable)

### Change Detection
- Use `ChangeDetectionStrategy.OnPush` by default for components (especially reusable/shared).
- Prefer immutable updates for state and inputs.

### Standalone-first
- Prefer **standalone components** and **route-level providers** (modern Angular pattern).
- Keep modules minimal; use them only when they simplify composition.

### Routing
- Lazy-load features (routes) by default.
- Co-locate route components under `features/<feature>/pages`.

---

## 5) TypeScript Rules (Non-Negotiable)

### Compiler Settings
- `strict: true`
- `noUncheckedIndexedAccess: true` (recommended)
- `exactOptionalPropertyTypes: true` (recommended)
- `noImplicitOverride: true` (recommended)

### Coding Rules
- **No `any`** in app code.
- Prefer `unknown` for untrusted data (API/local storage).
- Strong typing at boundaries:
  - HTTP responses
  - route params
  - forms value shapes

---

## 6) Code Style Rules (Non-Negotiable)

### Naming (Angular conventions)
- Files should follow Angular conventions:
  - `*.component.ts`, `*.component.html`, `*.component.css`
  - `*.service.ts`, `*.directive.ts`, `*.pipe.ts`, `*.guard.ts`
- Classes/Components: `PascalCase`
- Selectors: `app-` prefix (or org prefix), kebab-case (e.g., `app-trip-card`)
- Variables/functions: `camelCase`

### Imports
- Use path aliases for `src/*` (e.g., `@app/*`, `@shared/*`, `@features/*`).
- Keep import order consistent:
  1) Angular packages
  2) RxJS
  3) external libs
  4) internal (`@…`)
  5) relative

### Components
- Keep components small; extract reusable logic into:
  - pure helper functions
  - services (facades)
  - RxJS pipelines
- Avoid business logic in templates.

### Templates
- Keep templates readable:
  - Avoid deeply nested `*ngIf/*ngFor` without extracting.
  - Prefer `async` pipe over manual subscription.
- **No function calls in templates** if they can re-run often; use memoized observables or signals.

---

## 7) Formatting Rules (Prettier)

- Prettier is the **only** formatter.
- Recommended settings:
  - `singleQuote: true`
  - `semi: true`
  - `printWidth: 100`
  - `trailingComma: all`

---

## 8) ESLint Rules (Must Enforce)

Minimum enforce:
- Angular ESLint recommended rules
- Template linting (a11y-related and best practices)
- No unused vars/imports
- Import ordering (optional but recommended)
- No `console.log` in production code (allow `console.error` in error boundaries only if policy permits)

**Strictness policy**
- Lint must fail CI on `error` level rules.
- Avoid disabling lint rules. If needed:
  - `// eslint-disable-next-line <rule> -- rationale`
  - include short rationale.

---

## 9) RxJS & State Rules (Non-Negotiable)

### Subscription hygiene
- Prefer `async` pipe in templates (no manual subscription).
- If manual subscriptions are necessary:
  - use `takeUntilDestroyed()` (Angular modern) or a controlled `Subject` teardown pattern.
- Never leak subscriptions.

### State approach
Choose **one** and standardize (team decision):
- RxJS services (facade pattern), or
- Signals-based state (where applicable), or
- ComponentStore / NgRx (only if complexity demands it)

---

## 10) Forms Standard

- Prefer **Reactive Forms** for non-trivial forms.
- Define typed form models (strong types for form value).
- Validate at boundaries:
  - sync validators for simple constraints
  - async validators for server checks
- Provide accessible labels and error messages.

---

## 11) Unit Testing Standard

### What to test
- Component behavior (inputs/outputs, DOM state, interactions)
- Service logic (facade/service methods, mapping/transforms)
- Guards/resolvers/interceptors (policy and branching logic)

### What NOT to test
- Tailwind class strings
- Angular internals

### Guidelines
- Tests must be deterministic and isolated.
- Use Angular testing utilities properly (TestBed or standalone testing patterns).
- Prefer testing behavior via DOM queries and events.

### Coverage
- Target thresholds (adjust by maturity):
  - Lines: 70%+
  - Branches: 60%+
  - Functions: 70%+

---

## 12) E2E Testing Standard (Playwright)

### Scope
- Critical happy paths (routing, primary workflows, create/edit flows)
- Regressions (navigation, auth flows if present, data persistence)
- Cross-browser sanity (Chromium + WebKit minimum)

### Guidelines
- Prefer role/label selectors.
- Use `data-testid` only when needed.
- Capture screenshots/videos on failure in CI.
- Keep tests isolated and data-independent.

---

## 13) Performance Standards (Angular-specific)

- Default to `OnPush`.
- Use `trackBy` with `*ngFor` for lists.
- Avoid heavy computations in templates.
- Prefer route-level code splitting and lazy loading.
- Use Angular’s recommended runtime performance practices (e.g., avoid unnecessary zone work where applicable).

---

## 14) Security Baseline

- No secrets in frontend.
- Sanitize untrusted HTML; avoid bypassing Angular sanitization.
- Avoid dangerous DOM usage unless strictly required and reviewed.
- Keep dependencies pinned; run audits/scanning in CI.

---

## 15) CI Quality Gates (Non-Negotiable)

A PR must pass:
1) `pnpm lint`
2) `pnpm format` (check)
3) `pnpm build` (AOT build)
4) Unit tests (`pnpm test` or `ng test --watch=false`)
5) `pnpm e2e` (Playwright headless)

Optional (recommended):
- Bundle size budget checks (Angular budgets)
- Lighthouse CI for customer-facing apps

---

## 16) Definition of Done

A ticket is done only when:
- Feature works end-to-end with loading/empty/error states
- Unit tests cover core behavior
- E2E covers critical workflow (if applicable)
- Lint/build/tests are green
- A11y basics verified (keyboard navigation + labels + error messages)
- No console errors/warnings in devtools

---

## 17) Agent Instructions (Copy/Paste)

When generating code:
- Follow feature-first structure and boundaries (`core`, `shared`, `features`).
- Use strict TypeScript; do not introduce `any`.
- Default to `OnPush` change detection.
- Prefer `async` pipe and subscription-safe RxJS patterns.
- Use Tailwind for styling; avoid additional CSS frameworks.
- Add unit tests for non-trivial components/services.
- Add Playwright E2E tests for end-to-end user workflows.
- Ensure lint, format, build, unit tests, and E2E tests pass before finalizing.
