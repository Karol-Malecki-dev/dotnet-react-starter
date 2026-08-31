# Stage 1: Builder - Kompilacja aplikacji
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS builder

# Ustawiamy folder roboczy w kontenerze
WORKDIR /app

# Kopiujemy cały kod backendu do kontenera
COPY . .

# Przywracamy zależności NuGet (packages)
RUN dotnet restore

# Budujemy aplikację w trybie Release (zoptymalizowany)
RUN dotnet build -c Release --no-restore

# Publikujemy tylko projekt API, aby katalog runtime zawierał spójne zależności aplikacji.
RUN dotnet publish API/API.csproj -c Release -o /app/publish --no-build


# Stage 2: Runtime - Uruchomienie aplikacji
FROM mcr.microsoft.com/dotnet/aspnet:9.0

# Ustawiamy folder roboczy
WORKDIR /app

RUN apt-get -o Acquire::Check-Valid-Until=false -o Acquire::Check-Date=false update \
	&& apt-get install -y --no-install-recommends curl \
	&& rm -rf /var/lib/apt/lists/*

# Kopiujemy TYLKO opublikowane binaria z etapu builder
# (Nie potrzebujemy SDK, tylko runtime - mały rozmiar!)
COPY --from=builder /app/publish .

# Runtime-owned directories must remain writable by the non-root application user.
RUN mkdir -p /home/app/.aspnet/DataProtection-Keys /app/logs /app/data/task-attachments \
	&& chown -R $APP_UID /home/app /app/logs /app/data

USER $APP_UID

# Otwieramy port 5000 (na tym porcie nasłuchuje API)
EXPOSE 5000

# Ustawiamy zmienną środowiskową dla ASP.NET Core
ENV ASPNETCORE_URLS=http://+:5000

# Uruchamiamy aplikację - API.dll to skompiowana DLL naszego projektu
ENTRYPOINT ["dotnet", "API.dll"]
