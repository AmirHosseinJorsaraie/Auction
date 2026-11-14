FROM mcr.microsoft.com/dotnet/sdk:10.0.401-alpine AS build
WORKDIR /src
COPY . .
RUN dotnet restore src/Frontend/PublicWeb/ArtMarketplace.PublicWeb.csproj --locked-mode
RUN dotnet publish src/Frontend/PublicWeb/ArtMarketplace.PublicWeb.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0.15-alpine AS final
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "ArtMarketplace.PublicWeb.dll"]
