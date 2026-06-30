# Build from rd-knowledge-mining/frontend
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY src/WebApp/Cohere.AgenticRDKnowledge.WebApp.csproj src/WebApp/
RUN dotnet restore src/WebApp/Cohere.AgenticRDKnowledge.WebApp.csproj

COPY src/WebApp/ src/WebApp/

RUN dotnet publish src/WebApp/Cohere.AgenticRDKnowledge.WebApp.csproj \
    -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
ENV ApiBaseUrl=

EXPOSE 8080
ENTRYPOINT ["dotnet", "Cohere.AgenticRDKnowledge.WebApp.dll"]
