# ⚛️ Frontend Setup - Struktura React + TypeScript

This frontend now uses a backend-driven runtime config during bootstrap.

- `RuntimeConfigProvider` loads feature flags before the app shell renders.
- `useFeatureAvailability()` centralizes feature gating.
- `AppBootstrapGate` prevents shell flashing while auth and config are loading.
- `Navbar` contains a small quick search bar enabled by runtime flags.

Keep real authorization in the backend. Use feature flags only for UI visibility and flow gating.

## Struktura Frontendowa

```

## Environment Configuration

Frontend ma tylko publiczne zmienne build-time z prefiksem `REACT_APP_`.

Najważniejsza zmienna w tym projekcie:

- `REACT_APP_API_URL`

Tryby użycia:

1. Local frontend -> local backend:
```env
REACT_APP_API_URL=http://localhost:5000
```

2. Frontend za nginx reverse proxy / Docker:
```env
REACT_APP_API_URL=/api
```

Zasady:

- Nigdy nie wkładaj sekretów do `REACT_APP_*`.
- Traktuj `frontend/.env.example` jako szablon, a własne wartości trzymaj w `frontend/.env.development.local` lub w pipeline builda.
- Dla auth cookie frontend musi wysyłać requesty z `credentials: 'include'`.

## Runtime Configuration

Backend exposes `GET /api/runtime-config`.

Useful flags in this starter:

- `GlobalSearchEnabled`
- `DashboardOverviewEnabled`
- `AdminNavigationEnabled`
- `UserManagementNavigationEnabled`
- `EmailFeatureSectionsEnabled`

Use a single hook/helper to read flags and avoid scattering `if` statements around `App.tsx`, `Navbar.tsx`, and pages.
frontend/
├── public/                 # Statyczne pliki (favicon, manifest)
├── src/
│   ├── components/         # Reusable komponenty
│   ├── pages/              # Page components (full pages)
│   ├── services/           # API calls (axios instance)
│   ├── hooks/              # Custom React hooks
│   ├── context/            # State management (Zustand)
│   ├── types/              # TypeScript interfaces
│   ├── utils/              # Helper functions
│   ├── App.tsx             # Główny komponent
│   └── index.tsx           # Entry point
└── package.json            # Dependencje
```

---

## 📦 Dostępne Packagi

```json
{
  "dependencies": {
    "react": "^18",
    "react-router-dom": "Latest",     // Routing
    "react-query": "Latest",           // Server state (API cache)
    "zustand": "Latest",               // State management (client state)
    "axios": "Latest",                 // HTTP client
    "@mui/material": "Latest",         // UI Components
    "@emotion/react": "Latest",        // CSS-in-JS (dla MUI)
    "react-hook-form": "Latest",       // Form handling
    "zod": "Latest",                   // Schema validation
    "@hookform/resolvers": "Latest",   // Zod + React Hook Form
    "date-fns": "Latest",              // Date manipulation
    "lodash-es": "Latest"              // Utility functions
  }
}
```

---

## 1️⃣ Types/Interfaces - TypeScript

### Struktura:
```
src/types/
├── index.ts               # Eksport wszystkich typów
├── api.ts                 # Typy API responses
├── domain.ts              # Domenowe typy biznesowe
└── forms.ts               # Typy formularzy
```

### Przykład - Utwórz typy produktów:

```typescript
// src/types/domain.ts
export interface Product {
  id: number;
  name: string;
  description: string;
  price: number;
  stockQuantity: number;
  sku: string;
  createdAt: Date;
  updatedAt: Date;
}

// src/types/api.ts (API Response types)
export interface ApiResponse<T> {
  success: boolean;
  message: string;
  data?: T;
  errors?: Record<string, string[]>;
}
```

---

## 2️⃣ Services - API Communication

### Struktura:
```
src/services/
├── api.ts                 # Axios instance
├── productService.ts
├── userService.ts
└── authService.ts
```

### Przykład - API Configuration:

```typescript
// src/services/api.ts
import axios from 'axios';

const API_URL = process.env.REACT_APP_API_URL || 'http://localhost:5000';

export const api = axios.create({
  baseURL: `${API_URL}/api`,
  headers: {
    'Content-Type': 'application/json',
  },
});

// Interceptor dla errorów
api.interceptors.response.use(
  (response) => response,
  (error) => {
    console.error('API Error:', error);
    return Promise.reject(error);
  }
);

export default api;
```

### Przykład - Service:

```typescript
// src/services/productService.ts
import api from './api';
import { Product, ApiResponse } from '../types';

export const productService = {
  // Pobierz jeden produkt
  getById: async (id: number) => {
    const response = await api.get<ApiResponse<Product>>(`/products/${id}`);
    return response.data.data;
  },

  // Pobierz wszystkie produkty
  getAll: async () => {
    const response = await api.get<ApiResponse<Product[]>>('/products');
    return response.data.data || [];
  },

  // Utwórz produkt
  create: async (data: Omit<Product, 'id' | 'createdAt' | 'updatedAt'>) => {
    const response = await api.post<ApiResponse<Product>>('/products', data);
    return response.data.data;
  },

  // Aktualizuj produkt
  update: async (id: number, data: Partial<Product>) => {
    const response = await api.put<ApiResponse<Product>>(`/products/${id}`, data);
    return response.data.data;
  },

  // Usuń produkt
  delete: async (id: number) => {
    await api.delete(`/products/${id}`);
  },
};
```

---

## 3️⃣ Hooks - Custom React Hooks

### Struktura:
```
src/hooks/
├── useProducts.ts         # Produkt logic
├── useAuth.ts             # Authentication
├── useNotification.ts     # Powiadomienia
└── useFetch.ts            # Generic fetching
```

### Przykład - Custom Hook z React Query:

```typescript
// src/hooks/useProducts.ts
import { useQuery, useMutation, useQueryClient } from 'react-query';
import { productService } from '../services';
import { Product } from '../types';

export const useProducts = () => {
  return useQuery(['products'], () => productService.getAll(), {
    staleTime: 5 * 60 * 1000, // 5 minut
  });
};

export const useProduct = (id: number) => {
  return useQuery(['product', id], () => productService.getById(id), {
    enabled: !!id,
  });
};

export const useCreateProduct = () => {
  const queryClient = useQueryClient();
  
  return useMutation(
    (data: Omit<Product, 'id' | 'createdAt' | 'updatedAt'>) =>
      productService.create(data),
    {
      onSuccess: () => {
        // Odśwież listę po dodaniu
        queryClient.invalidateQueries(['products']);
      },
    }
  );
};

export const useDeleteProduct = () => {
  const queryClient = useQueryClient();
  
  return useMutation((id: number) => productService.delete(id), {
    onSuccess: () => {
      queryClient.invalidateQueries(['products']);
    },
  });
};
```

---

## 4️⃣ Components - Reusable UI

### Struktura:
```
src/components/
├── Layout/
│   ├── Header.tsx
│   ├── Sidebar.tsx
│   └── Footer.tsx
├── Forms/
│   ├── ProductForm.tsx
│   └── LoginForm.tsx
├── Cards/
│   ├── ProductCard.tsx
│   └── UserCard.tsx
└── Common/
    ├── Button.tsx
    ├── Modal.tsx
    └── LoadingSpinner.tsx
```

### Przykład - Komponent Reusable:

```typescript
// src/components/Common/LoadingSpinner.tsx
import { Box, CircularProgress } from '@mui/material';

interface LoadingSpinnerProps {
  size?: 'small' | 'medium' | 'large';
  message?: string;
}

export const LoadingSpinner: React.FC<LoadingSpinnerProps> = ({
  size = 'medium',
  message = 'Loading...',
}) => {
  const sizeMap = { small: 30, medium: 50, large: 70 };

  return (
    <Box
      display="flex"
      flexDirection="column"
      justifyContent="center"
      alignItems="center"
      minHeight="200px"
    >
      <CircularProgress size={sizeMap[size]} />
      {message && <p style={{ marginTop: '16px' }}>{message}</p>}
    </Box>
  );
};
```

### Przykład - Komponent z formą:

```typescript
// src/components/Forms/ProductForm.tsx
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { TextField, Button, Box } from '@mui/material';
import { Product } from '../../types';

const productSchema = z.object({
  name: z.string().min(1, 'Name is required').max(200),
  description: z.string().max(1000).optional(),
  price: z.number().positive('Price must be positive'),
  stockQuantity: z.number().int().min(0),
  sku: z.string().min(1, 'SKU is required'),
});

type ProductFormData = z.infer<typeof productSchema>;

interface ProductFormProps {
  initialData?: Product;
  onSubmit: (data: ProductFormData) => Promise<void>;
  isLoading?: boolean;
}

export const ProductForm: React.FC<ProductFormProps> = ({
  initialData,
  onSubmit,
  isLoading = false,
}) => {
  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<ProductFormData>({
    resolver: zodResolver(productSchema),
    defaultValues: initialData,
  });

  return (
    <Box component="form" onSubmit={handleSubmit(onSubmit)} sx={{ gap: 2, display: 'flex', flexDirection: 'column' }}>
      <TextField
        {...register('name')}
        label="Product Name"
        error={!!errors.name}
        helperText={errors.name?.message}
      />

      <TextField
        {...register('price', { valueAsNumber: true })}
        label="Price"
        type="number"
        error={!!errors.price}
        helperText={errors.price?.message}
      />

      <Button type="submit" variant="contained" disabled={isLoading}>
        {isLoading ? 'Saving...' : 'Save'}
      </Button>
    </Box>
  );
};
```

---

## 5️⃣ Pages - Full Page Components

### Struktura:
```
src/pages/
├── ProductsPage.tsx       # Lista produktów
├── ProductDetailPage.tsx  # Szczegóły produktu
├── CreateProductPage.tsx  # Formularz dodawania
└── NotFoundPage.tsx       # 404
```

### Przykład - Page z routingiem:

```typescript
// src/pages/ProductsPage.tsx
import { useNavigate } from 'react-router-dom';
import { Button, Box, Grid } from '@mui/material';
import { useProducts } from '../hooks/useProducts';
import { LoadingSpinner } from '../components/Common/LoadingSpinner';
import { ProductCard } from '../components/Cards/ProductCard';

export const ProductsPage: React.FC = () => {
  const navigate = useNavigate();
  const { data: products, isLoading, error } = useProducts();

  if (isLoading) return <LoadingSpinner />;
  if (error) return <div>Error loading products</div>;

  return (
    <Box sx={{ p: 3 }}>
      <Box display="flex" justifyContent="space-between" mb={3}>
        <h1>Products</h1>
        <Button
          variant="contained"
          onClick={() => navigate('/products/create')}
        >
          Add Product
        </Button>
      </Box>

      <Grid container spacing={2}>
        {products?.map((product) => (
          <Grid item xs={12} sm={6} md={4} key={product.id}>
            <ProductCard product={product} />
          </Grid>
        ))}
      </Grid>
    </Box>
  );
};
```

---

## 6️⃣ Context/Store - State Management (Zustand)

### Struktura:
```
src/context/
├── authStore.ts           # Auth state
├── notificationStore.ts   # Notifications
└── appStore.ts            # Global app state
```

### Przykład - Zustand Store:

```typescript
// src/context/authStore.ts
import { create } from 'zustand';

interface AuthState {
  token: string | null;
  user: { id: number; email: string } | null;
  isAuthenticated: boolean;

  // Actions
  login: (token: string, user: any) => void;
  logout: () => void;
}

export const useAuthStore = create<AuthState>((set) => ({
  token: localStorage.getItem('token'),
  user: null,
  isAuthenticated: !!localStorage.getItem('token'),

  login: (token, user) => {
    localStorage.setItem('token', token);
    set({ token, user, isAuthenticated: true });
  },

  logout: () => {
    localStorage.removeItem('token');
    set({ token: null, user: null, isAuthenticated: false });
  },
}));
```

---

## 7️⃣ App.tsx - Routing Setup

### Przykład - React Router:

