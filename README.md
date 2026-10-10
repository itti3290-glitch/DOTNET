# Library API

An ASP.NET Core Web API (.NET 10) for a library loan system: members browse and search books and borrow
and return copies, and admins manage the catalogue and the members. Authentication is JWT with two roles
(`Admin`, `Member`), data lives in PostgreSQL through EF Core (code-first), and the whole request pipeline is
asynchronous with `CancellationToken` passed from the controller down to EF Core.

> Server only. The React + TypeScript client lives in its own repository/folder (`library-client`, next to this one) and talks to this API over HTTP; see its README. The API allows the client's origin through CORS (`Cors:AllowedOrigins`).

---

## The limited resource and how the race is handled

**The limited resource** is the copies of a book: each `Book` has `TotalCopies` and `AvailableCopies`.

**Where the competition is.** Two members press "borrow" for the last copy of the same book at the same
moment. A naive implementation reads `AvailableCopies = 1` in both requests, both pass the check, both save,
and the library reports two loans for one physical copy, with no error anywhere.

**How the system reacts** (optimistic concurrency, no locks):

1. `Book` is versioned by PostgreSQL's `xmin` system column - the id of the transaction that last wrote
   the row - mapped as a concurrency token with Fluent API in `Library.Data`. Postgres advances it on
   every update of the row, so no column of our own and no trigger is needed.
2. `LoanService.LoanBookAsync` reads the book (tracked), calls the domain method `Book.TakeCopy()`
   (which throws `NoCopiesAvailableException` at zero) and saves the new `Loan` and the decremented book
   **in one transaction**.
3. EF Core adds the `xmin` value it originally read to the `WHERE` clause of the `UPDATE`. If another
   request changed the row in between, no row is updated and EF throws `DbUpdateConcurrencyException`.
4. `UnitOfWork.SaveChangesAsync` (data layer) catches exactly that exception and rethrows the domain
   exception `ConcurrencyConflictException`, so the service and API layers never depend on EF Core.
5. `ExceptionHandlingMiddleware` maps it to **HTTP 409 Conflict** with a clear message, and logs it at
   **Warning** level with the request's CorrelationId. Out-of-stock (`NoCopiesAvailableException`) is also 409.

Extra protection layers:

- **CHECK constraints** in the database: `AvailableCopies >= 0` and `AvailableCopies <= TotalCopies`.
- A **filtered unique index** allows only one open loan per (book, member). If two identical requests race
  past the "already borrowed?" check, the index rejects the second, and `UnitOfWork` turns that into a 409
  too instead of a 500.

**Proof.** `Library.Tests/Concurrency/OptimisticConcurrencyTests.cs` runs against a real (throw-away) PostgreSQL
database: two `DbContext` instances read the same book, both take the last copy, the first save succeeds and
the second throws `DbUpdateConcurrencyException`. Another test races two members for the last copy through
the real services and asserts that exactly one wins and exactly one loan exists.

---

## Architecture

```
Library.Core      Entities, DTOs, Enums, Interfaces, Exceptions     (no NuGet packages, no EF Core)
Library.Data      DbContext, Fluent API configs, Repositories,      -> Core
                  UnitOfWork, Migrations, Seed data,
                  connection-string normalisation
Library.Service   Business logic, AutoMapper profile,               -> Core, Data
                  PasswordHasher (PBKDF2)
Library.API       Controllers, Middleware, JWT, NLog, Program.cs    -> Core, Service
                                                                       (Data only for DI registration in Program.cs)
Library.Tests     xUnit + Moq tests                                 -> Service, Core, Data
```

Request flow: `Controller -> Service -> Repository -> DbContext`. Controllers never touch the `DbContext`.
Entities never leave the API: requests and responses are separate DTOs mapped with AutoMapper.

Middleware order: `ForwardedHeaders -> CorrelationId -> RequestLogging -> ExceptionHandling -> Swagger
-> HTTPS -> CORS -> Authentication -> Authorization -> Controllers`. The HTTPS redirect is skipped when the
app is running behind a TLS-terminating proxy; see *Deploying to Render*.

---

## Running it locally

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- **PostgreSQL** 14 or newer, running locally or reachable over the network
- The EF tool, pinned in `.config/dotnet-tools.json`: run `dotnet tool restore` once, then use
  `dotnet dotnet-ef` (no global install needed)

### Setup (from the solution root, the folder containing `Library.sln`)

**1. Set the JWT signing key in User Secrets.** It is deliberately *not* in the repo, and the API (and
`dotnet ef`) will refuse to start without it. Use any random string of at least 32 characters:

