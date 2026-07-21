# Student Complaint Portal - Complete System

## Overview
Full-stack Student Complaint Portal with REST API, real-time SignalR messaging, and MVC/Blazor UI. Built with .NET 8, ASP.NET Core, Entity Framework Core, ASP.NET Core Identity, and SignalR.

**Phase 1**: Domain models, database, repository/service layers, REST API with dual authentication, comprehensive testing  
**Phase 2**: SignalR real-time messaging, file upload (photo/video/voice), notifications system  
**Phase 3**: MVC/Blazor Server UI with live chat, authentication, role-based dashboards

## Technology Stack

- **.NET 8** - Target framework
- **ASP.NET Core MVC + Web API** - Web framework with dual UI/API support
- **Blazor Server** - Real-time interactive chat component
- **SignalR** - Real-time bidirectional communication
- **Entity Framework Core 8.0** - ORM with code-first migrations
- **SQL Server LocalDB** - Development database
- **ASP.NET Core Identity** - User authentication and management
- **JWT Bearer + Cookie Authentication** - Dual authentication scheme
- **Swashbuckle/Swagger** - API documentation and testing UI
- **xUnit** - Unit and integration testing framework
- **Moq** - Mocking framework for unit tests
- **SQLite** - In-memory database for integration tests

## Solution Structure

```
StudentComplaintPortal.sln
├── src/
│   ├── StudentComplaintPortal.Domain/        # Domain entities and enums
│   ├── StudentComplaintPortal.Data/          # DbContext, repositories, UnitOfWork, migrations
│   ├── StudentComplaintPortal.Application/   # Services, DTOs, business logic
│   │   └── Services/FileStorage/             # File upload abstraction
│   └── StudentComplaintPortal.Web/           # ASP.NET Core host (API + UI)
│       ├── Controllers/Api/                   # REST API controllers
│       ├── Controllers/Mvc/                   # Phase 3: MVC controllers
│       ├── Views/                             # Phase 3: Razor views
│       ├── Components/                        # Phase 3: Blazor components
│       ├── Hubs/                              # SignalR ChatHub
│       ├── Services/                          # LocalDiskFileStorageService, SignalRNotificationPushService
│       ├── wwwroot/uploads/                   # Uploaded files storage
│       ├── wwwroot/js/                        # Phase 3: JavaScript modules
│       └── wwwroot/css/                       # Phase 3: Stylesheets
├── tests/
│   ├── StudentComplaintPortal.UnitTests/     # 19 unit tests
│   └── StudentComplaintPortal.IntegrationTests/ # 19 integration tests
├── tools/
│   └── archive/                               # Phase 2 throwaway test client
├── postman/
│   └── sample-files/                          # Sample files for upload testing
├── postman_collection.json                    # Complete API collection
└── postman_environment.json                   # Environment variables
```

## Getting Started

### Prerequisites

- .NET 8 SDK
- SQL Server LocalDB (included with Visual Studio or SQL Server Express)
- A modern web browser (Chrome, Edge, Firefox)

### Quick Start

1. **Restore dependencies and build**:
   ```bash
   dotnet restore
   dotnet build
   ```

2. **Set up the database**:
   ```bash
   dotnet ef database update --project src/StudentComplaintPortal.Data --startup-project src/StudentComplaintPortal.Web
   ```

3. **Run the application**:
   ```bash
   dotnet run --project src/StudentComplaintPortal.Web
   ```

4. **Access the application**:
   - **Web UI**: https://localhost:7001/Account/Login
   - **Swagger API**: https://localhost:7001/swagger

### Test Accounts

Two test accounts are automatically seeded on first run:

| Email              | Password      | Role    |
|--------------------|---------------|---------|
| student@test.com   | Student123!   | Student |
| admin@test.com     | Admin123!     | Admin   |

## Database Configuration

**SQL Server LocalDB** is configured as the development database:
- **Connection String**: `Server=(localdb)\\mssqllocaldb;Database=StudentComplaintPortalDb;Trusted_Connection=true;TrustServerCertificate=true`
- **Location**: Defined in `src/StudentComplaintPortal.Web/appsettings.json`

To change the database provider, update the connection string and modify the `UseSqlServer` call in `Program.cs`.

## Domain Model

### Core Entities

**AppUser** (extends IdentityUser)
- FullName, Role (Student/Admin), CreatedAt
- Navigation: Complaints, Messages, Notifications

