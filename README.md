# Library API

An ASP.NET Core Web API (.NET 10) for a library loan system: members browse and search books and borrow
and return copies, and admins manage the catalogue and the members. Authentication is JWT with two roles
(`Admin`, `Member`), data lives in SQL Server through EF Core (code-first), and the whole request pipeline is
asynchronous with `CancellationToken` passed from the controller down to EF Core.

> Server only. The React + TypeScript client lives in its own repository/folder (`library-client`, next to this one) and talks to this API over HTTP; see its README. The API allows the client's origin through CORS (`Cors:AllowedOrigins`).

---

## The limited resource and how the race is handled

**The limited resource** is the copies of a book: each `Book` has `TotalCopies` and `AvailableCopies`.

**Where the competition is.** Two members press "borrow" for the last copy of the same book at the same
moment. A naive implementation reads `AvailableCopies = 1` in both requests, both pass the check, both save,
and the library reports two loans for one physical copy, with no error anywhere.

**How the system reacts** (optimistic concurrency, no locks):

1. `Book` has a `RowVersion` column (SQL Server `rowversion`, configured with Fluent API in `Library.Data`).
   It changes on every update of the row.
2. `LoanService.LoanBookAsync` reads the book (tracked), calls the domain method `Book.TakeCopy()`
   (which throws `NoCopiesAvailableException` at zero) and saves the new `Loan` and the decremented book
   **in one transaction**.
3. EF Core adds the `RowVersion` value it originally read to the `WHERE` clause of the `UPDATE`. If another
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

**Proof.** `Library.Tests/Concurrency/OptimisticConcurrencyTests.cs` runs against a real (throw-away) SQL Server
database: two `DbContext` instances read the same book, both take the last copy, the first save succeeds and
the second throws `DbUpdateConcurrencyException`. Another test races two members for the last copy through
the real services and asserts that exactly one wins and exactly one loan exists.

---

## Architecture

```
Library.Core      Entities, DTOs, Enums, Interfaces, Exceptions     (no NuGet packages, no EF Core)
Library.Data      DbContext, Fluent API configs, Repositories,      -> Core
                  UnitOfWork, Migrations, Seed data
Library.Service   Business logic, AutoMapper profile,               -> Core, Data
                  PasswordHasher (PBKDF2)
Library.API       Controllers, Middleware, JWT, NLog, Program.cs    -> Core, Service
                                                                       (Data only for DI registration in Program.cs)
Library.Tests     xUnit + Moq tests                                 -> Service, Core, Data
```

Request flow: `Controller -> Service -> Repository -> DbContext`. Controllers never touch the `DbContext`.
Entities never leave the API: requests and responses are separate DTOs mapped with AutoMapper.

Middleware order: `CorrelationId -> RequestLogging -> ExceptionHandling -> Swagger -> HTTPS -> CORS -> Authentication
-> Authorization -> Controllers`.

---

## Running it locally

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- SQL Server **LocalDB** (installed with Visual Studio) or any SQL Server instance
- The EF tool: `dotnet tool install --global dotnet-ef`

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
dotnet ef database update --project Library.Data --startup-project Library.API
```

The connection string is in `Library.API/appsettings.Development.json` and defaults to LocalDB
(`Server=(localdb)\MSSQLLocalDB;Database=LibraryApiDb;...`). To use another server, override it in User Secrets:

```
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "<your connection string>" --project Library.API
```

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

23 tests (xUnit + Moq):

- `LoanServiceTests`: successful loan, loan rejected for no copies, loan rejected on a concurrency conflict,
  and the surrounding edge cases, with mocked repositories.
- `OptimisticConcurrencyTests`: the two-`DbContext` proof and the race test against a real database.
- Domain invariant and password hasher tests.

The concurrency tests create and drop a temporary LocalDB database. To use another SQL Server, set the
`LIBRARY_TEST_CONNECTION` environment variable to a connection string (the database will be created and deleted).

---

## Configuration and secrets

| Setting | Where | In the repo? |
|---|---|---|
| `Jwt:SecretKey` | User Secrets | **No** |
| `Jwt:Issuer`, `Jwt:Audience`, `Jwt:ExpiryMinutes` | `appsettings.json` | Yes (not secret) |
| `Cors:AllowedOrigins` | `appsettings.Development.json` (`http://localhost:5173`, the client dev server) | Yes (not secret) |
| `ConnectionStrings:DefaultConnection` | `appsettings.Development.json` (LocalDB, integrated security, no password) | Yes; override in User Secrets for a real server |

## Notes

- AutoMapper 16 prints a "no license key" warning at startup. It is allowed for development and does not affect behaviour.
- The password hashes in the seed data are PBKDF2-SHA256 (210,000 iterations, random salt) in the same format
  the `PasswordHasher` produces.
