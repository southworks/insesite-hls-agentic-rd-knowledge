param(
    [Parameter(Mandatory = $true)]
    [string]$RegistryLoginServer,

    [string]$ImageTag = "demo",
    [string]$RepositoryPrefix = "coherernd"
)

# Optional local maintainer helper. Publish api and mcp images before deploying infra.

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "../..")
$registry = $RegistryLoginServer.TrimEnd('/')

$images = @(
    @{ Name = "api"; Dockerfile = "backend/src/Api.Host/Dockerfile" },
    @{ Name = "mcp"; Dockerfile = "backend/src/RndKnowledgeMining.Mcp/Dockerfile" },
    @{ Name = "provisioning"; Dockerfile = "agent-provisioning/Dockerfile" }
)

foreach ($image in $images) {
    $imageName = "${RepositoryPrefix}-$($image.Name)"
    $fullImage = "${registry}/${imageName}:${ImageTag}"

    Write-Host "Building $fullImage ..."
    docker build -f (Join-Path $repoRoot $image.Dockerfile) -t $fullImage $repoRoot

    if ($LASTEXITCODE -ne 0) {
        throw "Failed to build image $fullImage."
    }

    Write-Host "Pushing $fullImage ..."
    docker push $fullImage

    if ($LASTEXITCODE -ne 0) {
        throw "Failed to push image $fullImage."
    }
}

Write-Host ""
Write-Host "Published demo images with tag '$ImageTag':"
Write-Host "  apiContainerImage: ${registry}/${RepositoryPrefix}-api:${ImageTag}"
Write-Host "  mcpContainerImage: ${registry}/${RepositoryPrefix}-mcp:${ImageTag}"
Write-Host "  provisioningContainerImage: ${registry}/${RepositoryPrefix}-provisioning:${ImageTag}"
Write-Host ""
Write-Host "Frontend image is not built here. Set frontendContainerImage when deploying infra."
