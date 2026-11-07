# RaceDay

RaceDay is a race-event management system for running, walking and cycling events. Organisers create events, add age or distance categories, manage enrolments and capture results. Participants browse events, enter a category, and follow their enrolments and race results.

This repository holds all three parts of the PROG6212 Portfolio of Evidence:

| Part | What | Where |
|---|---|---|
| Part 1 | ERD, API endpoint plan and SQL Server script (as submitted) | `docs/part1-submitted/` |
| Part 2 | RESTful API: ASP.NET Core Web API, EF Core, session authentication, Swagger, tests | `src/RaceDay.Api/`, `tests/RaceDay.Api.Tests/` |
| Part 3 | ASP.NET Core MVC web application that consumes the API, Azure Blob Storage, Docker | `src/RaceDay.Web/`, `tests/RaceDay.Web.Tests/`, `Dockerfile` |

Part 3 was developed on the `part3-mvc` branch.

**Part 3 demo video (unlisted YouTube):** _link to be added_

---

## Contents

1. [How the system fits together](#how-the-system-fits-together)
2. [Repository layout](#repository-layout)
3. [Running locally](#running-locally)
4. [Running with Docker](#running-with-docker)
5. [Using RaceDay](#using-raceday)
6. [MVC application structure](#mvc-application-structure)
7. [Authentication, sessions and roles](#authentication-sessions-and-roles)
8. [Azure Blob Storage](#azure-blob-storage)
9. [User interface](#user-interface)
10. [API endpoints](#api-endpoints)
11. [Tests and CI/CD](#tests-and-cicd)
12. [Differences from the Part 1 plan](#differences-from-the-part-1-plan)

---

## How the system fits together

- The **browser** only talks to the **MVC app** (`RaceDay.Web`).
- The MVC app has no database access and no business rules of its own. Every page gets and saves its data by calling the **API** (`RaceDay.Api`) over HTTP.
- The API owns all the rules (who may do what, capacity, bib numbers, result checks) and stores data in **SQL Server** through EF Core.
- Uploaded images (event banners and profile pictures) go from the MVC app to the API, and the API stores them in **Azure Blob Storage**. The MVC app never connects to Azure.

## Repository layout

```
RaceDay.sln
Dockerfile                          MVC app image (Part 3)
docker-compose.yml                  API + MVC + Azurite in one command
src/
  RaceDay.Api/                      Web API (Part 2, extended in Part 3)
    Auth/                           Session authentication handler, roles
    Controllers/                    Auth, Users, Events, Categories, Enrolments, Results
    Data/                           DbContext, EF Core migrations, database initialiser
    Dtos/                           Request and response models with validation
    Models/                         Role, User, Event, Category, EventEnrolment, Result
    Storage/                        Azure Blob Storage image service and upload checks
    Dockerfile                      API image (used by docker compose)
  RaceDay.Web/                      MVC web application (Part 3)
    Areas/Organiser/                Dashboard, events, categories, enrolments, results
    Areas/Participant/              My enrolments, my results
    Controllers/                    Home (events), Events (details), Account
    Filters/                        RequireRole (session-based access), ApiExceptionFilter
    Infrastructure/                 Display formatting, API error mapping
    Models/Api/                     Shapes of the API's JSON
    Services/                       RaceDayApiClient, API session cookie handler, UserSession
    ViewModels/                     Form and page models with validation
    Views/                          Razor views, shared layout and partials
    wwwroot/                        RaceDay theme (site.css), site.js, Bootstrap, jQuery validation
tests/
  RaceDay.Api.Tests/                API tests (including Azure Blob Storage tests run against Azurite in CI)
  RaceDay.Web.Tests/                MVC tests that run the MVC app against the real API
docs/
  ERD_RaceDay.png / .svg            Current ERD (version 3)
  RaceDay_Database_Schema.sql       SQL Server script matching the EF Core migrations
  API_Endpoint_Plan.md              Endpoint plan from Part 1
  part1-submitted/                  Part 1 files, unchanged
tools/make_erd.py                   Script that draws the ERD
.github/workflows/dotnet.yml        CI: build, tests, Docker smoke test
```

## Running locally

### Requirements

- .NET 8 SDK and Visual Studio 2022 (17.8 or later) with the ASP.NET workload
- SQL Server LocalDB (installed with Visual Studio)
- An Azure Storage account for images, or the Azurite emulator (see [Azure Blob Storage](#azure-blob-storage))

### 1. Give the API its Azure Blob Storage connection string

```bash
dotnet user-secrets set "BlobStorage:ConnectionString" "<your Azure Storage connection string>" --project src/RaceDay.Api
```

User secrets are stored outside the repository, so the storage key is never committed. Without a connection string the app still runs; image uploads show a friendly "not available" message.

### 2. Start the API and the MVC app

In Visual Studio: right-click the solution, choose **Configure Startup Projects**, select **Multiple startup projects**, set **RaceDay.Api** and **RaceDay.Web** to **Start**, then press **F5**.

From a terminal:

```bash
dotnet run --project src/RaceDay.Api --launch-profile http     # API and Swagger: http://localhost:5104/swagger
dotnet run --project src/RaceDay.Web --launch-profile http     # MVC app:         http://localhost:5230
```

On first start the API applies the EF Core migrations, which creates `RaceDayDB` in LocalDB with all its tables. The connection string is in `src/RaceDay.Api/appsettings.json` (`ConnectionStrings:RaceDayDB`). The MVC app finds the API through `RaceDayApi:BaseUrl` in `src/RaceDay.Web/appsettings.json` (default `http://localhost:5104/`).

No SQL Server available (for example on macOS)? Run the API against an in-memory database instead. Data is lost when the API stops.

```bash
dotnet run --project src/RaceDay.Api --launch-profile http -- --Database:Provider=InMemory
```

## Running with Docker

### MVC app container (single command)

Build the image once from the repository root:

```bash
docker build -t raceday-web .
```

Then start the MVC app with one command (the API must be running on the host, as in [Running locally](#running-locally)):

```bash
docker run -d -p 8080:8080 -e RaceDayApi__BaseUrl=http://host.docker.internal:5104/ --name raceday-web raceday-web
```

Open **http://localhost:8080**.

- On Linux, add `--add-host=host.docker.internal:host-gateway` so the container can reach the host.
- If the container cannot reach the API, start the API so it listens on all interfaces: `dotnet run --project src/RaceDay.Api -- --urls http://0.0.0.0:5104`.
- Stop and remove the container with `docker rm -f raceday-web`.

The container runs the same published code and the same configuration as the local app. The only setting that changes is `RaceDayApi__BaseUrl`, because inside a container `localhost` is the container itself. So the containerised app behaves exactly like the local one.

### Whole system with docker compose

```bash
docker compose up --build
```

This starts the MVC app (http://localhost:8080), the API (http://localhost:5104/swagger) and Azurite, the local Azure Blob Storage emulator. The API uses an in-memory database in this setup. To store images in a real Azure Storage account instead of Azurite, put `AZURE_STORAGE_CONNECTION_STRING=<connection string>` in a `.env` file next to `docker-compose.yml` (the file is git-ignored).

### How the Dockerfile is built

- **Multi-stage build.** The .NET SDK image restores and publishes the app. The final image is the much smaller ASP.NET Core runtime image, with no SDK or source code in it.
- **Layer caching.** `RaceDay.Web.csproj` is copied and restored before the rest of the source, so packages are only restored again when the project file changes.
- **Release build** with `dotnet publish -c Release`.
- **Non-root.** The container runs as the `app` user that ships with the .NET 8 images.
- **Configuration by environment variables** (`RaceDayApi__BaseUrl`, `ASPNETCORE_HTTP_PORTS=8080`), so the same image works on any machine.
- `.dockerignore` keeps `bin/`, `obj/`, tests, docs and Git data out of the build context.

## Using RaceDay

There are no built-in accounts. Each person registers their own account and chooses **Participant** or **Organiser**.

**Organiser**

1. Register as an Organiser. You land on the **Dashboard**: events created, upcoming events, total enrolments and results captured, with a row per event showing its date and how many places are taken.
2. **My events > Create event.** Enter the name, description, date, location, distance and event type, and optionally upload a banner image.
3. **Categories.** Add age categories (for example Under 20) or distance categories (for example 10km), each with places and an entry fee.
4. **Enrolments.** See everyone who entered, with their category, bib number and status. Change a status to Pending, Confirmed or Cancelled.
5. **Results.** After the event date, enter finish times and positions and choose **Save and publish results**. Participants see them straight away.

**Participant**

1. Register as a Participant. The home page lists upcoming events with filters for text, event type and dates.
2. Open an event to see its details, banner and categories. Choose a category and select **Enter now**. The enrolment is recorded immediately with a bib number.
3. **My enrolments** lists every entry with a colour-coded status, and lets you withdraw from upcoming events.
4. **My results** shows your race history: event, date, category, finish time and placing, for example "47th of 312 finishers".
5. **My profile** lets you update your name and phone number and upload a profile picture.

## MVC application structure

| Folder | Role in MVC |
|---|---|
| `Controllers/` and `Areas/*/Controllers/` | Handle each request, call the API through `RaceDayApiClient`, and choose the view. The `Organiser` and `Participant` areas keep each role's pages together. |
| `ViewModels/` | Form and page models. Validation attributes give the same checks in the browser (jQuery validation) and on the server. |
| `Views/` and `Areas/*/Views/` | Razor views. `_Layout.cshtml` holds the role-based navigation; partials hold the event card and status and type badges. |
| `Models/Api/` | The JSON shapes the API returns. |
| `Services/` | `RaceDayApiClient` (all API calls), `ApiSessionCookieHandler` (sends each user's API session cookie) and `UserSession` (logged-in user in the session). |
| `Filters/` | `RequireRole` protects pages by role. `ApiExceptionFilter` turns API outages into a friendly page. |

The MVC project has no Entity Framework reference and no database connection string. All data operations go through the Part 2 API.

## Authentication, sessions and roles

- **Register and log in** forms post to the MVC app, which calls `POST /api/auth/register` and `POST /api/auth/login`. The API checks the password hash and starts its own server-side session.
- **Session management in the MVC app.** After login, the MVC session (`.RaceDay.Web.Session`, HttpOnly, 30-minute idle timeout) stores the user's ID, name, role and the API's session cookie. Every API call for that user carries that user's API cookie. The shared `HttpClient` stores no cookies, so two users can never share an API session (a test checks this).
- **Role-based navigation.** The menu is different for each role: Organisers see Dashboard, My events and Browse events; Participants see Events, My enrolments and My results. Organisers go to their dashboard after login.
- **Protected pages.** `[RequireRole("Organiser")]` and `[RequireRole("Participant")]` guard the two areas. Visitors who are not logged in are sent to the login page (and back afterwards). Users with the other role see an "access denied" page. The API enforces the same rules again on every call, and also checks ownership: an Organiser can only manage their own events.
- **Logout** ends the API session and clears the MVC session, then returns to the events page.
- If the API session has expired (for example after the API restarts), the MVC app logs the user out and asks them to log in again instead of showing an error.

Passwords are hashed with ASP.NET Core Identity's `PasswordHasher` (PBKDF2 with a random salt per user). The plain password is never stored. An Admin account (used only by `GET /api/users/{id}` and to withdraw any enrolment) cannot be self-registered; it is only created when `Seed:AdminEmail` and `Seed:AdminPassword` are supplied through user secrets or environment variables.

## Azure Blob Storage

Blob Storage is integrated into the **API**:

- `src/RaceDay.Api/Storage/BlobImageStorage.cs` uses the Azure SDK (`Azure.Storage.Blobs`). It creates the container `raceday-images` on first use with **private** access, uploads with the correct content type, reads images back, and deletes old ones.
- Upload endpoints: `PUT /api/events/{id}/banner` (the event's Organiser) and `PUT /api/users/me/photo` (any logged-in user), as multipart/form-data.
- `ImageRules` accepts JPG, PNG, GIF and WEBP up to 5 MB. The type is checked from the file's first bytes, so a renamed text file is rejected.
- Blob names are stored in `Events.BannerImageName` and `Users.ProfileImageName` (migration `AddImageNames`). Replacing an image deletes the old blob, and deleting an event deletes its banner.
- Images are read back with `GET /api/events/{id}/banner` and `GET /api/users/me/photo`.

The **MVC app** sends uploads to those endpoints and shows images through its own actions (`/Events/Banner/{id}` and `/Account/Photo`), which fetch them from the API. The browser and the MVC app never talk to Azure directly. If storage is not reachable, the API returns 503 and the MVC app shows "Image uploads are not available right now" while the rest of the site keeps working.

**Setting up a storage account**

1. In the Azure portal create a **Storage account** (Standard, LRS is enough).
2. Open **Security + networking > Access keys** and copy a **Connection string**.
3. Save it as a user secret for the API (see [Running locally](#running-locally)).
4. After the first upload the images appear under **Data storage > Containers > raceday-images** (`events/<eventId>/...` and `profiles/<userId>/...`).

For local development without Azure, `appsettings.Development.json` points the API at Azurite (`UseDevelopmentStorage=true`). Start Azurite with:

```bash
docker run -d -p 10000:10000 mcr.microsoft.com/azure-storage/azurite azurite-blob --blobHost 0.0.0.0 --skipApiVersionCheck
```

## User interface

- **Theme.** RaceDay orange (`#FF5A1F`) on a dark navy navigation bar, with Barlow Condensed for headings and Barlow for body text. All colours, spacing and components come from one stylesheet (`wwwroot/css/site.css`), so every page looks the same.
- **Bootstrap 5** for layout and responsiveness. Pages work on phones, tablets and desktops.
- **Icons** from Google Material Symbols.
- **Colour-coded statuses:**

  | Status | Colour |
  |---|---|
  | Confirmed | Green |
  | Pending | Amber |
  | Cancelled | Red |
  | Finished (result captured) | Blue |

- **Validation and errors.** Forms validate in the browser and again on the server, and messages from the API appear next to the matching field. Unknown pages show a friendly "not found" page, and an unreachable API shows a "can't reach RaceDay" page. Raw error details are never shown to users.

## API endpoints

29 endpoints. The ones marked "Part 3" were added for the MVC app. Swagger (`/swagger`) shows request and response details and the role each endpoint needs.

| Method | Route | Role | Purpose |
|---|---|---|---|
| POST | `/api/auth/register` | Public | Register as Organiser or Participant |
| POST | `/api/auth/login` | Public | Log in, start session |
| POST | `/api/auth/logout` | Public | End session |
| GET | `/api/users/me` | Any logged in | View own profile |
| PUT | `/api/users/me` | Any logged in | Update own profile |
| PUT | `/api/users/me/photo` | Any logged in | Upload profile picture (Part 3) |
| GET | `/api/users/me/photo` | Any logged in | Get profile picture (Part 3) |
| GET | `/api/users/{id}` | Admin | View any user |
| GET | `/api/users/me/events` | Organiser | Dashboard summary of own events (Part 3) |
| POST | `/api/events` | Organiser | Create event |
| GET | `/api/events` | Public | Upcoming events; filters `eventType`, `search`, `from`, `to`, `includePast` (filters added in Part 3) |
| GET | `/api/events/{id}` | Public | View event |
| PUT | `/api/events/{id}` | Organiser (owner) | Update event |
| DELETE | `/api/events/{id}` | Organiser (owner) | Delete event, its categories and banner |
| PUT | `/api/events/{id}/banner` | Organiser (owner) | Upload banner image (Part 3) |
| GET | `/api/events/{id}/banner` | Public | Get banner image (Part 3) |
| POST | `/api/events/{eventId}/categories` | Organiser (owner) | Add age or distance category |
| GET | `/api/events/{eventId}/categories` | Public | List event's categories with places left |
| PUT | `/api/categories/{id}` | Organiser (owner) | Update category |
| DELETE | `/api/categories/{id}` | Organiser (owner) | Delete category |
| POST | `/api/categories/{categoryId}/enrol` | Participant | Enter an event through a category |
| GET | `/api/users/me/enrolments` | Participant | Own enrolments (with result if captured) |
| GET | `/api/categories/{categoryId}/enrolments` | Organiser (owner) | Enrolments in a category |
| GET | `/api/events/{eventId}/enrolments` | Organiser (owner) | All enrolments for an event |
| PUT | `/api/enrolments/{id}/status` | Organiser (owner) | Set status Pending, Confirmed or Cancelled (Part 3) |
| DELETE | `/api/enrolments/{id}` | Participant (owner) or Admin | Withdraw enrolment |
| POST | `/api/enrolments/{id}/result` | Organiser (owner) | Capture or update finish time and position |
| GET | `/api/categories/{categoryId}/results` | Public | Category leaderboard |
| GET | `/api/users/me/results` | Participant | Own results, with the number of finishers in each category |

## Tests and CI/CD

```bash
dotnet test
```

- **API tests** (`tests/RaceDay.Api.Tests`, 173 tests). These host the real API in memory with `WebApplicationFactory` and an EF Core in-memory database. They cover registration and login, role-based access (401 and 403 on every protected endpoint), event management and filters, categories, enrolments and statuses, results and finisher counts, the dashboard summary, and image uploads. Two tests run the real `BlobImageStorage` against Azurite; they are skipped when `RACEDAY_TEST_BLOB_CONNECTION` is not set, and CI sets it.
- **MVC tests** (`tests/RaceDay.Web.Tests`, 40 tests). These run the MVC app against an in-memory copy of the real API and use it like a browser (forms, antiforgery tokens, cookies). They cover registration and login, redirects to login and access denied, logout, creating an event, enrolling, validation messages, keeping users' sessions separate, and the friendly page when the API is down.

**GitHub Actions** (`.github/workflows/dotnet.yml`) runs on every push to `main` and `part3-mvc` and on pull requests:

1. **Build and test.** Builds the API, the MVC project and both test projects, starts Azurite, and runs all tests.
2. **Docker.** Starts the stack with `docker compose`, checks that the MVC home page renders through the API, uploads a banner through the API into Blob Storage and downloads it again through the MVC app, then starts the MVC image on its own with the `docker run` command from this README.

## Differences from the Part 1 plan

The Part 1 files are kept unchanged in `docs/part1-submitted/`. Every change since then is listed here with its reason.

### API

| # | Part 1 plan | Now | Reason |
|---|---|---|---|
| 1 | Login issues a JWT; response `{ token, user }` | Login starts a server-side session; response `{ message, user }` plus an HttpOnly session cookie | Parts 2 and 3 require session management for the user's authenticated state and role |
| 2 | No logout | `POST /api/auth/logout` | A session-based system needs a way to end the session |
| 3 | Enrolments listed per category only | `GET /api/events/{eventId}/enrolments` added (the per-category endpoint is kept) | Organisers need every enrolment for an event on one page |
| 4 | Event body `{ eventName, eventDate, location, description }` | Adds `distanceKm` and `eventType` (Run, Walk, Cycle) | Events must record distance and event type |
| 5 | Category body `{ categoryName, distanceKm, maxParticipants, entryFee }` | `distanceKm` optional; adds optional `minAge` and `maxAge` | Categories can be age based (Under 20) as well as distance based (10km) |
| 6 | Register body `{ fullName, email, password, role }` | Adds optional `phoneNumber` | The profile's phone number can be set when registering |
| 7 | `GET /api/events` lists upcoming events | Adds filters `eventType`, `search`, `from`, `to` and `includePast` | Part 3 home page filters; organisers need past events to capture results |
| 8 | No status changes | `PUT /api/enrolments/{id}/status`; cancelled enrolments free their place | Organisers manage enrolment status; statuses are colour coded in the MVC app |
| 9 | No images | Banner and profile picture endpoints backed by Azure Blob Storage | Part 3 Azure Blob Storage requirement |
| 10 | No dashboard data | `GET /api/users/me/events` returns each event with category, enrolment, capacity and result counts | Part 3 Organiser dashboard |
| 11 | Results without context | Results include `finishers`; enrolments include the captured result | "47th of 312 finishers" on My Results; results entry page |
| 12 | Sample users and events in the SQL script | No sample accounts or events | Users create their own accounts |

### Database schema and ERD

`docs/RaceDay_Database_Schema.sql`, `docs/ERD_RaceDay.png` and the EF Core migrations all describe the same schema.

| # | Change | Reason |
|---|---|---|
| 1 | `Events.DistanceKm DECIMAL(6,2)` and `Events.EventType NVARCHAR(10)` with a check for Run, Walk or Cycle | Event distance and type |
| 2 | `Categories.DistanceKm` nullable; new `Categories.MinAge` and `Categories.MaxAge` | Age categories have no distance |
| 3 | `ON DELETE CASCADE` from Events to Categories, EventEnrolments and Results | Deleting an event deletes its categories; their enrolments and results cannot exist on their own. User foreign keys stay `NO ACTION` |
| 4 | Unique rules are unique indexes with the same names, plus indexes on foreign keys | This is how EF Core code-first creates them; enforcement is the same |
| 5 | `Users.ProfileImageName` and `Events.BannerImageName` (`NVARCHAR(200) NULL`, migration `AddImageNames`) | Names of the images stored in Azure Blob Storage (Part 3) |
| 6 | Only the three roles are inserted; `__EFMigrationsHistory` rows are included | Users register themselves, and a script-created database works with the API |
| 7 | ERD redrawn with the new columns highlighted, relationship ends corrected and optionality shown | Accuracy |

---

PROG6212 Portfolio of Evidence: Parts 1, 2 and 3
