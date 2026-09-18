# What Is an Agent Harness?

## Executive summary

An **Agent Harness** is the runtime and governance layer that surrounds an AI model and
turns it into a dependable AI agent.

A model can understand a goal, reason about the information available to it, and suggest
the next action. For example, it might analyze documents, customer requests, operational
data, a user interface, or source code. By itself, however, it does not provide durable
state, safe tool execution, delegation, approval controls, observability, or an objective
definition of completion. The Agent Harness supplies those operational capabilities.

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
| Domain-owned outcome verification               |
| Rules | policy | checks | evidence               |
+------------------------+-------------------------+
                         |
                         v
              Verified result or correction
```

**Microsoft Learn reference:** [Agent Harness](https://learn.microsoft.com/en-us/agent-framework/concepts/harness)

The important distinction is:

> The model decides what may need to happen. The harness manages how work happens.
> Domain-specific verification determines whether the result is actually correct.

## Why a model alone is not enough

An AI model normally operates one response at a time. A real task may require dozens of
model calls, tool operations, delegated tasks, validation checks, and correction rounds.
Without a harness, the team building the solution must create that coordination plumbing
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
| A persuasive answer may be mistaken for success | Domain-owned outcome verification |

## Agent workflow versus Agent Harness

An **agent workflow** and an **Agent Harness** solve different problems. They are usually
complementary rather than competing approaches.

- An **agent workflow** defines the task logic: which steps, agents, decisions, and
  handoffs occur, and in what order.
- An **Agent Harness** provides the managed execution environment in which an agent can
  perform that work over time.

In short:

> A workflow describes **what work should happen and how it is coordinated**.
> A harness provides **the runtime capabilities, controls, and lifecycle for executing
> agent work reliably**.

### Venn diagram: what belongs to each and what intersects

![Venn diagram comparing agent workflow, shared concerns, and Agent Harness responsibilities](images/workflow-vs-agent-harness-venn.svg)

**Microsoft Learn references:** [Workflow concepts](https://learn.microsoft.com/en-us/agent-framework/concepts/workflows/)
and [Agent Harness](https://learn.microsoft.com/en-us/agent-framework/concepts/harness)

| Workflow only | Intersection: both participate | Harness only |
|---|---|---|
| Defines the business or task sequence | Agents perform tasks within a coordinated process | Preserves the agent's conversation and session history |
| Chooses branches, routes, and handoff order | Tools may be used by workflow steps or harness-managed agents | Compacts long context across model interactions |
| Defines which step follows success, failure, or approval | State and results move between tasks and agents | Runs the model/tool invocation pipeline |
| Establishes domain checkpoints and terminal workflow states | Failures can trigger retries or correction paths | Loads reusable skills, memory, todos, and modes |
| Coordinates non-agent steps, services, people, and agents | Policy, approvals, and telemetry can span both layers | Manages delegated-agent task IDs, status, and lifecycle |
| Expresses process-level service-level objectives | Both contribute evidence used by domain verification | Applies reusable runtime limits, scopes, and middleware |

The intersection does not mean both layers implement the same feature in the same way. For
example, a workflow may decide **when** approval is required, while the harness enforces
approval middleware around the agent's tool call. A workflow may decide **when** to
delegate, while the harness manages the delegated agent's execution and status.

| Dimension | Agent workflow | Agent Harness |
|---|---|---|
| **Primary purpose** | Orchestrate a business or task process | Operate an agent safely and consistently |
| **Main question** | “What happens next?” | “How does this agent execute, persist, use tools, and remain controlled?” |
| **Typical structure** | Sequence, graph, router, handoff, branch, loop, or approval step | Runtime pipeline around model calls, tools, state, policy, and telemetry |
| **Control style** | Often application-defined and explicit | Provides reusable managed capabilities to the agent |
| **State** | Workflow state usually tracks steps and outputs | Session state can include conversation history, memory, todos, modes, and delegated tasks |
| **Tools** | A workflow may call tools directly or ask agents to call them | The harness manages tool exposure, invocation, approval, and execution loops |
| **Delegation** | Defines when work moves to another agent or step | Supplies the mechanisms and lifecycle for running and tracking delegated agents |
| **Context** | Passes selected data between workflow steps | Preserves and compacts an agent's evolving context across many interactions |
| **Limits and policy** | Must be designed into each workflow or surrounding application | Can provide standard iteration limits, approvals, scopes, and execution policy |
| **Observability** | Tracks workflow steps and transitions | Tracks model calls, tool calls, sessions, delegation, and runtime behavior |
| **Completion** | Reaches the workflow's terminal step | Reaches a managed agent or task state; domain verification must still confirm the outcome |
| **Best fit** | Repeatable processes with known stages and routing | Long-running, tool-using, stateful, or delegated agent execution |

### Example

A customer-support workflow might define:

```text
Receive request -> classify issue -> retrieve account context
                -> draft resolution -> request approval -> respond
