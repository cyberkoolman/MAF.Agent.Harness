# Team Dashboard Sample

This React and Express application is the repair target used by the Agent Harness demo.
The repository stores the working version here and the intentionally broken benchmark
files under `..\demo-fixtures\buggy`.

## Run the backend

```powershell
Set-Location .\sample-app\backend
npm install
node --watch server.js
```

The API listens on `http://localhost:4000`.

## Run the frontend

```powershell
Set-Location .\sample-app\frontend
npm install
$env:PORT = 3001
npm start
```

The UI listens on `http://localhost:3001`.

## Test

```powershell
Set-Location .\sample-app\backend
npm test -- --runInBand --coverage=false

Set-Location ..\frontend
npm test -- --watchAll=false --runInBand
```

## Reset to the intentionally broken benchmark

From the repository root:

```powershell
.\scripts\Reset-SampleToBuggy.ps1
```

Run the Agent Harness mission to diagnose and repair the sample, then use
`git diff -- sample-app` to review its changes.