```powershell
# PowerShell (5.1 or 7): generate a random key and store it
$bytes = New-Object byte[] 48
[Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes)
dotnet user-secrets set "Jwt:SecretKey" ([Convert]::ToBase64String($bytes)) --project Library.API
```

**2. Create the database** and apply the migrations (schema plus demo seed data):

```
dotnet tool restore
dotnet dotnet-ef database update --project Library.Data --startup-project Library.API
```

The API also applies pending migrations on startup, so this step is optional locally and is what
brings the schema up on a deployed instance. Set `Database:MigrateOnStartup` to `false` to turn
that off and apply migrations out of band instead.

The connection string is in `Library.API/appsettings.Development.json` and defaults to
`Host=localhost;Port=5432;Database=LibraryApiDb;Username=postgres;Password=postgres`. To use
another server, override it in User Secrets:

```
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "<your connection string>" --project Library.API
```

Either form is accepted: an Npgsql keyword string as above, or a `postgres://user:pass@host/db`
URI of the kind managed hosts hand out - `PostgresConnectionString.Normalize` converts the latter.

**3. Run the API:**

```
dotnet run --project Library.API --launch-profile http
```

**4. Open Swagger UI:** <http://localhost:5188/swagger>
(HTTPS profile: <https://localhost:7058/swagger>, `--launch-profile https`.)

### Using Swagger

1. `POST /api/auth/login` with a demo user (below) and copy the `token` from the response.
2. Click **Authorize** (top right), paste the token **without** the word `Bearer`, and confirm.
3. Try the endpoints. Every error has the same JSON shape (`status`, `error`, `message`, `correlationId`).

---

## Demo users

Created by the seed data so the system can be used right after `database update`. **Development only:**
these passwords are public in this repository and must be changed or removed before any real deployment.

| Role | Email | Password | Can do |
|---|---|---|---|
| `Admin` | `admin@library.local` | `Admin123!` | Everything: manage books and members, borrow/return for anyone, see all loans |
| `Member` | `dana@library.local` | `Member123!` | Browse and search books, borrow/return **for herself**, see her own loans and profile |

`POST /api/auth/register` creates more `Member` accounts. Admins cannot be created through the API.

Seed data also contains 3 books (*Clean Code* x3 copies, *The Pragmatic Programmer* x2, *Dune* x1) and
3 categories, so *Dune*, with a single copy, is convenient for demonstrating the 409.

### Suggested demo of the concurrency behaviour

1. Log in as Dana, and `POST /api/loans` with `{ "bookId": 3, "memberId": 2 }`. She gets the only copy of *Dune* (201).
2. Log in as the admin and borrow the same book for member 1: **409**, "No copies ... available".
3. `POST /api/loans/{id}/return` with Dana's loan id (no body): the copy is back, and the admin can borrow it now.

A true simultaneous race needs two requests at the same instant; that is what the automated test does.

---

## API overview

| Endpoint | Access | Notes |
|---|---|---|
| `POST /api/auth/login`, `POST /api/auth/register` | Anonymous | 200 / 201 |
| `GET /api/books` | Any logged-in user | Search, filter, sort, paging (below) |
| `GET /api/books/{id}` | Any logged-in user | |
| `POST`, `PUT`, `DELETE /api/books...` | Admin | 201 + `Location` / 204 / 204 |
| `GET /api/members` | Admin | Paged |
| `GET /api/members/{id}` | Admin or that member | |
| `PUT`, `DELETE /api/members/{id}` | Admin | 204 |
| `POST /api/loans` | Logged in (members only for themselves) | Creates a loan (borrow): 201 + `Location`, 409 if no copy or a conflict |
| `POST /api/loans/{id}/return` | Logged in (members only their own loans) | Returns the copy, no body: 200 |
| `GET /api/loans/active`, `GET /api/loans/{id}` | Logged in (members see only their own) | Paged |

`GET /api/books` query parameters (all optional): `search` (title, author or ISBN), `author`, `categoryId`,
`availableOnly`, `sortBy` (`Title`, `Author`, `Availability`), `desc`, `page`, `pageSize` (max 100).
Filtering, sorting and paging all run inside the SQL query.

Status codes used: 200, 201 with `Location`, 204, 400, 401, 403, 404, 409, 500.

A detailed description of every endpoint is in [`docs/Swagger-Endpoints.md`](docs/Swagger-Endpoints.md) (Hebrew).

---

## Validation

- **Input shape** (required, length, range, e-mail format) uses Data Annotations on the request DTOs; failures
  return 400 with a per-field `errors` map.
- **Business rules** that depend on data (duplicate ISBN or e-mail, stock, ownership, deleting records that
  have loan history, lowering `TotalCopies` below the copies currently on loan) live in the Service layer.

---

## Logging (NLog)

`Library.API/nlog.config`, with `NLog.Web.AspNetCore`; application code uses the standard `ILogger`.

- Every request gets a unique **CorrelationId** (also returned in the `X-Correlation-Id` response header and in
  the error JSON) that appears on every log line of that request.
- One line when a request starts and one when it finishes (method, path, status, duration).
- Unhandled exceptions: **Error**. Resource conflicts (409): **Warning**.
- Passwords, tokens, headers, query strings and request bodies are never logged.

Log files are written to a `logs/` folder next to the running binaries (`Library.API/bin/.../logs`) and are git-ignored.

---

## Tests

```
dotnet test
```

33 tests (xUnit + Moq):

- `LoanServiceTests`: successful loan, loan rejected for no copies, loan rejected on a concurrency conflict,
  and the surrounding edge cases, with mocked repositories.
- `OptimisticConcurrencyTests`: the two-`DbContext` proof and the race test against a real database.
- `PostgresConnectionStringTests`: the `postgres://` URI to Npgsql keyword translation the deployed
  API depends on.
- Domain invariant and password hasher tests.

The concurrency tests create and drop a temporary PostgreSQL database, defaulting to
`localhost:5432`. Point `LIBRARY_TEST_CONNECTION` at another server to use that instead (the
database is created and deleted). With no server reachable those four tests report as **skipped**,
not passed, so a green run without a database never looks like the concurrency proof succeeded.

---

## Configuration and secrets

| Setting | Where | In the repo? |
|---|---|---|
| `Jwt:SecretKey` | User Secrets | **No** |
| `Jwt:Issuer`, `Jwt:Audience`, `Jwt:ExpiryMinutes` | `appsettings.json` | Yes (not secret) |
| `Cors:AllowedOrigins` | `appsettings.Development.json` (`http://localhost:5173`, the client dev server) | Yes (not secret) |
| `ConnectionStrings:DefaultConnection` | `appsettings.Development.json` (local PostgreSQL) | Yes, development only; override in User Secrets or `ConnectionStrings__DefaultConnection` |
| `Database:MigrateOnStartup` | defaults to `true` | Yes (not secret) |
| `Swagger:Enabled` | defaults to `true` in Development only | Yes (not secret) |

In a deployed environment every one of these can be supplied as an environment variable, with `__`
in place of `:` - for example `ConnectionStrings__DefaultConnection` and `Jwt__SecretKey`.

---

## Deploying to Render

`render.yaml` is a Blueprint describing the whole thing: one Docker web service plus the managed
PostgreSQL it talks to. In the Render dashboard choose **New > Blueprint** and point it at this
repository; Render reads the file and creates both.

What it wires up for you:

- `ConnectionStrings__DefaultConnection` comes from the database Render provisions. Render supplies
  it as a `postgres://` URI, which Npgsql does not accept, so `PostgresConnectionString.Normalize`
  translates it into keyword form (and requires TLS for any non-loopback host).
- `Jwt__SecretKey` is generated once by Render and kept out of the repository. Rotating it
  invalidates every token already issued.
- The schema is created on first boot, because the API applies pending migrations on startup.
- `/health` is the health check endpoint, unauthenticated and outside `/api`.
- `Swagger__Enabled` is `true` so the deployed demo stays browsable. Set it to `false` to close it.

Two things need your attention after the first deploy:

- **CORS.** `Cors__AllowedOrigins__0` is deliberately left unset, since the client's origin is not
  known until it is deployed. Set it in the dashboard to the origin the browser client is served
  from, or browser calls are refused.
- **Demo seed data.** The migration seeds two accounts with known passwords (see *Demo users*).
  They exist to make the demo usable; remove them before anything real.

How the container differs from a local run: Render assigns the port and injects it as `PORT`,
which `Program.cs` binds to. It also terminates TLS at its edge and forwards plain HTTP inwards,
so the app honours `X-Forwarded-For`/`X-Forwarded-Proto` and skips the HTTPS redirect it would
apply locally - redirecting there would break the platform's health checks.

Note that Render's free PostgreSQL plan expires after 30 days, and free web services sleep when
idle, so the first request after a pause is slow.

## Notes

- AutoMapper 16 prints a "no license key" warning at startup. It is allowed for development and does not affect behaviour.
- The password hashes in the seed data are PBKDF2-SHA256 (210,000 iterations, random salt) in the same format
  the `PasswordHasher` produces.
