# ConnectSphere — Full Stack Social Media Platform

> **Share. Connect. Discover. Your World.**

A complete social media platform with **7 .NET 8 microservices** and a **React 18 + Tailwind CSS** frontend.

## Repository Layout

```
ConnectSphereFullProject/
├── src/                          ← .NET 8 microservices (Auth/Post/Like/Comment/Follow/Notif/Feed)
├── shared/ConnectSphere.Shared/  ← MassTransit event contracts
├── frontend/                     ← React 18 + Tailwind CSS SPA
│   └── src/
│       ├── components/           ← PostCard, CommentThread, Layout, Avatar, FollowButton...
│       ├── contexts/AuthContext  ← Guest / User / Admin role detection
│       ├── pages/                ← Home, Explore, Profile, Notifications, Search, Settings + Admin panel
│       └── services/api.js       ← Axios clients for all 7 services
├── docker-compose.yml            ← Full local dev stack
└── render.yaml                   ← Render Blueprint
```

## Quick Start

```bash
# Start everything (Docker Desktop required)
docker-compose up --build

# Frontend dev server
cd frontend && npm install && npm run dev
# → http://localhost:3000
```

## Role System

| Role   | Condition                     | Access |
|--------|-------------------------------|--------|
| Guest  | Not logged in                 | Public posts, profiles, hashtags (read-only) |
| User   | Logged in, isAdmin = false    | Full social: post, like, comment, follow, notifications |
| Admin  | Logged in, isAdmin = true     | All + /admin panel: manage users, posts, send broadcasts |

### Create first admin (in Neon SQL console):
```sql
UPDATE auth_users SET "IsAdmin" = true WHERE "Email" = 'you@example.com';
```

## Deploy

**Backend:** Render Blueprint → `render.yaml` → set `DATABASE_URL` (Neon), `JWT_SECRET`, `REDIS_URL`, `RABBITMQ_*` env vars.

**Frontend:** Render Static Site → `frontend/` folder → set `VITE_*` service URLs.

See `src/README.md` and `frontend/README.md` for full details.
"# ConnectSphere" 
