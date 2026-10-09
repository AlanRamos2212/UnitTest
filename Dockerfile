FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["WebAppApi.csproj", "./"]
RUN dotnet restore "WebAppApi.csproj"
COPY . .
RUN dotnet publish "WebAppApi.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:80
RUN mkdir -p /app/data /app/backups && chmod 755 /app/data /app/backups
EXPOSE 80
EXPOSE 6061
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "WebAppApi.dll"]
