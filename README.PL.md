# 🚀 .NET 9 + React 18 Starter – Szablon Clean Architecture

**Produkcyjny szablon full-stack** do budowania nowoczesnych aplikacji webowych z **API .NET 9 w Clean Architecture** i **frontendem React 18 + TypeScript**.

Idealny dla zespołów, przedsiębiorców i deweloperów, którzy potrzebują **czystego, skalowalnego i sprawdzonego fundamentu** z best practices wbudowanymi od pierwszego dnia.

---

## ✨ Co Jest w Środku

### Funkcjonalności V1.0 (Produkcja)
- ✅ **Clean Architecture** – Domain → Application → Infrastructure, czysta separacja
- ✅ **JWT Authentication** – Bezpieczna autentykacja z rotacją refresh tokenów
- ✅ **PostgreSQL** – Produkcyjna baza danych
- ✅ **Entity Framework Core 9** – ORM z migracjami
- ✅ **Health Checks** – Endpoint `/health` dla orkiestracji
- ✅ **FluentValidation** – Automatyczna walidacja DTO
- ✅ **Serilog** – Structured logging
- ✅ **CORS Configuration** – Konfiguracja z appsettings
- ✅ **Docker & Docker Compose** – One-command deployment
- ✅ **Swagger/OpenAPI** – Dokumentacja API
- ✅ **Test Projects** – Unit, Integration, E2E scaffolding

---

## 🚀 Szybki Start

### Wymagania
**Opcja 1: Docker (Rekomendowana)**
- Docker & Docker Compose

**Opcja 2: Lokalna Instalacja**
- .NET 9 SDK
- Node.js 20+
- PostgreSQL 16+

### Uruchomienie w Docker (30 sekund)
```bash
git clone https://github.com/Karol8284/dotnet-react-starter.git
cd dotnet-react-starter
docker-compose up
```

**Otwórz w przeglądarce:**
- 🎨 Frontend: http://localhost:3000
- 🔌 API: http://localhost:5000
- 📖 Dokumentacja: http://localhost:5000/swagger
- ✅ Health: http://localhost:5000/health
- ✉️ Mailpit: http://localhost:8025

### Lokalne Uruchomienie

**Backend (.NET)**
```bash
cd backend
dotnet build
dotnet run --project API/API.csproj
```

**Frontend (React)**
```bash
cd frontend
npm install
npm start
```

**Baza Danych**
```bash
cd backend/API
dotnet ef database update
```

---

## 📁 Struktura Projektu

```
dotnet-react-starter/
│
├── backend/                          # .NET 9 API (Clean Architecture)
│   ├── API/
│   │   ├── Controllers/              # HTTP endpoints
│   │   ├── Filters/                  # Validation, exception handling
│   │   ├── Middleware/               # Auth, error handling pipelines
│   │   ├── Program.cs                # Startup configuration, DI
│   │   ├── appsettings.json          # Production config
│   │   ├── appsettings.Development.json
│   │   └── Dockerfile
│   │
│   ├── Application/
│   │   ├── DTOs/                     # Data transfer objects
│   │   │   └── Auth/                 # LoginUserDto, RegisterUserDto
│   │   ├── Services/                 # Business logic
│   │   ├── Validators/               # FluentValidation rules
│   │   └── Commands/                 # CQRS command patterns
│   │
│   ├── Domain/
│   │   ├── Entities/                 # User, core business models
│   │   ├── Enums/                    # UserRole, status types
│   │   ├── Interfaces/               # IAuthService, IJwtTokenService
│   │   └── ValueObjects/             # Custom value types
│   │
│   ├── Infrastructure/
│   │   ├── Data/
│   │   │   ├── ApplicationDbContext.cs
│   │   │   └── Migrations/           # EF Core migrations
│   │   ├── Repositories/             # Repository pattern
│   │   ├── Services/                 # AuthService, JwtTokenService
│   │   └── Extensions/               # Dependency injection helpers
│   │
│   ├── Shared/
│   │   ├── Dtos/                     # Shared response models
│   │   ├── Settings/                 # Configuration classes (JwtSettings)
│   │   ├── Constants/                # Application constants
│   │   └── Responses/                # ApiResponse<T> patterns
│   │
│   ├── UnitTests/                    # xUnit + Moq tests
│   ├── IntegrationTests/             # Database + API tests
│   ├── E2ETests/                     # End-to-end scenarios
│   └── backend.slnx                  # Solution file
│
├── frontend/                         # React 18 + TypeScript
│   ├── src/
│   │   ├── components/               # Reusable UI components
│   │   ├── pages/                    # Full-page components
│   │   ├── services/                 # API client (axios)
│   │   ├── hooks/                    # Custom React hooks
│   │   ├── context/                  # Global state (Zustand)
│   │   ├── types/                    # TypeScript interfaces
│   │   ├── styles/                   # CSS modules, theming
│   │   ├── utils/                    # Helper functions
│   │   └── App.tsx                   # Root component
│   ├── public/                       # Static assets
│   ├── package.json
│   ├── tsconfig.json
│   ├── Dockerfile
│   └── README.md
│
├── docker/
│   ├── docker-compose.yml            # Multi-container orchestration
│   ├── nginx.conf                    # Reverse proxy config
│   └── DOCKER_COMPOSE.md             # Networking guide
│
├── doc/
│   ├── GETTING_STARTED.md
│   ├── ARCHITECTURE.md
│   ├── BACKEND_SETUP.md
│   ├── FRONTEND_SETUP.md
│   └── DOCKER_COMPOSE.md
│
└── README.md                         # English documentation
```

