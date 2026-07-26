# Frontend Setup

This frontend is part of the `.NET React Starter` and uses backend-driven runtime flags during bootstrap.

## Available Scripts

```powershell
npm start
npm test
npm run test:once
npm run build
```

## Runtime Config

The app reads `GET /api/runtime-config` during bootstrap.

The central hook is `useFeatureAvailability()`, which controls:

- the navbar quick search bar
- dashboard visibility
- admin navigation
- users navigation
- email-related UI sections

The app shell shows a loading gate until both auth and runtime config are ready.

## Local Development

Set the API URL in `frontend/.env.development.local`:

```env
VITE_API_URL=http://localhost:5000
```

## Quick Search

When `GlobalSearchEnabled` is true, the navbar shows a `Ctrl+K` quick search field that can later be extended with pages, users, contacts, offers, or any other project-specific targets.
