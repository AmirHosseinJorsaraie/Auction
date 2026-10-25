FROM mcr.microsoft.com/dotnet/sdk:10.0.401-alpine AS build
WORKDIR /src
COPY . .
RUN dotnet restore src/Backend/ArtMarketplace.Identity/ArtMarketplace.Identity.csproj --locked-mode
RUN dotnet publish src/Backend/ArtMarketplace.Identity/ArtMarketplace.Identity.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0.12-alpine AS final
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "ArtMarketplace.Identity.dll"]
