# WBS Human Resource System (HRMS)

**We Build Stuff (WBS) | Human Resource Management Solution**

---

## 📋 Project Overview

We Build Stuff was struggling with a **legacy paper-based HR system** that resulted in:
- 🔴 Misplaced employee records
- 🔴 Payroll calculation errors
- 🔴 Scheduling conflicts and manual tracking
- 🔴 No centralized training or applicant management

The **WBS HRMS** is a comprehensive, role-based web application that automates the entire employee lifecycle—from initial job application through training completion and payroll processing. Built on .NET 10 with ASP.NET Core MVC and MySQL, this system provides a professional, secure portal for hiring managers, employees, instructors, and applicants.

---

## 🔄 How It Works (Simplified Workflow)

> **The Complete Employee Journey:**

```
┌─────────────────┐
│   Applicant     │ ← Browses and applies for open positions
│   Applies       │
└────────┬────────┘
         ↓
┌─────────────────────────┐
│  System Auto-Screens    │ ← AI-driven screening based on:
│  Resume (Exp/Skills)    │   • Years of Experience vs. Requirements
└────────┬────────────────┘   • Degree Level Matching
         │                    • Technical Skills Analysis
         ↓
┌─────────────────┐
│  Hiring Manager │ ← Reviews applications
│  Reviews Apps   │ ← One-click hiring promotes Applicant → Employee
└────────┬────────┘
         ↓
┌──────────────────────┐
│  Employee Onboards   │ ← Completes mandatory training
│  • Training Sessions │ ← Selects weekly shifts (36-hour rule enforced)
│  • Weekly Shifts     │
└────────┬─────────────┘
         ↓
┌──────────────────┐
│   Admin Exports  │ ← Generates comprehensive payroll reports
│   • Payroll      │ ← CSV export for accounting integration
│   • Reports      │ ← Work hours with training bonuses
└──────────────────┘
```

---

## 📁 Project Structure

