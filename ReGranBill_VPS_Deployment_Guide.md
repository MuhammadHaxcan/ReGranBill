# ReGranBill VPS Deployment Guide

## Scope

This guide is for deploying **ReGranBill as a second application on the same VPS** where **TradeLedger/PureLedger is already running as the first instance**.

It is based on the actual files in this repo:

- `ReGranBill.Server` is an ASP.NET Core API targeting **.NET 10**
- `regranbill.client` is an Angular app targeting **production build output at** `dist/regranbill.client/browser`
- the frontend calls the backend with **relative `/api/...` URLs**
- the backend reads secrets from **environment variables** or a repo-root **`.env`**
- the backend auto-runs **EF Core migrations** and **seed data** on startup

## Important Current Finding

The original Angular production budget issue has now been fixed in this repo.

Verified locally after the fix:

- `npm run build` succeeds for `regranbill.client`
- `dotnet publish ReGranBill.Server.csproj -c Release` succeeds
- current initial Angular bundle total is about `1.39 MB`
- production budget was updated to:
  - warning: `1.5 MB`
  - error: `2 MB`

ReGranBill is now deployment-ready from a build/publish perspective.

The recommended production topology is still:

1. deploy the **backend** as a systemd service
2. serve the **Angular frontend** with Nginx
3. proxy `/api` from Nginx to Kestrel

That matches your current TradeLedger-style VPS topology and is the cleanest way to run a second instance beside it.

---

## 1. Recommended Production Topology

### ReGranBill on same VPS as TradeLedger

- TradeLedger keeps its current domain, paths, and backend port
- ReGranBill gets:
  - its own domain or subdomain
  - its own PostgreSQL database
  - its own backend systemd service
  - its own internal backend port
  - its own Nginx site config
  - its own frontend build directory

### Recommended values

- Domain: `regranbill.online`
- Backend internal port: `5105`
- App root: `/var/www/regranbill`
- Backend publish dir: `/var/www/regranbill/backend-publish`
- Backend source dir: `/var/www/regranbill/source`
- Frontend build dir: `/var/www/regranbill/frontend-build`
- Service name: `regranbill-backend`
- Database name: `regranbill`
- Database user: `regranbilluser`

Use a **different port from TradeLedger**. If TradeLedger uses `5104`, ReGranBill should use `5105` or another unused internal port.

### Confirmed live VPS state

From the real VPS:

- VPS IP: `72.60.209.26`
- OS: `Ubuntu 24.04`
- TradeLedger Nginx site: `/etc/nginx/sites-available/pureledger.conf`
- TradeLedger enabled site: `/etc/nginx/sites-enabled/pureledger.conf`
- TradeLedger backend service: `/etc/systemd/system/pureledger-backend.service`
- TradeLedger backend port: `5104`
- TradeLedger frontend root: `/var/www/pureledger/frontend-build`
- Existing app root in `/var/www`: `/var/www/pureledger`
- .NET SDK: `10.0.107`
- .NET runtime: `Microsoft.AspNetCore.App 10.0.7`
- Node.js: `v24.12.0`
- npm: `11.6.2`
- PostgreSQL client: `16.13`
- PostgreSQL is listening on `0.0.0.0:5432` and `[::]:5432`
- UFW currently allows public `5432/tcp`

Because of that last point, the current VPS exposes PostgreSQL publicly unless you intentionally want remote DB access.

---

## 2. What The Repo Tells Us

### Backend behavior

From `ReGranBill.Server/Program.cs`:

- requires `ConnectionStrings__DefaultConnection`
- requires `JwtSettings__SecretKey`
- loads `.env` from:
  - current directory
  - parent directory
- runs:
  - `db.Database.MigrateAsync()`
  - `SeedData.InitializeAsync(db)`
- only enables CORS in `Development`
- in production, it expects same-origin hosting

### Frontend behavior

From Angular services and interceptor:

- API requests use relative paths like:
  - `/api/auth/login`
  - `/api/...`
- JWT token is stored in `localStorage`
- authenticated API requests send:
  - `Authorization: Bearer <token>`

This means:

- production does **not** need frontend env-based API host rewriting
- Nginx should serve the SPA at `/`
- Nginx should proxy `/api/` to the backend

