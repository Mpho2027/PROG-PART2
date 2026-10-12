# RaceDay System – API Endpoint Plan

This plan covers Authentication, User Profile, Events, Categories, Event Enrolments, and Results, as required, plus supporting endpoints identified as necessary for a complete system. It matches `ERD_RaceDay.png` and `RaceDay_Database_Schema.sql` exactly.

## Authentication

| HTTP Method | Route | Description | Role Required | Request Body | Expected Response |
|---|---|---|---|---|---|
| POST | /api/auth/register | Creates a new user account as an Organiser or Participant. | None (public) | `{ fullName, email, password, role }` | 201 Created – returns new user (without password) <br> 400 Bad Request – validation failed <br> 409 Conflict – email already registered |
| POST | /api/auth/login | Authenticates a user and issues a JWT access token. | None (public) | `{ email, password }` | 200 OK – returns `{ token, user }` <br> 401 Unauthorized – invalid credentials |

## User Profile

| HTTP Method | Route | Description | Role Required | Request Body | Expected Response |
|---|---|---|---|---|---|
| GET | /api/users/me | Returns the profile of the currently logged-in user. | Any (logged in) | None | 200 OK – returns user profile <br> 401 Unauthorized |
| PUT | /api/users/me | Updates the logged-in user's own profile details. | Any (logged in) | `{ fullName, phoneNumber }` | 200 OK – returns updated profile <br> 400 Bad Request |
| GET | /api/users/{id} | Retrieves a specific user's public profile. | Admin | None | 200 OK – returns user <br> 404 Not Found |

## Events

| HTTP Method | Route | Description | Role Required | Request Body | Expected Response |
|---|---|---|---|---|---|
| POST | /api/events | Creates a new event owned by the logged-in Organiser. | Organiser | `{ eventName, eventDate, location, description }` | 201 Created – returns new event <br> 400 Bad Request |
| GET | /api/events | Lists all upcoming events. | None (public) | None | 200 OK – returns array of events |
| GET | /api/events/{id} | Retrieves the details of a single event. | None (public) | None | 200 OK – returns event <br> 404 Not Found |
| PUT | /api/events/{id} | Updates an event's details. | Organiser (owner only) | `{ eventName, eventDate, location, description }` | 200 OK – returns updated event <br> 403 Forbidden – not the owner <br> 404 Not Found |
| DELETE | /api/events/{id} | Deletes an event and its categories. | Organiser (owner only) | None | 204 No Content <br> 403 Forbidden <br> 404 Not Found |

## Categories

| HTTP Method | Route | Description | Role Required | Request Body | Expected Response |
|---|---|---|---|---|---|
| POST | /api/events/{eventId}/categories | Adds a race category (e.g. distance) to an event. | Organiser (owner only) | `{ categoryName, distanceKm, maxParticipants, entryFee }` | 201 Created – returns new category <br> 403 Forbidden <br> 404 Not Found – event does not exist |
| GET | /api/events/{eventId}/categories | Lists all categories for a specific event. | None (public) | None | 200 OK – returns array of categories <br> 404 Not Found |
| PUT | /api/categories/{id} | Updates a category's details. | Organiser (owner only) | `{ categoryName, distanceKm, maxParticipants, entryFee }` | 200 OK – returns updated category <br> 403 Forbidden <br> 404 Not Found |
| DELETE | /api/categories/{id} | Removes a category from an event. | Organiser (owner only) | None | 204 No Content <br> 403 Forbidden <br> 404 Not Found |

## Event Enrolments

| HTTP Method | Route | Description | Role Required | Request Body | Expected Response |
|---|---|---|---|---|---|
| POST | /api/categories/{categoryId}/enrol | Enrols the logged-in Participant into a category. | Participant | None (uses logged-in user) | 201 Created – returns enrolment with bib number <br> 404 Not Found – category does not exist <br> 409 Conflict – already enrolled or category full |
| GET | /api/users/me/enrolments | Lists all enrolments belonging to the logged-in Participant. | Participant | None | 200 OK – returns array of enrolments |
| GET | /api/categories/{categoryId}/enrolments | Lists all participants enrolled in a category. | Organiser (owner only) | None | 200 OK – returns array of enrolments <br> 403 Forbidden |
| DELETE | /api/enrolments/{id} | Cancels an enrolment (withdrawal). | Participant (owner) or Admin | None | 204 No Content <br> 403 Forbidden <br> 404 Not Found |

## Results

| HTTP Method | Route | Description | Role Required | Request Body | Expected Response |
|---|---|---|---|---|---|
| POST | /api/enrolments/{id}/result | Captures or updates the finishing result for an enrolment. | Organiser (owner only) | `{ finishTime, position }` | 201 Created / 200 OK – returns result <br> 403 Forbidden <br> 404 Not Found – enrolment does not exist |
| GET | /api/categories/{categoryId}/results | Returns the leaderboard for a category, ordered by position. | None (public) | None | 200 OK – returns ordered array of results <br> 404 Not Found |
| GET | /api/users/me/results | Returns the logged-in Participant's own results across all events. | Participant | None | 200 OK – returns array of results |

---

**Note:** "Organiser (owner only)" means the endpoint checks that the logged-in Organiser's `UserID` matches the `OrganiserID` on the related Event before allowing the action; otherwise it returns 403 Forbidden.
