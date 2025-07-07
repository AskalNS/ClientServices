# 1. Базовый образ
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

# 2. SDK для сборки
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# 3. Копируем csproj и восстанавливаем зависимости
COPY *.csproj ./
RUN dotnet restore

# 4. Копируем весь проект и собираем
COPY . ./
RUN dotnet publish -c Release -o /app/publish

# 5. Финальный образ
FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "ClientServices.dll"]