### Database/bootstrap behavior

On first startup the backend will:

- apply migrations automatically
- seed the admin role/user bootstrap
- ensure the admin role has full page coverage
- create default admin user if missing:
  - username: `admin`
  - password: `Admin123!`
- keep an existing admin user's password unchanged, but re-link that user to the admin role if needed

You should log in and change that password immediately after first deployment.

---

## 3. Current Deployment Risk / Caveat

### Angular publish budget status

The original Angular production budget issue has been resolved.

Current verified status:

- source-based `dotnet publish` now succeeds
- `npm run build` now succeeds
- the Angular `initial` production budget has been updated to accommodate the current app size

### What this affects

- backend publish through the current `ReGranBill.Server.csproj`
- automated deploys that expect frontend+backend packaging in one publish step

### Recommended handling

You can now use either:

1. a single `dotnet publish` flow for backend publish artifacts
2. a split deployment flow where Angular is built separately and copied to the Nginx frontend directory

For this VPS, the split Nginx + Kestrel layout is still the preferred hosting model.

---

## 4. VPS Prerequisites

Assuming Ubuntu 22.04/24.04:

```bash
apt update && apt upgrade -y
apt install nginx postgresql postgresql-contrib certbot python3-certbot-nginx -y
```

### Install .NET 10

ReGranBill targets `net10.0`, so the VPS needs the matching runtime, and if you build on the VPS, the matching SDK too.

On your VPS, this is already satisfied, so this section is only needed if you rebuild another server.

```bash
wget https://packages.microsoft.com/config/ubuntu/24.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
dpkg -i packages-microsoft-prod.deb
rm packages-microsoft-prod.deb

apt update
apt install -y aspnetcore-runtime-10.0
apt install -y dotnet-sdk-10.0
```

Verify:

```bash
dotnet --info
```

### Install Node.js

Angular 21 should be built with a modern Node LTS release.

On your VPS, this is already satisfied with `Node v24.12.0` and `npm 11.6.2`.

```bash
curl -fsSL https://deb.nodesource.com/setup_22.x | bash -
apt install -y nodejs

node -v
npm -v
```

---

## 5. PostgreSQL Setup

### Security note for this VPS

Your current VPS firewall allows PostgreSQL from anywhere:

```bash
5432/tcp ALLOW IN Anywhere
5432/tcp (v6) ALLOW IN Anywhere (v6)
```

If ReGranBill and TradeLedger both use the database locally on the same VPS, the safer choice is to close public access:

```bash
ufw delete allow 5432/tcp
ufw status verbose
```

Only leave `5432` public if you explicitly need remote PostgreSQL access.

```bash
sudo -u postgres psql
```

```sql
CREATE DATABASE regranbill;
CREATE USER regranbilluser WITH ENCRYPTED PASSWORD 'Pagalmurgaa123@';
GRANT ALL PRIVILEGES ON DATABASE regranbill TO regranbilluser;
\c regranbill
GRANT ALL ON SCHEMA public TO regranbilluser;
GRANT ALL PRIVILEGES ON ALL TABLES IN SCHEMA public TO regranbilluser;
GRANT ALL PRIVILEGES ON ALL SEQUENCES IN SCHEMA public TO regranbilluser;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON TABLES TO regranbilluser;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON SEQUENCES TO regranbilluser;
\q
```

If the user already exists, update it instead:

```sql
ALTER USER regranbilluser WITH ENCRYPTED PASSWORD 'Pagalmurgaa123@';
```

Test:

```bash
psql -h localhost -U regranbilluser -d regranbill
```

---

## 6. Directory Layout On VPS

```bash
mkdir -p /var/www/regranbill/source
mkdir -p /var/www/regranbill/backend-publish
mkdir -p /var/www/regranbill/frontend-build
chown -R www-data:www-data /var/www/regranbill
```

Recommended final structure:

```text
/var/www/regranbill/
├── source/
├── backend-publish/
└── frontend-build/
```

---

## 7. Environment Configuration

Create:

`/var/www/regranbill/backend-publish/.env`

Suggested contents:

