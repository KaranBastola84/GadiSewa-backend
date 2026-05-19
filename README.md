# 🚗 GadiSewa Backend API

A comprehensive, production-ready backend for an automotive parts management and rental system built with **.NET 9** and **Clean Architecture** principles.

---

## 📋 Table of Contents

- [Overview](#overview)
- [Tech Stack](#tech-stack)
- [Project Structure](#project-structure)
- [Features](#features)
- [Prerequisites](#prerequisites)
- [Installation & Setup](#installation--setup)
- [Configuration](#configuration)
- [Running the Application](#running-the-application)
- [API Documentation](#api-documentation)
- [Database Schema](#database-schema)
- [Authentication & Authorization](#authentication--authorization)
- [Background Jobs](#background-jobs)
- [Real-time Notifications](#real-time-notifications)
- [Error Handling](#error-handling)
- [Logging](#logging)
- [Project Architecture](#project-architecture)
- [Key Services](#key-services)
- [Contributing](#contributing)
- [License](#license)

---

## Overview

**GadiSewa** is a modern backend API designed to manage:

- 🛠️ Automotive parts inventory and requests
- 📅 Vehicle appointment scheduling
- 👥 Multi-role user management (Admin, Staff, Customer, Vendor)
- 💳 Credit payments and invoice management
- 📧 Email notifications and verification
- 🔄 Real-time notifications via WebSocket
- ⏰ Recurring background jobs for automation

The backend is built following **Clean Architecture** principles with layered separation of concerns, dependency injection, and comprehensive error handling.

---

## Tech Stack

| Component                   | Technology                                   |
| --------------------------- | -------------------------------------------- |
| **Framework**               | .NET 9 (C# 13)                               |
| **Architecture**            | Clean Architecture with Dependency Injection |
| **Database**                | PostgreSQL                                   |
| **ORM**                     | Entity Framework Core 9.0                    |
| **Authentication**          | JWT (JSON Web Tokens)                        |
| **Background Jobs**         | Hangfire with PostgreSQL Storage             |
| **Real-time Communication** | SignalR                                      |
| **Validation**              | FluentValidation                             |
| **Logging**                 | Serilog with JSON formatting                 |
| **API Documentation**       | Swagger (Swashbuckle)                        |
| **Email Service**           | SMTP (Gmail/Custom)                          |

---

## Project Structure

```
GadiSewa-Backend/
├── GadiSewa.API/                 # API Layer (Entry point)
│   ├── Controllers/              # API Endpoints
│   ├── Middleware/               # Custom middleware (error handling, logging)
│   ├── Hubs/                     # SignalR hubs for real-time features
│   ├── Hangfire/                 # Background job configurations
│   ├── Extensions/               # Service extensions
│   ├── Program.cs                # Application entry point & configuration
│   ├── appsettings.json          # Configuration file
│   └── GadiSewa.API.csproj       # Project file
│
├── GadiSewa.Application/         # Business Logic Layer
│   ├── Services/                 # Business logic implementations
│   ├── Interfaces/               # Service contracts
│   ├── DTOs/                     # Data Transfer Objects
│   ├── Validators/               # FluentValidation validators
│   ├── Common/                   # Shared utilities & constants
│   ├── DependencyInjection.cs    # Service registration
│   └── GadiSewa.Application.csproj
│
├── GadiSewa.Domain/              # Core Domain Layer
│   ├── Entities/                 # Domain models (User, Vehicle, Appointment, etc.)
│   ├── Enums/                    # Enumerations (UserRole, AppointmentStatus, etc.)
│   ├── Interfaces/               # Repository contracts
│   ├── Common/                   # Base classes and shared logic
│   └── GadiSewa.Domain.csproj
│
├── GadiSewa.Infrastructure/      # Data & External Services Layer
│   ├── Persistence/              # DbContext & Migrations
│   ├── Authentication/           # JWT token handling
│   ├── Communication/            # Email service implementation
│   ├── BackgroundJobs/           # Hangfire job implementations
│   ├── Security/                 # Security utilities
│   ├── DependencyInjection.cs    # Infrastructure service registration
│   └── GadiSewa.Infrastructure.csproj
│
└── GadiSewa.sln                  # Solution file
```

---

## Features

### 🔐 Authentication & Authorization

- **JWT-based** authentication with configurable expiry
- **Role-based access control** (RBAC):
  - 👑 Admin
  - 👔 Staff
  - 👤 Customer
  - 🏭 Vendor
- Email verification for new accounts
- Password reset functionality with secure tokens

### 🛒 Inventory Management

- **Parts Management**: Create, update, delete, and search automotive parts
- **Part Requests**: Handle customer requests for unavailable parts
- **Stock Tracking**: Real-time inventory updates
- **Low Stock Alerts**: Automated notifications for low inventory (Hangfire job)

### 📅 Appointment System

- Schedule vehicle service appointments
- Track appointment status
- Staff assignment to appointments
- Customer notifications

### 💳 Financial Management

- **Purchase Invoices**: Track vendor purchases
- **Sales Invoices**: Record customer transactions
- **Credit Payments**: Customer credit tracking and management
- **Overdue Reminders**: Automated notifications for overdue credits (Hangfire job)

### 🚗 Vehicle Management

- Customer vehicle registration and tracking
- Vehicle details and history

### ⭐ Reviews & Ratings

- Customer reviews for services
- Rating system for service quality

### 📧 Communication

- Email notifications for various events
- SMS integration ready (extensible)
- Real-time WebSocket notifications via SignalR

### ⏰ Background Jobs (Hangfire)

- **Overdue Credit Reminders**: Daily job to notify about overdue credits
- **Low Stock Alerts**: Hourly checks for parts below minimum stock
- Extensible job framework for future automation

### 🔄 Real-time Features

- SignalR hub for live notifications
- Notification logging and tracking
- Multi-connection support

---

## Prerequisites

Ensure you have the following installed:

- **[.NET 9 SDK](https://dotnet.microsoft.com/download)** (or later)
- **[PostgreSQL 12+](https://www.postgresql.org/download/)**
- **[Git](https://git-scm.com/)**
- **[Visual Studio 2022](https://visualstudio.microsoft.com/)** or **[VS Code](https://code.visualstudio.com/)** with C# extension

### Verify Installation

```bash
dotnet --version
psql --version
```

---

## Installation & Setup

### 1. Clone the Repository

```bash
git clone https://github.com/KaranBastola84/GadiSewa-backend.git
cd GadiSewa-backend
```

### 2. Create PostgreSQL Database

```bash
psql -U postgres

# In psql shell:
CREATE DATABASE gadisewa;
\q
```

Alternatively, use **pgAdmin** for GUI-based database creation.

### 3. Restore NuGet Packages

```bash
dotnet restore
```

### 4. Update Database (Run Migrations)

```bash
cd GadiSewa.API
dotnet ef database update --project ../GadiSewa.Infrastructure
```

This will create all necessary tables, schemas, and initial data.

### 5. Verify Setup

```bash
dotnet build
```

If the build succeeds, your setup is complete!

---

## Configuration

### Environment Variables

Create an `appsettings.Development.json` file in the `GadiSewa.API` directory (or modify `appsettings.json`):

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=gadisewa;Username=postgres;Password=your_password"
  },
  "Jwt": {
    "Issuer": "GadiSewa.API",
    "Audience": "GadiSewa.Client",
    "Key": "your-very-long-and-secure-key-at-least-32-characters",
    "ExpiryMinutes": 120
  },
  "Smtp": {
    "Host": "smtp.gmail.com",
    "Port": 587,
    "EnableSsl": true,
    "FromEmail": "your-email@gmail.com",
    "FromName": "GadiSewa",
    "Username": "your-email@gmail.com",
    "Password": "your-app-password"
  },
  "AdminBootstrap": {
    "SetupKey": "your-unique-setup-key-for-admin-creation"
  },
  "Notifications": {
    "OverdueReminderIntervalInDays": 1
  },
  "Cors": {
    "AllowedOrigins": [
      "http://localhost:5173",
      "http://localhost:3000",
      "https://your-frontend-domain.com"
    ]
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
```

### Key Configuration Details

| Setting                               | Description                              | Example                                                                      |
| ------------------------------------- | ---------------------------------------- | ---------------------------------------------------------------------------- |
| `ConnectionStrings:DefaultConnection` | PostgreSQL connection string             | `Host=localhost;Port=5432;Database=gadisewa;Username=postgres;Password=pass` |
| `Jwt:Key`                             | Secret key for JWT signing (keep secure) | 64+ character random string                                                  |
| `Jwt:ExpiryMinutes`                   | Token expiration time in minutes         | `120` (2 hours)                                                              |
| `Smtp:*`                              | Email service configuration              | Gmail SMTP details                                                           |
| `AdminBootstrap:SetupKey`             | One-time admin account creation key      | Random secure key                                                            |
| `Cors:AllowedOrigins`                 | Frontend URLs allowed to access API      | Array of frontend URLs                                                       |

---

## Running the Application

### Development Environment

```bash
cd GadiSewa.API
dotnet run
```

The API will start at `https://localhost:5001` or `http://localhost:5000`

### Production Environment

```bash
dotnet publish -c Release
cd bin/Release/net9.0/publish
dotnet GadiSewa.API.dll
```

### With Docker (Optional)

Create a `Dockerfile`:

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /app
COPY . .
RUN dotnet publish -c Release -o /out

FROM mcr.microsoft.com/dotnet/runtime:9.0
WORKDIR /app
COPY --from=build /out .
ENTRYPOINT ["dotnet", "GadiSewa.API.dll"]
```

Build and run:

```bash
docker build -t gadisewa-api .
docker run -p 5000:80 -e ConnectionStrings__DefaultConnection="..." gadisewa-api
```

---

## API Documentation

### Swagger UI

Once the application is running, access the interactive API documentation:

```
http://localhost:5000/swagger
```

**Features:**

- Browse all available endpoints
- View request/response schemas
- Try out endpoints directly in the UI
- See required authentication headers

### Key Endpoints

#### Authentication

- `POST /api/auth/register` - Register new user
- `POST /api/auth/login` - Login with credentials
- `POST /api/auth/refresh-token` - Refresh JWT token
- `POST /api/auth/verify-email` - Verify email address
- `POST /api/auth/forgot-password` - Request password reset

#### Users

- `GET /api/users` - List all users (Admin only)
- `GET /api/users/{id}` - Get user details
- `PUT /api/users/{id}` - Update user information
- `DELETE /api/users/{id}` - Delete user (Admin only)

#### Vehicles

- `GET /api/vehicles` - List user's vehicles
- `POST /api/vehicles` - Register new vehicle
- `PUT /api/vehicles/{id}` - Update vehicle details
- `DELETE /api/vehicles/{id}` - Delete vehicle

#### Appointments

- `GET /api/appointments` - List appointments
- `POST /api/appointments` - Create new appointment
- `PUT /api/appointments/{id}` - Update appointment
- `PATCH /api/appointments/{id}/status` - Change appointment status

#### Parts

- `GET /api/parts` - List all parts
- `POST /api/parts` - Create new part (Admin/Staff)
- `PUT /api/parts/{id}` - Update part details
- `DELETE /api/parts/{id}` - Delete part (Admin)
- `POST /api/parts/{id}/reorder` - Trigger low stock reorder

#### Invoices

- `GET /api/invoices/sales` - List sales invoices
- `GET /api/invoices/purchase` - List purchase invoices
- `POST /api/invoices` - Create new invoice
- `GET /api/invoices/{id}/details` - Get invoice details with items

#### Reviews

- `GET /api/reviews` - List all reviews
- `POST /api/reviews` - Submit new review
- `PUT /api/reviews/{id}` - Update review

### Authentication Header

Include JWT token in all authenticated requests:

```
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
```

---

## Database Schema

### Core Entities

#### User (Base entity for all user types)

```
- Id (GUID)
- Email (unique)
- FullName
- PhoneNumber
- Role (Admin, Staff, Customer, Vendor)
- IsActive
- CreatedAt
- UpdatedAt
```

#### Customer (Extends User)

```
- UserId (FK)
- Address
- City
- PostalCode
- CreditLimit
- AvailableCredit
```

#### Staff (Extends User)

```
- UserId (FK)
- Department
- Specialization
- HourlyRate
```

#### Vendor (Extends User)

```
- UserId (FK)
- CompanyName
- VendorType
- PaymentTerms
```

#### Vehicle

```
- Id (GUID)
- CustomerId (FK)
- RegistrationNumber
- Make
- Model
- Year
- VIN
- CreatedAt
```

#### Appointment

```
- Id (GUID)
- CustomerId (FK)
- VehicleId (FK)
- StaffId (FK)
- AppointmentDate
- Description
- Status (Scheduled, InProgress, Completed, Cancelled)
- CreatedAt
```

#### Part

```
- Id (GUID)
- PartNumber (unique)
- Name
- Description
- Price
- QuantityInStock
- MinimumStockLevel
- VendorId (FK)
- CreatedAt
```

#### PartRequest

```
- Id (GUID)
- CustomerId (FK)
- PartName
- Quantity
- Status (Pending, Approved, Rejected, Fulfilled)
- CreatedAt
```

#### Invoice (Sales/Purchase)

```
- Id (GUID)
- InvoiceNumber (unique)
- InvoiceDate
- DueDate
- TotalAmount
- TaxAmount
- Status (Draft, Issued, Paid, Overdue, Cancelled)
- Type (Sales, Purchase)
- CreatedAt
```

#### CreditPayment

```
- Id (GUID)
- CustomerId (FK)
- Amount
- PaymentDate
- BalanceBefore
- BalanceAfter
- Status (Pending, Completed, Failed)
- CreatedAt
```

#### Review

```
- Id (GUID)
- CustomerId (FK)
- StaffId (FK)
- Rating (1-5)
- Comment
- ReviewDate
- CreatedAt
```

---

## Authentication & Authorization

### JWT Token Flow

1. **User Registration/Login**
   - User provides credentials
   - Server validates and generates JWT token with claims

2. **Token Payload**

   ```json
   {
     "sub": "user-id",
     "email": "user@example.com",
     "role": "Customer",
     "iat": 1234567890,
     "exp": 1234571490
   }
   ```

3. **Token Validation**
   - Signature verification using secret key
   - Expiry time check
   - Issuer and audience validation
   - Role extraction for authorization

### Role-Based Access Control (RBAC)

```csharp
// Controller level authorization
[Authorize(Policy = "AdminOnly")]
public async Task DeleteUser(Guid id) { ... }

[Authorize(Policy = "StaffOnly")]
public async Task AssignAppointment(Guid id) { ... }

[Authorize(Policy = "CustomerOnly")]
public async Task CreateAppointment(...) { ... }
```

### Available Policies

| Policy           | Roles        | Usage                        |
| ---------------- | ------------ | ---------------------------- |
| `AdminOnly`      | Admin        | Administrative operations    |
| `StaffOnly`      | Staff        | Staff-specific operations    |
| `BackOfficeOnly` | Admin, Staff | Back-office operations       |
| `CustomerOnly`   | Customer     | Customer-specific operations |

### SignalR Authentication

For real-time notifications, JWT token is passed as query parameter:

```javascript
const connection = new signalR.HubConnectionBuilder()
  .withUrl(`https://api.example.com/hubs/notifications?access_token=${token}`)
  .withAutomaticReconnect()
  .build();
```

---

## Background Jobs

### Hangfire Dashboard

Access the Hangfire dashboard (development only):

```
http://localhost:5000/hangfire
```

**View:**

- Job queues and statuses
- Recurring job schedules
- Failed job details
- Job history

### Recurring Jobs

#### 1. Overdue Credit Reminder

- **Schedule**: Daily (configurable)
- **Runs**: `OverdueCreditReminderJob.RunAsync()`
- **Action**: Sends email reminders to customers with overdue credits
- **Configuration**: `Notifications:OverdueReminderIntervalInDays`

#### 2. Low Stock Alert

- **Schedule**: Hourly
- **Runs**: `LowStockAlertJob.RunAsync()`
- **Action**: Checks for parts below minimum stock level and sends notifications to vendors
- **Configuration**: Modify `Program.cs` for custom intervals

### Implementing Custom Jobs

1. Create a job class in `GadiSewa.Infrastructure/BackgroundJobs/`:

```csharp
public class MyCustomJob
{
    private readonly IMyService _service;

    public MyCustomJob(IMyService service)
    {
        _service = service;
    }

    public async Task RunAsync()
    {
        // Job logic
        await _service.DoSomethingAsync();
    }
}
```

2. Register in `Program.cs`:

```csharp
RecurringJob.AddOrUpdate<MyCustomJob>(
    "my-custom-job",
    job => job.RunAsync(),
    Cron.Daily);
```

---

## Real-time Notifications

### SignalR Hub

**Hub Route**: `/hubs/notifications`

### Methods (Server → Client)

```csharp
// Notify single client
await Clients.Client(connectionId).SendAsync("ReceiveNotification", notification);

// Notify all connected clients
await Clients.All.SendAsync("ReceiveNotification", notification);

// Notify specific user
await Clients.User(userId).SendAsync("ReceiveNotification", notification);
```

### Client Implementation (JavaScript)

```javascript
const connection = new signalR.HubConnectionBuilder()
  .withUrl("http://localhost:5000/hubs/notifications", {
    accessTokenFactory: () => localStorage.getItem("token"),
  })
  .withAutomaticReconnect()
  .build();

connection.on("ReceiveNotification", (notification) => {
  console.log("Notification received:", notification);
  // Handle notification in UI
});

connection.start().catch((err) => console.error(err));
```

### Notification Events

- **Appointment Created**: Customer notified when appointment is scheduled
- **Appointment Status Changed**: Real-time status updates
- **Invoice Created**: Email and real-time notification
- **Credit Payment Received**: Immediate notification
- **Part Request Updated**: Status change notifications
- **Low Stock Alert**: Vendor notifications
- **Overdue Credit Reminder**: Customer notifications

---

## Error Handling

### Global Exception Middleware

The `GlobalExceptionMiddleware` catches all unhandled exceptions and returns standardized error responses:

```json
{
  "success": false,
  "message": "An error occurred",
  "statusCode": 400,
  "errors": [
    {
      "code": "VALIDATION_ERROR",
      "message": "Email is required",
      "field": "email"
    }
  ]
}
```

### Common Error Codes

| Code                    | Status | Description                       |
| ----------------------- | ------ | --------------------------------- |
| `VALIDATION_ERROR`      | 400    | Input validation failed           |
| `UNAUTHORIZED`          | 401    | Missing or invalid authentication |
| `FORBIDDEN`             | 403    | Insufficient permissions          |
| `NOT_FOUND`             | 404    | Resource not found                |
| `CONFLICT`              | 409    | Resource already exists           |
| `INTERNAL_SERVER_ERROR` | 500    | Unexpected server error           |

### Exception Handling Example

```csharp
try
{
    var user = await _userService.GetUserByIdAsync(userId);
    if (user == null)
        throw new NotFoundException($"User with ID {userId} not found");

    // Continue...
}
catch (NotFoundException ex)
{
    _logger.LogWarning(ex, "User not found");
    throw; // Caught by GlobalExceptionMiddleware
}
```

---

## Logging

### Serilog Configuration

Logs are configured via Serilog with JSON formatting for easy parsing:

```json
{
  "Timestamp": "2024-01-15T10:30:45.1234567Z",
  "Level": "Information",
  "MessageTemplate": "User {UserId} logged in successfully",
  "Properties": {
    "UserId": "12345",
    "Application": "GadiSewa.API"
  }
}
```

### Log Levels

- **Debug**: Detailed diagnostic information
- **Information**: General informational messages
- **Warning**: Warning events that might indicate problems
- **Error**: Error events with error details
- **Fatal**: Fatal errors causing application shutdown

### Viewing Logs

```bash
# Console output (during development)
dotnet run

# Structured logs in application
var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("Application started");
```

### Best Practices

1. Use structured logging with named properties:

   ```csharp
   _logger.LogInformation("Appointment created for user {UserId} on {Date}", userId, DateTime.UtcNow);
   ```

2. Log exceptions with context:

   ```csharp
   catch (Exception ex)
   {
       _logger.LogError(ex, "Failed to process payment for user {UserId}", userId);
   }
   ```

3. Use appropriate log levels to avoid noise

---

## Project Architecture

### Clean Architecture Principles

```
Presentation (API) ↓
Business Logic (Application) ↓
Core Domain (Domain) ↓
Infrastructure (Data, External Services)
```

### Dependency Direction

```
API → Application → Domain ← Infrastructure
```

**All dependencies point inward** - the Domain layer has no external dependencies.

### Design Patterns Used

1. **Repository Pattern**: Abstracted data access in Infrastructure layer
2. **Dependency Injection**: Loose coupling via IoC container
3. **Service Locator**: Configured services in DependencyInjection.cs
4. **DTO Pattern**: Data Transfer Objects for API contracts
5. **Middleware Pattern**: Custom request/response processing
6. **Observer Pattern**: Event-based notifications via SignalR
7. **Strategy Pattern**: Multiple authentication/validation strategies

### Layer Responsibilities

#### API Layer (GadiSewa.API)

- HTTP request/response handling
- Route definition
- Authentication/Authorization middleware
- Input validation
- Response formatting

#### Application Layer (GadiSewa.Application)

- Business logic implementation
- DTOs and mappers
- Validation rules
- Service interfaces
- Cross-cutting concerns

#### Domain Layer (GadiSewa.Domain)

- Core entities and domain models
- Business rules
- Enums and value objects
- Repository contracts
- No external dependencies

#### Infrastructure Layer (GadiSewa.Infrastructure)

- Database access (EF Core)
- Repository implementations
- External service integrations (Email, Hangfire)
- Security implementations
- Persistence configuration

---

## Key Services

### AuthService

Handles user authentication, JWT token generation, and email verification.

**Methods:**

- `RegisterAsync()` - New user registration
- `LoginAsync()` - User authentication
- `RefreshTokenAsync()` - Token refresh
- `VerifyEmailAsync()` - Email verification

### UserService

Manages user profiles, roles, and account information.

**Methods:**

- `GetUserByIdAsync()` - Fetch user details
- `UpdateUserAsync()` - Update user information
- `ListUsersAsync()` - Get all users (Admin)
- `DeleteUserAsync()` - Delete user account

### AppointmentService

Handles appointment scheduling and management.

**Methods:**

- `CreateAppointmentAsync()` - Schedule new appointment
- `UpdateAppointmentStatusAsync()` - Change appointment status
- `GetAppointmentsByCustomerAsync()` - Retrieve customer appointments
- `AssignStaffAsync()` - Assign staff to appointment

### InvoiceService

Manages sales and purchase invoices.

**Methods:**

- `CreateInvoiceAsync()` - Create new invoice
- `GetInvoiceDetailsAsync()` - Fetch invoice with items
- `UpdateInvoiceStatusAsync()` - Update invoice status
- `CalculateTotalAsync()` - Calculate totals with tax

### PartService

Inventory management for automotive parts.

**Methods:**

- `CreatePartAsync()` - Add new part
- `UpdateStockAsync()` - Update quantity
- `GetLowStockPartsAsync()` - Find parts below minimum
- `SearchPartsAsync()` - Search by name/number

### NotificationService

Handles email and real-time notifications.

**Methods:**

- `SendEmailAsync()` - Send email notifications
- `PublishRealtimeNotificationAsync()` - Push to SignalR clients
- `LogNotificationAsync()` - Audit trail

---

## Contributing

### Setup Development Environment

1. Fork the repository
2. Clone your fork:

   ```bash
   git clone https://github.com/your-username/GadiSewa-backend.git
   cd GadiSewa-backend
   ```

3. Create a feature branch:

   ```bash
   git checkout -b feature/your-feature-name
   ```

4. Make your changes and commit:

   ```bash
   git commit -m "Add your feature description"
   ```

5. Push to your fork:

   ```bash
   git push origin feature/your-feature-name
   ```

6. Create a Pull Request

### Code Style Guidelines

- Use PascalCase for public members
- Use camelCase for private/local variables
- Use `async/await` for asynchronous operations
- Add XML documentation comments for public methods
- Use dependency injection instead of static references
- Write unit tests for new features

### Testing

Run tests with:

```bash
dotnet test
```

---

## License

This project is licensed under the **MIT License**. See the [LICENSE](LICENSE) file for details.

---

## Support & Contact

For issues, questions, or suggestions:

- **GitHub Issues**: [GadiSewa-backend/issues](https://github.com/KaranBastola84/GadiSewa-backend/issues)
- **Email**: [Your Contact Email]
- **Documentation**: [Project Wiki](https://github.com/KaranBastola84/GadiSewa-backend/wiki)

---

## Troubleshooting

### Database Connection Issues

**Error**: `NpgsqlException: Connection refused`

**Solution**:

1. Verify PostgreSQL is running
2. Check connection string in `appsettings.json`
3. Ensure database exists: `psql -U postgres -l | grep gadisewa`

### Migration Failures

**Error**: `Migrations do not match the current model`

**Solution**:

1. Create a new migration: `dotnet ef migrations add MigrationName --project GadiSewa.Infrastructure`
2. Update database: `dotnet ef database update --project GadiSewa.Infrastructure`

### JWT Token Issues

**Error**: `401 Unauthorized`

**Solution**:

1. Verify JWT key in `appsettings.json` is configured
2. Check token expiry time
3. Ensure `Authorization` header is included in requests
4. Validate token format: `Authorization: Bearer {token}`

### CORS Errors

**Error**: `Access to XMLHttpRequest has been blocked by CORS policy`

**Solution**:

1. Add frontend URL to `Cors:AllowedOrigins` in `appsettings.json`
2. Ensure credentials are allowed: `AllowCredentials()` is set
3. Restart the API after configuration changes

### Hangfire Jobs Not Running

**Error**: Jobs appear queued but don't execute

**Solution**:

1. Verify `Hangfire` schema exists in PostgreSQL
2. Check Hangfire dashboard for exceptions
3. Ensure `AddHangfireServer()` is called in `Program.cs`
4. Verify database connection for Hangfire storage

---

## Performance Optimization Tips

1. **Database Indexing**: Add indexes on frequently queried columns
2. **Caching**: Implement caching for read-heavy operations
3. **Async/Await**: Use async operations throughout
4. **Connection Pooling**: Configure optimal pool size in connection string
5. **Query Optimization**: Use `.AsNoTracking()` for read-only queries

---

## Security Best Practices

1. ✅ Keep JWT key long and secure (minimum 32 characters)
2. ✅ Use HTTPS in production
3. ✅ Rotate JWT keys periodically
4. ✅ Implement rate limiting for API endpoints
5. ✅ Validate and sanitize all user inputs
6. ✅ Use parameterized queries (EF Core does this by default)
7. ✅ Keep dependencies updated for security patches
8. ✅ Enable CORS only for trusted origins
9. ✅ Log sensitive operations for audit trail
10. ✅ Use environment variables for secrets, never commit to git

---

## Roadmap

### Future Features

- [ ] Payment gateway integration (Stripe, Khalti)
- [ ] SMS notifications
- [ ] Mobile app support
- [ ] Advanced reporting and analytics
- [ ] AI-based recommendation system
- [ ] Multi-language support
- [ ] Inventory forecasting

---

**Last Updated**: May 2026

**Version**: 1.0.0

---

_Built with ❤️ using .NET 9 and Clean Architecture_
