# 🏗️ Backend Setup - Jak pracować z warstwami

## Struktura Backendowa

```

## Environment i Deployment Configuration

Backend ma trzy warstwy konfiguracji:

1. `appsettings.json` - bezpieczne wspólne defaulty
2. `appsettings.Development.json` - lokalne override bez sekretów
3. zmienne środowiskowe / secrets - dane wrażliwe i produkcyjne

Najważniejsze zmienne środowiskowe:

- `DefaultConnection`
- `Jwt__Secret`
- `Jwt__Issuer`
- `Jwt__Audience`
- `Jwt__RefreshTokenCookieSameSite`
- `Jwt__RefreshTokenCookieSecurePolicy`
- `Cors__AllowedOrigins__0`, `Cors__AllowedOrigins__1`, ...
- `EmailConfirmation__PublicOrigin`
- `EmailConfirmation__ConfirmationPath`
- `EmailTwoFactor__Enabled`
- `EmailTwoFactor__CodeExpiresInMinutes`
- `EmailDelivery__Enabled`
- `EmailDelivery__Host`
- `EmailDelivery__Port`
- `EmailDelivery__FromAddress`

Zasady organizacji:

- Sekrety nigdy nie trafiają do `appsettings*.json` w repo.
- CORS origins nie są hardcode'owane w `Program.cs`, tylko bind'owane z configu.
- Cookie policy ma być zależna od środowiska: local zwykle `SameAsRequest`, produkcja zwykle `Always`.
- Jeżeli frontend i backend działają cross-site, ustaw `SameSite=None` i `SecurePolicy=Always`.
- Dla lokalnego Docker Compose używamy Mailpit jako SMTP sinka, więc maile potwierdzające i kody 2FA można odebrać bez zewnętrznego providera.
backend/
├── API/                    # Entry point - Controllers, middleware
├── Application/            # Logika biznesowa - Services, DTOs
├── Domain/                 # Encje, interfejsy, reguły biznesowe
├── Infrastructure/         # Baza danych, repozytoria
├── Shared/                 # Wspólne klasy, helpersy
└── Testy (Unit, Integration, E2E)
```

## Clean Architecture - co to oznacza?

```
┌─────────────────────────────────┐
│         API (Controllers)       │  ← HTTP Requests
├─────────────────────────────────┤
│    Application (Services)       │  ← Business Logic
├─────────────────────────────────┤
│    Domain (Entities)            │  ← Pure Business Rules
├─────────────────────────────────┤
│  Infrastructure (DB, External)  │  ← Implementation Details
└─────────────────────────────────┘
```

**Zasada**: Każda warstwa zna tylko warstwę poniżej siebie. API nie zna bazy danych!

---

## 1️⃣ Domain Layer - Reguły Biznesowe

### Co tu robimy?
- **Encje** (Entities) - reprezentacja danych biznesowych
- **Interfejsy** - kontrakty dla innych warstw
- **Value Objects** - obiekty niemające ID, reprezentujące wartości
- **Wyjątki domenowe** - błędy specyficzne dla biznesu

### Struktura:
```
Domain/
├── Entities/
│   ├── BaseEntity.cs          # Bazowa klasa
│   ├── Product.cs
│   ├── Order.cs
│   └── User.cs
├── Interfaces/
│   ├── IRepository.cs         # Generyczny interfejs
│   ├── IProductRepository.cs  # Specjalizowany
│   └── IUnitOfWork.cs
├── ValueObjects/
│   ├── Money.cs
│   └── Price.cs
└── Exceptions/
    ├── DomainException.cs
    └── ProductNotFoundException.cs
```

### Przykład - Utwórz nową encję Product:

```csharp
// Domain/Entities/Product.cs
namespace Domain.Entities;

public class Product : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int StockQuantity { get; set; }
    public string Sku { get; set; } = string.Empty;
    
    // Walidacja biznesowa - robi się tutaj!
    public void UpdateStock(int quantity)
    {
        if (quantity < 0)
            throw new DomainException("Stock quantity cannot be negative");
        
        StockQuantity = quantity;
        UpdatedAt = DateTime.UtcNow;
    }
    
    public bool IsLowStock(int threshold = 10)
    {
        return StockQuantity <= threshold;
    }
}
```

**Ważne**: Logika biznesowa jest w encji, nie w serwisie!

---

## 2️⃣ Application Layer - Logika Aplikacyjna

### Co tu robimy?
- **Services** - orchestracja logiki biznesowej
- **DTOs** - transferu danych pomiędzy warstwami
- **Mappers** - konwersja Entity ↔ DTO
- **Validators** - walidacja requestów

