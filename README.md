# Diwali Crackers POS

React + TypeScript counter application backed by an ASP.NET Core 10 REST API, Entity Framework Core, and independently hosted PostgreSQL. Includes product/catalog administration, box quantities, percentage discounts, stock adjustments, receipts, bill history, and Admin/Cashier access.

**Verification status:** the frontend production build and three fixture-based DOM tests pass in the authoring environment. The backend, migration and PostgreSQL acceptance suite must run successfully in your environment before deployment. This environment did not contain .NET, Docker, PostgreSQL or a browser binary. No public deployment or GitHub remote has been created. This is a source delivery, not a claim that a live production system has passed acceptance testing.

## Architecture

```text
React / TanStack Query / React Hook Form / Tailwind
                  ↓ HTTPS / same-origin /api proxy
ASP.NET Core controllers → Application services
                  ↓ IPosDb
EF Core / Infrastructure → PostgreSQL
```

- `frontend/`: React SPA, Vite, TypeScript, Tailwind, React Query, React Hook Form, Lucide icons and PDF download.
- `backend/API/`: thin REST controllers, cookie authentication, authorization, rate limits, CORS and error handling.
- `backend/Application/`: validation, catalog, user, dashboard and transactional billing services.
- `backend/Domain/`: entities and historical sale snapshots.
- `backend/Infrastructure/`: EF Core PostgreSQL context and initial migration.
- `database/`: initial schema reference. Apply migrations, not this SQL file, for normal setup.
- `tests/acceptance.py`: real HTTP/database acceptance tests, including concurrent last-box sales.
- `.github/workflows/ci.yml`: frontend build, .NET build and PostgreSQL acceptance checks.

No SQL Server, browser-local billing database, fake API fallback or production sample credentials are included. A disconnected server produces an error state.

## Implemented flows

- Unique, case-normalized serial numbers; serial/name partial search; product/category activation, price editing and opening stock.
- Admin-only stock adjustments with signed quantity and mandatory reason; inventory audit records include before/after stock and sale reference.
- Cart with price per box, editable quantity, remove and clear; percentage discount and immediate totals.
- Server-authoritative decimal calculations and two-decimal rounding away from zero.
- Bill and items, stock deduction and inventory records saved in one transaction.
- Cashiers see their own bills; Admin sees all bills. Product/stock/price/user/settings changes require Admin at the API, not just the UI.
- History filters: bill number, product/serial, IST calendar date and exact final amount; paginated lists, receipt view, reprint and PDF download.
- Admin dashboard: IST today's sales, bills, boxes, discounts, active product count, low-stock count/list and seven-day sales bars.
- Shop details are snapshotted into bills so receipt identity is preserved as well as selling prices.
- 80mm thermal receipt CSS. Use a matching paper size and disable browser headers/footers when printing. Adapt `@page` and `.receipt` width for 58mm printers; physically test your printer before opening the counter.
- `Enter`: exact serial or a single matching product; `Tab`: navigate; `+`/`-` and `Delete`: selected row when focus is not inside an input; `Ctrl+Enter`: complete bill. New bill and adding a product focus the search field.
- PDF uses INR labels and built-in Latin fonts. If you need Telugu or another script on PDF receipts, embed a licensed matching Unicode font in `Receipt.tsx` before production use; browser printing uses the browser's installed fonts.

## Billing safety

**Historical pricing:** `BillItem.PriceAtSale`, `SerialNumber`, `ProductName`, `Quantity` and `Total` are persisted at checkout. Reprints never join to current prices.

**Concurrency:** billing takes PostgreSQL `FOR UPDATE` locks on products in deterministic ID order, checks current prices and stock, then commits all changes together. Inventory adjustments lock the same rows. Admin product edits use PostgreSQL `xmin` optimistic concurrency to reject stale edits. Stock cannot be changed through product editing after creation; use the audited adjustment endpoint.

