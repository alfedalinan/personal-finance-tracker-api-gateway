# Personal Finance Tracker API Gateway - YARP Usage Guide

This guide explains how to use the API Gateway with YARP (Yet Another Reverse Proxy), the configured endpoints, routing rules, path transformations, and environment-specific backend mappings.

---

## 1. Overview of YARP Routes & Endpoints

The API Gateway acts as topological single entry point for client requests, routing traffic to downstream microservices based on URL path prefixes.

### A. Authentication & Identity Service (`/auth/*`)
- **Gateway Prefix:** `/auth/{**catch-all}`
- **Cluster ID:** `identity-cluster`
- **Path Transform:** `PathRemovePrefix: "/auth"` (removes `/auth` before forwarding)
- **Rate Limit Policy:** `strict-write-auth` (stricter limits for authentication endpoints)

### B. Core Finance / Transaction API (`/api/*`)
- **Gateway Prefix:** `/api/{**catch-all}`
- **Cluster ID:** `finance-cluster`
- **Path Transform:** `PathRemovePrefix: "/api"` (removes `/api` before forwarding)
- **Authorization Policy:** `RequireFinanceRead` (requires `finance.read` scope and role `FinanceReader` or `Admin`)
- **Rate Limit Policy:** `standard-read`

---

## 2. Summary Table of Endpoints

| Client Request to Gateway | Path Transformation | Forwarded to Downstream Backend | Target Cluster |
| :--- | :--- | :--- | :--- |
| `https://<gateway>/auth/login` | Strips `/auth` | `<backend-address>/login` | `identity-cluster` |
| `https://<gateway>/api/transactions` | Strips `/api` | `<backend-address>/transactions` | `finance-cluster` |

---

## 3. Environment-Specific Backend Destinations

The backend destination addresses depend on your active ASP.NET Core environment (`ASPNETCORE_ENVIRONMENT`).

### Development (`appsettings.Development.json`)
When running locally (`dotnet run`):
- **Identity Service (`identity-cluster`):**
  - Destination Address: `https://localhost:5001`
  - Example: `GET https://localhost:5000/auth/user/profile` → `GET https://localhost:5001/user/profile`
- **Finance Service (`finance-cluster`):**
  - Destination Address: `https://localhost:5002`
  - Example: `GET https://localhost:5000/api/transactions` → `GET https://localhost:5002/transactions`

### Production (`appsettings.json`)
In production environments:
- **Identity Service (`identity-cluster`):**
  - Destination Address: `https://auth.example.com`
  - Example: `GET https://gateway.example.com/auth/user/profile` → `GET https://auth.example.com/user/profile`
- **Finance Service (`finance-cluster`):**
  - Destination Address: `https://api.example.com`
  - Example: `GET https://gateway.example.com/api/transactions` → `GET https://api.example.com/transactions`

---

## 4. Gateway Security & Policies

1. **JWT Authentication:** Incoming requests to protected routes must include a valid Bearer token in the `Authorization` header.
2. **Authorization:** Checked via custom scope and role handlers (`ScopeHandler`).
3. **Rate Limiting:** Protects downstream services against abuse per route policy.
4. **Correlation IDs & Sanitization:** Automatically attaches correlation IDs (`X-Correlation-ID`) and sanitizes headers and telemetry.
