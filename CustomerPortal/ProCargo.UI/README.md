# ProCargo.UI — public website, customer, truck-owner and driver portals

React 19 + TypeScript + Vite + Material UI, TanStack Query, React Hook Form + Zod.

| Area | Routes | Who |
| --- | --- | --- |
| Public website | `/`, `/partners`, `/contact`, `/login`, `/register`, `/forgot-password`, `/reset-password` | Everyone |
| Customer portal | `/customer/...` — bookings, quotations, tracking, invoices and online payment, addresses | Customers |
| Owner portal | `/owner/...` — vehicles, drivers, documents, trips, earnings, bank accounts | Vehicle owners |
| Driver portal | `/driver/...` — assigned trips, OTP pickup/delivery, live location, proof of delivery | Drivers |
| Shared | `/notifications`, `/support`, `/account/password` | All signed-in users |

## Run locally

```bash
npm install
cp .env.example .env.local        # adjust VITE_API_PROXY_TARGET if the API runs elsewhere
npm run dev                        # http://localhost:5173
```

Start `Backend/ProCargo.API.sln` first (`https` profile, https://localhost:7180). The dev server proxies `/api` to the
API so the refresh-token cookie stays first-party.

Test accounts (development database, password `ProCargo@Dev1`): `customer@procargo.test`,
`owner@procargo.test`, `driver@procargo.test`.

## Scripts

`npm run build` (type-check + production bundle), `npm test` (Vitest), `npm run lint`.

## Security notes

* The access token is held in memory only; the refresh token is an HttpOnly, Secure, SameSite=Strict cookie the
  script cannot read. Refresh and logout send the `X-ProCargo-Client` header.
* Every screen's permission is enforced by the API; the route guards only decide what to show.
* No secrets live in this project. `VITE_*` variables are public by design.