```
HRMS/
├── Program.cs                          # Application entry point & dependency injection
├── appsettings.json                    # Production configuration
├── appsettings.Development.json        # Development configuration & database
├── HRMS.csproj                         # Project file (.NET 10 target)
├── HRMS.sln                            # Solution file
│
├── Constants/
│   └── Roles.cs                        # Five role definitions (HiringManager, Employee, Instructor, Applicant, ProductionManager)
│
├── Controllers/                        # ASP.NET MVC Controllers (Backend Business Logic)
│   ├── AccountController.cs            # Authentication (Login/Register/Logout)
│   ├── HomeController.cs               # Dashboard & role-based portal landing
│   ├── JobPostingsController.cs        # Hiring Manager: Job CRUD & Open Positions
│   ├── ApplicationsController.cs       # Hiring Manager: Screening & Applicant Review
│   ├── InstructorsController.cs        # Hiring Manager: Instructor Management
│   ├── SchedulingController.cs         # Employee: Weekly shift selection (36-hour validation)
│   ├── TrainingController.cs           # Instructor & Employee: Training sessions & attendance
│   └── ReportsController.cs            # Hiring Manager: Payroll, Work Hours, Training Reports
│
├── Models/                             # Data entities & ViewModels
│   ├── ApplicationUser.cs              # Extended Identity user with DisplayName
│   ├── JobPosting.cs                   # Job Posting entity (Title, Exp, Skills, OpenPositions)
│   ├── JobApplication.cs               # Application entity (Resume, Status, Score)
│   ├── TrainingSession.cs              # Training Session entity (Title, Date, Instructor, Enrollment)
│   ├── TrainingEnrollment.cs           # Employee training enrollment & completion tracking
│   ├── WorkShift.cs                    # Employee shift entity (36-hour weekly constraint)
│   ├── Instructor.cs                   # Instructor entity (Credentials, Department)
│   ├── ApplicationStatus.cs             # Enum: Applied, Screened, Rejected, Hired
│   ├── AccountViewModels.cs            # Login/Register/Profile ViewModels
│   ├── ApplyForJobViewModel.cs         # Job application form ViewModel
│   ├── InstructorViewModels.cs         # Instructor CRUD ViewModels
│   ├── ApplicationWorkflowViewModels.cs # Screening & hiring workflow ViewModels
│   └── ReportsViewModels.cs            # Dashboard, WorkHours, Training, Hiring reports
│
├── Data/                               # Database Context & Seeding
│   ├── ApplicationDbContext.cs         # Entity Framework DbContext with DbSets
│   ├── ApplicationUser.cs              # Identity user model
│   └── IdentitySeed.cs                 # Seeding default roles & admin user
│
├── Migrations/                         # Entity Framework database migration history
│   ├── 20260420154234_InitialIdentitySetup.cs
│   ├── 20260420173346_AddJobPostingOpenPositions.cs
│   ├── 20260420193017_AddApplicationAttemptNumber.cs
│   ├── 20260420205354_FixApplicationAttemptNumberDefault.cs
│   ├── 20260420211821_AddInstructorManagement.cs
│   ├── 20260420214437_AddWorkShiftWeekStartDate.cs
│   ├── 20260420220317_AddTrainingModule.cs
│   ├── 20260420224207_AddDetailedApplicationScreening.cs
│   └── 20260420230156_AddDefaultHourlyRateForPayroll.cs
│
├── Views/                              # ASP.NET Razor Templates (Frontend)
│   ├── _ViewStart.cshtml               # View initialization file
│   ├── _ViewImports.cshtml             # Global using statements & helpers
│   ├── Shared/
│   │   ├── _Layout.cshtml              # Master layout (Sticky navbar with role-based navigation)
│   │   └── _LoginPartial.cshtml        # Login/Logout partial
│   ├── Account/
│   │   ├── Login.cshtml
│   │   ├── Register.cshtml
│   │   └── Profile.cshtml
│   ├── Home/
│   │   ├── Index.cshtml                # Role-based dashboard landing
│   │   └── Privacy.cshtml
│   ├── JobPostings/
│   │   ├── Index.cshtml                # Job listing (public)
│   │   ├── Details.cshtml              # Job details & apply
│   │   ├── Create.cshtml               # Hiring Manager: Create job posting
│   │   └── Edit.cshtml
│   ├── Applications/
│   │   ├── Index.cshtml                # Hiring Manager: Application screening dashboard
│   │   └── Details.cshtml              # Hiring Manager: Applicant review & one-click hire
│   ├── Instructor/
│   │   ├── Index.cshtml                # Hiring Manager: Instructor list
│   │   ├── Create.cshtml
│   │   └── Edit.cshtml
│   ├── Scheduling/
│   │   ├── Index.cshtml                # Employee: Weekly shift selection (36-hour rule)
│   │   └── MySchedule.cshtml
│   ├── Training/
│   │   ├── Index.cshtml                # Instructor: Training session management
│   │   ├── Details.cshtml              # Instructor: Attendance tracking
│   │   ├── AllSessions.cshtml          # Employee: Browse all training sessions
│   │   └── MySessions.cshtml           # Employee: Personal training enrollment
│   └── Reports/
│       ├── Index.cshtml                # Dashboard with quick stats
│       ├── WorkHours.cshtml            # Weekly/Monthly employee hours (36hr threshold)
│       ├── TrainingCompletion.cshtml   # Training completion tracking
│       ├── HiringFunnel.cshtml         # Hiring metrics & acceptance rate
│       ├── Payroll.cshtml              # Payroll summary & export
│       └── ExportPayrollToCsv.cshtml   # CSV export generation
│
├── wwwroot/                            # Static files
│   ├── css/
│   │   └── site.css                    # Custom styling + print media queries
│   ├── js/
│   │   └── site.js                     # Client-side interactions
│   └── lib/                            # Bootstrap 5 & jQuery libraries
│
└── Properties/
    └── launchSettings.json             # Debug profiles (localhost:5107)
```

---

## 🎯 Role-Based Features

### 👔 **Hiring Manager (Admin)**
*Full system access for recruitment, staffing, and reporting*

