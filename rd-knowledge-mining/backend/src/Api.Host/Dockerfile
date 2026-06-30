FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["backend/src/Api.Host/CohereRndKnowledgeMining.Api.Host.csproj", "backend/src/Api.Host/"]
RUN dotnet restore "backend/src/Api.Host/CohereRndKnowledgeMining.Api.Host.csproj"
COPY backend/src/Api.Host/ backend/src/Api.Host/
COPY dataset-seed/ dataset-seed/
WORKDIR /src/backend/src/Api.Host
RUN dotnet publish "CohereRndKnowledgeMining.Api.Host.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
COPY --from=build /src/dataset-seed /app/dataset-seed
ENV Dataset__RootPath=/app/dataset-seed
ENTRYPOINT ["dotnet", "CohereRndKnowledgeMining.Api.Host.dll"]
