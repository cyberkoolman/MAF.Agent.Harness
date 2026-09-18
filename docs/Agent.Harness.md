# What Is an Agent Harness?

## Executive summary

An **Agent Harness** is the runtime and governance layer that surrounds an AI model and
turns it into a dependable software agent.

A model can understand a request, reason about source code, and suggest the next action.
By itself, however, it does not provide durable state, safe tool execution, delegation,
approval controls, observability, or an objective definition of completion. The Agent
Harness supplies those operational capabilities.

```text
+------------------------+
| Human goal or incident |
+-----------+------------+
            |
            v
+------------------------+
| AI model               |
| Reason and decide      |
+-----------+------------+
            |
            v
+--------------------------------------------------+
| Agent Harness                                    |
| State | tools | skills | delegation | approvals |
| limits | memory | telemetry | lifecycle          |
+------------------------+-------------------------+
                         |
                         v
+--------------------------------------------------+
| Application-owned acceptance                    |
| Tests | APIs | browser | policy | evidence       |
+------------------------+-------------------------+
                         |
                         v
              Verified result or correction
```

The important distinction is:

> The model decides what may need to happen. The harness manages how work happens.
> Independent acceptance determines whether the result is actually correct.

## Why a model alone is not enough

An AI model normally operates one response at a time. A real engineering mission may
require dozens of model calls, tool operations, delegated tasks, test runs, and correction
rounds. Without a harness, an application team must build that coordination plumbing
themselves.

Common gaps include:

| Model-only gap | Harness capability |
|---|---|
| No durable mission state | Session and history persistence |
| Context grows until it becomes unmanageable | Context compaction |
| Tool calls need execution and validation | Function invocation pipeline |
| Complex work needs specialists | Background-agent delegation |
| Instructions must be repeatable | Agent Skills |
| Actions require policy controls | Tool approvals and scoped capabilities |
| Loops could continue indefinitely | Iteration, task, time, and budget limits |
| Progress is difficult to audit | OpenTelemetry traces and structured task state |
| A persuasive answer may be mistaken for success | Application-owned acceptance |

## What the harness does in this repository

Phoenix is the coordinator and is created as a Microsoft Agent Framework
`HarnessAgent`:

```csharp
var phoenix = chatClient.AsHarnessAgent(phoenixOptions);
```

The harness gives Phoenix:

- a durable session across the repair mission;
- function invocation and a bounded model/tool loop;
- todo state and reusable skills;
- long-context compaction and file memory;
- approval middleware;
- OpenTelemetry instrumentation; and
- typed background-agent tools.

Phoenix delegates to four specialists:

| Agent | Responsibility | Boundary |
|---|---|---|
| Ember | Frontend diagnosis, repair, and browser inspection | Frontend files |
| Forge | Backend diagnosis, repair, and HTTP checks | Backend files |
| Sentinel | Regression tests and independent browser inspection | Test-related scope |
| Scroll | Verified operational documentation | Documentation scope |

Phoenix coordinates the work but does not receive unrestricted file-editing tools. This
separation makes the mission easier to understand, constrain, and audit.

## What the harness does not do

The harness is not:

- the AI model;
- a substitute for tests;
- a guarantee that generated code is correct;
- the business definition of success;
- permission to access every file or system; or
- a replacement for human accountability.

This repository therefore keeps the final acceptance contract outside the harness. The
application independently checks source code, HTTP responses, Jest results, browser
rendering, browser errors, screenshots, and documentation.

A specialist task reaching `completed` means the specialist finished its assigned work.
It does **not** mean the application is fixed. Only the acceptance gate can make that
decision.

## Business benefits

### Repeatability

Skills, tool definitions, boundaries, and completion rules are configured once and applied
consistently across missions.

### Safer autonomy

Agents receive only the capabilities needed for their roles. Approval rules and hard
limits reduce the risk of uncontrolled actions.

### Better use of specialists

One coordinator can delegate work to agents with focused responsibilities instead of
forcing one model conversation to hold every concern.

### Operational visibility

Model calls, tool calls, delegation, duration, failures, and acceptance rounds can be
correlated under one mission trace.

### Clearer completion

The system separates an agent's claim from verifiable evidence. Failed acceptance can
return precise evidence for a bounded correction round.

### Reusable engineering platform

The same harness pattern can support code repair, test generation, incident investigation,
documentation maintenance, migration assistance, or other controlled workflows.

## Agent framework, agent, and harness

These terms are related but not interchangeable:

| Term | Meaning |
|---|---|
| **Model** | Produces reasoning and language or multimodal responses |
| **Agent** | A model configured with instructions, identity, and tools |
| **Agent framework** | APIs and abstractions used to build agents and workflows |
| **Agent Harness** | The managed runtime pipeline around an agent: state, tools, skills, delegation, policy, limits, and telemetry |
| **Application acceptance** | Domain-specific proof that the agent's result satisfies the real requirement |

## Practical design principle

Reliable autonomous engineering requires all three layers:

```text
Model intelligence
        +
Harness-managed execution
        +
Application-owned verification
```

Removing any one of these layers weakens the system. Reasoning without execution cannot
perform the work. Execution without governance is difficult to trust. Governance without
independent verification can still accept the wrong outcome.

## Related documents

- [Agent Harness in Action](Agent-Harness.md)
- [What Is a Computer-Use Model?](Computer.Use.md)
- [How the Code Repair Actually Works](code-fix.md)
- [Setup and Demo Guide](SETUP.md)
