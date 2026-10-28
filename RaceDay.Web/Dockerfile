# ---- build stage ----
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# restore first so this layer is cached until the .csproj changes
COPY RaceDay.Web.csproj ./
RUN dotnet restore

COPY . ./
RUN dotnet publish RaceDay.Web.csproj -c Release -o /app/publish --no-restore

# ---- runtime stage (small image, no SDK) ----
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080

COPY --from=build /app/publish ./

# run as the non-root user that ships with the .NET 8 images
USER app
ENTRYPOINT ["dotnet", "RaceDay.Web.dll"]
