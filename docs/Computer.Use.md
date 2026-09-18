# What Is a Computer-Use Model?

## Executive summary

A **computer-use model** is a multimodal AI model that can understand the state of a user
interface and choose actions that move a task forward.

Traditional language models primarily receive text and return text. A computer-use model
can also reason from visual or structured interface evidence such as screenshots,
accessibility trees, visible controls, browser errors, and page state. When connected to
controlled tools, it can navigate, click, type, inspect, and verify a real application.

```text
+-------------------------+
| Observe                 |
| Screenshot, page state, |
| accessibility snapshot  |
+------------+------------+
             |
             v
+-------------------------+
| Reason                  |
| Understand the UI and   |
| choose the next action  |
+------------+------------+
             |
             v
+-------------------------+
| Act                     |
| Navigate, click, type,  |
| or call another tool    |
+------------+------------+
             |
             v
+-------------------------+
| Verify                  |
| Inspect the new state,  |
| errors, and evidence    |
+------------+------------+
             |
             +----------> repeat until done or bounded
```

Computer use is the **observe-reason-act** capability. It is not, by itself, the harness,
the safety policy, or the acceptance test.

## How computer use works in this repository

This solution uses a vision-capable Azure OpenAI model with bounded Playwright browser
tools. Selected agents can:

- navigate only to configured application origins;
- capture a real PNG screenshot;
- inspect an accessibility-oriented page snapshot;
- identify visible interactive elements;
- click through references from the latest snapshot;
- read bounded browser-console and page-error output; and
- revisit the UI after a repair.

The screenshot is delivered to the model as actual image content. It is not merely a file
name or a text statement claiming that the page looks correct.

```text
Playwright browser
        |
        +-- screenshot ------------+
        +-- accessibility snapshot |
        +-- console/page errors    |
                                   v
                          Vision-capable model
                                   |
                           chooses bounded action
                                   |
                                   v
                         Scoped browser tool call
```

The model does not receive unrestricted desktop control. The browser capability is limited
to the application under test, explicit actions, current-page references, timeouts, and
configured evidence limits.

## Benefits of computer use

### 1. It validates what the user actually sees

Source code can look correct while the rendered application remains broken because of
runtime data, CSS, JavaScript errors, stale processes, or integration failures. Browser
inspection tests the user-facing result.

### 2. It can diagnose visual failures

Screenshots expose problems that are difficult to infer from source alone, including
missing controls, overlapping content, blank pages, incorrect labels, layout defects, and
unexpected error states.

### 3. It connects symptoms to engineering evidence

The model can combine the visible failure with browser-console errors, accessibility
structure, HTTP behavior, source code, and test output. This produces a stronger diagnosis
than relying on one evidence type.

### 4. It supports unfamiliar applications

A model can explore a UI based on visible labels and page structure instead of requiring a
hard-coded automation script for every path. This is useful when interfaces change or when
the exact source location is not known initially.

### 5. It improves regression coverage

A reproduced user journey can inform new automated tests. After repair, the same journey
can be checked again through deterministic browser acceptance.

### 6. It creates understandable evidence

Screenshots and observed page state make autonomous work easier to explain to clients,
operators, and reviewers who do not want to interpret source code or raw logs.

### 7. It can complement accessibility testing

Accessibility-oriented snapshots expose roles, labels, names, and interactive structure.
They help the model reason about the interface while also highlighting missing or unclear
semantics.

## Computer use compared with traditional automation

| Traditional scripted automation | Computer-use model |
|---|---|
| Follows predefined selectors and steps | Chooses steps from the current interface state |
| Highly repeatable | More adaptive |
| Efficient for stable regression suites | Useful for investigation and changing workflows |
| Usually fails when expected structure changes | Can reason about alternate visible paths |
| Produces deterministic pass/fail results | Produces model-driven observations and actions |

The strongest approach combines both:

> Use computer use for flexible observation and investigation. Use deterministic
> automation for policy, safety, and final pass/fail acceptance.

## Why computer use is not enough by itself

A visually convincing page does not prove that:

- every API returns the correct status and data;
- the implementation is secure;
- tests pass;
- hidden workflows work;
- the model edited only approved files;
- the change will remain correct after restart; or
- the repair satisfies all business requirements.

Computer-use observations may also be affected by animation, timing, ambiguous controls,
incomplete screenshots, pop-ups, stale processes, or nondeterministic page state.

This repository therefore combines computer use with:

- exact source assertions;
- live HTTP status checks;
- backend and frontend Jest suites;
- browser-console and page-error checks;
- separate browser contexts;
- screenshot-size and visibility evidence;
- scoped file and command tools;
- bounded correction rounds; and
- application-owned acceptance.

## Safety and governance requirements

Production computer-use systems should apply:

| Control | Purpose |
|---|---|
| Origin allowlist | Prevent navigation to unapproved sites |
| Isolated browser context | Avoid reusing personal cookies, sessions, or credentials |
| Scoped actions | Expose only necessary navigation, click, typing, and inspection tools |
| Stable element references | Bind actions to the latest observed page state |
| Time and action limits | Prevent uncontrolled loops |
| Sensitive-data controls | Keep screenshots, credentials, and personal data out of logs and prompts where inappropriate |
| Approval gates | Require people to authorize purchases, deployments, destructive actions, or privileged operations |
| Independent acceptance | Prevent the model from grading its own work |
| Telemetry and audit records | Explain what the system observed and did |
| Human review | Preserve accountability before merge, release, or consequential action |

## How it benefits autonomous code repair

In this demonstration, computer use closes the loop between code and experience:

```text
Broken browser experience
          |
          v
Model observes screenshot and browser errors
          |
          v
Agent Harness delegates scoped code repair
          |
          v
Tests and HTTP checks run
          |
          v
Browser is inspected again
          |
          v
Independent acceptance passes or returns evidence
```

This matters because code repair should not end when a patch looks plausible. It should end
when the relevant behavior is independently demonstrated.

## Suitable use cases

Computer-use models can help with:

- reproducing reported UI defects;
- exploratory testing;
- accessibility-oriented inspection;
- validating forms and navigation;
- comparing behavior before and after a change;
- collecting evidence during incident investigation;
- assisting with repetitive browser workflows; and
- investigating applications whose source location is initially unknown.

They should be used carefully for financial, medical, legal, production-administration,
identity, or other high-impact workflows. In those settings, strong isolation, explicit
approval, deterministic validation, and human accountability are essential.

## Related documents

- [What Is an Agent Harness?](Agent.Harness.md)
- [Agent Harness in Action](Agent-Harness.md)
- [How the Code Repair Actually Works](code-fix.md)
- [Setup and Demo Guide](SETUP.md)