**Complaint**
- Title, Description, Category (Academic/Hostel/Administrative/Other)
- Status (Open/InProgress/Resolved/Closed)
- StudentId (FK to AppUser), CreatedAt, UpdatedAt
- Navigation: Student, Messages

**Message**
- ComplaintId (FK), SenderId (FK to AppUser)
- Content (nullable - supports attachment-only messages)
- SentAt, IsRead
- Navigation: Complaint, Sender, Attachments

**Attachment**
- MessageId (FK), FileUrl, FileType (Photo/Video/VoiceNote)
- FileSizeBytes, UploadedAt
- Navigation: Message

**Notification**
- UserId (FK), Message, IsRead, CreatedAt
- Navigation: User

### Architecture

```
┌─────────────────────────────────────────────────────────┐
│                    Presentation Layer                    │
│  ┌────────────────────┐      ┌────────────────────────┐ │
│  │   MVC Controllers  │      │   API Controllers      │ │
│  │   Razor Views      │      │   (REST Endpoints)     │ │
│  │   Blazor Components│      └────────────────────────┘ │
│  └────────────────────┘                                  │
│           │                           │                  │
│           └───────────┬───────────────┘                  │
│                       │                                  │
├───────────────────────┼──────────────────────────────────┤
│                       ▼                                  │
│              Application Layer                           │
│  ┌──────────────────────────────────────────────────┐   │
│  │  Services (IComplaintService, IMessageService,   │   │
│  │  INotificationService, IAttachmentService)       │   │
│  │  DTOs, Business Logic                            │   │
│  └──────────────────────────────────────────────────┘   │
│                       │                                  │
├───────────────────────┼──────────────────────────────────┤
│                       ▼                                  │
│               Data Access Layer                          │
│  ┌──────────────────────────────────────────────────┐   │
│  │  Repositories, UnitOfWork, DbContext             │   │
│  └──────────────────────────────────────────────────┘   │
│                       │                                  │
├───────────────────────┼──────────────────────────────────┤
│                       ▼                                  │
│                 Domain Layer                             │
│  ┌──────────────────────────────────────────────────┐   │
│  │  Entities, Enums, Domain Logic                   │   │
│  └──────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────┘

            Real-time Communication (SignalR)
┌──────────────────────────────────────────────────────────┐
│  ChatHub (/hubs/chat)                                    │
│  - JoinComplaintGroup, SendMessage                       │
│  - ReceiveMessage, ReceiveNotification events            │
└──────────────────────────────────────────────────────────┘
```

## Authentication

### Dual Authentication Scheme

Both authentication methods work simultaneously on every protected endpoint:

1. **JWT Bearer** - For API clients (Postman, mobile apps) and SignalR
   - Token returned on login at `POST /api/v1/auth/login`
   - Use `Authorization: Bearer <token>` header for REST API calls
   - Use query string `?access_token=<token>` for SignalR WebSocket connections

2. **Cookie Authentication** - For web UI (browsers)
   - Cookie set automatically on login via MVC `/Account/Login`
   - HttpOnly, Secure, SameSite=Strict
   - 7-day expiration with sliding renewal

### SignalR Authentication

JWT tokens for SignalR are read from the `access_token` query parameter specifically for `/hubs/chat` connections. This is the standard SignalR JWT authentication pattern.

Alternatively, browser-based Blazor components use cookie authentication automatically when connecting to the hub (same-origin).

## Features by Phase

### Phase 1: Core API & Foundation
- ✅ Domain models with Entity Framework Core
- ✅ Repository pattern with UnitOfWork
- ✅ Service layer with DTOs
- ✅ REST API endpoints for complaints and messages
- ✅ Dual authentication (JWT + Cookie)
- ✅ ASP.NET Core Identity integration
- ✅ Swagger/OpenAPI documentation
- ✅ 11 unit tests + 6 integration tests

### Phase 2: Real-time & File Upload
- ✅ SignalR ChatHub with group-based messaging
- ✅ Real-time notifications system
- ✅ File upload API (photos, videos, voice notes)
- ✅ Local disk file storage with validation
- ✅ Attachment management
- ✅ 8 additional unit tests + 6 integration tests
- ✅ Postman collection with all endpoints

