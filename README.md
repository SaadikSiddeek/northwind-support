# Northwind Support Desk

A support ticket management system: raise tickets, search and filter them, assign them to agents,
and move them through their lifecycle.

**.NET 10 Web API** + **React 19 / TypeScript** + **SQL Server**, in one monorepo.

---

## The assignment

You are joining a team that already has this app working. Complete the tasks **in order**.
Full requirements are in **[`ASSIGNMENT_CANDIDATE.md`](ASSIGNMENT_CANDIDATE.md)**.

| # | Task | Done looks like | Effort |
| --- | --- | --- | --- |
| 1 | **Fix a defect, add server-side filtering** | Root cause fixed with a regression test; every list filter applied in the database with a correct count | ~2.5 h |
| 2 | **Ticket escalation & assignment** | Automatic triage on create, escalation with history, SLA status, UI for all of it | ~7 h |
| 3 | **Login and authentication** | Agents sign in; API rejects unauthenticated calls; login UI | ~4 h |
| 4 | **Dockerise the API** *(optional)* | `docker compose up` gives a migrated, seeded API with no baked-in secret | ~1.5 h |

### Guidance

- **Time: 2 days.** Honestly marking a task "not finished" beats rushing all of them.
- **Fork** this repo to your own GitHub account and work there. **Do not push to the original.**
- **AI tools are allowed.** You must understand and be able to defend every line you submit.
- **Commit as you go** and keep the real history. Commit the Task 1 defect fix and the filtering
  separately from each other and from the feature work. A single squashed commit is not accepted.
- **Where the spec is ambiguous**, make a decision, write it down in the README, and move on.
- **Out of scope:** registration, password reset, roles, OAuth/SSO, refresh tokens, MFA,
  notifications, CI, caching, visual redesign. Mention them under *Known limitations* instead.

### Submission

1. Update this **README** with what you built, your design decisions (chosen, rejected and why),
   the defect's root cause, dev login credentials, how to run and test it, AI usage, and known
   limitations. A reviewer must be able to run it without asking you anything.
2. Add an **APPROACH.md** explaining your approach and thought process for each task.
3. Commit both, then send us the **link to your fork**, the **dev login credentials**, and a
   **rough note of time spent** on what. (If the fork is private, add the reviewers as collaborators.)

---

## Quick start

**Prerequisites:** .NET SDK 10 (`global.json`), Node.js 22 (`.nvmrc`; 20.19+ works), Docker.

```bash
cp .env.example .env          # optional, every value has a working default
npm install                   # installs the web app
npm run db:up                 # SQL Server 2022 in Docker on localhost:1433
npm run api                   # terminal 1: migrates, seeds, then serves the API
npm run web                   # terminal 2: React dev server
```

| What | Where |
| --- | --- |
| Web app | http://localhost:5173 |
| API | https://localhost:7043 (also http://localhost:5043) |
| Swagger | https://localhost:7043/swagger |

In Development the API applies migrations and seeds demo data on startup. **Never commit real
credentials.**

**Visual Studio 2026:** run `npm run db:up`, open `SupportDesk.sln`, set
**SupportDesk.Presentation** as the startup project, and press F5 on the **https** profile.

## Tests

```bash
npm test                      # dotnet test, then the web tests (Vitest)
```

Both pass on a clean checkout and neither needs a database.

## Commands

| Command | What it does |
| --- | --- |
| `npm run db:up` / `db:down` / `db:reset` | Start / stop / wipe SQL Server |
| `npm run api` / `npm run web` | Run the API / the React app |
| `npm run build` | Build both |
| `npm run lint` / `typecheck` / `format` | Web lint and types / `dotnet format` |
| `npm run ef -- migrations add <Name>` | Add an EF Core migration |

## API at a glance

| Endpoint | Description |
| --- | --- |
| `GET /api/tickets` | Paged list with `search`, `status`, `priority`, `categoryId`, `customerId`, `assignedAgentId`, `unassignedOnly`, `sortBy`, `sortDirection`. **Filters are accepted but not applied yet** (Task 1). |
| `GET /api/tickets/{id}` | One ticket |
| `POST /api/tickets` | Create a ticket |
| `PATCH /api/tickets/{id}/status` · `/assignment` | Change status / assignee |
| `GET /api/customers` · `/api/agents` · `/api/categories` | Reference data |

Errors are `ProblemDetails`: `400` validation, `404` not found, `409` conflict or business rule.

**Seed data:** 5 categories, 6 customers (3 Premium), 6 agents (one inactive, one at their limit)
and 40 tickets across every status, priority and SLA state.

## Troubleshooting

| Symptom | Fix |
| --- | --- |
| API cannot reach the database | `npm run db:up`, wait ~20 s, check `docker compose ps` |
| Port 1433 already in use | Change the port in `docker-compose.yml` and `.env` |
| Browser warns about the certificate | `dotnet dev-certs https --trust`, or use the web app, which proxies |
| Web app shows "Unable to load tickets" | The API is not running, or `VITE_API_PROXY_TARGET` points at the wrong port |
| SQL Server slow on Apple Silicon | Enable Rosetta in Docker Desktop → Settings → General |


## Implementation Notes

### Task 1 — Defect Fix and Server-Side Filtering

