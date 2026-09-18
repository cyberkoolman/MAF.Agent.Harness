# Agent Harness Code-Repair Case Study

## From AI code repair to verified autonomous engineering

This client-facing case study demonstrates why an **Agent Harness** is the foundation of
reliable autonomous software engineering.

A capable model can reason about code, but the model alone does not provide durable state,
tool execution, delegation, context management, approval policy, observability, or a safe
stopping condition. Microsoft Agent Framework's Agent Harness supplies that runtime
scaffolding around the model.

In this solution, the Agent Harness enables Phoenix to:

1. maintain a plan and todo state across model calls;
2. delegate concurrently to four specialized agents;
3. drive model and tool calls through a bounded execution loop;
4. preserve and compact a long-running conversation;
5. apply tool-approval policy;
6. load reusable Agent Skills;
7. emit OpenTelemetry for model, tool, and delegation activity; and
8. continue working until delegated tasks reach terminal states.

The surrounding application then adds computer-use tools, deterministic acceptance,
Telegram triggering, and Azure Monitor export.

The key learning is:

> The model provides reasoning. The Agent Harness turns that reasoning into a managed,
> observable, and bounded work loop. Application-owned acceptance then proves whether the
> resulting software actually works.

![Agent Harness architecture showing Phoenix, specialist agents, scoped tools, independent acceptance, telemetry, Telegram, and browser evidence](images/agent-harness-architecture.svg)

_The Agent Harness manages the autonomous execution loop. Scoped tools control what agents
can change, while application-owned acceptance independently decides whether the repair
is complete._

## What the Agent Harness is doing

Phoenix is created as a real `HarnessAgent`:

```csharp
var phoenix = chatClient.AsHarnessAgent(phoenixOptions);
```

Ember, Forge, Sentinel, and Scroll are ordinary `ChatClientAgent` instances registered in
Phoenix's `BackgroundAgents` option. This is intentional: Phoenix receives the complete
harness pipeline, while each specialist receives only the tools and workspace scope
required for its role.

| Harness capability | How this demonstration uses it |
|---|---|
| **Function invocation** | Drives Phoenix's model-and-tool loop with a maximum of 40 iterations per request |
| **History persistence** | Saves progress after model calls so a long mission has durable conversational state |
| **Context compaction** | Keeps a long tool-heavy repair mission within a 128,000-token context budget |
| **Todo provider** | Preserves Phoenix's task state while specialists work and context changes |
| **Agent modes** | Makes planning and execution available within the same agent |
| **File memory** | Provides durable session notes beyond one model turn |
| **Tool approvals** | Applies the harness approval middleware; this unattended demo supplies an explicit auto-approval rule |
| **OpenTelemetry** | Emits model, tool, and delegation spans under the `MAF.RPTeam` activity source |
| **Agent Skills** | Loads checked-in `SKILL.md` operating procedures |
| **Background agents** | Gives Phoenix typed tools for starting, waiting for, continuing, retrieving, and clearing delegated tasks |
| **Bounded looping** | Re-invokes Phoenix while child work is still running, with a hard maximum of 24 loop iterations |

Web search is deliberately disabled because this mission operates on a local application.

### What the harness replaced

The original workflow depended on custom coordination plumbing such as:

- polling transcript history every ten seconds;
- searching for completion keywords in agent messages;
- comparing timestamps to reject stale results;
- manually clearing old sessions and transcript files;
- heartbeat and silence-timeout logic; and
- a shared text protocol for reporting completion.

The Agent Harness replaces that plumbing with background-task IDs, structured task states,
typed task results, managed persistence, and a completion evaluator. Phoenix can therefore
focus on coordination rather than implementing its own agent runtime.

### What remains application-owned

The harness does not know the business definition of “the application is fixed.” This
repository therefore keeps the following outside the harness:

- the exact frontend and backend acceptance contract;
- Playwright browser tools and visual acceptance;
- workspace and command allowlists;
- Telegram authorization and mission launching;
- Azure Application Insights exporter configuration; and
- terminal and browser presentation.

This separation is important: **the harness manages execution; the application owns the
definition of success.**

## The business problem

The sample application contained realistic failures across the frontend and backend:

| Area | Defect | User impact | Verified repair |
|---|---|---|---|
| Frontend | The Navbar attempted to iterate through a misspelled property instead of `items.map(...)` | The navigation component crashed and the application could not render correctly | Source inspection, focused Jest tests, browser rendering, and browser-console checks |
| Backend | `GET /api/users/999` accessed properties on a missing user | A normal “not found” request returned HTTP 500 | The route now returns HTTP 404 with `User not found` |
| Backend | `GET /api/users/1/stats` referenced task data that had not been loaded | The statistics endpoint returned HTTP 500 | The route loads `db.tasks` and returns HTTP 200 with calculated statistics |
| Quality | Existing tests did not fully protect the repaired behavior | The same defects could regress later | Focused frontend and backend regression tests were added or corrected |
| Documentation | The repair needed an operational record | Future operators could not easily reproduce or verify the result | Mission documentation is updated as part of full acceptance |

