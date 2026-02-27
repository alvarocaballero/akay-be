FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["src/Akay.Be.Host/Akay.Be.Host.csproj", "src/Akay.Be.Host/"]
COPY ["src/Akay.Be.Application/Akay.Be.Application.csproj", "src/Akay.Be.Application/"]
COPY ["src/Akay.Be.Domain/Akay.Be.Domain.csproj", "src/Akay.Be.Domain/"]
COPY ["src/Akay.Be.Infrastructure/Akay.Be.Infrastructure.csproj", "src/Akay.Be.Infrastructure/"]
COPY ["Directory.Build.props", "."]
COPY ["Directory.Packages.props", "."]
RUN dotnet restore "src/Akay.Be.Host/Akay.Be.Host.csproj"
COPY . .
WORKDIR "/src/src/Akay.Be.Host"
RUN dotnet build "Akay.Be.Host.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "Akay.Be.Host.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Akay.Be.Host.dll"]