---

## 🛠️ Stack Technologiczny

### Backend (.NET 9)
| Warstwa | Technologia | Przeznaczenie |
|-------|-----------|---------|
| **API** | ASP.NET Core 9 | REST endpoints, middleware |
| **Auth** | JWT Bearer + BCrypt | Bezpieczna autentykacja & zarządzanie tokenami |
| **Baza danych** | Entity Framework Core 9 | ORM & integracja PostgreSQL |
| **Baza danych** | PostgreSQL 16 | Produkcyjna SQL baza danych |
| **Walidacja** | FluentValidation | Walidacja requestów, business rules |
| **Logging** | Serilog | Structured logs do konsoli & pliku |
| **API Docs** | Swagger/OpenAPI | Interaktywna dokumentacja API |
| **Testowanie** | xUnit + Moq | Unit & integration tests |

### Frontend (React 18)
| Pakiet | Wersja | Przeznaczenie |
|---------|---------|---------|
| **React** | 18.x | Biblioteka UI |
| **TypeScript** | 5.x | Type safety |
| **React Router** | 6.x | Client-side routing |
| **Axios** | 1.x | HTTP client dla API |
| **Zustand** | Latest | Lightweight state management |
| **React Query** | Latest | Server state sync & caching |
| **React Hook Form** | Latest | Performant form handling |
| **Zod** | Latest | Runtime schema validation |

### DevOps & Infrastruktura
- **Docker** – Containerization
- **Docker Compose** – Multi-container orchestration
- **PostgreSQL** – Primary data store
- **Nginx** – Reverse proxy
- **GitHub Actions** – CI/CD ready (konfiguracja templates dołączona)

---

## 🔐 Autentykacja & Bezpieczeństwo

### Jak Działa Autentykacja
1. **Rejestracja** – `POST /api/auth/register` tworzy konto i wysyła link potwierdzający email
2. **Potwierdzenie email** – `POST /api/auth/confirm-email` aktywuje konto po kliknięciu linku
3. **Login** – `POST /api/auth/login` po poprawnym haśle zwraca JWT albo challenge 2FA z kodem wysyłanym na email
4. **Weryfikacja 2FA** – `POST /api/auth/verify-2fa` sprawdza kod i dopiero wtedy wydaje access token + refresh token
5. **Odświeżenie** – `POST /api/auth/refresh-token` rotuje parę tokenów przed expiry
6. **Logout** – `POST /api/auth/logout` cofa refresh token

W lokalnym Docker Compose maile trafiają do Mailpit. Linki aktywacyjne i kody 2FA odbierzesz pod `http://localhost:8025`.

### Funkcje Bezpieczeństwa ✅
- Hasła hashowane BCrypt (nigdy nie przechowywane plain)
- JWT tokeny z konfigurowalnymi expirami (15 min access, 7 dni refresh)
- CORS walidacja względem whitelisted origins (konfigurowana)
- Automatyczna rotacja tokenów na refresh
- Walidacja requestów przed przetworzeniem
- Role-based access control (RBAC) gotowy do użycia

