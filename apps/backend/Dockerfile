FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build

WORKDIR /src

COPY src/Abadar.Backend/Abadar.Backend.csproj src/Abadar.Backend/
RUN dotnet restore src/Abadar.Backend/Abadar.Backend.csproj

COPY src/Abadar.Backend/ src/Abadar.Backend/
RUN dotnet publish src/Abadar.Backend/Abadar.Backend.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runner

WORKDIR /app

ENV ASPNETCORE_URLS=http://0.0.0.0:8080
ENV DOTNET_EnableDiagnostics=0

RUN addgroup -S abadar \
    && adduser -S -G abadar -u 10001 abadar

COPY --from=build /app/publish .

USER abadar
EXPOSE 8080

ENTRYPOINT ["dotnet", "Abadar.Backend.dll"]