```env
ConnectionStrings__DefaultConnection=Host=localhost;Port=5432;Database=regranbill;Username=regranbilluser;Password=Pagalmurgaa123@
JwtSettings__SecretKey=obYh45TurFlSbTn2zp7aKIKSkDOqiWsvzVaiYG4W23pgXlB61wwEidBUH+xPuoJYSGdu4TW1YDnTgGbOqD6JCg==
JwtSettings__Issuer=ReGranBill
JwtSettings__Audience=ReGranBill
JwtSettings__ExpiryInHours=24
SeedAdmin__Username=admin
SeedAdmin__Password=CHANGE_THIS_BEFORE_FIRST_BOOT
SeedAdmin__FullName=Administrator
```

### Production CORS

If frontend and backend are served from the **same domain through Nginx**, production CORS is effectively not needed for browser traffic because requests stay same-origin.

If you expect a different frontend origin, then also set:

```env
Cors__AllowedOrigins__0=https://regranbill.online
Cors__AllowedOrigins__1=https://www.regranbill.online
```

### Important notes

- `appsettings.Production.json` should stay non-secret; use `.env` or service-level environment variables for secrets.
- The repo still includes dev-like secrets in `appsettings.json`. For VPS deployment, rely on `.env` or service-level environment variables, not committed values.

---

## 8. Source Transfer

### Option A: Git on VPS

```bash
cd /var/www/regranbill/source
git clone https://github.com/MuhammadHaxcan/ReGranBill.git .
```

If the repo already exists:

```bash
git config --global --add safe.directory /var/www/regranbill/source
cd /var/www/regranbill/source
git pull origin master
```

### Option B: Upload from local machine

Copy the repo into:

```text
/var/www/regranbill/source
```

---

## 9. Backend Build / Publish

## Recommended path

`dotnet publish` triggers the Angular build through the JS project reference, and that publish path is now working again after the budget fix.

### Recommended backend approach

If you build from source on the VPS, normal publish does invoke the Angular project reference.

That means you can use the standard publish command directly:

```bash
cd /var/www/regranbill/source/ReGranBill.Server
dotnet publish -c Release -o /var/www/regranbill/backend-publish
```

### Minimum backend runtime requirement

At runtime, the service must expose Kestrel on an internal port only, for example:

```env
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://127.0.0.1:5105
```

I recommend binding to `127.0.0.1` rather than `0.0.0.0` because Nginx is on the same box and the backend does not need direct public exposure.

---

## 10. Frontend Build / Deploy

### Angular production build target

The frontend output path is:

```text
regranbill.client/dist/regranbill.client/browser
```

### Current status

This build now passes after the production budget adjustment.

### Deploy frontend build output

```bash
cd /var/www/regranbill/source/regranbill.client
npm install
npm run build
rm -rf /var/www/regranbill/frontend-build/*
cp -r dist/regranbill.client/browser/* /var/www/regranbill/frontend-build/
chown -R www-data:www-data /var/www/regranbill/frontend-build
```

---

## 11. Nginx Configuration

## Why this layout is correct for ReGranBill

This app should be hosted as:

- Angular SPA from `/`
- ASP.NET API behind `/api/`

That matches the repo because frontend services already call relative `/api/...` endpoints.

## Step 1: Temporary HTTP-only config for first bring-up and certbot

Use this version first, before the SSL certificate exists:

Create:

`/etc/nginx/sites-available/regranbill.conf`

```nginx
server {
    listen 80;
    listen [::]:80;
    server_name regranbill.online www.regranbill.online;

    root /var/www/regranbill/frontend-build;
    index index.html;

    location /.well-known/acme-challenge/ {
        root /var/www/html;
    }

    location / {
        try_files $uri $uri/ /index.html;
    }

    location /api/ {
        proxy_pass http://127.0.0.1:5105;
        proxy_http_version 1.1;

        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;

        proxy_connect_timeout 60s;
        proxy_send_timeout 60s;
        proxy_read_timeout 60s;
    }
}
```

Enable/test/reload:

```bash
ln -sf /etc/nginx/sites-available/regranbill.conf /etc/nginx/sites-enabled/regranbill.conf
nginx -t
systemctl reload nginx
```

Verify HTTP first:

