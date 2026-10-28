# RaceDay (PROG6212)

RaceDay is an event platform for runs, walks and cycle races.

| Project | What it is |
|---------|-----------|
| `RaceDay.Api` | **Part 2** - ASP.NET Core Web API (.NET 8), EF Core Code-First, SQL Server, cookie sessions, Swagger, Azure Blob image storage |
| `RaceDay.Api.Tests` | xUnit tests for the API (run in CI) |
| `RaceDay.Web` | **Part 3** - ASP.NET Core MVC front end. It has **no database access**; every operation goes through the API |

## Roles

| Role | What they can do |
|------|------------------|
| **Organiser** | Dashboard with enrolment totals and upcoming dates; create, edit and delete events (with banner upload); manage categories; view and confirm/cancel enrolments; capture and publish results; edit their profile. |
| **Participant** | Browse and filter upcoming events; see event details and categories; enrol under a category; see their enrolments with colour-coded status; see their race history ("47th out of 312 finishers"); edit their profile and upload a profile picture. |

Unauthenticated visitors can browse events and event details only. Organiser pages return an "access denied" page for Participants and vice versa.

## Architecture

```
Browser  ->  RaceDay.Web (MVC)  ->  RaceDay.Api (Web API)  ->  SQL Server
                                            |
                                            +->  Azure Blob Storage (images)
```

* **Session management.** On login the MVC app calls `POST /api/auth/login`, keeps the API's session cookie in the **server-side ASP.NET Core session**, and issues its own cookie holding only the user id, name and role. The API cookie is never sent to the browser. Every API call forwards it. Logging out clears both and calls `POST /api/auth/logout`.
* **Role-based navigation.** The navbar and pages change per role; controllers use `[Authorize(Roles = ...)]`.
* **Azure Blob Storage.** Only the API talks to Azure. The MVC app uploads the image to the API (`/api/events/{id}/banner`, `/api/users/me/profilepicture`), which stores it in Blob Storage and returns the URL.
* **Error handling.** API problems are turned into friendly messages; unexpected errors show a branded error page, never a stack trace.

## Run locally

Prerequisites: Visual Studio 2022, .NET 8 SDK, SQL Server LocalDB/Express.

1. Open `RaceDay.sln`.
2. In `RaceDay.Api/appsettings.json` check the `DefaultConnection` server name.
3. Package Manager Console (default project `RaceDay.Api`):
   ```
   Add-Migration InitialCreate
   Update-Database
   ```
4. Right-click the solution -> **Configure Startup Projects** -> **Multiple startup projects**: set `RaceDay.Api` and `RaceDay.Web` to **Start**, then press F5.
   * API: https://localhost:7053/swagger
   * Web: https://localhost:7100
5. The web app reads the API address from `RaceDay.Web/appsettings.json` (`Api:BaseUrl`).

## Azure Blob Storage setup

1. In the Azure portal create a **Storage account**.
2. Open it -> **Configuration** -> make sure **Allow Blob anonymous access** is **Enabled** (images are shown to the public).
3. **Access keys** -> copy a **Connection string**.
4. Give it to the **API** (never the MVC app), for example with .NET user secrets:
   ```
   cd RaceDay.Api
   dotnet user-secrets init
   dotnet user-secrets set "AzureBlob:ConnectionString" "<your connection string>"
   ```
   The container `raceday-images` is created automatically. Without a connection string the API falls back to saving files in `wwwroot/uploads` so development still works.

## Docker (MVC app)

The Dockerfile is a multi-stage build (SDK image to build, small ASP.NET runtime image to run, non-root user).

Build:
```
docker build -t raceday-web ./RaceDay.Web
```

Run (single command; the API must already be running on your machine on http port 5053):
```
docker run --rm -p 8080:8080 -e Api__BaseUrl=http://host.docker.internal:5053 -e Api__PublicBaseUrl=http://localhost:5053 raceday-web
```
Then open http://localhost:8080.

* `Api__BaseUrl` is where the **container** reaches the API (`host.docker.internal` = your machine).
* `Api__PublicBaseUrl` is where your **browser** reaches the API, used for locally stored images. Azure images use their own URLs.
* Start the API with the http profile so port 5053 is available: `dotnet run --project RaceDay.Api --urls "http://0.0.0.0:5053"`.

The container uses the same code and settings as the local run, so it behaves identically.

## Tests and CI/CD

```
dotnet test
```

GitHub Actions (`.github/workflows/ci.yml`) restores, builds the whole solution (API, MVC and tests), runs the tests, then builds the MVC Docker image on every push.

![Green build](docs/ci-green-build.png)

## Demo video

YouTube (unlisted): _paste link here_
