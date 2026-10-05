# Customer portal (ProCargo.UI)

`CustomerPortal/ProCargo.UI` — React 19, TypeScript (strict), Vite, Material UI 7, TanStack Query, React Hook Form +
Zod, axios, dayjs. One application serves the public website and three signed-in experiences, chosen by the
account type in the token.

| Area | Routes | Highlights |
| --- | --- | --- |
| Public | `/`, `/partners`, `/contact`, `/login`, `/register`, `/forgot-password`, `/reset-password` | Instant price estimate, partner sign-up, contact form |
| Customer | `/customer/...` | Booking editor (multi-item, saved addresses), quotation accept/reject, live trip map, invoices, Razorpay checkout, addresses and profile |
| Owner | `/owner/...` | Vehicles, drivers, KYC uploads with expiry, assigned trips, earnings and settlements, business and bank details |
| Driver | `/driver/...` | Today's trips, pickup and delivery OTP steps, location sharing, proof-of-delivery photos — built for a phone |
| Shared | `/notifications`, `/support`, `/account/password` | |

## Structure

```
src/api         axios client (token in memory, single-flight refresh), endpoint modules, response types
src/auth        AuthProvider, route guards by account type
src/components  layout shells, DataTable (cards on phones), forms, PlateTag, timelines, tracking map, documents
src/lib         IST formatting, status labels, Zod rules for Indian formats (mobile, PIN, GSTIN, IFSC, bank account, vehicle number)
src/pages       one folder per area
src/test        Vitest + Testing Library
```

## Design

* Palette: lorry teal `#0F5F5B` for actions and navigation, asphalt `#1E2328` for text and the dark shell, and
  commercial-plate yellow `#F5C518` used **only** on `PlateTag` — booking, trip and vehicle numbers look like the
  number plates drivers and dispatchers already read at a glance.
* Type: Archivo (condensed weights for headings and numbers, regular for text).
* Lists switch to tappable cards below 600 px; the driver portal is single-column with large touch targets.
* All forms validate on the client with the same rules as the API and show the API's field errors on the field.
* Accessibility: semantic landmarks, labelled inputs, visible focus, status conveyed by text not colour alone,
  `prefers-reduced-motion` respected.

## Security in the browser

* Access token in memory only; refresh token in an HttpOnly, Secure, SameSite=Strict cookie scoped to
  `/api/v1/auth`. Reloading the page restores the session through `/auth/refresh`.
* Route guards only decide what to render; every call is authorised by the API.
* Protected files (KYC, POD) are fetched with the bearer token and opened as blob URLs — no public links.
* Strict security headers and a Content-Security-Policy are set in `public/staticwebapp.config.json`.
* No secrets in the bundle: `VITE_*` variables are public by design (only the API base URL and Razorpay key id).

## Run

```bash
cd CustomerPortal/ProCargo.UI
npm install && npm run dev         # http://localhost:5173, proxies /api to https://localhost:7180
npm test && npm run lint && npm run build
```
