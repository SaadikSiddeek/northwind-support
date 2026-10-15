# Northwind Support Desk

A support ticket management system: raise tickets, search and filter them, assign them to agents,
and move them through their lifecycle.

**.NET 10 Web API** + **React 19 / TypeScript** + **SQL Server**, in one monorepo.

---

## The assignment

You are joining a team that already has this app working. Complete the tasks **in order**.
Full requirements are in **[`ASSIGNMENT_CANDIDATE.md`](ASSIGNMENT_CANDIDATE.md)**.

| #   | Task                                        | Done looks like                                                                                         | Effort |
| --- | ------------------------------------------- | ------------------------------------------------------------------------------------------------------- | ------ |
| 1   | **Fix a defect, add server-side filtering** | Root cause fixed with a regression test; every list filter applied in the database with a correct count | ~2.5 h |
| 2   | **Ticket escalation & assignment**          | Automatic triage on create, escalation with history, SLA status, UI for all of it                       | ~7 h   |
| 3   | **Login and authentication**                | Agents sign in; API rejects unauthenticated calls; login UI                                             | ~4 h   |
| 4   | **Dockerise the API** _(optional)_          | `docker compose up` gives a migrated, seeded API with no baked-in secret                                | ~1.5 h |

### Guidance

- **Time: 2 days.** Honestly marking a task "not finished" beats rushing all of them.
- **Fork** this repo to your own GitHub account and work there. **Do not push to the original.**
- **AI tools are allowed.** You must understand and be able to defend every line you submit.
- **Commit as you go** and keep the real history. Commit the Task 1 defect fix and the filtering
  separately from each other and from the feature work. A single squashed commit is not accepted.
- **Where the spec is ambiguous**, make a decision, write it down in the README, and move on.
- **Out of scope:** registration, password reset, roles, OAuth/SSO, refresh tokens, MFA,
  notifications, CI, caching, visual redesign. Mention them under _Known limitations_ instead.

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

| What    | Where                                               |
| ------- | --------------------------------------------------- |
| Web app | http://localhost:5173                               |
| API     | https://localhost:7043 (also http://localhost:5043) |
| Swagger | https://localhost:7043/swagger                      |

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

| Command                                  | What it does                         |
| ---------------------------------------- | ------------------------------------ |
| `npm run db:up` / `db:down` / `db:reset` | Start / stop / wipe SQL Server       |
| `npm run api` / `npm run web`            | Run the API / the React app          |
| `npm run build`                          | Build both                           |
| `npm run lint` / `typecheck` / `format`  | Web lint and types / `dotnet format` |
| `npm run ef -- migrations add <Name>`    | Add an EF Core migration             |

## API at a glance

| Endpoint                                                 | Description                                                                                                                                                                                        |
| -------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `GET /api/tickets`                                       | Paged list with `search`, `status`, `priority`, `categoryId`, `customerId`, `assignedAgentId`, `unassignedOnly`, `sortBy`, `sortDirection`. **Filters are accepted but not applied yet** (Task 1). |
| `GET /api/tickets/{id}`                                  | One ticket                                                                                                                                                                                         |
| `POST /api/tickets`                                      | Create a ticket                                                                                                                                                                                    |
| `PATCH /api/tickets/{id}/status` · `/assignment`         | Change status / assignee                                                                                                                                                                           |
| `GET /api/customers` · `/api/agents` · `/api/categories` | Reference data                                                                                                                                                                                     |

Errors are `ProblemDetails`: `400` validation, `404` not found, `409` conflict or business rule.

**Seed data:** 5 categories, 6 customers (3 Premium), 6 agents (one inactive, one at their limit)
and 40 tickets across every status, priority and SLA state.

## Troubleshooting

| Symptom                                | Fix                                                                         |
| -------------------------------------- | --------------------------------------------------------------------------- |
| API cannot reach the database          | `npm run db:up`, wait ~20 s, check `docker compose ps`                      |
| Port 1433 already in use               | Change the port in `docker-compose.yml` and `.env`                          |
| Browser warns about the certificate    | `dotnet dev-certs https --trust`, or use the web app, which proxies         |
| Web app shows "Unable to load tickets" | The API is not running, or `VITE_API_PROXY_TARGET` points at the wrong port |
| SQL Server slow on Apple Silicon       | Enable Rosetta in Docker Desktop → Settings → General                       |

