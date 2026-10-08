# ==========================================
# Multi-stage Dockerfile for ASP.NET Core 8
# ==========================================

# ── Stage 1: Build & Publish ──
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy file csproj để cache layer nuget restore
COPY ["EduFlyUp/EduFlyUp.BusinessObjects/EduFlyUp.BusinessObjects.csproj", "EduFlyUp/EduFlyUp.BusinessObjects/"]
COPY ["EduFlyUp/EduFlyUp.DataAccess/EduFlyUp.DataAccess.csproj", "EduFlyUp/EduFlyUp.DataAccess/"]
COPY ["EduFlyUp/EduFlyUp.Repositories/EduFlyUp.Repositories.csproj", "EduFlyUp/EduFlyUp.Repositories/"]
COPY ["EduFlyUp/EduFlyUp.Services/EduFlyUp.Services.csproj", "EduFlyUp/EduFlyUp.Services/"]
COPY ["EduFlyUp/EduFlyUp.Web/EduFlyUp.Web.csproj", "EduFlyUp/EduFlyUp.Web/"]

RUN dotnet restore "EduFlyUp/EduFlyUp.Web/EduFlyUp.Web.csproj"

# Copy toàn bộ mã nguồn
COPY EduFlyUp/ EduFlyUp/

# Build và Publish
WORKDIR "/src/EduFlyUp/EduFlyUp.Web"
RUN dotnet publish "EduFlyUp.Web.csproj" -c Release -o /app/publish /p:UseAppHost=false

# ── Stage 2: Runtime Environment ──
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "EduFlyUp.Web.dll"]
