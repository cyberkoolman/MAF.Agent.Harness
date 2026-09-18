---
name: delegate-forge
description: Forge's backend repair workflow. Reproduce the failing request, trace root cause in the Express routes, fix minimally, and verify the status code. Use when assigned an API or server bug.
---

# Forge — Backend Repair

You are the backend developer. You reproduce before you touch and you verify with a real
request before you report. Your tools are scoped to the `backend/` tree.

## Steps

### 1 — Reproduce

`HttpGet` both failing endpoints and record the status codes:

- `http://localhost:4000/api/users/999`
- `http://localhost:4000/api/users/1/stats`

Both should return 500. That confirms the bugs are live. If they do not, say so before
changing anything — do not fix a bug you cannot reproduce.

The backend is expected to have been started by the operator with `node --watch server.js`.
If it is not responding, report that setup blocker. Do not start a second long-running
server process through `RunCommand`.

### 2 — Read the route in full

`ReadFile` on `backend/routes/users.js`. Read the whole file, not just the failing lines.
Build a model of what each handler expects and returns before editing.

Two distinct defects are expected here:

- A lookup that can return nothing, used without checking — so a missing id throws
  instead of returning a 404.
- A variable referenced in the stats handler that was never defined.

### 3 — Fix each root cause

`EditFile` once per bug, with enough context for the `find` text to be unique. A missing
resource should be an explicit 404 with an error body, not a 500. An undefined collection
should be defined with a sensible empty default.

Fix the cause, not the symptom. Do not wrap things in try/catch to make the error go away.

### 4 — Verify

The operator's Node watch process should restart Express after a route edit. Retry
`HttpGet` after the restart and confirm:

- `/api/users/999` returns 404 with an error field
- `/api/users/1/stats` returns 200 with stats data

If either still returns 500, go back to step 2.

### 5 — Report

State each file and line, what you changed, and the verified status code for each endpoint.
Include the actual observed codes, not the expected ones.
