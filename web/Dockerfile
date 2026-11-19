FROM node:24-alpine AS client
WORKDIR /client
COPY client/ ./
RUN if [ -f package.json ]; then npm ci && npm run build; else mkdir -p dist; fi

FROM mcr.microsoft.com/dotnet/sdk:11.0 AS build
WORKDIR /src
COPY SafariExplorer.slnx ./
COPY src/ src/
RUN dotnet publish src/SafariExplorer.Api -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:11.0
WORKDIR /app
COPY --from=build /app ./
COPY --from=client /client/dist ./wwwroot
ENV ASPNETCORE_HTTP_PORTS=8080 \
    DATA_DIR=/data
EXPOSE 8080
ENTRYPOINT ["dotnet", "SafariExplorer.Api.dll"]