**Idempotency:** each checkout carries a UUID `submissionId`. A transaction-level PostgreSQL advisory lock serializes attempts for that user/key. A unique `(CreatedBy, SubmissionId)` index is a second safeguard. Identical retries return the original bill; changing the payload with the same key returns 409. If a response is lost, the UI keeps the original payload and offers a safe retry. Do not close or navigate away from an uncertain submission; resolve it through Retry or inspect history first.

**Bill numbers:** an atomic PostgreSQL sequence produces `INV-2026-000001`. The displayed year uses IST; sequence values continue across years rather than resetting. Rollbacks may leave gaps. Gaps are deliberate and numbers remain unique.

**Price changes while a cart is open:** a stale expected price returns 409. The cashier refreshes prices and stock, reviews the updated cart, and resubmits. The browser cannot dictate the price charged.

## Requirements

- Git
- Node.js 22 LTS (or supported newer LTS)
- .NET SDK 10.x, for native development
- PostgreSQL 17+ (separate host supported)
- Docker Engine / Docker Desktop and Compose v2 for the easiest full-stack setup
- Python 3.11+ for the acceptance suite

## Quick start with Docker

```bash
git clone YOUR_GITHUB_REPOSITORY
cd diwali-crackers-pos
cp .env.example .env
```

Edit `.env`: replace both placeholder passwords with separate, strong values. The admin password must be 12–128 characters. Choose a username. Do not commit `.env`.

```bash
docker compose up -d db
docker compose build api frontend
# Explicit, reviewed first-time schema application. Nothing migrates on normal startup.
docker compose run --rm api --migrate
# One-time administrator bootstrap, using configuration from .env.
docker compose run --rm api --seed-admin
# Optional development catalog, only permitted with Development environment.
docker compose run --rm api --seed-demo
docker compose up -d api frontend
```

Open **http://localhost:8080** and sign in with the values you supplied. Update Shop settings, create categories/products, then add a Cashier through Users.

```bash
docker compose logs -f api
docker compose down  # Retains database volume.
```

`docker compose down -v` deletes the database volume. Do not use it for real shop data.

The provided Compose file is **local development only**: API runs in Development so cookies work on HTTP localhost, frontend/API bind to loopback, and PostgreSQL is not exposed to the host. Use the production instructions below rather than copying this configuration into a public server.

## Native local setup

1. Install the prerequisites and create a PostgreSQL database/user with permission to apply this application's schema. For example, use `createuser --pwprompt diwali` and `createdb --owner=diwali diwali_pos` with your PostgreSQL administrative account.
2. Export environment variables. `.env` is **not automatically loaded by .NET**. Use shell exports, an IDE launch environment, or .NET user secrets. Do not commit secrets in `launchSettings.json`.

Bash example:

```bash
export ConnectionStrings__DefaultConnection='Host=localhost;Port=5432;Database=diwali_pos;Username=diwali;Password=YOUR_PASSWORD'
export ASPNETCORE_ENVIRONMENT=Development
export ASPNETCORE_URLS=http://localhost:5080
export Cors__AllowedOrigins__0=http://localhost:5173
export SeedAdmin__Username=admin
export SeedAdmin__Name='Shop Administrator'
read -rs -p 'Initial admin password: ' SeedAdmin__Password
export SeedAdmin__Password
```

PowerShell equivalent uses `$env:ConnectionStrings__DefaultConnection = '...'`, `$env:ASPNETCORE_ENVIRONMENT = 'Development'`, and the other exact names above. Prefer your terminal's secure prompt or secret manager for the password.

```bash
dotnet tool restore
dotnet restore backend/API/API.csproj
dotnet build backend/API/API.csproj
# Use this OR the explicit --migrate CLI, not both for the first migration.
dotnet ef database update --project backend/Infrastructure --startup-project backend/API
dotnet run --project backend/API -- --seed-admin
dotnet run --project backend/API -- --seed-demo  # optional
dotnet run --project backend/API
```

In another terminal:

```bash
cd frontend
npm ci
cp .env.example .env
npm run dev
```

