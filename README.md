# MyCare Appointments: Test Automation Demo updated

A small demo web app that puts the **Automation Test Implementation Strategy (v1.0)** into practice:
three layers of automated tests, requirement traceability, release gates, and automatic deployment with GitHub Actions.

> Synthetic data only. No real patient data (PHI) is used anywhere.

---

## What's in here

| Folder / file | What it is |
|---|---|
| `src/MyCare.Web` | The app: a web page for booking appointments, a REST API, and the booking rules |
| `tests/MyCare.UnitTests` | **Unit + integration** tests (xUnit + Shouldly). Temporary database, wiped after every test |
| `tests/MyCare.ApiTests` | **API / contract** tests (RestSharp). Calls the running service directly, without the UI |
| `tests/MyCare.UiTests` | **UI regression** tests (Playwright). Real browsers: Chromium, Firefox, WebKit |
| `tests/MyCare.TestSupport` | Shared test helpers: required base URL, synthetic test data |
| `.github/workflows/ci-cd.yml` | The pipeline: build → test → release gates → deploy → smoke test |
| `scripts/Test-ReleaseGates.ps1` | Decides whether a change can ship (strategy section 6) |
| `scripts/Sync-XrayResults.ps1` | Posts every result to Jira/Xray (switched off until Xray credentials are added) |
| `Dockerfile` | How Render packages and runs the app |

### Business rules the tests check

| Jira key | Requirement |
|---|---|
| LC-101 | Book an appointment |
| LC-102 | Appointments can't be in the past |
| LC-103 | Weekdays only, within clinic hours (08:00–17:00 UTC) |
| LC-104 | 15–120 minutes, in 15-minute slots |
| LC-105 | A provider can't be double-booked |
| LC-106 | Upcoming appointments can be cancelled (once) |
| LC-107 | View and filter appointments by provider |
| LC-108 | Automation activity is distinguishable from real users in audit logs |
| LC-109 | Health check for monitoring |
| LC-110 | Required fields are validated |

Every test name starts with its Jira key, for example `LC-105 | Double-booking a provider is rejected with 409`.
The pipeline blocks any test that has no key.

---

## How the strategy maps to this demo

