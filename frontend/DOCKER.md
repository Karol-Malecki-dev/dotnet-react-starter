# 🐳 Frontend Docker - Wyjaśnienie

## Co to jest Dockerfile?
Przepis na zbudowanie **kontenera z aplikacją React**. Frontend wymaga innego podejścia niż backend!

## Struktura naszego frontendu Dockerfile

### 📦 Stage 1: Builder (Etap budowania)
```dockerfile
FROM node:20-alpine AS builder
```
- **node:20-alpine** = bazowy obraz Node.js (alpine = bardzo mały, ~160MB)
- **AS builder** = nazwa etapu do odwołania się później
- Alpine = Linux minimalistyczny, bez zbędnych rzeczy

```dockerfile
WORKDIR /app
```
- Folder roboczy w kontenerze (jak `cd /app`)

```dockerfile
COPY package*.json ./
```
- Kopiujemy `package.json` i `package-lock.json`
- `*` = wildcard, pobiera oba pliki jeśli istnieją
- Kopiujemy je OSOBNO zanim kod, bo npm install się cachuje (szybciej!)

```dockerfile
RUN npm ci
```
- **npm ci** (clean install) = instaluje dokładnie wersje z package-lock.json
- Lepsze od `npm install` w Dockerze (bardziej deterministyczne)
- Pobiera wszystkie biblioteki React, React Router, Material-UI, etc.

```dockerfile
COPY . .
```
- Kopiujemy cały kod React

```dockerfile
RUN npm run build
```
- Buduje aplikację React
- Tworzy folder `build/` z zoptymalizowanymi `.js`, `.css`, `.html` plikami
- HTML aplikacji: `build/index.html`

### 🌐 Stage 2: Runtime - Nginx (serwer WWW)
```dockerfile
FROM nginx:alpine
```
- Nowy bazowy obraz - **nginx** (ultra lekki serwer HTTP)
- Tylko ~10MB!
- Doskonały do serwowania statycznych plików (HTML, CSS, JS)

```dockerfile
COPY docker/nginx.conf /etc/nginx/nginx.conf
```
- Kopiujemy **konfigurację nginx**
- Plik `docker/nginx.conf` opisuje jak nginx ma się zachowywać
- (Będziemy musieć stworzyć ten plik)

```dockerfile
RUN rm -rf /usr/share/nginx/html/*
```
- Usuwamy domyślną stronę nginx
- `html/` to folder gdzie nginx szuka plików do serwowania

```dockerfile
COPY --from=builder /app/dist /usr/share/nginx/html
```
- Kopujemy zbudowaną aplikację React z etapu builder
- Nginx będzie serwować te pliki

## Dlaczego nginx zamiast Node.js?

### ❌ Node.js (złe):
```dockerfile
FROM node:20
COPY . .
CMD ["npm", "start"]
```
- Obraz: ~1GB (zbędny Node SDK)
- Wolniej start
- Wiele potrzebnych zależności

### ✅ Nginx (dobre):
```dockerfile
FROM nginx:alpine
COPY build /usr/share/nginx/html
```
- Obraz: ~20MB (nginx + alpine linux)
- Szybko start
- Tylko to co potrzebne

**React już jest zbudowany do statycznych plików!** Nginx je serwuje 100x szybciej niż Node.js.

## Nginx - co to robi?

Nginx to serwer HTTP. W naszym przypadku:
1. Nasłuchuje na porcie 3000
2. Kiedy użytkownik otworzy `http://localhost:3000`, serwuje `index.html`
3. React się ładuje (React Router obsługuje routy)
4. Kiedy użytkownik zmienia stronę, React robi to bez odświeżenia!

## Jak to działa lokalnie?

### Budowanie obrazu:
```bash
docker build -t dotnet-react-starter:frontend -f frontend/Dockerfile frontend/
```

### Uruchamianie:
```bash
docker run -p 3000:3000 dotnet-react-starter:frontend
```
- Otwórz `http://localhost:3000`

## 🔑 Zmienne

### EXPOSE 3000
- Port na którym nasłuchuje aplikacja
- Frontend React → port 3000 (standard)

### nginx -g "daemon off;"
- **daemon off** = nginx nie uruchamia się w tle
- Dzięki temu Docker widzi proces (może go kontrolować)

## Checklist - co się dzieje:

1. ✅ Docker pobiera Node.js
2. ✅ Instaluje npm packages
3. ✅ Buduje React (`npm run build` → folder `build/`)
4. ✅ Docker pobiera nginx
5. ✅ Kopiuje folder `build/` do nginx
6. ✅ Uruchamia nginx na porcie 3000
7. ✅ `http://localhost:3000` serwuje `build/index.html`

## Czyszczenie cache (jeśli zmienisz package.json)

```bash
# Usuń obraz
docker rmi dotnet-react-starter:frontend

# Przebuduj od zera
docker build -t dotnet-react-starter:frontend frontend/
```

## Hot-reload w Development

W Dockerzie (produkcja) = brak hot-reload (build jest do serwowania)

W development (docker-compose.dev.yml) = będziemy mieć volume mount:
```yaml
volumes:
  - ./frontend/src:/app/src
```
To pozwoli zmieniać kod i React będzie się przebudowywać na bieżąco!

## Problemy i rozwiązania

### ❌ Błąd: "Cannot find module"
```
RUN npm ci  # użyj zamiast npm install
```

### ❌ Błąd: nginx: unknown error
```
# Sprawdź nginx.conf - musi być poprawna składnia
docker logs <container_id>
```

### ❌ Port już w użyciu
```bash
docker run -p 3001:3000 dotnet-react-starter:frontend
# Teraz dostęp przez port 3001, a w kontenerze 3000
```
