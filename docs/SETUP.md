# Setup and Demo Guide

Run every command from the cloned repository root unless a step says otherwise.

## 1. Build the .NET solution

```powershell
dotnet build .\MAF.Agent.Harness.slnx
```

## 2. Install sample dependencies

```powershell
Set-Location .\sample-app\backend
npm install

Set-Location ..\frontend
npm install

Set-Location ..\..
```

## 3. Install Playwright Chromium

```powershell
pwsh .\src\MAF.RPTeam\bin\Debug\net8.0\playwright.ps1 install chromium
```

## 4. Configure Azure OpenAI

The application uses `DefaultAzureCredential`; it does not accept an API key in source.

```powershell
az login
dotnet user-secrets set --project .\src\MAF.RPTeam "AzureOpenAI:Endpoint" "https://<resource>.openai.azure.com/"
dotnet user-secrets set --project .\src\MAF.RPTeam "AzureOpenAI:Deployment" "<deployment>"
```

Enable headed computer use:

```powershell
$env:RPTEAM_Browser__Enabled = "true"
$env:RPTEAM_Browser__Headless = "false"
```

Optional Azure Monitor export:

```powershell
$env:APPLICATIONINSIGHTS_CONNECTION_STRING = "<connection-string>"
```

## 5. Start the sample application

Use two PowerShell terminals.

**Backend**

```powershell
Set-Location .\sample-app\backend
node --watch server.js
```

**Frontend**

```powershell
Set-Location .\sample-app\frontend
$env:PORT = 3001
npm start
```

Confirm the working sample:

```powershell
Invoke-WebRequest http://localhost:4000/api/users/999 -SkipHttpErrorCheck
Invoke-WebRequest http://localhost:4000/api/users/1/stats
Invoke-WebRequest http://localhost:3001
```

Expected status codes are 404, 200, and 200.

## 6. Run the independent verifier

```powershell
dotnet run --project .\src\MAF.RPTeam -- --verify-only
```

Playwright opens the browser automatically. Do not interact with it. Wait for the terminal
to print `[PASS]` for source, API, frontend, browser, backend tests, frontend tests, and
README checks.

## 7. Run the repair demonstration

Reset the included sample to the intentionally broken benchmark:

```powershell
.\scripts\Reset-SampleToBuggy.ps1
```

The frontend and backend watch processes reload the changed files. Confirm the broken
state, then run:

```powershell
dotnet run --project .\src\MAF.RPTeam
```

The final success marker is:

```text
Mission Complete - independently verified
```

Review the repair:

```powershell
git --no-pager diff -- sample-app
```

Restore the committed working sample after a demonstration:

```powershell
git restore --worktree -- sample-app
```

## 8. Optional Telegram trigger

Store the bot token and allowlisted chat ID outside source control:

```powershell
dotnet user-secrets set --project .\src\MAF.RPTeam.Telegram "Telegram:BotToken" "<bot-token>"
dotnet user-secrets set --project .\src\MAF.RPTeam.Telegram "Telegram:AllowedChatIds:0" "<chat-id>"
```

Build before starting Telegram because its mission child uses `--no-build`:

```powershell
dotnet build .\MAF.Agent.Harness.slnx
dotnet run --project .\src\MAF.RPTeam.Telegram --no-build
```

Send:

```text
/mission Mission Control: diagnose and repair the Team Dashboard, then verify everything.
```
