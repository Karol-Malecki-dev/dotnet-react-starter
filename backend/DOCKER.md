# 🐳 Backend Docker - Wyjaśnienie

## Co to jest Dockerfile?
Plik, który opisuje jak zbudować **obraz Dockera** - to jak przepis na zestawienie aplikacji w kontenerze.

## Struktura naszego backendu Dockerfile

### 🏗️ Stage 1: Builder (Etap budowania)
```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS builder
```
- **FROM** - pobieramy bazowy obraz (w tym wypadku SDK .NET 9)
- **AS builder** - nazwę etapu (będziemy do niego odwoływać się później)
- **mcr.microsoft.com** - Microsoft Container Registry, oficjalny source Microsoftu

```dockerfile
WORKDIR /app
```
- Ustawiamy **folder roboczy** w kontenerze (jak `cd /app`)
- Wszystkie polecenia będą wykonane w tym folderze

```dockerfile
COPY . .
```
- Kopiujemy **wszystkie pliki z lokalnego folderu** do folderu `/app` w kontenerze
- Pierwszy `.` = z gdzie brać (lokalna maszyna)
- Drugi `.` = dokąd (folder `/app` w kontenerze)

```dockerfile
RUN dotnet restore
```
- **RUN** = wykonaj polecenie w kontenerze
- **dotnet restore** = pobierz wszystkie NuGet packages (biblioteki)
- Jest to jak `npm install` dla Node.js

```dockerfile
RUN dotnet build -c Release --no-restore
```
- **-c Release** = tryb Release (zoptymalizowany dla produkcji)
- **--no-restore** = nie pobieraj packagesów (już to zrobiliśmy)

```dockerfile
RUN dotnet publish -c Release -o /app/publish --no-build
```
- **publish** = przygotowuje aplikację do uruchomienia
- **-o /app/publish** = wyjście (gdzie zapisać)
- **--no-build** = nie buduj, już zbudowaliśmy

### 🚀 Stage 2: Runtime (Etap uruchomienia)
```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:9.0
```
- Pobieramy **nowy bazowy obraz** - tylko runtime (bez SDK)
- To robi obraz **DUŻO mniejszym** (brak zbędnego SDK)

```dockerfile
COPY --from=builder /app/publish .
```
- Kopujemy opublikowane binaria **z poprzedniego etapu (builder)**
- Nie kopiujemy cały kod źródłowy, tylko gotowe do uruchomienia pliki
- Rozmiarem: builder ~1GB, runtime ~400MB ✅

### Dlaczego 2 etapy?
**Multi-stage build** = mniejszy obraz!
- Etap 1 (builder): kompiluje kod, duży rozmiar
- Etap 2 (runtime): uruchamia już gotowe binaria, mały rozmiar
- Finalna aplikacja nie ma zbędnego SDK!

## Jak to działa lokalnie?

### Budowanie obrazu:
```bash
docker build -t dotnet-react-starter:backend -f backend/Dockerfile backend/
```
- `docker build` = zbuduj obraz
- `-t dotnet-react-starter:backend` = nazwa:tag
- `-f backend/Dockerfile` = ścieżka do pliku Dockerfile
- `backend/` = kontekst (folder z kodem)

### Uruchamianie:
```bash
docker run -p 5000:5000 dotnet-react-starter:backend
```
- `-p 5000:5000` = mapowanie portów (host:container)
- Port 5000 w kontenerze będzie dostępny na porcie 5000 na maszynie

## 🔑 Kluczowe zmienne

### ENV ASPNETCORE_URLS=http://+:5000
- **+** = nasłuchuj na wszystkich interfejsach sieciowych
- Dzięki temu Docker może się połączyć z aplikacją

### EXPOSE 5000
- **Dokumentacja** - informuje że aplikacja używa portu 5000
- Nie otwiera rzeczywiście - to tylko dla dokumentacji
- Port otwieramy za pomocą `-p` w `docker run`

## 📝 Czytanie logów

```bash
docker logs <container_id>
```
- Zobaczyć co aplikacja wypisuje na console

```bash
docker logs -f <container_id>
```
- `-f` = follow, śledź logi na żywo (jak `tail -f`)

## 🐛 Debugowanie

Jeśli coś nie działa:
```bash
# Wejdź do kontenera
docker exec -it <container_id> /bin/bash

# Sprawdź procesy
ps aux

# Sprawdź czy port nasłuchuje
netstat -an | grep 5000
```

## Checklist - co się dzieje gdy uruchamiamy container:

1. ✅ Docker pobiera bazowy obraz `.NET runtime`
2. ✅ Ustawia folder roboczy `/app`
3. ✅ Kopiuje nasze binaria
4. ✅ Otwiera port 5000
5. ✅ Uruchamia `dotnet API.dll`
6. ✅ API nasłuchuje na `http://0.0.0.0:5000`
7. ✅ Możemy się podłączyć z `http://localhost:5000`

## Producent vs Development

### Produkcja (nasz Dockerfile):
- Multi-stage build
- Release build (zoptymalizowany)
- Mały rozmiar obrazu

### Development (docker-compose.dev.yml):
- Możliwość hot-reload
- Volume mounts (zmiany na żywo)
- Debugowanie