Open **http://localhost:5173**. Vite proxies `/api` to `http://localhost:5080`; cookies remain same-origin. All screens require the real API and PostgreSQL.

## Environment reference

| Variable | Purpose |
| --- | --- |
| `ConnectionStrings__DefaultConnection` | Npgsql key/value connection string; host/database/user/password/SSL policy belong only on the API host. |
| `ASPNETCORE_ENVIRONMENT` | `Development` locally; **`Production`** on the public host. |
| `PORT` | API listens on `0.0.0.0:$PORT`; frontend nginx also honors its hosting platform's `PORT`. Defaults to container port 8080. |
| `Cors__AllowedOrigins__0`, `__1`, … | Exact allowed frontend origins, no trailing slash or wildcard. HTTPS required in Production. |
| `DataProtection__KeyPath` | Optional persistent protected path for ASP.NET cookie encryption keys. All API replicas must share the same protected key ring. Without persistent keys, container replacement logs sessions out. |
| `SeedAdmin__Username`, `SeedAdmin__Name`, `SeedAdmin__Password` | One-time bootstrap command configuration; remove password from hosting environment after successful bootstrap. Existing users are never silently reset. |
| `VITE_API_URL` | Frontend **build-time** API prefix; recommended `/api` with nginx proxy. Public configuration, never secrets. |
| `API_UPSTREAM` | Frontend nginx runtime API origin, e.g. `https://diwali-api.onrender.com`, no trailing slash and no `/api` suffix. |

This implementation uses **HttpOnly secure cookie authentication**, not JWT. `JWT_SECRET` is therefore not needed. Passwords are salted/hashed with ASP.NET Identity's password hasher and a configured work factor. Cookies expire after eight hours; changing an account/password/role revokes its existing sessions. Logout clears the current browser session.

## Deploy to GitHub and Docker-capable hosting

### 1. Push the repository

A local Git repository is included in the working folder; the downloadable ZIP contains clean source without `.git`. Create your GitHub repository, then:

```bash
git init
git add .
git commit -m "Initial Diwali POS application"
git branch -M main
git remote add origin YOUR_GITHUB_REPOSITORY
git push -u origin main
```

Never stage `.env`, credentials, database dumps or the data-protection key ring. CI must pass before deploying.

### 2. Provision PostgreSQL independently

Create PostgreSQL on your selected provider and obtain its host, port, database, username and password. Convert provider URLs to the Npgsql format:

```text
Host=DB_HOST;Port=5432;Database=DB_NAME;Username=DB_USER;Password=DB_PASSWORD;SSL Mode=VerifyFull
```

Use the provider's documented CA/TLS configuration. Do not disable certificate verification as a default. For values containing semicolons or quotes, quote connection-string values according to Npgsql rules rather than pasting an unescaped password.

**Render free PostgreSQL expires 30 days after creation. It is for initial testing, not permanent production storage.** Render's free database also lacks its paid recovery/backup features; free web services can sleep. Use persistent paid storage or migrate to a suitable PostgreSQL provider before that deadline. Check current terms before deploying: [Render free services](https://render.com/docs/free), [PostgreSQL](https://render.com/docs/postgresql), [backups](https://render.com/docs/postgresql-backups).

Changing providers only requires moving the data and updating the connection string. Stop writes during final cutover; use `pg_dump`/`pg_restore` with provider-compatible clients, verify row counts and sequence state, run the acceptance tests on a staging copy, then resume. Preserve the bill sequence so bill numbers cannot repeat. Set and test a backup/restore schedule before using real sales data.

### 3. Deploy the API

Create a Docker Web Service connected to the repository, with **repository root as build context** and `backend/Dockerfile` as Dockerfile. Configure:

```text
ConnectionStrings__DefaultConnection=YOUR_POSTGRES_NPGSQL_CONNECTION_STRING
ASPNETCORE_ENVIRONMENT=Production
Cors__AllowedOrigins__0=https://YOUR_FRONTEND_DOMAIN
```

