FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution and project files first for layer caching
COPY src/Shared/UrlShortener.Shared/UrlShortener.Shared.csproj src/Shared/UrlShortener.Shared/
COPY src/ShorteningApi/UrlShortener.ShorteningApi/UrlShortener.ShorteningApi.csproj src/ShorteningApi/UrlShortener.ShorteningApi/

RUN dotnet restore src/ShorteningApi/UrlShortener.ShorteningApi/UrlShortener.ShorteningApi.csproj

# Copy all source
COPY src/Shared/ src/Shared/
COPY src/ShorteningApi/ src/ShorteningApi/

RUN dotnet publish src/ShorteningApi/UrlShortener.ShorteningApi/UrlShortener.ShorteningApi.csproj \
    -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "UrlShortener.ShorteningApi.dll"]