This matters because a plausible code diff is not the same as a working repair. The
workflow must prove the behavior at the source, API, test, and rendered-UI levels.

## How computer use extends the Agent Harness

The vision-capable Azure OpenAI model does not receive unrestricted access to a desktop.
It uses a bounded Playwright browser capability designed for the application under test.

The model can:

- navigate to the configured application;
- capture a real PNG screenshot;
- inspect an accessibility-oriented page snapshot;
- click a visible element through a stable reference;
- read bounded browser-console and page-error output; and
- compare the broken and repaired user experiences.

The browser screenshot is supplied to the model as actual multimodal image content. It is
not reduced to a filename, a Base64 string in a prompt, or a textual claim that the page
looks correct.

The browser tools become capabilities available to selected agents inside the
harness-managed mission. This creates a practical computer-use loop:

```text
+--------------------------------------+
|  Observe source and live application |
+-------------------+------------------+
                    |
                    v
+--------------------------------------+
|  Reason about the failure            |
+-------------------+------------------+
                    |
                    v
+--------------------------------------+
|  Edit through scoped engineering     |
|  tools                               |
+-------------------+------------------+
                    |
                    v
+--------------------------------------+
|  Run tests and HTTP checks           |
+-------------------+------------------+
                    |
                    v
+--------------------------------------+
|  Inspect the repaired UI with        |
|  Playwright                          |
+-------------------+------------------+
                    |
                    v
          +----------------------+
          | Independent          |
          | acceptance           |
          +----------+-----------+
                     |
             +-------+-------+
             |               |
          FAILED           PASSED
             |               |
             v               v
+----------------------+  +----------------------+
| Return evidence to   |  | Verified completion  |
| Phoenix for a bounded|  +----------------------+
| correction round     |
+----------+-----------+
           |
           +-----------> Reason about the failure
```

The browser is one source of evidence, not the final authority. A screenshot alone cannot
prove that an API returns the correct status, that tests pass in a fresh process, or that
the source contains the required fix.

## How the Agent Harness coordinates the five-agent team

| Agent | Responsibility | Enforced access |
|---|---|---|
| **Phoenix** | Leads the mission, delegates work, and integrates results | No direct file-editing tools |
| **Ember** | Diagnoses and repairs the frontend using source and browser evidence | `frontend/` only |
| **Forge** | Diagnoses and repairs backend routes and API behavior | `backend/` only |
| **Sentinel** | Performs independent QA and strengthens regression tests | `backend/` plus isolated browser verification |
| **Scroll** | Updates technical and operational documentation | Whole mission workspace |

These boundaries are enforced by the application, not merely requested in prompts.
Phoenix cannot silently bypass delegation and edit everything itself, while frontend and
backend specialists cannot write outside their assigned areas.

## End-to-end repair lifecycle

### 1. A human starts the mission

The mission can be entered at the command line or sent to an allowlisted Telegram bot:

```text
/mission Mission Control: diagnose and repair the Team Dashboard, then verify everything.
```

Telegram is only a secure trigger and status channel. It starts the same mission
executable and does not duplicate the agent team or acceptance logic.

### 2. The agents investigate in parallel

Phoenix delegates frontend and backend diagnosis to Ember and Forge. The specialists use
the tools appropriate to their scopes:

- source-code reads and edits;
- targeted HTTP requests;
- Jest tests;
- browser navigation, screenshots, page snapshots, and console diagnostics.

The task lifecycle is maintained by the Agent Harness. Phoenix receives structured
background-task IDs and states instead of polling prose for completion keywords. A
specialist returning a message does not automatically mean its work is accepted.

### 3. The model repairs the application

The team corrects the frontend expression, restores proper API behavior, and adds
regression coverage. File watchers reload the frontend and backend so the agents can test
the changed application rather than a stale process.

### 4. Independent acceptance checks the result

After the agents report completion, application-owned code verifies:

- the exact Navbar source expression;
- `GET /api/users/999` returns HTTP 404;
- `GET /api/users/1/stats` returns HTTP 200;
- the frontend responds;
- the Navbar is visibly rendered;
- the expected navigation items appear;
- the browser reports no unhandled page or console errors;
- a nonempty screenshot can be captured;
- backend Jest tests pass in a fresh process;
- focused Navbar Jest tests pass; and
- the required documentation exists and, for a full mission, was updated.

Acceptance can reject the first result and return evidence to Phoenix for a bounded
correction round. In the demonstrated run, the mission passed in **acceptance round 2**.
That means the system found the initial result incomplete, corrected it, and then proved
the repaired state.

The final success message is emitted by the application only after acceptance passes:

```text
Mission Complete - independently verified
```

## What the audience can see

| Experience | What it communicates |
|---|---|
| **Telegram** | A simple human entry point for starting, checking, or cancelling a mission |
| **Application browser** | The broken interface before repair and the functioning interface afterward |
| **Streaming terminal** | Phoenix's live model output, acceptance rounds, and final evidence |
| **Git diff** | The exact source, test, and documentation changes produced by the team |
| **Application Insights** | Durable mission traces containing model calls, tool calls, duration, outcome, and acceptance evidence |