| Feature | Description |
|---------|-------------|
| **Job Management** | Create, edit, delete job postings with experience/degree/skills requirements |
| **Automated Screening** | System auto-scores applications based on experience years, degree match, skill analysis |
| **Application Review** | Dashboard showing all applications with screening scores, accept/reject buttons |
| **One-Click Hiring** | Automatically promotes selected Applicant → Employee role with a single click |
| **Instructor Management** | Full CRUD for instructor records (name, credentials, department assignment) |
| **Work Hours Report** | View all employees' weekly/monthly hours with 36-hour threshold highlighting |
| **Training Completion Report** | Track employee training enrollment, completion rates, identify incomplete sessions |
| **Hiring Funnel Report** | Visualize recruitment metrics: Applications → Screened → Rejected → Hired with acceptance rate % |
| **Payroll Report** | Calculate gross pay: (Hours × HourlyRate) + Training Bonuses |
| **CSV Export** | Export payroll data to CSV for accounting software integration |

---

### 📚 **Instructor**
*Manage training sessions and monitor employee attendance*

| Feature | Description |
|---------|-------------|
| **Training Session Management** | Create sessions with title, description, date, max enrollment limit |
| **Attendance Tracking** | Mark employee attendance as Completed, Incomplete, or Absent in batch |
| **Training Console** | Centralized view of all assigned training sessions and enrollment status |
| **Employee Progress** | View which employees are enrolled, completed, or need follow-up |

---

### 👥 **Employee**
*Manage personal schedule and training enrollment*

| Feature | Description |
|---------|-------------|
| **Weekly Schedule Selection** | Self-service shift scheduling with **mandatory 36-hour weekly rule** enforcement |
| **6-Hour Shift Rule** | System validates each shift is exactly 6 hours; total weekly ≥ 36 hours |
| **Training Enrollment** | Browse and enroll in available training sessions |
| **Training Progress** | View completion status of enrolled training sessions |
| **Payroll Visibility** | Access personal work hours and training bonus calculations |

---

### 📝 **Applicant**
*Browse and apply for open positions*

| Feature | Description |
|---------|-------------|
| **Job Browsing** | View all active job postings with detailed requirements and descriptions |
| **Online Application** | Structured form submission with resume upload and background information |
| **Application Status** | Track application progress (Applied → Screened → Hired/Rejected) |
| **Self-Promotion** | On hire action, automatically promoted to Employee role with schedule access |

---

### 🔐 **Public (Unauthenticated)**
*Limited access to portal entry points*

| Feature | Description |
|---------|-------------|
| **Job Postings** | Browse available positions without login |
| **Login** | Navigate to login page |
| **Register** | Create new Applicant account |

---

## 🛠️ Technical Setup & Installation

### ✅ Prerequisites