```bash
curl -I http://127.0.0.1 -H "Host: regranbill.online"
curl -I http://127.0.0.1 -H "Host: www.regranbill.online"
curl -I http://regranbill.online
```

## Step 2: Final HTTPS config after certificate issuance

Create:

`/etc/nginx/sites-available/regranbill.conf`

```nginx
server {
    listen 80;
    listen [::]:80;
    server_name regranbill.online www.regranbill.online;

    location /.well-known/acme-challenge/ {
        root /var/www/html;
    }

    location / {
        return 301 https://regranbill.online$request_uri;
    }
}

server {
    listen 443 ssl http2;
    listen [::]:443 ssl http2;
    server_name www.regranbill.online;

    ssl_certificate /etc/letsencrypt/live/regranbill.online/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/regranbill.online/privkey.pem;
    include /etc/letsencrypt/options-ssl-nginx.conf;
    ssl_dhparam /etc/letsencrypt/ssl-dhparams.pem;

    return 301 https://regranbill.online$request_uri;
}

server {
    listen 443 ssl http2;
    listen [::]:443 ssl http2;
    server_name regranbill.online;

    ssl_certificate /etc/letsencrypt/live/regranbill.online/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/regranbill.online/privkey.pem;
    include /etc/letsencrypt/options-ssl-nginx.conf;
    ssl_dhparam /etc/letsencrypt/ssl-dhparams.pem;

    add_header Strict-Transport-Security "max-age=31536000; includeSubDomains" always;
    add_header X-Content-Type-Options "nosniff" always;
    add_header X-Frame-Options "SAMEORIGIN" always;
    add_header Referrer-Policy "strict-origin-when-cross-origin" always;

    root /var/www/regranbill/frontend-build;
    index index.html;

    location / {
        try_files $uri $uri/ /index.html;
    }

    location ~* \.(js|css|png|jpg|jpeg|gif|ico|svg|woff|woff2|ttf|eot)$ {
        expires 1y;
        add_header Cache-Control "public, immutable";
        try_files $uri =404;
    }

    location /api/ {
        proxy_pass http://127.0.0.1:5105;
        proxy_http_version 1.1;

        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;

        proxy_connect_timeout 60s;
        proxy_send_timeout 60s;
        proxy_read_timeout 60s;
    }

    gzip on;
    gzip_vary on;
    gzip_min_length 1024;
    gzip_types
        text/plain
        text/css
        text/xml
        text/javascript
        application/javascript
        application/json
        application/xml;
}
```

### Enable site

```bash
ln -sf /etc/nginx/sites-available/regranbill.conf /etc/nginx/sites-enabled/regranbill.conf
nginx -t
systemctl reload nginx
```

## Notes specific to coexistence with TradeLedger

- do **not** reuse TradeLedger’s `server_name`
- do **not** reuse TradeLedger’s backend port
- do **not** reuse `/var/www/pureledger`
- keep ReGranBill fully isolated under `/var/www/regranbill`

---

## 12. SSL

After DNS points to the VPS and the temporary HTTP-only config is active:

```bash
certbot --nginx -d regranbill.online -d www.regranbill.online
certbot certificates
certbot renew --dry-run
```

Then replace the temporary HTTP-only Nginx file with the final HTTPS config from Section 11 and reload Nginx:

```bash
nginx -t
systemctl reload nginx
```

If `nginx -t` shows warnings like `protocol options redefined`, Certbot may have injected overlapping `443` settings into the site file. In that case, overwrite the file with the clean final HTTPS config from Section 11 and reload again.

---

## 13. systemd Service

Create:

`/etc/systemd/system/regranbill-backend.service`

```ini
[Unit]
Description=ReGranBill Backend
After=network.target postgresql.service

[Service]
Type=simple
WorkingDirectory=/var/www/regranbill/backend-publish
ExecStart=/usr/bin/dotnet /var/www/regranbill/backend-publish/ReGranBill.Server.dll
Restart=always
RestartSec=10
User=www-data
Group=www-data

Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=ASPNETCORE_URLS=http://127.0.0.1:5105
Environment=DOTNET_PRINT_TELEMETRY_MESSAGE=false

NoNewPrivileges=true
PrivateTmp=true

StandardOutput=journal
StandardError=journal
SyslogIdentifier=regranbill-backend

[Install]
WantedBy=multi-user.target
```