```

**Microsoft Learn reference:** [Workflow concepts](https://learn.microsoft.com/en-us/agent-framework/concepts/workflows/)

The Agent Harness can support the agent within those steps by preserving its session,
providing approved retrieval and communication tools, loading support skills, tracking
delegated research, enforcing limits, and recording telemetry.

In this repository's software-repair example, the workflow is approximately:

```text
Inspect problem -> delegate frontend and backend work
                -> add regression coverage -> update documentation
                -> run independent acceptance -> correct or complete
```

**Microsoft Learn references:** [Workflow concepts](https://learn.microsoft.com/en-us/agent-framework/concepts/workflows/)
and [Agent Harness](https://learn.microsoft.com/en-us/agent-framework/concepts/harness)

The harness does not replace that sequence. It gives Phoenix the state, skills, tools,
delegation, bounded loops, approvals, memory, and telemetry needed to execute its part of
the sequence.

### When a workflow may be enough

A simple, deterministic process may only need a workflow. For example, an application that
always retrieves one record, applies a fixed transformation, and sends the result may not
need a long-lived agent runtime.

### When a harness becomes valuable

A harness becomes more valuable when an agent must:

- reason and act across many model turns;
- retain or compact evolving context;
- choose among multiple tools;
- delegate and monitor parallel work;
- recover from incomplete results;
- follow reusable skills and approval policy;
- operate within hard limits; or
- provide detailed runtime telemetry.

Many production systems use both: the workflow provides predictable business
orchestration, while the harness provides dependable agent execution inside one or more
workflow steps.

## Example: what the harness does in this repository

The concepts above are domain-neutral. This repository applies them to one specific
example: autonomous software repair.

Phoenix is the coordinator and is created as a Microsoft Agent Framework
`HarnessAgent`:

```csharp
var phoenix = chatClient.AsHarnessAgent(phoenixOptions);
```

**Microsoft Learn references:** [Create a harness agent with `AsHarnessAgent`](https://learn.microsoft.com/en-us/agent-framework/get-started/harness)
and [Agent Harness concepts and architecture](https://learn.microsoft.com/en-us/agent-framework/concepts/harness)

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
- a substitute for domain validation, such as tests, policy checks, or human review;
- a guarantee that the generated result is correct;
- the business definition of success;
- permission to access every file or system; or
- a replacement for human accountability.

Every implementation still needs a domain-specific way to verify outcomes. For example, a
customer-service workflow might verify policy compliance and case resolution, while a
document-processing workflow might verify required fields and approval status.

In this repository's software-repair example, the application keeps the final acceptance
contract outside the harness. It independently checks source code, HTTP responses, Jest
results, browser rendering, browser errors, screenshots, and documentation.

A specialist task reaching `completed` means the specialist finished its assigned work.
It does **not** necessarily mean the real-world outcome is correct. In this example, only
the software acceptance gate can decide that the application is fixed.

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

### Reusable automation platform

The same harness pattern can support many controlled workflows. Examples include customer
service, document processing, research, compliance review, incident investigation,
operations, code repair, test generation, and documentation maintenance.

## Agent framework, agent, and harness

These terms are related but not interchangeable:

| Term | Meaning |
|---|---|
| **Model** | Produces reasoning and language or multimodal responses |
| **Agent** | A model configured with instructions, identity, and tools |
| **Agent framework** | APIs and abstractions used to build agents and workflows |
| **Agent workflow** | Application-defined orchestration of steps, decisions, agents, handoffs, and outcomes |
| **Agent Harness** | The managed runtime pipeline around an agent: state, tools, skills, delegation, policy, limits, and telemetry |
| **Outcome verification** | Domain-specific proof that the agent's result satisfies the real requirement |

## Practical design principle

Reliable autonomous systems require all three layers:

```text
Model intelligence
        +
Harness-managed execution
        +
Domain-owned verification
```

**Microsoft Learn references:** [Microsoft Agent Framework overview](https://learn.microsoft.com/en-us/agent-framework/overview/)
and [Agent concepts](https://learn.microsoft.com/en-us/agent-framework/concepts/agents/)

Removing any one of these layers weakens the system. Reasoning without execution cannot
perform the work. Execution without governance is difficult to trust. Governance without
independent verification can still accept the wrong outcome.

## Related documents

- [Agent Harness Code-Repair Case Study](Code-Repair-Case-Study.md)
- [What Is a Computer-Use Model?](Computer.Use.md)
- [How the Code Repair Actually Works](code-fix.md)
- [Setup and Demo Guide](SETUP.md)