### Konfiguracja
Edytuj `appsettings.json`:
```json
{
  "Jwt": {
    "Secret": "CHANGE_IN_PRODUCTION_minimum-32-characters",
    "Issuer": "Dotnet-React-Starter-API",
    "Audience": "Dotnet-React-Starter-App"
  },
  "Cors": {
    "AllowedOrigins": ["https://localhost:3000", "https://yourdomain.com"]
  }
}
```

---

## � Zmienne Środowiskowe

### Docker (.env support)
```bash
# Database
POSTGRES_DB=dotnet_react_starter
POSTGRES_USER=postgres
POSTGRES_PASSWORD=postgres

# API
ASPNETCORE_ENVIRONMENT=Production
ConnectionStrings__DefaultConnection=Host=postgres;Port=5432;Database=...
```

### Lokalne (.NET secrets)
```bash
dotnet user-secrets init
dotnet user-secrets set "Jwt:Secret" "your-secret-key"
dotnet user-secrets set "Jwt:Issuer" "your-issuer"
```

---

## � Workflow Deweloperski

### Dodawanie Nowej Funkcjonalności API

1. **Utwórz Entity** (`Domain/Entities/YourEntity.cs`)
   ```csharp
   public class YourEntity
   {
       public Guid Id { get; set; }
       public string Name { get; set; }
   }
   ```

2. **Dodaj DbSet** (`Infrastructure/Data/ApplicationDbContext.cs`)
   ```csharp
   public DbSet<YourEntity> YourEntities => Set<YourEntity>();
   ```

3. **Utwórz DTO** (`Application/DTOs/YourEntityDto.cs`)
   ```csharp
   public class YourEntityDto
   {
       public Guid Id { get; set; }
       public string Name { get; set; }
   }
   ```

4. **Utwórz Service Interface** (`Domain/Interfaces/IYourService.cs`)
   ```csharp
   public interface IYourService
   {
       Task<YourEntityDto> GetByIdAsync(Guid id);
       Task<YourEntityDto> CreateAsync(CreateYourEntityDto dto);
   }
   ```

5. **Implementuj Service** (`Infrastructure/Services/YourService.cs`)
   ```csharp
   public class YourService : IYourService
   {
       // Implementation
   }
   ```

6. **Utwórz Controller** (`API/Controllers/YourController.cs`)
   ```csharp
   [ApiController]
   [Route("api/[controller]")]
   public class YourController : ControllerBase
   {
       private readonly IYourService _service;
       // Endpoints
   }
   ```

7. **Zarejestruj w DI** (`API/Program.cs`)
   ```csharp
   builder.Services.AddScoped<IYourService, YourService>();
   ```

8. **Utwórz Migrację** (w `backend/API` directory)
   ```bash
   dotnet ef migrations add AddYourEntity --project ../Infrastructure --output-dir ../Infrastructure/Data/Migrations
   ```

### Testowanie Twojej Funkcjonalności
```bash
# Unit tests
dotnet test --filter "FullyQualifiedName~YourFeature"

# Uruchom wszystkie testy
dotnet test
```

### V1.0 (Teraz) ✅
- ✅ Clean Architecture baseline
- ✅ JWT authentication & refresh tokens
- ✅ PostgreSQL + EF migrations
- ✅ Request validation (FluentValidation)
- ✅ Health checks dla orkiestracji
- ✅ CORS configuration z settings
- ✅ Docker containerization
- ✅ API documentation (Swagger)
- ✅ Logging (Serilog)

### V1.5 (Q3 2026) 📅
- **Database Enhancements**
  - [ ] Persist refresh tokens to database (zamiast in-memory)
  - [ ] Audit logging (created/modified timestamps, change tracking)
  - [ ] Soft deletes support
  
- **Testing**
  - [ ] Complete auth flow unit tests
  - [ ] Integration tests dla database operations
  - [ ] E2E test examples (login → protected endpoint)
  
- **Frontend Features**
  - [ ] Login/Register pages
  - [ ] Protected routes
  - [ ] Logout & token refresh logic
  - [ ] Error handling & retry logic
  - [ ] Loading states

