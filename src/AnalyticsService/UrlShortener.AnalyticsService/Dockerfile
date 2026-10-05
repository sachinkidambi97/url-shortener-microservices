FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY src/Shared/UrlShortener.Shared/UrlShortener.Shared.csproj src/Shared/UrlShortener.Shared/
COPY src/AnalyticsService/UrlShortener.AnalyticsService/UrlShortener.AnalyticsService.csproj src/AnalyticsService/UrlShortener.AnalyticsService/

RUN dotnet restore src/AnalyticsService/UrlShortener.AnalyticsService/UrlShortener.AnalyticsService.csproj

COPY src/Shared/ src/Shared/
COPY src/AnalyticsService/ src/AnalyticsService/

RUN dotnet publish src/AnalyticsService/UrlShortener.AnalyticsService/UrlShortener.AnalyticsService.csproj \
    -c Release -o /app/publish 

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "UrlShortener.AnalyticsService.dll"]
