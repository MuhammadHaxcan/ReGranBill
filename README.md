# ReGranBill

Angular + ASP.NET Core accounting app for delivery challans, purchase vouchers, cash/journal vouchers, and reporting.

## Runtime config

The server expects its database and JWT secrets from environment variables or the repo-root `.env` file.

Required keys:

- `ConnectionStrings__DefaultConnection`
- `JwtSettings__SecretKey`
- `JwtSettings__Issuer`
- `JwtSettings__Audience`
- `JwtSettings__ExpiryInHours`

Use [.env.example](/Users/Claude/Desktop/ReGranBill/ReGranBill/.env.example) as the template for local development.