### Struktura:
```
Application/
├── Services/
│   ├── IService.cs            # Interfejs bazowy
│   ├── ProductService.cs
│   └── OrderService.cs
├── DTOs/
│   ├── BaseDto.cs
│   ├── ProductDto.cs
│   ├── CreateProductDto.cs
│   └── UpdateProductDto.cs
├── Mappers/
│   ├── ProductMapper.cs
│   └── OrderMapper.cs
└── Validators/
    ├── ProductValidator.cs
    └── OrderValidator.cs
```

### Przykład - Utwórz DTO:

```csharp
// Application/DTOs/CreateProductDto.cs
namespace Application.DTOs;

public class CreateProductDto
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int StockQuantity { get; set; }
    public string Sku { get; set; } = string.Empty;
}

// Application/DTOs/ProductDto.cs
public class ProductDto : BaseDto
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int StockQuantity { get; set; }
    public string Sku { get; set; } = string.Empty;
}
```

**DTO** = Data Transfer Object
- Nie wysyłasz encji do klienta (zawiera wrażliwe dane!)
- Zawsze zwracasz DTO z tylko potrzebnymi danymi

### Przykład - Utwórz Service:

```csharp
// Application/Services/ProductService.cs
using Domain.Entities;
using Domain.Interfaces;
using Application.DTOs;

namespace Application.Services;

public class ProductService : IService<ProductDto>
{
    private readonly IRepository<Product> _repository;

    public ProductService(IRepository<Product> repository)
    {
        _repository = repository;
    }

    public async Task<ProductDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var product = await _repository.GetByIdAsync(id, cancellationToken);
        if (product == null)
            return null;

        return MapToDto(product);
    }

    public async Task<IEnumerable<ProductDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var products = await _repository.GetAllAsync(cancellationToken);
        return products.Select(MapToDto);
    }

    public async Task<ProductDto> CreateAsync(ProductDto dto, CancellationToken cancellationToken = default)
    {
        var product = new Product
        {
            Name = dto.Name,
            Description = dto.Description,
            Price = dto.Price,
            StockQuantity = dto.StockQuantity,
            Sku = dto.Sku
        };

        var created = await _repository.AddAsync(product, cancellationToken);
        return MapToDto(created);
    }

    private ProductDto MapToDto(Product product)
    {
        return new ProductDto
        {
            Id = product.Id,
            Name = product.Name,
            Description = product.Description,
            Price = product.Price,
            StockQuantity = product.StockQuantity,
            Sku = product.Sku,
            CreatedAt = product.CreatedAt,
            UpdatedAt = product.UpdatedAt
        };
    }
}
```

---

## 3️⃣ Infrastructure Layer - Baza Danych

### Co tu robimy?
- **DbContext** - EF Core mapowanie bazy
- **Repositories** - dostęp do danych
- **Migrations** - zarządzanie schematem bazy
- **Seeders** - wstępne dane

### Struktura:
```
Infrastructure/
├── Data/
│   ├── ApplicationDbContext.cs
│   ├── Migrations/
│   │   └── 001_InitialCreate.cs
│   └── Configurations/
│       └── ProductConfiguration.cs
└── Repositories/
    ├── Repository.cs          # Generyczne
    ├── ProductRepository.cs   # Specjalizowane
    └── UnitOfWork.cs
```

### Przykład - Konfiguracja encji:

```csharp
// Infrastructure/Data/Configurations/ProductConfiguration.cs
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.Description)
            .HasMaxLength(1000);

        builder.Property(x => x.Price)
            .HasPrecision(10, 2);

        builder.Property(x => x.Sku)
            .IsRequired()
            .HasMaxLength(50);

        // Indeks na Sku dla szybszych zapytań
        builder.HasIndex(x => x.Sku)
            .IsUnique();
    }
}
```

### Krok - Dodaj konfigurację do DbContext:

```csharp
// Infrastructure/Data/ApplicationDbContext.cs
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder);
    
    // Automatycznie aplikuj wszystkie konfiguracje
    modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
}
```

---

## 4️⃣ API Layer - HTTP Endpoints

### Co tu robimy?
- **Controllers** - endpoints HTTP
- **Middleware** - przetwarzanie requestów (error handling, auth)
- **Extensions** - rejestracja serwisów w DI

### Struktura:
```
API/
├── Controllers/
│   ├── ProductsController.cs
│   └── OrdersController.cs
├── Middleware/
│   ├── ErrorHandlingMiddleware.cs
│   └── AuthenticationMiddleware.cs
├── Extensions/
│   └── ServiceCollectionExtensions.cs
└── Program.cs
```

### Przykład - Utwórz Controller:

