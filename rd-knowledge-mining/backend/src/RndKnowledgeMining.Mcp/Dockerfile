FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["backend/src/RndKnowledgeMining.Mcp/RndKnowledgeMining.Mcp.csproj", "backend/src/RndKnowledgeMining.Mcp/"]
RUN dotnet restore "backend/src/RndKnowledgeMining.Mcp/RndKnowledgeMining.Mcp.csproj"
COPY backend/src/RndKnowledgeMining.Mcp/ backend/src/RndKnowledgeMining.Mcp/
COPY policies/hls_policies.txt policies/hls_policies.txt
WORKDIR /src/backend/src/RndKnowledgeMining.Mcp
RUN dotnet publish "RndKnowledgeMining.Mcp.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
COPY --from=build /src/policies/hls_policies.txt /app/policies/hls_policies.txt
ENV PolicySeed__PolicyFilePath=/app/policies/hls_policies.txt
ENTRYPOINT ["dotnet", "RndKnowledgeMining.Mcp.dll"]
