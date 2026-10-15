FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/VeriPass.Domain/VeriPass.Domain.csproj", "src/VeriPass.Domain/"]
COPY ["src/VeriPass.Application/VeriPass.Application.csproj", "src/VeriPass.Application/"]
COPY ["src/VeriPass.Infrastructure/VeriPass.Infrastructure.csproj", "src/VeriPass.Infrastructure/"]
COPY ["src/VeriPass.API/VeriPass.API.csproj", "src/VeriPass.API/"]
RUN dotnet restore "src/VeriPass.API/VeriPass.API.csproj"
COPY . .
WORKDIR "/src/src/VeriPass.API"
RUN dotnet build "VeriPass.API.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "VeriPass.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "VeriPass.API.dll"]
