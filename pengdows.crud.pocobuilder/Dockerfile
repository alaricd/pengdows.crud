FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["pengdows.crud.pocobuilder/pengdows.crud.pocobuilder.csproj", "pengdows.crud.pocobuilder/"]
RUN dotnet restore "pengdows.crud.pocobuilder/pengdows.crud.pocobuilder.csproj"
COPY . .
WORKDIR "/src/pengdows.crud.pocobuilder"
RUN dotnet build "./pengdows.crud.pocobuilder.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./pengdows.crud.pocobuilder.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "pengdows.crud.pocobuilder.dll"]
