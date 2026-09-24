# Builds the API and the Blazor client, and ships the client inside the API's wwwroot
# (same layout as .github/workflows/azure-deploy.yml).
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/BiteShare.Api -c Release -o /out \
 && dotnet publish src/BiteShare.Client -c Release -o /client \
 && mkdir -p /out/wwwroot && cp -r /client/wwwroot/. /out/wwwroot/

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /out .
# Render injects PORT; fall back to 10000 (Render's default).
ENV ASPNETCORE_ENVIRONMENT=Production
CMD ["sh", "-c", "ASPNETCORE_URLS=http://+:${PORT:-10000} dotnet BiteShare.Api.dll"]
