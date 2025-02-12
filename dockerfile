# Use uma imagem do .NET como base
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 5000
EXPOSE 5001

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["demanda_service.csproj", "./"]
RUN dotnet restore "./demanda_service.csproj"
COPY . .
RUN dotnet publish "demanda_service.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "demanda_service.dll"]
