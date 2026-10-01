# Verification record

Date: 2026-10-01

## Passed in the authoring environment

- TypeScript compilation and Vite production build (`npm run build`).
- Three Vitest/jsdom tests (`npm test`):
  - Five boxes at ₹150 plus three at ₹80, 10% discount: subtotal ₹990, discount ₹99, final ₹891; receipt state and new-bill focus.
  - Rejection of excessive quantity and disabling checkout for invalid discounts.
  - Interrupted submission retries with exactly the original submission identifier and payload; cart inputs remain locked during uncertain submission.
- Production frontend dependency audit: `npm audit --omit=dev` reports zero known vulnerabilities at verification time. jsPDF was updated to 4.2.1 to resolve a reported advisory.
- Python acceptance script syntax check.

## Not executed here

- .NET restore/build, EF migration application, live PostgreSQL queries and transaction/concurrency tests: .NET, PostgreSQL and Docker were absent; the .NET SDK download was unavailable.
- Real browser rendering, mobile visual inspection, actual printer output and PDF download inspection: the environment had no browser binary.
- GitHub push, CI execution or public hosting: no remote repository/provider deployment was configured.

The included GitHub workflow builds the backend and runs `tests/acceptance.py` against PostgreSQL. Run it and complete the README shop rehearsal before real sales. The frontend tests explicitly use fixtures and do not establish backend correctness.