Then:

```bash
systemctl daemon-reload
systemctl enable regranbill-backend
systemctl start regranbill-backend
systemctl status regranbill-backend
journalctl -u regranbill-backend -f
```

---

## 14. Firewall

Minimum:

```bash
ufw allow OpenSSH
ufw allow 80/tcp
ufw allow 443/tcp
ufw enable
ufw status
```

Do **not** expose the backend port publicly.

On this VPS, `80/tcp`, `443/tcp`, and `22/tcp` are already allowed. The rule that most likely needs cleanup is public `5432/tcp`.

---

## 15. First Boot Checklist

### Backend checks

```bash
systemctl status regranbill-backend
journalctl -u regranbill-backend -n 100
ss -tlnp | grep 5105
```

Expected:

- service is active
- backend listens on `127.0.0.1:5105`
- startup logs show migrations/seeding completed successfully

### Nginx checks

```bash
nginx -t
systemctl status nginx
tail -f /var/log/nginx/error.log
```

### HTTP checks

```bash
curl -I http://127.0.0.1 -H "Host: regranbill.online"
curl -I http://127.0.0.1 -H "Host: www.regranbill.online"
curl -I http://regranbill.online
curl -I https://regranbill.online
curl -I https://regranbill.online/api/auth/login
```

For the login endpoint, `GET` may return `405` or similar, which is acceptable; the important part is that the request reaches the backend and not a static 404 from Nginx.

### Browser checks

1. SPA loads at `/`
2. refresh on an inner Angular route works
3. login request goes to `/api/auth/login`
4. protected API calls include `Authorization: Bearer ...`
5. no CORS errors in console
6. `www.regranbill.online` redirects to `regranbill.online`

### Backend log note

You may see ASP.NET Core DataProtection warnings about ephemeral key storage on startup. For the current JWT-based deployment this is not blocking traffic, but if you later add persisted cookie/session protection requirements, configure a persistent key ring.

---

## 16. Ongoing Deploy Commands

### Frontend-only deploy

```bash
cd /var/www/regranbill/source/regranbill.client
npm install
npm run build
rm -rf /var/www/regranbill/frontend-build/*
cp -r dist/regranbill.client/browser/* /var/www/regranbill/frontend-build/
chown -R www-data:www-data /var/www/regranbill/frontend-build
```

### Backend-only deploy

```bash
cd /var/www/regranbill/source/ReGranBill.Server
dotnet publish -c Release -o /var/www/regranbill/backend-publish
chown -R www-data:www-data /var/www/regranbill/backend-publish
systemctl restart regranbill-backend
systemctl status regranbill-backend
```

### Full deploy

```bash
cd /var/www/regranbill/source
git pull

cd /var/www/regranbill/source/ReGranBill.Server
dotnet publish -c Release -o /var/www/regranbill/backend-publish

cd /var/www/regranbill/source/regranbill.client
npm install
npm run build
rm -rf /var/www/regranbill/frontend-build/*
cp -r dist/regranbill.client/browser/* /var/www/regranbill/frontend-build/

chown -R www-data:www-data /var/www/regranbill
systemctl restart regranbill-backend
nginx -t && systemctl reload nginx
```

Note: full deploy is no longer blocked by the Angular production budget issue.

---

## 17. Troubleshooting

### 502 Bad Gateway

Check:

```bash
systemctl status regranbill-backend
journalctl -u regranbill-backend -n 100
ss -tlnp | grep 5105
```

Likely causes:

- backend failed to start
- wrong port in Nginx
- missing `.env`
- database connection failure

### Angular routes return 404 after refresh

Cause:

- missing SPA fallback

Fix:

- make sure Nginx root server block has:

```nginx
location / {
    try_files $uri $uri/ /index.html;
}
```

### API works locally but fails on VPS

Check:

- frontend is calling `/api/...` and not `localhost`
- Nginx has a `location /api/`
- systemd service uses the same backend port that Nginx proxies to

### Login works but later requests fail with 401

Check:

- token exists in `localStorage`
- `Authorization: Bearer ...` is being sent
- server clock/timezone is sane

### Angular app freezes or shows endless `loading...` after login

