# Этап 1: Сборка
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Копируем файлы проектов
COPY ["../src/EducationServer.Api/EducationServer.Api.csproj", "EducationServer.Api/"]
COPY ["../src/EducationServer.Data/EducationServer.Data.csproj", "EducationServer.Data/"]
COPY ["../src/EducationServer.Domain/EducationServer.Domain.csproj", "EducationServer.Domain/"]
COPY ["../src/EducationServer.Services/EducationServer.Services.csproj", "EducationServer.Services/"]

# Восстанавливаем пакеты
RUN dotnet restore "EducationServer.Api/EducationServer.Api.csproj"

# Копируем весь остальной исходный код
COPY ../src/ .

# Собираем и публикуем
WORKDIR "/src/EducationServer.Api"
RUN dotnet publish "EducationServer.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Этап 2: Запуск
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "EducationServer.Api.dll"]