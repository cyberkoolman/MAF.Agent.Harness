# Team Dashboard API

Express API used by the Agent Harness repair demonstration.

## Run

```powershell
npm install
npm start
```

The server listens on `http://localhost:4000`.

## Test

```powershell
npm test -- --runInBand --coverage=false
```

## Endpoints

| Method and path | Success |
|---|---|
| `GET /api/health` | Service status and uptime |
| `GET /api/users` | Team-member array |
| `GET /api/users/:id` | User profile, or HTTP 404 when absent |
| `GET /api/users/:id/stats` | Task totals, completion count, and rate, or HTTP 404 when absent |
| `GET /api/tasks` | Task array |
| `GET /api/tasks/:id` | Task record, or HTTP 404 when absent |

The data is read from `data\db.json`. Tests import `app.js` directly, so Jest can exercise
the API without binding a network port.