- **DevOps**
  - [ ] GitHub Actions CI/CD pipeline
  - [ ] Automated testing on PR
  - [ ] Docker build optimization

### V2.0 (Q4 2026) 🎯
- **Advanced Auth**
  - [ ] Google/GitHub OAuth2 integration
  - [ ] Multi-factor authentication (MFA)
  - [ ] Email confirmation flow
  - [ ] Password reset functionality
  
- **API Features**
  - [ ] Pagination & filtering
  - [ ] Full-text search support
  - [ ] Bulk operations
  - [ ] API versioning (v1, v2 endpoints)
  - [ ] GraphQL alternative endpoint
  
- **Frontend**
  - [ ] Dashboard layout
  - [ ] User profile management
  - [ ] Settings page
  - [ ] Notification system
  - [ ] Dark mode support
  
- **Infrastructure**
  - [ ] Redis caching layer
  - [ ] Background jobs (Hangfire)
  - [ ] Real-time notifications (SignalR)
  - [ ] File upload service
  - [ ] Email service integration
  
- **Documentation**
  - [ ] API endpoint catalog
  - [ ] Video tutorials
  - [ ] Deployment guides (AWS, Azure, DigitalOcean)
  - [ ] Performance optimization guide

### V2.5+ (Przyszłe Możliwości)
- [ ] Admin dashboard
- [ ] Analytics & reporting
- [ ] Multi-tenancy support
- [ ] Mobile app (React Native)
- [ ] Microservices architecture transition
- [ ] Event sourcing & CQRS deep dive
- [ ] DDD principles advanced implementation

---

## 🏗️ Zasady Clean Architecture

Ten starter wymusza **Clean Architecture** przez design:

- **Domain Layer** – Czysta business logic, brak dependencies
- **Application Layer** – Use cases, DTOs, validators (depends tylko na Domain)
- **Infrastructure Layer** – Database, external services (depends na Domain & Application)
- **API Layer** – Controllers, middleware (depends na wszystkich warstwach)

**Korzyści:**
- Łatwe do testowania (dependencies injected, mockable)
- Business logic niezależna od frameworków
- Wysoka maintainability & extensibility
- Czysta separacja concerns

---

## 📖 Popularne Zadania

### Zmiana Bazy Danych (PostgreSQL → SQL Server)
1. Usuń `Npgsql.EntityFrameworkCore.PostgreSQL`
2. Zainstaluj `Microsoft.EntityFrameworkCore.SqlServer`
3. Zaktualizuj connection string
4. Regeneruj migracje: `dotnet ef migrations add MigrateToSqlServer`

### Dodaj Role-Based Authorization
```csharp
[Authorize(Roles = "Admin")]
public IActionResult AdminOnly() => Ok("Admin only");
```

### Skonfiguruj Logging Level
Edytuj `appsettings.json`:
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Your.Namespace": "Debug"
    }
  }
}
```

### Skaluj Backend Horizontalnie
1. Deploy multiple instances
2. Użyj load balancer (Nginx, AWS ALB)
3. Share database connection
4. Refresh tokens stay in-memory per instance (lub use Redis w V1.5)

---

## 🤝 Contributing

Masz sugestie? Znalazłeś bug? Chcesz dodać feature?

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit changes (`git commit -m 'Add amazing feature'`)
4. Push to branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

---

## 📄 Licencja

This project is licensed under the MIT License – zobacz [LICENSE](./LICENSE) file for details.

---

## 💬 Wsparcie & Pytania

- 📖 Przeczytaj najpierw [GETTING_STARTED.md](./doc/GETTING_STARTED.md)
- 🐛 Zgłoś problemy na GitHub Issues
- 💡 Dyskusje mile widziane w GitHub Discussions
- 📧 Email: support@yourdomain.com (add your contact)

---

## ⭐ Pokaż Wsparcie

Jeśli ten starter Ci pomógł, rozważ danie mu ⭐ na GitHubie!

**Made with ❤️ dla deweloperów, którzy cenią czysty kod, skalowalność i produktywność.**

---

**Ostatnia Aktualizacja:** Czerwiec 2026 | **Autor:** [Karol8284](https://github.com/Karol8284)