Presentation is deliberately separate from acceptance. A convincing explanation or visual
result cannot turn a failed mission into a successful one.

## Measurable evidence

Two complete observed missions both passed independent acceptance:

| Mission | Duration | Model calls | Tool operations | Total observable operations | Acceptance passed |
|---|---:|---:|---:|---:|---:|
| Mission 1 | 160.5 seconds | 17 | 22 | 39 | Yes |
| Mission 2 | 99.7 seconds | 18 | 19 | 37 | Yes |

The second mission reached the same verified result approximately **61 seconds faster**,
or about **38% faster**.

Two runs are demonstration evidence, not a statistically significant benchmark. The
important lesson is that a success count alone hides meaningful differences. OpenTelemetry
makes duration, model activity, tool activity, errors, and acceptance rounds available for
analysis under one correlated mission identifier.

## Harness and application controls that make autonomy credible

### Tool and file boundaries

- Specialist write access is limited to the directories required by each role.
- Phoenix coordinates but has no direct file tools.
- Browser navigation is limited to configured application origins.
- Browser clicks use references from the latest page snapshot rather than arbitrary
  selectors supplied by the model.

### Browser isolation

- Ember, Sentinel, and final acceptance use separate browser contexts.
- No persistent browser profile, cookies, or credentials are reused.
- Navigation, actions, and screenshots have explicit limits and timeouts.
- Screenshot Base64 content is not written to logs or telemetry.

### Independent verification

- Agent narration is treated as provisional.
- Fresh-process tests reduce the risk of accepting cached or stale results.
- HTTP checks validate actual runtime behavior.
- Browser checks validate what the user sees and capture console failures.
- Acceptance is bounded to a configured maximum number of correction rounds.

### Harness execution limits

- Phoenix is limited to 40 model/tool iterations per request.
- The background-task completion loop is limited to 24 re-invocations.
- Long context is compacted instead of growing without bound.
- Tool execution passes through approval middleware.
- Background work has structural running, completed, failed, and lost states.

### Operational security

- Telegram chats require explicit allowlisting.
- Azure OpenAI authentication uses `DefaultAzureCredential`, supporting developer identity
  and managed identity without embedding an API key in source.
- Raw Telegram chat IDs are not exported to telemetry; a truncated hash is used for
  correlation.

## Most important learnings

### 1. Computer use should complement deterministic checks

Vision and browser interaction are valuable for failures that exist in the rendered user
experience. They should be combined with source assertions, API checks, and automated
tests rather than replacing them.

### 2. A harness-managed completion is not business acceptance

A harness background task marked complete means that the specialist returned a result. It
does not mean the software works. The application must independently evaluate the outcome
before declaring success.

### 3. Specialized agents need real boundaries

Role descriptions alone are weak governance. Code-enforced tool scopes make delegation
auditable and prevent a coordinator from becoming an unrestricted super-user.

### 4. Observability changes the conversation

Without tracing, the audience sees only “success.” With OpenTelemetry, the team can discuss
execution time, model calls, tool calls, correction rounds, failures, and efficiency using
evidence.

### 5. Visible progress builds trust, but it is not proof

Streaming output, browser activity, and a reviewable git diff make autonomous work
understandable to an audience. The durable proof still comes from tests, runtime checks,
browser acceptance, and telemetry.

### 6. Watchers and process freshness matter

An agent can edit the right file while an old process continues serving stale code. The
demo uses file watchers for rapid feedback and fresh-process test execution for final
acceptance.

### 7. The Agent Harness removes plumbing, not accountability

The harness removes the need to rebuild common agent-runtime infrastructure. It does not
remove the need for application-specific controls, testing, security, or human
accountability. The strongest result comes from combining:

- a capable multimodal model;
- an agent harness;
- narrowly designed tools;
- role-based scope enforcement;
- independent acceptance;
- telemetry;
- secure human triggers; and
- an understandable visualization layer.

## A concise presentation narrative

> A user sends one repair request from Telegram. The Agent Harness gives Phoenix a durable
> plan, tools, skills, telemetry, bounded looping, and four background agents. Phoenix
> delegates frontend, backend, QA, and documentation work without custom polling or
> completion keywords. Selected agents use a vision-capable model and a real Playwright
> browser to examine the application. The application—not the agents—then verifies the
> exact source, API responses, tests, visible Navbar, browser console, and screenshot. If
> anything fails, the evidence returns to the harness-managed mission for a bounded
> correction round. Application Insights records the execution, while the browser and
> terminal make it visible. The result is not simply AI-generated code; it is an Agent Harness converting
> model reasoning into observable, constrained, and independently verified engineering.

## Reproduce the final proof

After a mission completes, run the acceptance suite without calling the model:

```powershell
dotnet run --project .\src\MAF.RPTeam -- --verify-only
```

This command is the simplest independent proof that the repaired application still meets
the required contract.

For implementation and operator details, see:

- [How the code repair actually works](code-fix.md)
- [Setup and demo guide](SETUP.md)
