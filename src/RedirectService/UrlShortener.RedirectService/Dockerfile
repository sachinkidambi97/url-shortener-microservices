FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY src/Shared/UrlShortener.Shared/UrlShortener.Shared.csproj src/Shared/UrlShortener.Shared/
COPY src/RedirectService/UrlShortener.RedirectService/UrlShortener.RedirectService.csproj src/RedirectService/UrlShortener.RedirectService/

RUN dotnet restore src/RedirectService/UrlShortener.RedirectService/UrlShortener.RedirectService.csproj

COPY src/Shared/ src/Shared/
COPY src/RedirectService/ src/RedirectService/

RUN dotnet publish src/RedirectService/UrlShortener.RedirectService/UrlShortener.RedirectService.csproj \
    -c Release -o /app/publish 

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "UrlShortener.RedirectService.dll"]