The host supplies `PORT`. Expose only HTTPS at the public edge and configure its HTTP-to-HTTPS redirect. The container intentionally listens on the platform's internal HTTP port behind that TLS edge. `/health` checks database connectivity without exposing credentials. Do not serve the production API directly over public HTTP.

The runtime container is non-root and contains the compiled .NET application, not the SDK. A persistent key ring is recommended for uninterrupted sessions across replacements; otherwise replacing the container invalidates existing cookies. For multiple replicas, configure one shared protected key ring (or a supported cloud-backed DataProtection store) first.

### 4. Apply reviewed migrations explicitly

Normal startup never runs migrations or seeds automatically. Back up the database and review migration SQL first:

```bash
dotnet tool restore
dotnet ef migrations script --idempotent --project backend/Infrastructure --startup-project backend/API --output reviewed-migration.sql
# With the production connection string in a protected shell:
dotnet ef database update --project backend/Infrastructure --startup-project backend/API
```

Alternatively run the built API image as a one-off job using the same protected environment, with command `--migrate` appended to its entrypoint, or execute `dotnet API.dll --migrate` through a supported hosting shell. If a free host does not provide one-off jobs/shell access, run the EF command from your trusted workstation against its external database endpoint. Never expose a public migration endpoint.

The initial migration's destructive Down operation is blocked intentionally. `InitialModel.cs` is frozen v1 metadata; do not edit it after applying the initial migration. For subsequent changes, edit the current domain/context and run `dotnet ef migrations add NAME --project backend/Infrastructure --startup-project backend/API`, review the generated operations/snapshot, and test on staging.

### 5. Create the first administrator

Temporarily configure `SeedAdmin__Username`, `SeedAdmin__Name`, and a unique strong `SeedAdmin__Password` on the one-off command environment. Run `dotnet API.dll --seed-admin` (or the image with `--seed-admin`). Remove the bootstrap password afterward. Do not use demo data or a development password in Production. Add normal cashier accounts through the Users page.

### 6. Deploy the frontend

Recommended: create a second Docker Web Service with repository-root build context and `frontend/Dockerfile`:

```text
VITE_API_URL=/api                 # Docker build argument, defaults to /api
API_UPSTREAM=https://YOUR_API_HOST  # frontend runtime variable
```

The included nginx proxy sends `/api/*` to the API and serves the SPA on the same public origin. This avoids third-party cookie blocking and keeps PDF/scripts/fonts self-hosted. Deploy behind HTTPS.

A static frontend is also possible if your host can proxy `/api` to the API. If serving the API on another subdomain instead, use same-site custom domains, set `VITE_API_URL=https://api.YOUR_DOMAIN/api` at build time, and allow the exact frontend origin in CORS. **Do not put the frontend and API on unrelated sites with this SameSite=Lax cookie configuration.** Prefer the included same-origin proxy; switching to cross-site cookies requires additional browser/security testing.

### 7. Confirm CORS and security

Set only your actual HTTPS frontend origins in `Cors__AllowedOrigins__N`. There is no `AllowAnyOrigin`. Mutating endpoints require `X-POS-Request: 1`; browser origins are checked against the allowlist. Cookies are HttpOnly, Secure in Production, and SameSite=Lax. The frontend proxy has CSP, clickjacking and MIME-sniffing protections. Database commands are EF queries or parameterized SQL.

The API applies a global per-user/IP limiter and a stricter login limiter. With a reverse proxy, anonymous IP limiting may group clients by the proxy IP; configure trusted forwarded-header handling for your specific host if you need accurate client-IP quotas. Do not blindly trust arbitrary forwarded headers. Never log passwords, raw credentials or request bodies containing secrets.

### 8. Acceptance and shop rehearsal

Run the automated suite on a **dedicated disposable test database**, never your real shop database. It creates test products, users and sales.

```bash
# API running at localhost:5080, seeded with a test administrator:
export SeedAdmin__Username=YOUR_TEST_ADMIN
export SeedAdmin__Password=YOUR_TEST_PASSWORD
python tests/acceptance.py
```

