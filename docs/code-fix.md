# How the Code Repair Actually Works

This document explains the mechanics behind the Mission Control demonstration. The system
does not send the entire repository to one model and ask it to “fix everything.” It uses
an Agent Harness, specialized agents, constrained engineering tools, and an independent
acceptance loop.

The short version is:

> The model diagnoses and proposes actions. The Agent Harness manages the work loop and
> delegation. Scoped tools perform exact file and command operations. Application-owned
> acceptance decides whether the repair is real.

## Important disclosure: this is a guided repair benchmark

This demonstration is intentionally repeatable. It is **not** a blind test in which the
agents receive an unknown repository and independently discover every defect with no
prior guidance.

The checked-in skills and acceptance code provide advance structure:

- Phoenix is given the two-phase frontend/backend, then QA/documentation workflow.
- Ember is directed toward the frontend components and is warned about the expected
  `items.itmes.map(...)` defect.
- Forge receives the two failing endpoint URLs, the route-file location, and the expected
  categories of backend failure.
- Sentinel receives the existing test-file path and the minimum API cases that must be
  covered.
- The acceptance verifier contains the exact source, HTTP, browser, test, and
  documentation conditions required for this demonstration.

The model still performs real reasoning and tool use. It reads the current files, observes
the rendered application, interprets screenshots and browser errors, reproduces live API
failures, selects and invokes edits, evaluates test output, and responds to failed
acceptance evidence. However, the defect domain and success contract are known in advance.

The accurate description is:

> This is a guided autonomous code-repair workflow: the scenario and acceptance contract
> are predefined, while the model performs diagnosis, scoped implementation, delegation,
> verification, and corrective work.

The guidance is a feature of the demonstration rather than an attempt to hide model
limitations. It makes repeated runs comparable, gives the audience objective pass/fail
evidence, and prevents a presentation from depending on an unpredictable defect search.

## Can the same approach support open-ended autonomous repair?

**Yes. A more open-ended autonomous computer-use workflow is technically possible with
the same architecture.** The Agent Harness does not require Phoenix to know the file names,
endpoints, or defect in advance. Those details are currently supplied by the skills because
this scenario is designed as a controlled benchmark.

In a generalized mode, the initial instruction could contain only a symptom, issue, or
failed user journey. The agents could then:

1. inventory the repository and identify its languages, projects, and test commands;
2. run the existing test suite and inspect build or runtime failures;
3. start the application in an isolated environment;
4. use Playwright screenshots, accessibility snapshots, browser-console output, and
   bounded interaction to reproduce the user's experience;
5. trace requests from the UI to APIs and then to likely source locations;
6. form and test hypotheses through minimal scoped edits;
7. generate or strengthen regression tests from the reproduced failure;
8. validate the repair against tests, runtime behavior, and the observed user journey; and
9. produce a reviewable patch, evidence report, and telemetry trace.

That mode would be more autonomous, but **computer use alone would not make it safe or
reliable**. A production implementation should also add:

- an isolated disposable workspace or container;
- repository-wide read access with narrowly approved write scopes;
- automatic discovery of build and test commands;
- protection for secrets, production endpoints, and external network access;
- human approval for dependency changes, migrations, deployments, and destructive tools;
- source-control checkpoints and a reliable rollback path;
- time, token, cost, and iteration budgets;
- dynamically derived acceptance criteria from the issue, tests, and reproduced behavior;
- confidence reporting and escalation when the evidence is ambiguous; and
- mandatory human review before merge or deployment.

The distinction is therefore not “possible versus impossible.” It is:

| Mode | Starting knowledge | Main advantage | Main tradeoff |
|---|---|---|---|
| **This guided demonstration** | Known defect area, workflow, and acceptance contract | Repeatable, measurable, and presentation-safe | Does not prove blind discovery of arbitrary defects |
| **Open-ended autonomous repair** | Symptom, issue, failed test, or user journey | Can investigate unfamiliar code and discover likely causes | Requires stronger isolation, dynamic acceptance, larger budgets, and more human governance |