---

### Assessment Completion Status and Time Breakdown

**Assessment Time Limit:** 2 days  
**Total Estimated Time Spent:** 8 hours 30 minutes  
**Priority Followed:** Task 1 → Task 2 → Task 3 → Task 4

#### Completion Summary

Following the assessment instructions, I prioritized completing Task 1 before proceeding to the remaining tasks.

Due to the limited timeframe, I focused on understanding the existing application, identifying and fixing the reported defect, implementing server-side filtering, and validating the changes through automated regression tests.

Rather than rushing through all four tasks without sufficient testing, I concentrated on delivering a maintainable and tested implementation of Task 1.

| Task                                    | Status        | Description                                                                                                                     |
| --------------------------------------- | ------------- | ------------------------------------------------------------------------------------------------------------------------------- |
| Task 1.1 — DEFECT-117                   | Completed     | Identified the root cause, fixed frontend filter request behavior, and added regression tests.                                  |
| Task 1.2 — Server-Side Filtering        | Completed     | Implemented database-side filtering, combined conditions, search, and filtered pagination with automated tests.                 |
| Task 2 — Ticket Escalation & Assignment | Not completed | Automatic triage, SLA calculations, agent assignment, escalation history, and related frontend functionality remain unfinished. |
| Task 3 — Login and Authentication       | Not completed | Agent login, password hashing, JWT authentication, protected endpoints, and session management remain unfinished.               |
| Task 4 — Dockerise the API              | Not completed | The required API Docker image, container configuration, automatic migration/seeding, and Docker verification remain unfinished. |

#### Estimated Time Breakdown — Task 1

| Activity                                                                   | Estimated Time         |
| -------------------------------------------------------------------------- | ---------------------- |
| Reviewing requirements and understanding the existing project architecture | 1 hour                 |
| Reproducing DEFECT-117 and investigating the root cause                    | 1 hour                 |
| Implementing the frontend fix and regression tests                         | 1 hour 15 minutes      |
| Reviewing the backend query architecture and filter requirements           | 45 minutes             |
| Implementing server-side filtering and search                              | 1 hour                 |
| Writing, debugging, and extending backend regression tests                 | 1 hour 45 minutes      |
| Running tests and reviewing verification results                           | 45 minutes             |
| Preparing documentation and organizing Git commits                         | 1 hour                 |
| **Total Estimated Time**                                                   | **8 hours 30 minutes** |

#### Implementation and Verification

Task 1 was implemented with an emphasis on correcting the underlying defect, preserving the existing application architecture, and verifying behavior through regression testing.

For DEFECT-117, I reproduced the problem using browser developer tools, corrected the data-fetching dependencies, and added frontend regression tests.

For server-side filtering, I extended the existing EF Core query to apply filtering before counting and pagination.

**Verified automated test results:**

- Ticket filtering tests: 10 passed, 0 failed.
- Complete backend unit test suite: 36 passed, 0 failed.
- Git whitespace check: passed.

SQL Server end-to-end verification remains pending.

#### Reason for Prioritizing Task 1

The assessment instructions recommend completing the tasks in order and clearly identifying unfinished work.

I followed this guidance by focusing on Task 1 and its regression tests instead of attempting partially implemented features across all four tasks.

My priority was to submit work that I could understand, explain, justify, and demonstrate through automated tests.

#### Unfinished Tasks

**Task 2 — Ticket Escalation & Assignment:** Automatic triage, configurable SLA rules, specialist-aware agent assignment, escalation endpoints and history, SLA filtering, React integration, and associated tests were not completed.

**Task 3 — Login and Authentication:** Secure agent credentials, JWT authentication, centralized API authorization, login interface, protected routes, and session handling were not completed.

**Task 4 — Dockerise the API:** Multi-stage Docker build, non-root API container, environment-based configuration, database migration and seeding workflow, health endpoint, and Docker verification were not completed.

These tasks are explicitly documented as unfinished in accordance with the assessment instructions.

#### Final Note

This submission reflects the completed Task 1 implementation and its verification within the available assessment timeframe. The remaining tasks and outstanding verification steps are documented transparently rather than represented as completed.
