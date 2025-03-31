### Etapa 1: Build da aplicação
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

# Copiar os arquivos do projeto e restaurar dependências
COPY . .
RUN dotnet restore
RUN dotnet publish -c Release -o /out

### Etapa 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Copiar os arquivos do build para a imagem final
COPY --from=build /out .

# Copiar o arquivo .env para a imagem
COPY .env .

# Expor a porta que a API escutará
EXPOSE 8080

# Definir a variável de ambiente para URLs
ENV ASPNETCORE_URLS=http://+:8080

# Carregar variáveis do .env
CMD export $(grep -v '^#' .env | xargs) && dotnet demanda_service.dll