The current implementation establishes most of the reusable foundation: harness-managed
delegation, scoped tools, browser vision, test execution, correction rounds, telemetry,
and independently enforced completion. Generalizing the discovery and acceptance layers
is the next step, not a replacement for the architecture.

## The major components

| Component | Responsibility |
|---|---|
| **Azure OpenAI model** | Reasons about source, screenshots, HTTP responses, and test output |
| **Agent Harness** | Maintains Phoenix's session, tools, todo state, skills, delegation, telemetry, compaction, and bounded loop |
| **Phoenix** | Decomposes the mission and delegates work; it has no direct file-editing tools |
| **Ember** | Repairs frontend code and verifies the rendered UI |
| **Forge** | Repairs backend routes and verifies live API responses |
| **Sentinel** | Builds and runs regression tests and independently inspects the UI |
| **Scroll** | Verifies and updates operational documentation |
| **Workspace tools** | Enforce file boundaries, exact edits, existing-file writes, and command allowlists |
| **Playwright tools** | Give selected agents bounded visual browser use |
| **Acceptance verifier** | Independently checks source, HTTP behavior, browser behavior, tests, and documentation |

## The complete repair flow

```text
+------------------------------+
| User submits repair mission  |
+--------------+---------------+
               |
               v
+------------------------------+
| Agent Harness creates one    |
| durable Phoenix session      |
+--------------+---------------+
               |
               v
+------------------------------+
| Phoenix starts Ember and     |
| Forge concurrently           |
+--------------+---------------+
               |
        +------+------+
        |             |
        v             v
+---------------+  +---------------+
| Ember observes|  | Forge calls   |
| UI and source |  | failing APIs  |
+-------+-------+  +-------+-------+
        |                  |
        v                  v
+---------------+  +---------------+
| Scoped frontend| | Scoped backend|
| edit and test |  | edits and HTTP|
+-------+-------+  +-------+-------+
        |                  |
        +--------+---------+
                 |
                 v
+------------------------------+
| Phoenix checks both reports  |
| against phase requirements   |
+--------------+---------------+
               |
               v
+------------------------------+
| Phoenix starts Sentinel and  |
| Scroll concurrently          |
+--------------+---------------+
               |
               v
+------------------------------+
| Application-owned acceptance |
| checks source, APIs, browser, |
| tests, and documentation     |
+--------------+---------------+
               |
       +-------+-------+
       |               |
    FAILED           PASSED
       |               |
       v               v
+--------------+  +------------------+
| Failure facts|  | Verified mission |
| return to the|  | completion       |
| same session |  +------------------+
+------+-------+
       |
       +----------> Phoenix delegates corrective work
```

## Step 1: Phoenix receives the mission

The command line or Telegram host sends the mission text to the existing MAF executable.
The orchestrator creates one Phoenix session and reuses it across all acceptance rounds:

```csharp
var session = await _phoenix.CreateSessionAsync(cancellationToken);
```

Phoenix is a `HarnessAgent`, created with:

```csharp
var phoenix = chatClient.AsHarnessAgent(phoenixOptions);
```

The harness provides Phoenix with the background-agent tools used to start, wait for,
continue, retrieve, and clear specialist tasks. The application does not implement a
custom polling loop for child-agent completion.

## Step 2: Phoenix delegates instead of editing

Phoenix has no file tools. Its mission skill requires it to start Ember and Forge before
waiting so the frontend and backend investigations can run concurrently.

The task descriptions include concrete outcomes:

- Ember must identify the frontend crash, make the minimal repair, and prove the Navbar
  renders.
- Forge must reproduce both HTTP 500 responses, repair their root causes, and report the
  observed replacement status codes.

The harness returns real task IDs and structural states such as running, completed,
failed, or lost. A task marked completed means only that the specialist returned a result;
Phoenix must still inspect the content.

## Step 3: Ember repairs the frontend

### Observe before editing

Ember uses Playwright to:

1. navigate to the frontend;
2. capture a screenshot;
3. inspect the visible failure;
4. read browser-console errors; and
5. inspect an accessibility-oriented snapshot when interaction is needed.

The screenshot is sent to the vision-capable model as real PNG image content. The model is
therefore able to reason about what the user sees rather than relying only on DOM text or
a filename.

### Read the source

Ember's file tools are restricted to the `frontend/` directory. It lists and reads the
components that render on application startup and identifies the planted Navbar defect.

The broken expression is:

```jsx
{items.itmes.map(item => (
```

The required repair is:

```jsx
{items.map(item => (
```

The specialist instruction explicitly warns against replacing only `itmes`, because that
could produce another invalid expression such as `items.items.map(...)`.

### Apply a precise edit

`EditFile` is not an unrestricted text-generation operation. It:

- resolves the path inside Ember's assigned workspace;
- refuses paths outside `frontend/`;
- requires the original text to exist;
- requires the match to occur exactly once; and
- reports the specific file changed.

This encourages a minimal root-cause repair instead of an unrelated rewrite.

### Verify the frontend

Ember reloads the page, captures another screenshot, confirms that the Mission Control
Navbar and the Dashboard, Team, and Tasks buttons are visible, checks browser errors, and
runs the focused Navbar test.

## Step 4: Forge repairs the backend

Forge's tools are restricted to the `backend/` directory and loopback HTTP endpoints.

### Reproduce the failures

Before editing, Forge calls:

```text
GET http://localhost:4000/api/users/999
GET http://localhost:4000/api/users/1/stats
```

Both endpoints are expected to return HTTP 500 in the planted state. If the failures
cannot be reproduced, Forge must report that fact rather than changing code blindly.

### Repair the missing-user path

The user lookup can return no result. The repaired route checks that condition before
reading user properties:

```javascript
if (!user) {
  return res.status(404).json({ error: 'User not found' });
}
```

The important change is semantic: a missing resource is a normal HTTP 404 response, not
an unhandled exception and HTTP 500.

### Repair the statistics path

The statistics handler previously referenced task data that was not defined in its scope.
The repair loads the database and obtains the task collection before filtering it:

```javascript
const db = require(path.join(__dirname, '..', 'data', 'db.json'));
const tasks = db.tasks;
```

Forge then verifies the live endpoints again:

- `/api/users/999` must return **404** with an error body;
- `/api/users/1/stats` must return **200** with statistics data.

The backend is run with `node --watch server.js`, allowing the process to reload after the
file changes. Final acceptance still runs tests in a fresh process so a watcher is not
treated as proof.

## Step 5: Phoenix applies a phase gate

Phoenix does not accept vague specialist statements such as “fixed,” “should work,” or
“restart the server.”

Before advancing, Phoenix requires:

- Ember to report the exact `items.map(...)` expression and a passing focused test;
- Forge to report an observed HTTP 404 and HTTP 200 from the repaired endpoints.

If either result is incomplete, Phoenix uses the harness continuation tool to send the
missing criteria back into the existing child session. It does not discard the context
and start the entire mission again.

## Step 6: Sentinel adds regression protection

After the source repairs pass the phase gate, Phoenix starts Sentinel and Scroll
concurrently.

Sentinel reads the actual server, route, package, and existing test files before changing
the suite. The expected backend coverage includes:

```text
GET /api/health              -> 200
GET /api/users               -> 200
GET /api/users/1             -> 200
GET /api/users/999           -> 404
GET /api/users/1/stats       -> 200
GET /api/tasks               -> 200
GET /api/tasks/1             -> 200
GET /api/tasks/999           -> 404
```

`WriteFile` can replace an existing file but cannot create arbitrary new files. Sentinel
then runs Jest with coverage and must report the real exit code, passing-test count, and
coverage output.

Sentinel also receives a separate Playwright context. Its browser state, navigation,
console buffer, and screenshot evidence do not interfere with Ember's browser session.

## Step 7: Scroll records the repaired state

Scroll can read the whole mission workspace but is instructed to verify before
documenting. It reads the git diff and the actual route and package files, runs the backend
test command, calls a live endpoint, and then updates the existing root README.