```typescript
// src/App.tsx
import { BrowserRouter as Router, Routes, Route } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from 'react-query';
import { ProductsPage } from './pages/ProductsPage';
import { ProductDetailPage } from './pages/ProductDetailPage';
import { CreateProductPage } from './pages/CreateProductPage';
import { NotFoundPage } from './pages/NotFoundPage';

const queryClient = new QueryClient();

function App() {
  return (
    <QueryClientProvider client={queryClient}>
      <Router>
        <Routes>
          <Route path="/products" element={<ProductsPage />} />
          <Route path="/products/:id" element={<ProductDetailPage />} />
          <Route path="/products/create" element={<CreateProductPage />} />
          <Route path="*" element={<NotFoundPage />} />
        </Routes>
      </Router>
    </QueryClientProvider>
  );
}

export default App;
```

---

## 📝 Workflow - Jak dodać nową funkcjonalność

### 1. Dodaj nowy endpoint (np. Categories)

**Krok 1 - Types**
```typescript
// src/types/domain.ts
export interface Category {
  id: number;
  name: string;
  description: string;
}
```

**Krok 2 - Service**
```typescript
// src/services/categoryService.ts
export const categoryService = {
  getAll: async () => { ... },
  getById: async (id) => { ... },
  create: async (data) => { ... },
};
```

**Krok 3 - Hook**
```typescript
// src/hooks/useCategories.ts
export const useCategories = () => {
  return useQuery(['categories'], () => categoryService.getAll());
};
```

**Krok 4 - Component**
```typescript
// src/components/Cards/CategoryCard.tsx
export const CategoryCard: React.FC<{ category: Category }> = ({ category }) => {
  return <div>{category.name}</div>;
};
```

**Krok 5 - Page**
```typescript
// src/pages/CategoriesPage.tsx
export const CategoriesPage: React.FC = () => {
  const { data: categories } = useCategories();
  return <Grid>{categories?.map(cat => <CategoryCard key={cat.id} category={cat} />)}</Grid>;
};
```

**Krok 6 - Routing**
```typescript
// src/App.tsx
<Route path="/categories" element={<CategoriesPage />} />
```

---

## 💡 Best Practices

✅ **DO:**
- Dziel komponenty na małe, reusable części
- Używaj TypeScript dla type safety
- Trzymaj logikę w hooks (nie w komponentach!)
- Używaj React Query dla server state
- Używaj Zustand dla client state
- Waliduj formy z Zod
- Komponenty > 200 linii ponieważ za duże

❌ **NIE:**
- Komponenty nie powinny robić API calls bezpośrednio
- Nie mieszaj hooks z logika biznesową
- Nie trzymaj wszystkiego w App.tsx
- Nie robić nieskończonych re-renderów
- Nie używaj `any` zamiast TypeScript typów

---

## 🔗 Diagram Przepływu Frontend

```
User Interaction
    ↓
Component (JSX)
    ↓
Hook (useProducts)
    ↓
React Query (Caching)
    ↓
Service (productService)
    ↓
Axios (API Call)
    ↓
Backend API
    ↓
Response (JSON)
    ↓
Store (Component State)
    ↓
Re-render (UI Update)
```

---

## 🧪 Testowanie

```typescript
// src/components/__tests__/ProductCard.test.tsx
import { render, screen } from '@testing-library/react';
import { ProductCard } from '../Cards/ProductCard';

describe('ProductCard', () => {
  it('renders product name', () => {
    const product = {
      id: 1,
      name: 'Test Product',
      price: 10,
      description: '',
      stockQuantity: 5,
      sku: 'TEST',
      createdAt: new Date(),
      updatedAt: new Date(),
    };

    render(<ProductCard product={product} />);
    expect(screen.getByText('Test Product')).toBeInTheDocument();
  });
});
```

---

## Szybki Start - Nowy Feature

1. Stwórz typy w `src/types`
2. Stwórz service w `src/services`
3. Stwórz hook w `src/hooks`
4. Stwórz komponenty w `src/components`
5. Stwórz page w `src/pages`
6. Dodaj routing w `src/App.tsx`

**Gotowe!** 🎉
