# 1. ������� �����
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

# 2. SDK ��� ������
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# 3. �������� csproj � ��������������� �����������
COPY *.csproj ./
RUN dotnet restore

# 4. �������� ���� ������ � ��������
COPY . ./
RUN dotnet publish -c Release -o /app/publish

# 5. ��������� �����
FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "ClientServices.dll"]