This prevents documentation from becoming a model-generated description of behavior that
was never observed.

## Step 8: deterministic acceptance decides success

After Phoenix reports that the team is finished, `MissionAcceptanceVerifier` runs outside
the model and checks the application directly.

| Check | Evidence |
|---|---|
| Navbar source | File contains `items.map(...)` and none of the known invalid expressions |
| Missing user | Live request returns HTTP 404 |
| User statistics | Live request returns HTTP 200 |
| Frontend health | Frontend URL returns HTTP 200 |
| Visual UI | Navbar is visible, title is correct, three navigation buttons exist, no browser errors occur, and a screenshot is nonempty |
| Backend regression | Fresh Jest process exits successfully with a nonzero passing-test count |
| Frontend regression | Focused Navbar Jest process exits successfully with a nonzero passing-test count |
| Documentation | README exists; a full mission also requires it to change during that mission |

This verifier is ordinary deterministic application code. The model cannot persuade it,
rewrite its output in prose, or declare success around it.

## Step 9: failed evidence becomes corrective work

If any acceptance check fails, the verifier produces a corrective prompt containing only
the failed checks. The orchestrator sends that evidence back to Phoenix using the same
harness session:

```text
The application-enforced acceptance gate failed.
Continue the existing mission and delegate corrections...

- <failed check>: <observed evidence>
```

Phoenix retains the mission history, todo state, and existing context, then delegates the
correction to the responsible specialist. The loop is bounded by the configured maximum
acceptance rounds.

This is what **“Mission verified in acceptance round 2”** means:

1. the agents completed their first repair attempt;
2. deterministic acceptance found at least one unmet condition;
3. the failure evidence was returned to Phoenix;
4. corrective work was performed; and
5. the second independent verification passed.

## Step 10: completion is emitted only after proof

Only the application can produce:

```text
Mission Complete - independently verified
```

On success, the same event is:

- returned to the command line or Telegram;
- recorded as mission and acceptance telemetry; and
- reflected in the browser, terminal output, and reviewable git diff.

## What is model-driven and what is deterministic?

| Model-driven | Deterministic |
|---|---|
| Understanding the mission | Workspace path enforcement |
| Selecting the next specialist task | Command allowlists |
| Interpreting source and test failures | Exact one-match file editing |
| Interpreting screenshots | Loopback-only HTTP policy |
| Proposing and applying a minimal repair | Browser origin restrictions |
| Deciding which failed evidence needs follow-up | HTTP status assertions |
| Summarizing specialist results | Jest exit-code and test-count checks |
| Explaining the final repair | Navbar source assertion |
|  | Browser visibility and console checks |
|  | Acceptance-round limit |

The model is used where judgment is valuable. Deterministic code is used where policy,
safety, and pass/fail truth must not depend on persuasion.

## Why `--verify-only` opens a browser

This command:

```powershell
dotnet run --project .\src\MAF.RPTeam -- --verify-only
```

does not call the model and does not edit files. It runs the same independent acceptance
checks against the current application. When browser support is enabled, Playwright opens
the frontend, verifies the Navbar, reads browser errors, captures a screenshot, and closes
when verification finishes.

The operator does not need to interact with the browser. The final result is the list of
`[PASS]` or `[FAIL]` lines printed in the terminal.

## Why this approach is useful

This implementation demonstrates a practical division of responsibility:

- **The model** supplies flexible reasoning.
- **The Agent Harness** supplies repeatable agent-runtime behavior.
- **Scoped tools** supply controlled effects on the environment.
- **Specialized agents** supply separation of concerns.
- **Independent acceptance** supplies trustworthy completion.
- **OpenTelemetry** supplies an auditable execution record.
- **Browser evidence, terminal output, and Telegram** supply an understandable human experience.

That combination is what turns a code-generating model into a governed software-repair
system.

## Related documentation

- [Agent Harness in Action](Agent-Harness.md)
- [Setup and demo guide](SETUP.md)