If backend login works, curl requests succeed, and the page still hangs in the browser, the problem may be in the frontend runtime rather than the VPS infrastructure.

In this project, the final root cause was a combination of Angular UI-state issues:

- `zone.js` was not explicitly imported in `regranbill.client/src/main.ts`
- the sidebar was using `routerLinkActive` with unstable inputs, which caused Angular `NG0103` endless change-notification loops in production
- some routed pages needed their `ChangeDetectorRef.detectChanges()` calls restored so async data would fully paint on the first click instead of only after a second interaction

Final stable fix that worked on the live VPS:

- import `zone.js` in `regranbill.client/src/main.ts`
- replace sidebar `routerLinkActive` usage with manual button navigation and manual active-state styling
- keep the page-level `detectChanges()` calls that this app depends on after async loads

If you see symptoms like:

- login succeeds but the shell hangs
- clicking a sidebar page only works after pressing Enter or clicking a second time
- browser console shows `NG0103` and mentions endless change notifications

then recheck the frontend build, not just Nginx, PostgreSQL, or backend auth.

### First startup fails during migration

Check DB permissions:

```bash
psql -h localhost -U regranbilluser -d regranbill
```

The service user does not need direct DB file permissions, but the DB user must have schema/table/sequence privileges.

---

## 18. Recommended Next Technical Fixes Before Final Production

1. Remove hardcoded dev secrets from `ReGranBill.Server/appsettings.json`.
2. Decide whether you want:
   - split hosting: Nginx static frontend + API proxy
   - unified hosting: published ASP.NET app serving the SPA directly
3. If unified hosting is desired, verify Angular artifacts are copied into ASP.NET static web assets / web root during publish.

---

## 19. Final VPS Summary

This guide now matches the live VPS state you provided:

1. TradeLedger remains on:
   - domain: `tradeledger.cloud`
   - Nginx file: `/etc/nginx/sites-available/pureledger.conf`
   - backend service: `/etc/systemd/system/pureledger-backend.service`
   - backend port: `5104`
2. ReGranBill should be deployed on:
   - domain: `regranbill.online`
   - Nginx file: `/etc/nginx/sites-available/regranbill.conf`
   - backend service: `/etc/systemd/system/regranbill-backend.service`
   - backend port: `127.0.0.1:5105`
   - root path: `/var/www/regranbill`
3. VPS runtime compatibility is already good:
   - .NET 10 installed
   - Node 24 installed
   - npm 11 installed
   - PostgreSQL 16 installed
4. Live deployment verification succeeded:
   - `https://regranbill.online` returns `200`
   - `https://www.regranbill.online` returns `200`
   - certificate issuance and renewal dry-run both succeeded
5. The main remaining operational concern is that PostgreSQL is publicly exposed on `5432` unless you close that firewall rule.

---

## 20. Final Debugging Summary

The hardest production issue on this deployment was not the backend, database, SSL, or Nginx.

What was confirmed working during debugging:

- login API worked correctly
- JWT issuance worked correctly
- admin role and page permissions were correct
- protected API endpoints like `/api/accounts/...`, `/api/categories`, and `/api/delivery-challans/...` returned valid responses on the live VPS
- Nginx proxying and the live HTTPS setup were healthy

What was actually broken:

- the Angular frontend could enter an unstable render/change-detection loop after login
- the sidebar navigation path was especially sensitive because of `routerLinkActive` reacting to unstable inputs
- some routed pages would appear to stay on `loading...` until a second user interaction forced the UI to repaint

Final frontend fixes that resolved the live issue:

1. import `zone.js` explicitly in `regranbill.client/src/main.ts`
2. replace sidebar `routerLink`/`routerLinkActive` behavior with manual button navigation and manual active-page styling
3. keep the page-level `ChangeDetectorRef.detectChanges()` calls required by this app after async page loads

Practical conclusion:

- if ReGranBill logs in successfully but page transitions freeze, do not assume the VPS or backend is broken first
- verify the frontend build actually includes the final navigation/change-detection fixes
- if needed, inspect the browser console for Angular errors like `NG0103`

This matters because the deployment itself can be completely correct while the production frontend still misbehaves unless these UI fixes are present.