### Phase 3: MVC/Blazor UI
- ✅ MVC authentication (login/register/logout)
- ✅ Role-based dashboards (Student/Admin)
- ✅ Complaint creation and viewing
- ✅ Admin status management
- ✅ Blazor Server `ComplaintChat` component with:
  - Real-time messaging via SignalR
  - Photo/video upload with preview
  - Voice note recording (MediaRecorder API)
  - Message history
  - Live delivery and read receipts
- ✅ Notification bell with live count updates
- ✅ Responsive Bootstrap UI
- ✅ 7 additional integration tests for MVC controllers

## Using the Application

### Web UI Walkthrough

1. **Login**: Navigate to `/Account/Login` and use one of the test accounts
2. **Student Dashboard** (`/Dashboard`):
   - View your submitted complaints
   - Click "New Complaint" to create one
   - Click "View & Chat" on any complaint to open the conversation
3. **Admin Dashboard** (`/Dashboard`):
   - View all complaints from all students
   - Filter by status (Open/InProgress/Resolved/Closed)
   - Click "View" to see complaint details and chat
   - Update complaint status via dropdown
4. **Complaint Detail** (`/Complaint/Detail/{id}`):
   - View complaint metadata (title, description, category, status)
   - Send text messages in real-time
   - Upload photos/videos with inline preview
   - Record voice notes using the microphone button
   - See live updates when the other party sends messages
5. **Notifications**: Click the bell icon in the navbar to see unread notification count

### REST API Usage

The full REST API remains accessible alongside the web UI:

#### Authentication
```bash
POST /api/v1/auth/login
POST /api/v1/auth/register
```

#### Complaints
```bash
POST   /api/v1/complaints              # Create complaint (Student only)
GET    /api/v1/complaints              # List all (Admin) or own (Student)
GET    /api/v1/complaints/{id}         # Get complaint details
PUT    /api/v1/complaints/{id}/status  # Update status (Admin only)
```

#### Messages
```bash
GET    /api/v1/messages/complaint/{complaintId}  # Get conversation
POST   /api/v1/messages                          # Send message (deprecated - use SignalR)
```

#### Attachments
```bash
POST   /api/v1/complaints/{id}/attachments       # Upload file
Content-Type: multipart/form-data
Body: file (binary), fileType (Photo/Video/VoiceNote)
```

#### Notifications
```bash
GET    /api/v1/notifications        # Get user's notifications
PUT    /api/v1/notifications/{id}   # Mark as read
```

### SignalR Real-time Events

#### Connect
```javascript
const connection = new signalR.HubConnectionBuilder()
    .withUrl("/hubs/chat?access_token=YOUR_JWT_TOKEN")
    .build();

await connection.start();
```

#### Join a complaint conversation
```javascript
await connection.invoke("JoinComplaintGroup", complaintId);
```

#### Send a message
```javascript
await connection.invoke("SendMessage", complaintId, "Hello!");
```

#### Receive messages
```javascript
connection.on("ReceiveMessage", (message) => {
    console.log(`${message.senderName}: ${message.content}`);
});
```

#### Receive notifications
```javascript
connection.on("ReceiveNotification", (notification) => {
    console.log(notification.message);
});
```

## Testing

### Run All Tests
```bash
dotnet test
```

### Run Unit Tests Only
```bash
dotnet test tests/StudentComplaintPortal.UnitTests
```

### Run Integration Tests Only
```bash
dotnet test tests/StudentComplaintPortal.IntegrationTests
```

### Test Coverage

| Project                | Unit Tests | Integration Tests | Total |
|------------------------|------------|-------------------|-------|
| Phase 1 (API Core)     | 11         | 6                 | 17    |
| Phase 2 (SignalR)      | 8          | 6                 | 14    |
| Phase 3 (UI)           | 0          | 7                 | 7     |
| **Total**              | **19**     | **19**            | **38**|

### Manual End-to-End Verification

Open two browser windows (or one normal + one incognito):