**Status:** Implemented and covered by automated regression tests. End-to-end verification against SQL Server is pending.

#### 1.1 DEFECT-117 — Ticket Filters Not Taking Effect

**Problem**

Changing the Status dropdown on the ticket list did not immediately trigger a new `GET /api/tickets` request. However, typing into the search field triggered a request containing the previously selected status, making the filters appear inconsistent.

**Root cause**

The ticket-list data-fetching hook did not react to changes in all filter dependencies. Search changes triggered a new request through the existing search-related dependency, while changes to filters such as Status did not independently trigger a refetch.

**Fix**

Updated the existing ticket-list hook so changes to any relevant filter trigger a new API request. Search text remains debounced to avoid sending a request for every keystroke.

The fix addresses the request lifecycle rather than filtering tickets locally, forcing a page reload, or remounting the component.

**Regression testing**

Added frontend regression tests using Vitest and React Testing Library. The original implementation failed the regression test, and the corrected implementation passed.

The tests verify that changing filters triggers the expected request while preserving debounced search behavior.

**Design decision**

I retained the existing custom data-fetching hook instead of introducing a second filtering mechanism. This keeps request handling in one place and allows additional filters, such as the SLA status filter required by Task 2, to participate in the same request lifecycle.

#### 1.2 Server-Side Filtering

**Problem**

The API already accepted filtering parameters through `TicketQuery`, but the database query did not apply them. Consequently, the API returned unfiltered results and pagination counts were calculated from the full dataset.

**Implementation**

Implemented filtering in `TicketQueries.GetPagedAsync()` using a separate, composable `ApplyFilters()` method.

Supported filters:

- Exact-match filtering by status, priority, category ID, customer ID, and assigned agent ID.
- `unassignedOnly=true` to return tickets without an assigned agent.
- Search across ticket title, ticket reference, and customer name.
- Trimming search input and ignoring blank searches.
- Combining supplied filters using AND conditions.
- Applying filters in the database before counting, sorting, and pagination.

The filtered `IQueryable` is used for both the total count and the paginated result. This ensures pagination metadata reflects the filtered dataset rather than all tickets.

**Search behavior**

Search is case-insensitive and ignores surrounding whitespace. During testing, I discovered that SQLite's translation of `Contains()` behaved case-sensitively for the tested expressions. I updated the search comparison to normalize both sides to lowercase, allowing the regression tests to pass consistently with SQLite.

**Design decisions**

I kept filtering in the existing infrastructure query rather than loading all tickets and filtering them in application memory. This allows EF Core to translate the predicates into SQL and lets the database perform filtering before pagination.

I extracted the predicates into `ApplyFilters()` instead of adding separate query pipelines for each filter. To support a future `slaStatus` filter, the existing query can be extended with another predicate without restructuring the counting or paging logic.

For this assessment, lowercase normalization provides straightforward cross-provider search behavior. In production, I would review database collation and indexing because applying `LOWER()` to database columns can reduce index efficiency.

#### Testing and Verification

**Backend**

Added xUnit regression tests using an in-memory SQLite database to execute the EF Core query.

The tests cover:

- Exact-match status and priority filtering.
- Combining multiple filters.
- Search by title, reference, and customer name.
- Case-insensitive search and trimmed input.
- Pagination after filtering, including the correct total count.
- Excluding assigned tickets when `unassignedOnly` is enabled.
- Exact-match category, customer, and assigned-agent ID filters.

**Results**

- Ticket filtering tests: **10 passed, 0 failed**.
- Complete backend unit test suite: **36 passed, 0 failed**.
- Git whitespace check: passed.

**Commands**

```bash
dotnet test apps/api/tests/SupportDesk.UnitTests/SupportDesk.UnitTests.csproj --filter "FullyQualifiedName~TicketQueriesTests"
dotnet test apps/api/tests/SupportDesk.UnitTests/SupportDesk.UnitTests.csproj
```

**Verification still required**

The backend filtering tests use SQLite. The application uses SQL Server, so the actual API endpoint should also be checked against the running SQL Server database, including its generated SQL and filtered pagination results.

#### Git History

The defect fix and server-side filtering were committed separately, as required:

- `c838df3` — `fix(tickets): refetch on all filter changes`
- `5fcdd37` — `feat(tickets): add server-side filtering and query regression tests`

#### AI-Assisted Development

I used ChatGPT as a development assistant to discuss the defect, understand the existing data-fetching flow, plan regression tests, and review approaches to EF Core filtering.

I verified the implementation by reproducing the original issue, running failing and passing regression tests, reviewing the test output, and running the complete backend unit test suite.

An important issue identified during testing was the difference in case-sensitive search behavior. The initial `Contains()` implementation failed two search cases, so I revised the implementation and reran the tests.

The implementation and tests remain subject to my own review and explanation during the technical discussion.

#### Known Limitations

- SQL Server end-to-end filtering and generated SQL have not yet been verified.
- The case-insensitive search implementation may not use ordinary indexes efficiently on large datasets.
- NuGet currently reports security advisories for `System.Security.Cryptography.Pkcs` version `9.0.11`; the dependency and appropriate remediation still require review.
- Tasks 2, 3, and 4 are not documented as completed at this stage.

