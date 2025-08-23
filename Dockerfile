FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 5000

# Install curl for health checks
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["Backend_Gestion_Magasin_API.csproj", "./"]
RUN dotnet restore "Backend_Gestion_Magasin_API.csproj"
COPY . .
WORKDIR "/src"
RUN dotnet build "Backend_Gestion_Magasin_API.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Backend_Gestion_Magasin_API.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Backend_Gestion_Magasin_API.dll"] 