# ReGranBill VPS Deployment Guide

## Adding ReGranBill to an Existing VPS (PureLedger Already Running)

**Application:** ReGranBill — Billing & Inventory Management System  
**Stack:** Angular 21 Frontend + .NET 10 Backend + PostgreSQL  
**Domain:** regranbill.online  
**Backend Port:** 5105 (PureLedger uses 5104 — do not change that)  
**Last Updated:** April 2026

---

## Critical: What NOT to Touch

The VPS already runs PureLedger. The following must remain untouched throughout this entire deployment:

| Do NOT touch | Why |
|---|---|
| `/etc/nginx/sites-enabled/pureledger.conf` | PureLedger's live nginx config |
| `/var/www/pureledger/` | PureLedger's files |
| `pureledger-backend` systemd service | PureLedger's backend process |
| `pureledgerdb` PostgreSQL database | PureLedger's data |
| Port `5104` | PureLedger's backend port |

ReGranBill uses its own separate directory, database, nginx config file, service, and port. Nothing overlaps.

---

## Table of Contents

1. [DNS Setup](#1-dns-setup)
2. [Install .NET 10 Runtime](#2-install-net-10-runtime)
3. [PostgreSQL: New Database](#3-postgresql-new-database)
4. [Directory Structure](#4-directory-structure)
5. [Backend: Build and Deploy](#5-backend-build-and-deploy)
6. [Frontend: Build and Deploy](#6-frontend-build-and-deploy)
7. [Nginx: Add ReGranBill Site](#7-nginx-add-regranbill-site)
8. [SSL Certificate](#8-ssl-certificate)
9. [Systemd Service](#9-systemd-service)
10. [Verify Everything Works](#10-verify-everything-works)
11. [Update Deployment Script](#11-update-deployment-script)
12. [Troubleshooting](#12-troubleshooting)

---

## 1. DNS Setup

At your domain registrar for `regranbill.online`, add two A records pointing to the same VPS IP address that PureLedger uses:

```
Type  Name    Value
A     @       YOUR_VPS_IP
A     www     YOUR_VPS_IP
```

DNS propagation can take a few minutes to a few hours. You can check propagation with:

```bash
nslookup regranbill.online
```

---

## 2. Install .NET 10 Runtime

The VPS already has .NET 8 for PureLedger. Installing .NET 10 alongside it is safe — both runtimes coexist independently.

```bash
# The Microsoft package repo should already be configured from the PureLedger setup.
# If not, run these first:
# wget https://packages.microsoft.com/config/ubuntu/22.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
# dpkg -i packages-microsoft-prod.deb
# rm packages-microsoft-prod.deb

apt update
apt install -y aspnetcore-runtime-10.0

# Verify both runtimes are present
dotnet --list-runtimes
# You should see both:
#   Microsoft.AspNetCore.App 8.x.x
#   Microsoft.AspNetCore.App 10.x.x
```

> **If building on VPS:** Install the SDK instead (it includes the runtime):
> ```bash
> apt install -y dotnet-sdk-10.0
> ```

---

## 3. PostgreSQL: New Database

Connect to PostgreSQL and create a completely separate database and user for ReGranBill. This does not touch `pureledgerdb` at all.

```bash
sudo -u postgres psql
```

```sql
-- Create database
CREATE DATABASE regranbilldb;

-- Create user with a strong password
CREATE USER regranbilluser WITH ENCRYPTED PASSWORD 'YOUR_SECURE_PASSWORD';

-- Grant privileges
GRANT ALL PRIVILEGES ON DATABASE regranbilldb TO regranbilluser;

-- Connect to the new database
\c regranbilldb

-- Grant schema privileges (required for EF Core migrations)
GRANT ALL ON SCHEMA public TO regranbilluser;
GRANT ALL PRIVILEGES ON ALL TABLES IN SCHEMA public TO regranbilluser;
GRANT ALL PRIVILEGES ON ALL SEQUENCES IN SCHEMA public TO regranbilluser;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON TABLES TO regranbilluser;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON SEQUENCES TO regranbilluser;

-- Exit
\q
```

Test the connection:

```bash
psql -U regranbilluser -d regranbilldb -h localhost
# Enter password when prompted
\q
```

---

## 4. Directory Structure

```bash
mkdir -p /var/www/regranbill/backend-publish
mkdir -p /var/www/regranbill/backend-source
mkdir -p /var/www/regranbill/frontend-build
chown -R www-data:www-data /var/www/regranbill
```

Final structure:
```
/var/www/
├── pureledger/          ← DO NOT TOUCH
│   ├── backend-publish/
│   ├── backend-source/
│   └── frontend-build/
└── regranbill/          ← ReGranBill lives here
    ├── backend-publish/
    ├── backend-source/
    └── frontend-build/
```

---

## 5. Backend: Build and Deploy

### 5.1 Transfer Source Code

**Option A: Git**

```bash
cd /var/www/regranbill/backend-source
git clone YOUR_REPO_URL .
```

**Option B: SCP from local machine**

```bash
# Run this on your local machine (Windows Git Bash or WSL)
scp -r /c/Users/Claude/Desktop/ReGranBill/ReGranBill root@YOUR_VPS_IP:/var/www/regranbill/backend-source/
```

### 5.2 Create the .env File (Secrets)

The backend reads a `.env` file from its working directory at startup. Create this file on the VPS with your production secrets. **Never commit this file to git.**

```bash
nano /var/www/regranbill/backend-publish/.env
```

Paste the following, replacing the placeholder values:

```env
ConnectionStrings__DefaultConnection=Host=localhost;Port=5432;Database=regranbilldb;Username=regranbilluser;Password=YOUR_SECURE_PASSWORD;Pooling=true;

JwtSettings__SecretKey=YOUR_VERY_LONG_RANDOM_JWT_SECRET_AT_LEAST_64_CHARACTERS
JwtSettings__Issuer=ReGranBill
JwtSettings__Audience=ReGranBill
JwtSettings__ExpiryInHours=24
```

Generate a secure JWT key:

```bash
openssl rand -base64 64
```

Generate a secure database password:

```bash
openssl rand -base64 32
```

Secure the .env file so only the service user can read it:

```bash
chown www-data:www-data /var/www/regranbill/backend-publish/.env
chmod 600 /var/www/regranbill/backend-publish/.env
```

### 5.3 Create appsettings.Production.json (Non-Secret Config)

This file configures CORS for the production domain and other non-secret settings. Create it inside the source directory:

```bash
nano /var/www/regranbill/backend-source/ReGranBill/ReGranBill.Server/appsettings.Production.json
```

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Warning",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "Cors": {
    "AllowedOrigins": [
      "http://regranbill.online",
      "https://regranbill.online",
      "http://www.regranbill.online",
      "https://www.regranbill.online"
    ]
  }
}
```

### 5.4 Build the Backend

**Option A: Build on VPS (requires .NET 10 SDK)**

```bash
cd /var/www/regranbill/backend-source/ReGranBill/ReGranBill.Server
dotnet publish -c Release -o /var/www/regranbill/backend-publish
```

**Option B: Build locally, transfer artifacts**

```bash
# On your local machine
cd C:\Users\Claude\Desktop\ReGranBill\ReGranBill\ReGranBill.Server
dotnet publish -c Release -o ./publish

# Transfer publish output to VPS
scp -r ./publish/* root@YOUR_VPS_IP:/var/www/regranbill/backend-publish/
```

### 5.5 Run Database Migrations

```bash
# Requires .NET 10 SDK and EF Core tools on VPS
cd /var/www/regranbill/backend-source/ReGranBill/ReGranBill.Server

# Set env vars so the migration connects to the right database
export ConnectionStrings__DefaultConnection="Host=localhost;Port=5432;Database=regranbilldb;Username=regranbilluser;Password=YOUR_SECURE_PASSWORD;"

dotnet ef database update
```

> The app also runs `SeedData.InitializeAsync()` on every startup, which seeds initial data automatically.

### 5.6 Set Permissions

```bash
chown -R www-data:www-data /var/www/regranbill/backend-publish
chmod -R 755 /var/www/regranbill/backend-publish
# Re-secure .env after permission reset
chmod 600 /var/www/regranbill/backend-publish/.env
```

---

## 6. Frontend: Build and Deploy

### 6.1 Build Locally (Recommended)

The Angular frontend is built locally and the output is transferred. This avoids needing Node.js on the VPS.

```bash
# On your local machine
cd C:\Users\Claude\Desktop\ReGranBill\ReGranBill\regranbill.client

npm install
npm run build
# Output lands at: dist/regranbill.client/browser/
```

Transfer the build output to the VPS:

```bash
scp -r dist/regranbill.client/browser/* root@YOUR_VPS_IP:/var/www/regranbill/frontend-build/
```

### 6.2 Build on VPS (Alternative)

If Node.js is installed on the VPS (it was installed for PureLedger):

```bash
cd /var/www/regranbill/backend-source/ReGranBill/regranbill.client

npm install
npm run build
# Output is at dist/regranbill.client/browser/

cp -r dist/regranbill.client/browser/* /var/www/regranbill/frontend-build/
```

### 6.3 Set Permissions

```bash
chown -R www-data:www-data /var/www/regranbill/frontend-build
chmod -R 755 /var/www/regranbill/frontend-build
```

---

## 7. Nginx: Add ReGranBill Site

This creates a **new, separate nginx config file** for ReGranBill. The PureLedger config at `/etc/nginx/sites-enabled/pureledger.conf` is never modified.

### 7.1 Initial HTTP Config

```bash
nano /etc/nginx/sites-available/regranbill.conf
```

Paste:

```nginx
# Redirect www to non-www
server {
    listen 80;
    server_name www.regranbill.online;
    return 301 http://regranbill.online$request_uri;
}

server {
    listen 80;
    server_name regranbill.online;

    # Frontend - Angular SPA
    location / {
        root /var/www/regranbill/frontend-build;
        index index.html;
        try_files $uri $uri/ /index.html;

        # Cache static assets
        location ~* \.(js|css|png|jpg|jpeg|gif|ico|svg|woff|woff2|ttf|eot)$ {
            expires 1y;
            add_header Cache-Control "public, immutable";
        }
    }

    # Backend API
    location /api/ {
        proxy_pass http://localhost:5105;
        proxy_http_version 1.1;

        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_set_header Connection keep-alive;

        proxy_pass_request_headers on;

        proxy_connect_timeout 60s;
        proxy_send_timeout 60s;
        proxy_read_timeout 60s;

        proxy_buffering on;
        proxy_buffer_size 4k;
        proxy_buffers 8 4k;
    }

    gzip on;
    gzip_vary on;
    gzip_min_length 1024;
    gzip_types text/plain text/css text/xml text/javascript application/javascript application/json application/xml;
}
```

### 7.2 Enable the Site

```bash
# Enable ReGranBill config (leave pureledger.conf alone)
ln -s /etc/nginx/sites-available/regranbill.conf /etc/nginx/sites-enabled/

# Verify both sites are enabled
ls -la /etc/nginx/sites-enabled/
# Should show: pureledger.conf -> ...  AND  regranbill.conf -> ...

# Test nginx config (checks ALL enabled sites)
nginx -t

# Reload nginx (zero downtime - PureLedger stays up)
systemctl reload nginx
```

---

## 8. SSL Certificate

Certbot will issue a separate certificate for `regranbill.online`. It does not touch the existing `tradeledger.cloud` certificate.

```bash
certbot --nginx -d regranbill.online -d www.regranbill.online
```

Certbot will:
1. Obtain a Let's Encrypt certificate for `regranbill.online`
2. Automatically modify `/etc/nginx/sites-enabled/regranbill.conf` to add SSL blocks
3. Reload nginx

After Certbot completes, manually update the nginx config to use the full HTTPS configuration (replace the content of `/etc/nginx/sites-available/regranbill.conf`):

```bash
nano /etc/nginx/sites-available/regranbill.conf
```

Replace the entire file contents with:

```nginx
# Redirect HTTP to HTTPS
server {
    listen 80;
    listen [::]:80;
    server_name regranbill.online www.regranbill.online;

    # Allow ACME challenge for Let's Encrypt certificate renewal
    location /.well-known/acme-challenge/ {
        root /var/www/html;
    }

    # Redirect all other HTTP requests to HTTPS
    location / {
        return 301 https://regranbill.online$request_uri;
    }
}

# Redirect www to non-www (HTTPS)
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

# Main HTTPS server
server {
    listen 443 ssl http2;
    listen [::]:443 ssl http2;
    server_name regranbill.online;

    ssl_certificate /etc/letsencrypt/live/regranbill.online/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/regranbill.online/privkey.pem;
    include /etc/letsencrypt/options-ssl-nginx.conf;
    ssl_dhparam /etc/letsencrypt/ssl-dhparams.pem;

    # Security headers
    add_header Strict-Transport-Security "max-age=31536000; includeSubDomains" always;
    add_header X-Content-Type-Options "nosniff" always;
    add_header X-Frame-Options "DENY" always;
    add_header X-XSS-Protection "0" always;
    add_header Referrer-Policy "strict-origin-when-cross-origin" always;

    # Frontend - Angular SPA
    location / {
        root /var/www/regranbill/frontend-build;
        index index.html;
        try_files $uri $uri/ /index.html;

        # Cache static assets
        location ~* \.(js|css|png|jpg|jpeg|gif|ico|svg|woff|woff2|ttf|eot)$ {
            expires 1y;
            add_header Cache-Control "public, immutable";
        }
    }

    # Backend API
    location /api/ {
        proxy_pass http://localhost:5105;
        proxy_http_version 1.1;

        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_set_header Connection keep-alive;

        proxy_pass_request_headers on;

        proxy_connect_timeout 60s;
        proxy_send_timeout 60s;
        proxy_read_timeout 60s;

        proxy_buffering on;
        proxy_buffer_size 4k;
        proxy_buffers 8 4k;
    }

    gzip on;
    gzip_vary on;
    gzip_min_length 1024;
    gzip_types text/plain text/css text/xml text/javascript application/javascript application/json application/xml;
}
```

```bash
nginx -t && systemctl reload nginx
```

Verify auto-renewal covers both certificates:

```bash
certbot certificates
# Should list: tradeledger.cloud AND regranbill.online

certbot renew --dry-run
```

---

## 9. Systemd Service

### 9.1 Create Service File

```bash
nano /etc/systemd/system/regranbill-backend.service
```

Paste:

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

# Environment
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=ASPNETCORE_URLS=http://0.0.0.0:5105
Environment=DOTNET_PRINT_TELEMETRY_MESSAGE=false

# Security hardening
NoNewPrivileges=true
PrivateTmp=true

# Logging
StandardOutput=journal
StandardError=journal
SyslogIdentifier=regranbill-backend

[Install]
WantedBy=multi-user.target
```

### 9.2 Enable and Start

```bash
systemctl daemon-reload
systemctl enable regranbill-backend
systemctl start regranbill-backend
systemctl status regranbill-backend
```

### 9.3 Service Management

```bash
# Start / Stop / Restart
systemctl start regranbill-backend
systemctl stop regranbill-backend
systemctl restart regranbill-backend

# Live logs
journalctl -u regranbill-backend -f

# Last 50 lines
journalctl -u regranbill-backend -n 50
```

---

## 10. Verify Everything Works

### Check Both Services are Running

```bash
systemctl status pureledger-backend regranbill-backend
# Both should show: active (running)
```

### Check Ports

```bash
ss -tlnp | grep dotnet
# Should show two processes:
#   *:5104   ← PureLedger
#   *:5105   ← ReGranBill
```

### Check Nginx Handles Both Domains

```bash
nginx -t
# nginx: configuration file /etc/nginx/nginx.conf test is successful

# Test HTTP redirect to HTTPS
curl -I http://regranbill.online
# Expect: 301 redirect to https://regranbill.online

# Test API responds
curl https://regranbill.online/api/
# Expect: some JSON response (401 or 404, not 502)

# Confirm PureLedger still works
curl -I https://tradeledger.cloud
# Expect: 200 OK
```

### Check Nginx Sites

```bash
ls -la /etc/nginx/sites-enabled/
# pureledger.conf -> /etc/nginx/sites-available/pureledger.conf
# regranbill.conf -> /etc/nginx/sites-available/regranbill.conf
```

### Check SSL Certificates

```bash
certbot certificates
# Should list both: tradeledger.cloud and regranbill.online
```

---

## 11. Update Deployment Script

Save this as `/var/www/regranbill/deploy.sh` for future updates:

```bash
nano /var/www/regranbill/deploy.sh
```

```bash
#!/bin/bash
# ReGranBill update deployment script
# Run as root: bash /var/www/regranbill/deploy.sh

set -e

BACKEND_SOURCE="/var/www/regranbill/backend-source/ReGranBill/ReGranBill.Server"
BACKEND_PUBLISH="/var/www/regranbill/backend-publish"
FRONTEND_SOURCE="/var/www/regranbill/backend-source/ReGranBill/regranbill.client"
FRONTEND_PUBLISH="/var/www/regranbill/frontend-build"

echo "=== Deploying ReGranBill Update ==="

# 1. Pull latest code
echo "--- Pulling latest code ---"
cd /var/www/regranbill/backend-source
git pull origin main

# 2. Backup current publish
echo "--- Backing up current build ---"
cp -r "$BACKEND_PUBLISH" "${BACKEND_PUBLISH}.bak" 2>/dev/null || true
cp -r "$FRONTEND_PUBLISH" "${FRONTEND_PUBLISH}.bak" 2>/dev/null || true

# 3. Build backend
echo "--- Building backend ---"
cd "$BACKEND_SOURCE"
dotnet publish -c Release -o "$BACKEND_PUBLISH"

# 4. Restore .env (publish may overwrite it - keep the original)
# The .env is preserved because dotnet publish does not touch it
# but double-check it exists:
if [ ! -f "$BACKEND_PUBLISH/.env" ]; then
    echo "ERROR: .env file missing from $BACKEND_PUBLISH"
    echo "Restore it manually before starting the service."
    exit 1
fi

# Re-secure .env permissions after publish
chown www-data:www-data "$BACKEND_PUBLISH/.env"
chmod 600 "$BACKEND_PUBLISH/.env"

# 5. Build frontend
echo "--- Building frontend ---"
cd "$FRONTEND_SOURCE"
npm install
npm run build
rm -rf "$FRONTEND_PUBLISH"/*
cp -r dist/regranbill.client/browser/* "$FRONTEND_PUBLISH/"

# 6. Set permissions
echo "--- Setting permissions ---"
chown -R www-data:www-data /var/www/regranbill/
chmod -R 755 "$BACKEND_PUBLISH"
chmod -R 755 "$FRONTEND_PUBLISH"
chmod 600 "$BACKEND_PUBLISH/.env"

# 7. Restart backend (does NOT restart pureledger-backend)
echo "--- Restarting ReGranBill backend ---"
systemctl restart regranbill-backend

# 8. Verify
sleep 3
systemctl status regranbill-backend --no-pager

echo "=== Deployment complete ==="
```

```bash
chmod +x /var/www/regranbill/deploy.sh
```

### Quick Commands Reference

```bash
# Backend only update
cd /var/www/regranbill/backend-source/ReGranBill/ReGranBill.Server && \
dotnet publish -c Release -o /var/www/regranbill/backend-publish && \
systemctl restart regranbill-backend

# Frontend only update (from local machine)
# Build locally, then:
scp -r dist/regranbill.client/browser/* root@YOUR_VPS_IP:/var/www/regranbill/frontend-build/

# Check all services
systemctl status nginx pureledger-backend regranbill-backend postgresql

# Watch all logs simultaneously
journalctl -u regranbill-backend -u pureledger-backend -u nginx -f
```

---

## 12. Troubleshooting

### Backend Not Starting

```bash
journalctl -u regranbill-backend -n 50

# Common causes:
# - .env file missing or wrong path
# - Wrong DB password in .env
# - Port 5105 already in use
# - Missing .NET 10 runtime

# Check port conflict
ss -tlnp | grep 5105

# Test manually (run as www-data to match service user)
cd /var/www/regranbill/backend-publish
sudo -u www-data dotnet ReGranBill.Server.dll
```

### 502 Bad Gateway on regranbill.online

```bash
# Is the backend running?
systemctl status regranbill-backend

# Is it actually listening on 5105?
ss -tlnp | grep 5105

# Check nginx error log
tail -50 /var/log/nginx/error.log
```

### CORS Errors in Browser

1. Check `appsettings.Production.json` has the correct `AllowedOrigins` (both http and https)
2. Restart backend: `systemctl restart regranbill-backend`
3. Confirm the request is hitting the backend: check browser Network tab for the actual response

### SSL Certificate Not Working

```bash
# Check certificate status
certbot certificates

# Check if regranbill.online cert is present
ls /etc/letsencrypt/live/
# Should show: regranbill.online/ and tradeledger.cloud/

# Re-issue if missing
certbot --nginx -d regranbill.online -d www.regranbill.online

# Test renewal (safe - dry run only)
certbot renew --dry-run
```

### PureLedger Broke After This Deployment

If PureLedger stops working, the nginx config is the most likely cause.

```bash
# Check nginx config is valid
nginx -t

# Check PureLedger service is still running
systemctl status pureledger-backend

# Verify pureledger nginx config is still enabled and unchanged
cat /etc/nginx/sites-enabled/pureledger.conf

# Reload nginx
systemctl reload nginx
```

### Log Locations

| Log | Command |
|---|---|
| ReGranBill backend | `journalctl -u regranbill-backend -f` |
| PureLedger backend | `journalctl -u pureledger-backend -f` |
| Nginx access | `/var/log/nginx/access.log` |
| Nginx error | `/var/log/nginx/error.log` |
| PostgreSQL | `/var/log/postgresql/postgresql-14-main.log` |
| Certbot | `/var/log/letsencrypt/letsencrypt.log` |

### Debug Mode (Temporary)

To run ReGranBill backend temporarily with verbose logging:

```bash
systemctl stop regranbill-backend
cd /var/www/regranbill/backend-publish
ASPNETCORE_ENVIRONMENT=Development dotnet ReGranBill.Server.dll
# Ctrl+C when done, then:
systemctl start regranbill-backend
```

---

## Deployment Summary Checklist

Use this checklist for the initial deployment:

- [ ] DNS A records for `regranbill.online` and `www.regranbill.online` point to VPS IP
- [ ] .NET 10 runtime installed (`dotnet --list-runtimes` shows 10.x)
- [ ] PostgreSQL database `regranbilldb` and user `regranbilluser` created
- [ ] `/var/www/regranbill/` directory structure created
- [ ] Backend built and published to `/var/www/regranbill/backend-publish/`
- [ ] `.env` file created at `/var/www/regranbill/backend-publish/.env` with correct secrets
- [ ] `appsettings.Production.json` created with CORS origins
- [ ] EF Core migrations applied to `regranbilldb`
- [ ] Angular frontend built and copied to `/var/www/regranbill/frontend-build/`
- [ ] `/etc/nginx/sites-available/regranbill.conf` created (HTTP config)
- [ ] Symlinked to `/etc/nginx/sites-enabled/regranbill.conf`
- [ ] `nginx -t` passes, `systemctl reload nginx` done
- [ ] SSL certificate issued via `certbot --nginx -d regranbill.online -d www.regranbill.online`
- [ ] Nginx config updated to HTTPS version, reloaded
- [ ] `/etc/systemd/system/regranbill-backend.service` created
- [ ] Service enabled and started (`systemctl enable --now regranbill-backend`)
- [ ] Both services running: `systemctl status pureledger-backend regranbill-backend`
- [ ] Both ports active: `ss -tlnp | grep dotnet` shows 5104 and 5105
- [ ] `https://regranbill.online` loads the Angular app
- [ ] `https://tradeledger.cloud` still works (PureLedger unaffected)
