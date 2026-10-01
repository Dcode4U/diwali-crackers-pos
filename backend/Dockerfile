FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY backend/Directory.Build.props ./backend/
COPY backend/Domain/Domain.csproj ./backend/Domain/
COPY backend/Application/Application.csproj ./backend/Application/
COPY backend/Infrastructure/Infrastructure.csproj ./backend/Infrastructure/
COPY backend/API/API.csproj ./backend/API/
RUN dotnet restore backend/API/API.csproj
COPY backend ./backend
RUN dotnet publish backend/API/API.csproj -c Release -o /out --no-restore /p:UseAppHost=false
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /out .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet","API.dll"]
