# Build and publish on the SDK image, then ship only the framework-dependent output on the
# much smaller ASP.NET runtime image.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore against the project files alone first, so editing source does not invalidate the
# package layer and re-download everything on each rebuild.
COPY Library.sln ./
COPY Library.Core/Library.Core.csproj Library.Core/
COPY Library.Data/Library.Data.csproj Library.Data/
COPY Library.Service/Library.Service.csproj Library.Service/
COPY Library.API/Library.API.csproj Library.API/
RUN dotnet restore Library.API/Library.API.csproj

COPY Library.Core/ Library.Core/
COPY Library.Data/ Library.Data/
COPY Library.Service/ Library.Service/
COPY Library.API/ Library.API/
RUN dotnet publish Library.API/Library.API.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

COPY --from=build /app/publish ./

# NLog's file target writes under the app directory, so that one directory has to be owned by
# the non-root user the runtime image provides. Everything else stays read-only to the app.
RUN mkdir -p /app/logs && chown -R $APP_UID:0 /app/logs
USER $APP_UID

# Render assigns the port at runtime and injects it as PORT, which Program.cs binds to. This
# value only applies when nothing sets PORT, as in a plain `docker run`.
ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "Library.API.dll"]