Override `TEST_API_URL` if needed. The test suite covers roles/ownership, serial lookup, 990 subtotal / 10% discount / 891 final amount, stock deduction, original prices, same-key replay/conflict, rollback, simultaneous last-box sales, concurrent duplicate submissions, validation, stock adjustments and logout.

Also manually rehearse:

1. Admin login, create category/product/serial, set price/MRP/stock; try duplicate serial.
2. Create Cashier, sign in, verify forbidden screens and API permissions.
3. Search by serial and partial product name; add multiple products; keyboard workflow and mobile/tablet layout.
4. Quantity edits, stock-limit errors, discounts at 0/100 and invalid values.
5. Complete bill, print on your actual thermal printer, download PDF, start new bill.
6. Find bill by number/date/product/amount; verify cashier scope.
7. Change product price as Admin; reprint old bill and check the original price.
8. Disconnect the network during submit, then Retry; confirm one bill and one stock deduction.
9. Run two cashiers against the last box, confirm exactly one sale.
10. Logout; check session expiry, backups, restore rehearsal and restart behavior.

## Frontend verification

```bash
cd frontend
npm ci
npm run build
npm test
```

The three UI tests use explicit mocked HTTP fixtures to check cart math, validation and uncertain-response retries. They do not verify the real API or database. Real browser visual/printing checks are still required because this environment had no browser binary.

## API reference

All business endpoints require authentication. Login is rate-limited. Send JSON and `X-POS-Request: 1` on mutations.

| Endpoint | Access / behavior |
| --- | --- |
| `POST /api/auth/login`, `POST /api/auth/logout`, `GET /api/auth/me` | Sign in/out, current session |
| `GET /api/products?q=&page=` | Admin catalog / cashier active products, 50 per page |
| `GET /api/products/search?q=` | Active products, partial name/serial, exact serial sorted first, 50 results |
| `GET /api/products/{id}` | View available product |
| `POST /api/products`, `PUT /api/products/{id}`, `DELETE /api/products/{id}` | Admin create/edit/deactivate (no historical deletion) |
| `GET /api/categories`, `POST /api/categories`, `PUT /api/categories/{id}` | Read / Admin write |
| `POST /api/bills` | Atomic completion; submission key, discount and product/quantity/expectedPrice items |
| `GET /api/bills?q=&date=YYYY-MM-DD&amount=&page=` | Scoped history, 30 per page |
| `GET /api/bills/{id}`, `GET /api/bills/{id}/print` | Immutable receipt JSON consumed by thermal/PDF rendering |
| `GET /api/inventory?page=`, `POST /api/inventory/adjust` | Admin audit / adjustment |
| `GET /api/dashboard/summary` | Admin daily summary and seven-day series |
| `GET /api/users`, `POST /api/users`, `PUT /api/users/{id}` | Admin users, no password hashes returned |
| `GET /api/settings`, `PUT /api/settings` | Read / Admin update |
| `GET /health` | Connectivity health; no authentication |

The `/print` API returns a receipt DTO, not printer binary/PDF bytes; the authenticated frontend provides native browser printing and PDF download.

## Operational boundaries

- Unsaved carts exist only in memory and clear on navigation/logout/reload; completed bills are durable in PostgreSQL. No offline selling mode.
- No tax/GST, returns/voids, payment gateway, cash reconciliation, multi-shop or purchase-order module was requested or implemented. Discounts are 0–100% for both roles as requested. Add tax-specific requirements before using this as a statutory tax invoice.
- Sample catalog contains only the five requested products and is development-only.
- Search is bounded to 50 results; refine the query for larger catalogs. For significantly larger inventories, benchmark PostgreSQL search and add reviewed trigram indexes if needed.
- Version dependencies are locked for the frontend. Patch .NET/Npgsql/container images through tested dependency updates and rebuild; do not assume an untested source package is production-certified.
