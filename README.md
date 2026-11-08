# RaceDay

PROG6212 Portfolio of Evidence, Parts 1 to 3

Lebogang Sebopetji, ST10475905

RaceDay is a web system for running, walking and cycling events. Organisers create events and categories, see who entered and capture the results. Participants find events, enter a category and see their own results.

Part 3 demo video: https://youtu.be/3oluslptu9o

## Projects in the solution

- RaceDay.Api (Part 2): ASP.NET Core Web API with EF Core and SQL Server. For Part 3 I added Azure Blob Storage for images and a few endpoints the website needed.
- RaceDay.Web (Part 3): ASP.NET Core MVC website. It has no database of its own and gets all of its data from the API.
- RaceDay.Api.Tests and RaceDay.Web.Tests: xUnit tests for the API and the website.
- docs: the updated ERD and SQL script. My Part 1 files are in docs/part1-submitted.

I did Part 3 on the part3-mvc branch and merged it into main. The part2 tag marks the version I handed in for Part 2.

## How to run it

You need the .NET 8 SDK, Visual Studio 2022, SQL Server LocalDB and an Azure Storage account.

First give the API the connection string of the storage account. I keep it in user secrets so the key is not on GitHub. From the solution folder run:

dotnet user-secrets set "BlobStorage:ConnectionString" "your-connection-string" --project src/RaceDay.Api

Then in Visual Studio right-click the solution, choose Configure Startup Projects, select Multiple startup projects and set RaceDay.Api and RaceDay.Web to Start. You can also run them from two terminals:

dotnet run --project src/RaceDay.Api

dotnet run --project src/RaceDay.Web

The API runs on http://localhost:5104 (Swagger is at /swagger) and the website on http://localhost:5230. The database is created from the EF Core migrations the first time the API starts.

There are no built-in accounts. Register on the website as an Organiser or a Participant.

## Docker

The Dockerfile for the MVC website is in the root folder. To build the image and run the container:

docker build -t raceday-web .

docker run -d -p 8080:8080 -e RaceDayApi__BaseUrl=http://host.docker.internal:5104/ --name raceday-web raceday-web

Then open http://localhost:8080. The API has to be running on the computer first. The container runs the same code and settings as when I run the website locally. Only the API address is different, because inside a container localhost means the container itself.

The Dockerfile is a multi-stage build. The .NET SDK image restores and publishes the app, and the final image only has the ASP.NET runtime, so it is much smaller. The packages are restored in their own layer so Docker can cache them, and the app runs as a non-root user.

To run the API, the website and Azurite (local blob storage) together in containers, run docker compose up --build from the root folder.

## How the website works

- The controllers never touch a database. They call the API through RaceDayApiClient in the Services folder, which uses HttpClient.
- The Organiser pages (dashboard, events, categories, enrolments and results) and the Participant pages (my enrolments and my results) are in two separate areas.
- The login and register forms send the details to the API. After login I keep the user's ID, name, role and the API session cookie in the MVC session. The cookie is sent with every API call for that user, so each user only uses their own API session.
- The RequireRole filter protects the pages. If you are not logged in it sends you to the login page, and if you have the wrong role you get an access denied page. Organisers and Participants also see different menus.
- The forms are validated in the browser and on the server, and errors from the API show next to the field they belong to. If the API is not running, the website shows a message instead of crashing.
- Enrolment statuses are colour coded: green for Confirmed, amber for Pending and red for Cancelled.

## Azure Blob Storage

The blob code is in the API, in src/RaceDay.Api/Storage/BlobImageStorage.cs, and uses the Azure.Storage.Blobs package. Organisers can add a banner image to an event and every user can upload a profile picture. The images are saved in a private container called raceday-images and the blob name is stored in the database. The website gets the images through the API, so it never connects to Azure itself. Only JPG, PNG, GIF and WEBP images up to 5 MB are accepted.

## Tests and GitHub Actions

Running dotnet test runs both the API tests and the website tests. The GitHub Actions workflow runs on every push to main and part3-mvc. It builds the API, the MVC project and the tests, runs the tests and builds the Docker image.

## Changes since my Part 1 plan

- I used sessions instead of JWT, because Parts 2 and 3 ask for session management.
- I added POST /api/auth/logout to end a session.
- Events have a distance and an event type (Run, Walk or Cycle).
- Categories can be based on age (minimum and maximum age) as well as distance.
- I added GET /api/events/{eventId}/enrolments so an organiser can see all the enrolments for an event on one page.
- For Part 3 I added event filters, the organiser dashboard summary (GET /api/users/me/events), changing an enrolment's status, and uploading event banners and profile pictures.
- The new columns Events.BannerImageName and Users.ProfileImageName hold the blob names of the images.
- The SQL script no longer has sample users. People register their own accounts.

The ERD and the SQL script in docs match the EF Core migrations, and all the endpoints are documented in Swagger.
