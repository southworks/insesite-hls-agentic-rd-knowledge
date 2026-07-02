# Build context: rd-knowledge-mining (same as .github/workflows/publish-container-images.yml)
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY frontend/src/WebApp/Cohere.AgenticRDKnowledge.WebApp.csproj frontend/src/WebApp/
RUN dotnet restore frontend/src/WebApp/Cohere.AgenticRDKnowledge.WebApp.csproj

COPY frontend/src/WebApp/ frontend/src/WebApp/

RUN dotnet publish frontend/src/WebApp/Cohere.AgenticRDKnowledge.WebApp.csproj \
    -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
ENV ApiBaseUrl=

EXPOSE 8080
ENTRYPOINT ["dotnet", "Cohere.AgenticRDKnowledge.WebApp.dll"]