```csharp
// API/Controllers/ProductsController.cs
using Microsoft.AspNetCore.Mvc;
using Application.Services;
using Application.DTOs;
using Shared.Responses;

namespace API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly IService<ProductDto> _service;

    public ProductsController(IService<ProductDto> service)
    {
        _service = service;
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponse<ProductDto>>> GetById(int id)
    {
        var product = await _service.GetByIdAsync(id);
        if (product == null)
            return NotFound(ApiResponse<ProductDto>.ErrorResult("Product not found"));

        return Ok(ApiResponse<ProductDto>.SuccessResult(product));
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<IEnumerable<ProductDto>>>> GetAll()
    {
        var products = await _service.GetAllAsync();
        return Ok(ApiResponse<IEnumerable<ProductDto>>.SuccessResult(products));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<ProductDto>>> Create([FromBody] CreateProductDto dto)
    {
        var created = await _service.CreateAsync(dto);
        return Created($"api/products/{created.Id}", 
            ApiResponse<ProductDto>.SuccessResult(created, "Product created"));
    }
}
```

---

## 5️⃣ Shared Layer - Wspólne Klasy

### Co tu robimy?
- **Responses** - standardowe odpowiedzi API
- **Constants** - stałe aplikacyjne
- **Utils** - funkcje pomocnicze
- **Exceptions** - wyjątki aplikacyjne

### Już mamy ApiResponse<T>!

---

## 📝 Workflow - Jak dodać nową funkcjonalność

### 1. Zaproponuj: Dodaj produkt do sklepu

**Krok 1 - Domain (Encja)**
```csharp
// Domain/Entities/Product.cs
public class Product : BaseEntity { ... }
```

**Krok 2 - DTOs**
```csharp
// Application/DTOs/CreateProductDto.cs
// Application/DTOs/ProductDto.cs
```

**Krok 3 - Repository Interface**
```csharp
// Domain/Interfaces/IProductRepository.cs
public interface IProductRepository : IRepository<Product> { }
```

**Krok 4 - Service**
```csharp
// Application/Services/ProductService.cs
public class ProductService : IService<ProductDto> { }
```

**Krok 5 - Repository Implementation**
```csharp
// Infrastructure/Repositories/ProductRepository.cs
public class ProductRepository : Repository<Product>, IProductRepository { }
```

**Krok 6 - Controller**
```csharp
// API/Controllers/ProductsController.cs
public class ProductsController : ControllerBase { }
```

**Krok 7 - Dependency Injection** (w Program.cs)
```csharp
builder.Services.AddScoped<IRepository<Product>, ProductRepository>();
builder.Services.AddScoped<IService<ProductDto>, ProductService>();
```

---

## 💡 Zasady Clean Architecture

✅ **DO:**
- Logika biznesowa w Domain i Application
- DTOs dla komunikacji API
- Interfejsy dla dependency injection
- Walidacja w serwisach
- Mapperów do konwersji

❌ **NIE:**
- Logika biznesowa w kontrollerach!
- Wysyłania encji do klienta
- Bezpośredniego dostępu do bazy z kontrolera
- Mieszania warstw (Infrastructure w Domain)

---

## 🧪 Testowanie

Każda warstwa ma testy:
- **UnitTests** - testy serwisów bez bazy
- **IntegrationTests** - testy kontrollerów z prawdziwą bazą
- **E2ETests** - testy całego API

```csharp
// UnitTests/Services/ProductServiceTests.cs
[Fact]
public async Task CreateProduct_WithValidData_ReturnsProductDto()
{
    var mockRepository = new Mock<IRepository<Product>>();
    var service = new ProductService(mockRepository.Object);
    
    var dto = new CreateProductDto { Name = "Test", Price = 10 };
    var result = await service.CreateAsync(dto);
    
    Assert.NotNull(result);
    Assert.Equal("Test", result.Name);
}
```

---

## 🔗 Diagram Przepływu

```
HTTP Request
    ↓
Controller (API)
    ↓
Service (Application)
    ↓
Repository (Infrastructure)
    ↓
DbContext
    ↓
Database (SQL Server/PostgreSQL)
    ↓
Entity (Domain)
    ↓
DTO (Application)
    ↓
ApiResponse (Shared)
    ↓
HTTP Response (JSON)
```

---

## Szybki Start - Nowa funkcjonalność w 7 kroków

1. Stwórz Entity w `Domain/Entities`
2. Stwórz DTOs w `Application/DTOs`
3. Stwórz Repository Interface w `Domain/Interfaces`
4. Stwórz Service w `Application/Services`
5. Stwórz Repository Implementation w `Infrastructure/Repositories`
6. Stwórz Controller w `API/Controllers`
7. Zarejestruj w `Program.cs` Services i Repositories

**Gotowe!** 🎉