1. **Window 1**: Log in as `student@test.com`
2. **Window 2**: Log in as `admin@test.com`
3. **As Student**: Create a new complaint → verify it appears in Student dashboard
4. **As Admin**: Verify complaint appears in Admin dashboard (with or without refresh)
5. **As Admin**: Open the complaint detail page
6. **As Admin**: Send a text message → verify it appears live in Student's open chat
7. **As Student**: Reply with a text message → verify it appears live for Admin
8. **As Student**: Upload a photo → verify it renders inline for both parties live
9. **As Admin**: Upload a video → verify it renders with playback controls for both parties
10. **As either party**: Record and send a voice note → verify audio player appears for the other party
11. **As Admin**: Change complaint status → verify Student sees live notification (bell badge) and status updates
12. **Verify**: Student cannot access another student's complaint URL directly (403/Forbid)
13. **Verify**: Postman collection works against running app (parallel to browser sessions)

## Postman Collection

A complete Postman collection with all API endpoints is provided in `postman_collection.json`.

### Import Steps
1. Open Postman
2. Click "Import" → Select `postman_collection.json`
3. Import `postman_environment.json` as well
4. Select "Student Complaint Portal Env" from environment dropdown

### Collection Structure
- **Auth**: Login, Register
- **Complaints**: CRUD operations
- **Messages**: Send and retrieve messages
- **Attachments**: Upload photos/videos/voice notes (samples in `postman/sample-files/`)
- **Notifications**: Retrieve and mark as read
- **SignalR**: Connection test instructions

### Running the Collection
1. **Login** as Student → JWT token auto-saved to environment
2. **Create Complaint** → complaintId auto-saved
3. **Send Message** → messageId auto-saved
4. **Upload Attachment** → select file from `postman/sample-files/`
5. All subsequent requests use saved variables automatically

## File Upload

Attachments are stored in `src/StudentComplaintPortal.Web/wwwroot/uploads/` and served as static files.

### Supported File Types
- **Photo**: image/* (JPG, PNG, GIF, etc.)
- **Video**: video/* (MP4, WebM, etc.)
- **VoiceNote**: audio/* (WebM, WAV, MP3, etc.)

### Limits
- Max file size: 50 MB (configurable in Blazor component)
- Files are validated and stored with unique names

### Sample Files
Test files are provided in `postman/sample-files/`:
- `sample-photo.jpg`
- `sample-video.mp4`

Voice notes are recorded live via the browser's MediaRecorder API in the UI.

## Project Highlights

### Clean Architecture
- Clear separation of concerns across layers
- Domain entities independent of infrastructure
- Dependency inversion via interfaces

### Dual Authentication
- Single codebase supports both web UI and external API clients
- Cookie auth for browsers, JWT for programmatic access
- Same authorization logic enforced for both schemes

### Real-time Communication
- SignalR provides sub-second message delivery
- Group-based messaging ensures privacy (Students only see their own complaints)
- Live notifications for status changes

### Comprehensive Testing
- 38 automated tests covering services, repositories, controllers, and hubs
- Integration tests use in-memory SQLite for isolation
- Manual verification checklist for UI flows

### Production-Ready Patterns
- Repository + UnitOfWork for transactional integrity
- Service layer for business logic
- DTOs for API contract stability
- File storage abstraction for easy cloud migration

## Troubleshooting

### Database Connection Errors
- Ensure SQL Server LocalDB is installed
- Run `dotnet ef database update` from the solution root
- Check connection string in `appsettings.json`

### SignalR Connection Fails
- Verify HTTPS is enabled (required for secure cookies)
- Check browser console for WebSocket errors
- Ensure JWT token is valid (not expired)

### File Upload Fails
- Check `wwwroot/uploads/` directory exists and is writable
- Verify file size is under 50MB
- Confirm file type matches allowed types

### Blazor Component Not Rendering
- Ensure `_framework/blazor.server.js` is loaded
- Check browser console for JavaScript errors
- Verify SignalR hub is mapped in `Program.cs`

## Future Enhancements

- **Email Notifications**: Send email when complaint status changes
- **File Upload to Cloud**: Replace local disk storage with Azure Blob Storage
- **Advanced Search**: Filter complaints by date range, keyword, category
- **Complaint Analytics**: Dashboard with charts and statistics
- **Mobile App**: Native iOS/Android app consuming the REST API
- **Admin Bulk Actions**: Assign complaints, bulk status updates
- **Audit Logging**: Track all changes to complaints and messages

## License

This is a demonstration project for educational purposes.

## Authors

Built across three phases:
- **Phase 1**: REST API foundation, authentication, testing
- **Phase 2**: Real-time SignalR messaging, file uploads
- **Phase 3**: MVC/Blazor UI with live chat

**Final Delivery Date**: July 20, 2026
