# syntax=docker/dockerfile:1

# C4 — API imajı.
#
# İki aşama: SDK ile derlenir, çalışma zamanı imajında yalnızca çıktı taşınır.
# Tek aşamalı bir imaj SDK'yı (yüzlerce MB) ve kaynak kodu üretime taşırdı.

ARG DOTNET_VERSION=8.0

FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION} AS build
WORKDIR /src

# Önce yalnızca proje dosyaları: kaynak değişince restore katmanı yeniden
# koşmasın.
COPY Directory.Build.props ./
COPY HabitTracker.sln ./
COPY src/HabitTracker.Domain/*.csproj src/HabitTracker.Domain/
COPY src/HabitTracker.Application/*.csproj src/HabitTracker.Application/
COPY src/HabitTracker.Infrastructure/*.csproj src/HabitTracker.Infrastructure/
COPY src/HabitTracker.Api/*.csproj src/HabitTracker.Api/
COPY tests/HabitTracker.UnitTests/*.csproj tests/HabitTracker.UnitTests/
COPY tests/HabitTracker.IntegrationTests/*.csproj tests/HabitTracker.IntegrationTests/
RUN dotnet restore src/HabitTracker.Api/HabitTracker.Api.csproj

COPY src/ src/
RUN dotnet publish src/HabitTracker.Api/HabitTracker.Api.csproj \
    -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION} AS runtime
WORKDIR /app
COPY --from=build /app ./

# Kök olmayan kullanıcı: imaj `app` kullanıcısıyla geliyor.
USER app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "HabitTracker.Api.dll"]
