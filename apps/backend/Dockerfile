FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src

# Restore backend and matching-engine dependencies before copying full sources.
COPY apps/backend/src/Abadar.Backend/Abadar.Backend.csproj apps/backend/src/Abadar.Backend/
COPY apps/matching-engine/src/Abadar.MatchingEngine/Abadar.MatchingEngine.csproj apps/matching-engine/src/Abadar.MatchingEngine/
RUN dotnet restore apps/backend/src/Abadar.Backend/Abadar.Backend.csproj

# Publish the backend together with its Version 1 matching-engine reference.
COPY apps/backend/src/Abadar.Backend/ apps/backend/src/Abadar.Backend/
COPY apps/matching-engine/src/Abadar.MatchingEngine/ apps/matching-engine/src/Abadar.MatchingEngine/
RUN dotnet publish apps/backend/src/Abadar.Backend/Abadar.Backend.csproj \
    --configuration Release \
    --output /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runner

WORKDIR /app

# Bind the container API port and disable runtime diagnostics in the slim image.
ENV ASPNETCORE_URLS=http://0.0.0.0:8080
ENV DOTNET_EnableDiagnostics=0

# Run the published backend as an unprivileged application user.
RUN addgroup -S abadar \
    && adduser -S -G abadar -u 10001 abadar \
    && apk add --no-cache krb5-libs

COPY --from=build /app/publish .

USER abadar
EXPOSE 8080

ENTRYPOINT ["dotnet", "Abadar.Backend.dll"]