- **.NET 10 SDK** ([Download](https://dotnet.microsoft.com/en-us/download/dotnet/10.0))
- **MySQL Server 8.0+** ([Download](https://dev.mysql.com/downloads/mysql/))
- **Visual Studio Code** or **Visual Studio 2022** (recommended)
- **Git** (for version control)

### 🗄️ Database Configuration

1. **Install MySQL Server** and note the port (default: 3306)

2. **Update `appsettings.Development.json`** with your connection string:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Port=3306;Database=hrms;User Id=root;Password=YOUR_MYSQL_PASSWORD;"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    }
  }
}
```

3. **Create Database** using Entity Framework Migrations:
```bash
dotnet ef database update
```

This will:
- Create the `hrms` database
- Run all migrations (schema setup)
- Seed default roles and admin user

### 🔐 Default Seeded Credentials

> [!IMPORTANT]
> These are development credentials only. Change passwords in production!

| Role | Email | Password | Purpose |
|------|-------|----------|---------|
| **Hiring Manager** | `Hiring.manager01@wbs.local` | `Hiring.manager01@wbs.local` | System admin, recruitment, reporting |
| **Employee** | `employee01@wbs.local` | `employee01@wbs.local` | Test shift scheduling & training |
| **Instructor** | `instructor01@wbs.local` | `instructor01@wbs.local` | Test training session management |
| **Applicant** | `applicant01@wbs.local` | `applicant01@wbs.local` | Test job browsing & application |
| **Production Manager** | `prodmgr01@wbs.local` | `prodmgr01@wbs.local` | Workforce production analytics |

> **Note:** To add more test users, login as Hiring Manager and navigate to user management features.

---

### 📦 CLI Commands

#### **1. Restore Dependencies**
```bash
dotnet restore
```
Restores all NuGet packages from `HRMS.csproj`

#### **2. Apply Database Migrations**
```bash
dotnet ef database update
```
Creates/updates MySQL schema and seeds initial data

#### **3. Build Project**
```bash
dotnet build
```
Compiles solution (checks for compilation errors)

#### **4. Run Application (Single Run)**
```bash
dotnet run
```
Starts the application at `https://localhost:5107`

#### **5. Watch Mode (Development)**
```bash
dotnet watch run
```
Starts application with auto-reload on file changes (hot reload)

#### **6. Publish Release Build**
```bash
dotnet publish -c Release -o ./publish
```
Generates optimized build for production deployment

---

## 📊 Business Logic: The "36-Hour Rule"

> [!IMPORTANT]
> **WBS operates under strict workforce scheduling requirements. The HRMS enforces this through system validation.**

### Mandatory Requirements

**Each Employee Must Work:**
- ✅ **Minimum 36 hours per week**
- ✅ **Exactly 6-hour shifts** (no partial or irregular shifts)
- ✅ **Consistent scheduling** (system prevents invalid combinations)

### Implementation Details

#### **Shift Validation**
```
WorkShift entity:
  - EmployeeUserId (FK to ApplicationUser)
  - WeekStartDate (Monday of the week)
  - StartTimeUtc (shift start)
  - EndTimeUtc (shift end)
  - Validation: (EndTimeUtc - StartTimeUtc) MUST equal exactly 6 hours
```

#### **Weekly Total Validation**
```
SchedulingController enforces:
  - SUM(shifts in week) >= 36 hours
  - NO gaps in scheduling allowed
  - System prevents employee from selecting incomplete or fractional schedules
```

#### **Business Impact**
- Employees cannot finalize schedules that violate the rule
- Hiring Manager can view compliance dashboard
- Payroll calculations factor 36-hour baseline + overtime (if applicable)

---

## 🏗️ Architecture & Data Models

### Application Flow

```
User Request
     ↓
[Routing] → Route.MapControllerRoute()
     ↓
[Controllers] → RequestHandler.cs processes User input
     ↓
[Models] → ViewModel transforms data for view
     ↓
[DbContext] → Entity Framework queries MySQL
     ↓
[Database] → MySQL persists/retrieves data
     ↓
[Response] → Controller returns View with data
     ↓
[Razor View] → HTML rendered + sent to browser
     ↓
User sees rendered page
```

### Core Data Models (Entity Reference)

#### **JobPosting**
```
{
  Id: int (PK),
  Title: string (max 120),
  Description: string (max 4000),
  RequiredExperienceYears: int (0-50),
  RequiredDegree: string (e.g., "Bachelor's", "High School"),
  RequiredSkillsCsv: string (comma-separated: "C#,SQL,Azure"),
  OpenPositions: int (1-1000),
  BaseHourlyRate: decimal,
  IsActive: bool,
  CreatedAtUtc: DateTime
}
```

#### **JobApplication**
```
{
  Id: int (PK),
  JobPostingId: int (FK → JobPosting),
  ApplicantUserId: string (FK → ApplicationUser),
  Resume: string,
  ExperienceYears: int,
  DegreeLevel: string,
  SkillsCsv: string,
  Status: ApplicationStatus (Applied|Screened|Rejected|Hired),
  ScreeningScore: int (0-100),
  AttemptNumber: int,
  CreatedAtUtc: DateTime,
  UpdatedAtUtc: DateTime
}
```

#### **TrainingSession**
```
{
  Id: int (PK),
  Title: string (max 160),
  Description: string (max 2000),
  SessionDateUtc: DateTime,
  MaxEnrollment: int (1-500),
  InstructorId: int (FK → Instructor),
  IsActive: bool,
  Enrollments: ICollection<TrainingEnrollment>
}
```

#### **TrainingEnrollment**
```
{
  Id: int (PK),
  TrainingSessionId: int (FK → TrainingSession),
  EmployeeUserId: string (FK → ApplicationUser),
  Status: TrainingStatus (Enrolled|Completed|Absent|Incomplete),
  EnrolledAtUtc: DateTime,
  CompletedAtUtc: DateTime?
}
```

#### **WorkShift**
```
{
  Id: int (PK),
  EmployeeUserId: string (FK → ApplicationUser),
  WeekStartDate: DateTime (Monday of week),
  StartTimeUtc: DateTime,
  EndTimeUtc: DateTime,
  HourlyRate: decimal,
  Validation: (EndTimeUtc - StartTimeUtc) == 6 hours exactly
}
```

#### **Instructor**
```
{
  Id: int (PK),
  DisplayName: string,
  Credentials: string (e.g., "M.Ed, CISSP"),
  Department: string,
  IsActive: bool,
  CreatedAtUtc: DateTime
}
```

#### **ApplicationUser** (extends IdentityUser)
```
{
  Id: string (PK),
  UserName: string,
  Email: string,
  EmailConfirmed: bool,
  DisplayName: string,
  Role: string (HiringManager|Employee|Instructor|Applicant|ProductionManager),
  LockoutEnabled: bool,
  AccessFailedCount: int
}
```

---

## 🗺️ Route Map & Access Control

| Route | Method | Controller | Action | Roles | Purpose |
|-------|--------|-----------|--------|-------|---------|
| `/` | GET | Home | Index | All | Dashboard landing page |
| `/Account/Login` | GET/POST | Account | Login | Public | User authentication |
| `/Account/Register` | GET/POST | Account | Register | Public | New applicant registration |
| `/Account/Logout` | POST | Account | Logout | Authenticated | User logout |
| `/JobPostings` | GET | JobPostings | Index | All | Browse open positions |
| `/JobPostings/Details/{id}` | GET | JobPostings | Details | All | View job + apply form |
| `/JobPostings/Create` | GET/POST | JobPostings | Create | HiringManager | Create new job posting |
| `/JobPostings/Edit/{id}` | GET/POST | JobPostings | Edit | HiringManager | Edit job posting |
| `/JobPostings/Delete/{id}` | POST | JobPostings | Delete | HiringManager | Delete job posting |
| `/Applications` | GET | Applications | Index | HiringManager | Applicant screening dashboard |
| `/Applications/Details/{id}` | GET | Applications | Details | HiringManager | Review single application |
| `/Applications/UpdateStatus` | POST | Applications | UpdateStatus | HiringManager | Accept/Reject applicant |
| `/Instructor` | GET | Instructor | Index | HiringManager | List all instructors |
| `/Instructor/Create` | GET/POST | Instructor | Create | HiringManager | Add new instructor |
| `/Instructor/Edit/{id}` | GET/POST | Instructor | Edit | HiringManager | Update instructor |
| `/Scheduling` | GET | Scheduling | Index | Employee | Select weekly shifts |
| `/Scheduling/MySchedule` | GET | Scheduling | MySchedule | Employee | View personal shifts |
| `/Training` | GET | Training | Index | Instructor | Manage training sessions |
| `/Training/Details/{id}` | GET | Training | Details | Instructor | View/mark attendance |
| `/Training/AllSessions` | GET | Training | AllSessions | Employee | Browse available training |
| `/Training/MySessions` | GET | Training | MySessions | Employee | Personal training enrollment |
| `/Reports` | GET | Reports | Index | HiringManager | Reporting dashboard |
| `/Reports/WorkHours` | GET | Reports | WorkHours | HiringManager | Weekly/monthly hours report |
| `/Reports/TrainingCompletion` | GET | Reports | TrainingCompletion | HiringManager | Training completion tracking |
| `/Reports/HiringFunnel` | GET | Reports | HiringFunnel | HiringManager | Hiring metrics & funnel |
| `/Reports/Payroll` | GET | Reports | Payroll | HiringManager | Payroll calculation & summary |
| `/Reports/ExportPayrollToCsv` | POST | Reports | ExportPayrollToCsv | HiringManager | Download payroll as CSV |

---

## 🎨 UI/UX Features

### Navigation & Layout
- ✅ **Sticky Navbar** - Navigation stays visible while scrolling
- ✅ **Role-Based Menu** - Only relevant links shown per user role
- ✅ **Role Badge** - Display prominent role indicator next to username
- ✅ **Responsive Design** - Mobile-friendly Bootstrap 5 layout
- ✅ **Print Optimization** - Reports include print-friendly CSS (@media print)

### Dashboard & Cards
- ✅ **Role-Based Dashboard** - Home page shows different content by role
- ✅ **Quick Stats Cards** - Key metrics (Active Employees, Open Jobs, Training Completed)
- ✅ **Card Grid Layout** - Responsive col-lg-4/col-md-6 Bootstrap grid

### Tables & Data Display
- ✅ **Sortable Tables** - Column headers support sorting (via Bootstrap classes)
- ✅ **Highlighted Rows** - Visual indication (table-warning) for items requiring attention
  - Work hours below 36/week highlighted in amber
  - Incomplete training sessions highlighted in reports
- ✅ **Pagination** - Large datasets split across pages

### Forms & Validation
- ✅ **Client-Side Validation** - HTML5 form validation (required, min/max, etc.)
- ✅ **Server-Side Validation** - ModelState verification in controllers
- ✅ **Error Messages** - Clear feedback on validation failures
- ✅ **Success Notifications** - TempData messages confirm actions

### Reports & Export
- ✅ **Print Button** - Each report has dedicated print styling
- ✅ **CSV Export** - Payroll exportable as comma-separated values
- ✅ **Date Range Filters** - Week/month selection for time-based reports
- ✅ **PDF-Ready** - Print-to-PDF captures full report formatting

---

## 🔒 Security Features

### Authentication & Authorization
- ✅ **ASP.NET Core Identity** - Built-in user management & password hashing
- ✅ **Role-Based Access Control** - `[Authorize(Roles = "...")]` attributes enforce permissions
- ✅ **Claim-Based Authorization** - Fine-grained access checks via User.Claims
- ✅ **CSRF Protection** - `@Html.AntiForgeryToken()` on all forms
- ✅ **Password Hashing** - bcrypt hashing via Identity framework
- ✅ **Email Confirmation** - Optional email verification for new accounts

### Data Protection
- ✅ **HTTPS Enforced** - SSL/TLS encryption in production
- ✅ **SQL Injection Prevention** - Parameterized queries via Entity Framework
- ✅ **XSS Protection** - Razor templating auto-escapes user input
- ✅ **CORS Configuration** - Restrict cross-origin requests if needed

### Audit Trail
- ✅ **CreatedAtUtc / UpdatedAtUtc** - Timestamp critical actions
- ✅ **Change Tracking** - EF Core tracks entity modifications
- ✅ **User Attribution** - Actions tied to ApplicationUser

---

## 📈 Scalability & Performance

### Database Optimization
- ✅ **Entity Framework Eager Loading** - `.Include()` prevents N+1 queries
- ✅ **Async/Await Throughout** - Non-blocking database operations
- ✅ **Indexed Foreign Keys** - Fast joins on common relationships
- ✅ **Query Optimization** - Filtered/paginated results reduce memory footprint

### Caching Strategy (Future Enhancement)
- Recommended: Implement Redis for session/dashboard stats
- Cache training session roster (invalidate on updates)
- Cache applicant screening scores (24-hour TTL)

### Load Testing
- Tested on: .NET 10 local environment (localhost:5107)
- Recommended production environment:
  - Cloud: Azure App Service + Azure Database for MySQL
  - On-premise: IIS 10+ on Windows Server 2019+ + MySQL Cluster

---

## 🚀 Running the Application

### **Quick Start (5 minutes)**

```bash
# 1. Clone repository
git clone <repo-url>
cd HRMS

# 2. Configure database in appsettings.Development.json

# 3. Restore packages & create database
dotnet restore
dotnet ef database update

# 4. Start development server
dotnet watch run

# 5. Open browser
# Navigate to: https://localhost:5107
```

### **Testing with Seeded Credentials**

1. **Login as Hiring Manager:**
   - Email: `Hiring.manager01@wbs.local`
   - Password: `Hiring.manager01@wbs.local`
   - Actions: Create jobs, review applications, access reports

2. **Create Test Users:**
   - Use employee registration form to create additional test accounts
   - Assign roles via Hiring Manager dashboard (in real usage)

3. **Test Role-Based Features:**
   - Logout and re-login with different test accounts
   - Verify navigation changes per role
   - Confirm access restrictions (403 Forbidden on unauthorized routes)

---

## 📝 Development Workflow

### **Adding a New Feature**

1. **Create Model** in `Models/` directory
2. **Create DbSet** in `ApplicationDbContext.cs`
3. **Generate Migration:**
   ```bash
   dotnet ef migrations add DescriptiveNameOfChange
   dotnet ef database update
   ```
4. **Create Controller** in `Controllers/` with action methods
5. **Create Views** in `Views/[ControllerName]/`
6. **Add Authorization** via `[Authorize(Roles = "...")]`
7. **Test** via `dotnet watch run` with hot reload

### **Code Style & Conventions**

- **Naming:** PascalCase for classes/methods, camelCase for variables
- **Async:** Use `async/await` for all I/O operations
- **DbContext:** Always use using statement or dependency injection
- **ViewModels:** Separate input models (for forms) from output models (for display)
- **Validation:** Use DataAnnotations on models + server-side verification

---

## 🐛 Troubleshooting

### **Issue: Connection String Error**
```
Error: "Unable to connect to database"
```
**Solution:**
1. Verify MySQL is running: `mysql -u root -p` (on macOS: `brew services list | grep mysql`)
2. Check `appsettings.Development.json` connection string
3. Verify database name is `hrms`
4. Run `dotnet ef database update`

### **Issue: Migration Failed**
```
Error: "Unable to apply migration"
```
**Solution:**
1. Drop database: `DROP DATABASE hrms;`
2. Re-run: `dotnet ef database update`
3. Re-run seeding via application startup

### **Issue: Razor Page Not Found (404)**
```
Error: "The resource you are looking for has not been found"
```
**Solution:**
1. Verify controller name matches route: `[controller]` → filename
2. Verify view file exists in `Views/[ControllerName]/[ActionName].cshtml`
3. Check routing in `Program.cs` endpoint configuration
4. Confirm `[Authorize]` attributes don't block access unexpectedly

### **Issue: User Role Not Applying**
```
Error: User sees "Access Denied" on authorized pages
```
**Solution:**
1. Verify user has role assigned in `[AspNetUserRoles]` table
2. Check role name matches exactly in database (case-sensitive)
3. Clear browser cookies & re-login
4. Verify `User.IsInRole("RoleName")` check in code

---

## 📚 Additional Resources

- **[ASP.NET Core Documentation](https://learn.microsoft.com/en-us/aspnet/core/)**
- **[Entity Framework Core Guide](https://learn.microsoft.com/en-us/ef/core/)**
- **[Bootstrap 5 Docs](https://getbootstrap.com/docs/5.0/)**
- **[MySQL Documentation](https://dev.mysql.com/doc/)**
- **[Identity Framework Best Practices](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity/)**

---

## 📞 Support & Contact

**Project Owner:** We Build Stuff (WBS)
**Framework:** ASP.NET Core 10 MVC
**Database:** MySQL 8.0+
**License:** Internal Use Only

For issues or feature requests, contact your development team lead.

---

**Last Updated:** April 21, 2026
**Status:** ✅ Production Ready
