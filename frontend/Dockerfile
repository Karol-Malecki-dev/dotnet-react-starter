# Stage 1: Builder - Budowanie aplikacji React
FROM node:22-alpine AS builder

# Ustawiamy folder roboczy
WORKDIR /app

ARG VITE_API_URL=/api
ARG NODE_OPTIONS=--max_old_space_size=4096
ENV VITE_API_URL=$VITE_API_URL
ENV NODE_OPTIONS=$NODE_OPTIONS

# Kopiujemy package.json i package-lock.json
COPY package*.json ./

# Instalujemy wszystkie zależności (npm packages)
RUN npm ci

# Kopiujemy kod źródłowy React
COPY . .

# Budujemy aplikację React (tworzy folder dist/)
RUN npm run build


# Stage 2: Runtime - Serwowanie aplikacji za pomocą Nginx
FROM nginx:1.28-alpine

# Kopiujemy konfigurację nginx (będzie w docker/nginx.conf)
COPY docker/nginx.conf /etc/nginx/nginx.conf

# Usuwamy domyślny HTML nginx
RUN rm -rf /usr/share/nginx/html/*

# Kopiujemy zbudowaną aplikację React z etapu builder
COPY --from=builder /app/dist /usr/share/nginx/html

# Otwieramy port 3000 (na tym porcie nasłuchuje aplikacja)
EXPOSE 3000

# Uruchamiamy nginx
CMD ["nginx", "-g", "daemon off;"]
