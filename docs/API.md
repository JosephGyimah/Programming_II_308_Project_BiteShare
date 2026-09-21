# BiteShare — API reference

Base URL: configured via `ApiBaseUrl` in the client's `wwwroot/appsettings.json`
(defaults to `https://localhost:5001`). Full interactive docs are also served at
`/swagger` in Development.

Every endpoint below except `POST /api/auth/*` requires an `Authorization: Bearer <token>`
header. See `docs/ARCHITECTURE.md` for which token type (identity vs participant) each
endpoint needs.

## Auth — `/api/auth`

| Method | Route | Auth | Body | Notes |
|---|---|---|---|---|
| POST | `/register` | none | `{ email, password, displayName }` | Returns an identity token |
| POST | `/login` | none | `{ email, password }` | Returns an identity token |
| POST | `/guest-join` | none | `{ joinCode, displayName }` | No account needed — returns a participant token scoped to that one session |

## Sessions — `/api/sessions`

| Method | Route | Auth | Body | Notes |
|---|---|---|---|---|
| POST | `/` | Identity | `{ name, orderDeadlineUtc? }` | Creates a session, adds the caller as host participant, returns a participant token |
| POST | `/join` | Identity | `{ joinCode }` | Joins an existing open session as a logged-in (non-guest) participant |
| GET | `/{id}` | Any | — | Session summary |
| GET | `/` | Identity | — | Sessions the caller hosts or has joined |

## Menu items — `/api/sessions/{sessionId}/menuitems`

| Method | Route | Auth | Body | Notes |
|---|---|---|---|---|
| GET | `/` | Participant | — | All menu items for the session |
| POST | `/` | Participant (host only) | `{ name, description?, price }` | |

## Cart — `/api/sessions/{sessionId}/cart`

| Method | Route | Auth | Body | Notes |
|---|---|---|---|---|
| GET | `/` | Participant | — | Everyone's cart items (the "shared cart") |
| POST | `/` | Participant | `{ menuItemId, quantity, notes? }` | Broadcasts `CartUpdated` over `OrderHub` |
| DELETE | `/{cartItemId}` | Participant (owner only) | — | Broadcasts `CartUpdated` |

## Orders — `/api/sessions/{sessionId}/orders`

| Method | Route | Auth | Body | Notes |
|---|---|---|---|---|
| POST | `/submit` | Participant (host only) | `{ splitMode, tax, tip, deliveryFee, paymentMethodIds }` | Computes the split, attempts Stripe capture per participant (skipped if no `paymentMethodId` supplied for them), creates the `Order` + `Receipt` rows, broadcasts `OrderStatusChanged` |
| POST | `/{orderId}/status` | Participant (host only) | `"Preparing" \| "OutForDelivery" \| "Delivered"` (raw string body) | Broadcasts `OrderStatusChanged` |

## Receipts — `/api/sessions/{sessionId}/orders/{orderId}/receipts`

| Method | Route | Auth | Notes |
|---|---|---|---|
| GET | `/` | Participant | JSON list of who owes what |
| GET | `/pdf` | Participant | Itemized PDF, generated with QuestPDF |

## Participants — `/api/sessions/{sessionId}/participants`

| Method | Route | Auth | Notes |
|---|---|---|---|
| GET | `/` | Participant | Roster for the session |
| DELETE | `/{participantId}` | Participant (host only) | Host can't remove themself |

## SignalR — `/hubs/order`

Connect with the participant token as `?access_token=<token>` (browsers can't set an
`Authorization` header on a WebSocket handshake). Client → server: `JoinSession(sessionId)`,
`LeaveSession(sessionId)`. Server → client: `CartUpdated(CartEvent)`, `OrderStatusChanged(OrderStatusUpdate)`.
