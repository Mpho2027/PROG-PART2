# RaceDay

PROG6212 Portfolio of Evidence (Parts 1, 2 and 3). Student number: ST10475905.

RaceDay is a web system for running, walking and cycling events. Organisers create events and categories, see who entered and capture the results. Participants look for events, enter a category and see their own results.

**Part 3 demo video:** _link to be added_

## What is in the solution

- **RaceDay.Api** (Part 2): ASP.NET Core Web API with EF Core and SQL Server. In Part 3 I added Azure Blob Storage for images and a few endpoints the website needed.
- **RaceDay.Web** (Part 3): ASP.NET Core MVC website. It has no database connection of its own and gets all of its data from the API.
- **RaceDay.Api.Tests** and **RaceDay.Web.Tests**: xUnit tests.
- **docs**: the updated ERD and SQL script. My original Part 1 files are in `docs/part1-submitted`.

The Part 3 work was done on the `part3-mvc` branch. The tag `part2` marks the version I handed in for Part 2.

## Running the project

You need the .NET 8 SDK, Visual Studio 2022, SQL Server LocalDB and an Azure Storage account.

1. Give the API the connection string of the Azure Storage account. I used user secrets so the key is not on GitHub:

   ```
   dotnet user-secrets set "BlobStorage:ConnectionString" "<connection string>" --project src/RaceDay.Api
   ```

2. In Visual Studio, set **RaceDay.Api** and **RaceDay.Web** as startup projects (right-click the solution, Configure Startup Projects, Multiple startup projects) and run. From a terminal:

   ```
   dotnet run --project src/RaceDay.Api
   dotnet run --project src/RaceDay.Web
   ```

The API runs on http://localhost:5104 (Swagger at `/swagger`) and the website on http://localhost:5230. The database is created by the EF Core migrations the first time the API starts.

There are no built-in accounts. Register on the website as an Organiser or a Participant.

## Docker

The MVC website runs in a Docker container. Build the image and start it:

```
docker build -t raceday-web .
docker run -d -p 8080:8080 -e RaceDayApi__BaseUrl=http://host.docker.internal:5104/ --name raceday-web raceday-web
```

Then open http://localhost:8080. The API has to be running on the computer first.

The container runs the same code and settings as when I run the website locally. The only difference is the API address, because inside the container `localhost` means the container itself.

The Dockerfile uses a multi-stage build. The .NET SDK image builds and publishes the app, and the final image only has the smaller ASP.NET runtime. Packages are restored in their own layer so they are cached, and the app runs as a non-root user.

To run the API, the website and Azurite (local blob storage) together in containers: `docker compose up --build`

## How the MVC website works

- The controllers get and save everything through `RaceDayApiClient` in the `Services` folder, which calls the API with `HttpClient`.
- The Organiser pages (dashboard, events, categories, enrolments, results) and the Participant pages (my enrolments, my results) are in separate areas.
- **Login and sessions:** the login and register forms send the details to the API. After login I keep the user's ID, name, role and the API's session cookie in the MVC session. That cookie is sent with every API call for that user, so each user only ever uses their own API session.
- **Roles:** the `RequireRole` filter protects the pages. If you are not logged in it sends you to the login page, and if you have the wrong role it shows an access denied page. Organisers and Participants also get different menus.
- **Validation:** the forms validate in the browser and on the server, and errors from the API are shown next to the right field. If the API is not running, the website shows a friendly message instead of an error.
- **Statuses:** enrolment statuses are colour coded: green for Confirmed, amber for Pending and red for Cancelled.

## Azure Blob Storage

The blob storage code is in the API (`src/RaceDay.Api/Storage/BlobImageStorage.cs`) and uses the `Azure.Storage.Blobs` package. Organisers can upload a banner image when they create or edit an event, and users can upload a profile picture. The images go into a private container called `raceday-images`, and the blob name is saved in the database. The website shows the images by asking the API for them, so the website never connects to Azure itself. Only JPG, PNG, GIF and WEBP images up to 5 MB are accepted.

## Tests and GitHub Actions

`dotnet test` runs the API tests and the MVC tests. The GitHub Actions workflow builds the API, the MVC project and the tests, runs all the tests and builds the Docker image.

## Changes from my Part 1 plan

- I used **sessions instead of JWT**, because Parts 2 and 3 ask for session management.
- Added `POST /api/auth/logout` to end a session.
- Events have a distance and an event type (Run, Walk or Cycle).
- Categories can be age based (minimum and maximum age) as well as distance based.
- Added `GET /api/events/{eventId}/enrolments` so an organiser can see all enrolments for an event on one page.
- For Part 3 I added event filters, an organiser dashboard summary (`GET /api/users/me/events`), changing an enrolment's status, and uploading event banners and profile pictures.
- New columns `Events.BannerImageName` and `Users.ProfileImageName` store the blob names of the images.
- The SQL script no longer has sample users. Users register their own accounts.

The ERD and the SQL script in `docs` match the EF Core migrations. All the endpoints are documented in Swagger.