| Strategy says | Where it happens |
|---|---|
| Three check types run automatically, without manual triggering (§4) | Every push and pull request runs `ci-cd.yml` |
| UI regression runs in parallel across three browser engines; recording kept only on failure (§4) | `ui-regression` job matrix. A Playwright trace is saved only when a test fails (download it from the run's artifacts) |
| Unit + integration tests use a temporary, wiped database with no connection to any real system (§4) | `MyCareAppFactory` creates a throwaway SQLite database per test |
| Results sync to Jira/Xray automatically (§4) | `Sync-XrayResults.ps1` step in the `release-gate` job |
| Base URLs are required inputs; the test fails if unset (§5) | `TestSettings.BaseUrl` throws when `MYCARE_BASE_URL` is missing |
| Credentials come from pipeline secrets storage (§5) | Render deploy hook and Xray keys are GitHub **secrets**, never in code |
| Unit + integration: 100% must pass or the release cannot proceed (§6) | Hard gate in `Test-ReleaseGates.ps1` |
| UI regression: 100% expected, otherwise the PM decides (§6) | The deploy goes to the `production-pm-approval` environment, which waits for a named approver |
| Flaky tests are quarantined (§6) | Tag a test `[Trait("Category", "Quarantine")]`. It still runs and is reported, but can't block a release |
| Test-account activity is distinguishable in audit trails (§7) | Tests send `X-Actor: automation`. The app records and logs it (`CreatedBy`) |

**Added beyond v1.0 (to fill gaps in the strategy):** the API/contract tier is a hard gate, and a post-deploy smoke test runs against the live site.

---

## One-time setup (about 20 minutes)

### Step 1: Create the GitHub repository
1. Go to <https://github.com/new>.
2. Give it a name, e.g. `mycicddemo`.
3. Choose **Public**. On a free personal account, the PM-approval gate (step 5) only works on public repos.
4. Leave all the "Initialize" boxes unticked. Click **Create repository**.

### Step 2: Upload the code
1. On the new repo's page, click the **uploading an existing file** link.
2. Open this project folder in File Explorer, select **everything inside it**, and drag it onto the GitHub page.
   Make sure the `.github` folder is included, because it contains the pipeline.
3. Click **Commit changes**.
   - Check that the repo's file list shows a `.github` folder. The web uploader sometimes skips it.
     If it's missing, add its two files by hand: **Add file → Create new file**, type the path
     (`.github/workflows/ci-cd.yml`, then `.github/actions/start-app/action.yml`), paste in the file's contents, and commit.
4. Open the **Actions** tab. The first run starts automatically. The test jobs should pass (green).
   The **Deploy to Render** job will fail at this point because Render isn't set up yet. That's expected.

### Step 3: Create the Render web service
1. Go to <https://render.com> and sign up with **GitHub**.
2. Click **New → Web Service**, then pick your repo (grant Render access if asked).
3. Settings:
   - **Language:** Docker
   - **Branch:** `main`
   - **Instance type:** Free
   - **Advanced → Health Check Path:** `/api/health`
4. Click **Create Web Service**. Render builds it once by itself (a few minutes).
5. Open the service's **Settings**:
   - Set **Auto-Deploy** to **Off**. From now on only the pipeline deploys, and only after the tests pass.
   - Copy the **Deploy Hook** URL. Keep it private, because anyone with it can trigger a deploy.
   - Copy your site URL from the top of the page, e.g. `https://mycare-demo.onrender.com`.

### Step 4: Give GitHub the Render details
In the GitHub repo, go to **Settings → Secrets and variables → Actions**:
- **Secrets** tab → **New repository secret**: name `RENDER_DEPLOY_HOOK_URL`, value = the deploy hook URL
- **Variables** tab → **New repository variable**: name `RENDER_APP_URL`, value = the site URL (no trailing `/`)

### Step 5: Set up the PM-approval gate
Go to **Settings → Environments**:
1. **New environment** → `production` → **Configure environment**. No rules are needed; this path is used when all UI tests pass.
2. **New environment** → `production-pm-approval` → tick **Required reviewers** → add the project manager (or yourself for the demo) → **Save protection rules**.

### Step 6: Run it
Go to **Actions → CI/CD → Run workflow** (or push any change). All jobs should go green, and your site is live at `RENDER_APP_URL`.

> Render's free tier sleeps when idle, so the first visit after a while takes about a minute to wake up. The pipeline waits for it.

### Optional: turn on the Jira/Xray sync
Add secrets `XRAY_CLIENT_ID` and `XRAY_CLIENT_SECRET` (from Xray → Settings → API Keys), plus a variable `XRAY_PROJECT_KEY` (your Jira project key).
If your Jira key isn't `LC`, rename the `LC-` prefixes in the test names too.

---

## Demo scenarios to show

| To show | Do this | What happens |
|---|---|---|
| Happy path | Push any small change | All gates ✅ → deploy → smoke tests on the live site |
| Hard gate blocks a release | In `AppointmentRules.cs`, change `ClinicCloses` to `18` and push | Unit tests fail → **Release gates: BLOCKED** → no deploy |
| PM decides on UI failures | In `AppointmentJourneyTests.cs`, change `"Scheduled"` to `"Booked"` in the first test and push | UI tests fail, other gates pass → the deploy waits for approval under **production-pm-approval** |
| Traceability gate | Remove the `LC-101 \| ` prefix from any test name and push | Release is blocked and the summary lists the untraced test |
| Failure diagnostics | After any UI failure, download the `results-ui-<browser>` artifact | Open the `.zip` trace at <https://trace.playwright.dev> to replay the failure step by step |
| Quarantine | Add `[Trait("Category", "Quarantine")]` to a failing UI test | It still runs and is reported, but the release is no longer blocked |

Remember to revert each scenario afterwards.

Every run has a **Summary** page with the release-gate table and the pass/fail counts per tier.

---

## Running locally (optional)
Needs the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```bash
dotnet run --project src/MyCare.Web --urls http://localhost:5080
```

In a second terminal (PowerShell):

```powershell
$env:MYCARE_BASE_URL = "http://localhost:5080"
dotnet test tests/MyCare.UnitTests
dotnet test tests/MyCare.ApiTests
dotnet build tests/MyCare.UiTests
pwsh tests/MyCare.UiTests/bin/Debug/net8.0/playwright.ps1 install chromium
dotnet test tests/MyCare.UiTests
```

Set `$env:HEADED = "1"` to watch the browser, and `$env:BROWSER = "firefox"` (or `webkit`) to switch engines.
