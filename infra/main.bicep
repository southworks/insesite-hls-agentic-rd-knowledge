@description('Azure region for all resources.')
param location string = resourceGroup().location

@description('Base name used for deployed resources.')
param baseName string = 'hlsrd'

@description('Optional suffix for retry deployments. Set when redeploying after a partial failure left names reserved.')
param nameSuffix string = ''

@description('Fabric workspace name. Required. Must be capacity-backed and accessible to the operator.')
param fabricWorkspaceName string

@description('Fabric lakehouse name. Created at deploy time if missing.')
param fabricLakehouseName string = 'HlsRdKnowledgeLakehouse'

@description('UAMI resource ID. Must be created by setup-fabric-provision-identity.ps1 with workspace role. Used as the Fabric seed deployment script identity. Its client ID is auto-derived by the deployment.')
param fabricUamiResourceId string

@description('When false, the lakehouse is still provisioned but no data is uploaded. The MCP will see an empty lakehouse and the adapter handles this at runtime.')
param enableFabricSeed bool = true

@description('Repository archive URL the seed script downloads to fetch infra/scripts/ and dataset-seed/cases/.')
param fabricRepositoryArchiveUrl string = 'https://github.com/southworks/insesite-hls-agentic-rd-knowledge/archive/refs/heads/main.zip'

@secure()
@description('Optional GitHub PAT for private repos or higher rate limits.')
param fabricGithubToken string = ''

var resourceTags = {
  project: 'inesite'
}

resource resourceGroupTags 'Microsoft.Resources/tags@2021-04-01' = {
  name: 'default'
  properties: {
    tags: resourceTags
  }
}

module naming 'modules/naming.bicep' = {
  name: 'naming'
  params: {
    baseName: baseName
    nameSuffix: nameSuffix
  }
}

module fabricProvision 'modules/fabric-provision.bicep' = {
  name: 'fabric-provision'
  params: {
    location: location
    resourceTags: resourceTags
    deploymentSuffix: naming.outputs.deploymentSuffix
    fabricUamiResourceId: fabricUamiResourceId
    fabricWorkspaceName: fabricWorkspaceName
    fabricLakehouseName: fabricLakehouseName
  }
}

module postDeployScripts 'modules/post-deploy-scripts.bicep' = {
  name: 'post-deploy-scripts'
  params: {
    location: location
    resourceTags: resourceTags
    deploymentSuffix: naming.outputs.deploymentSuffix
    enableFabricSeed: enableFabricSeed
    fabricUamiResourceId: fabricUamiResourceId
    fabricWorkspaceId: fabricProvision.outputs.workspaceId
    fabricWorkspaceName: fabricProvision.outputs.workspaceName
    fabricLakehouseId: fabricProvision.outputs.lakehouseId
    fabricLakehouseName: fabricProvision.outputs.lakehouseName
    fabricRepositoryArchiveUrl: fabricRepositoryArchiveUrl
    fabricGithubToken: fabricGithubToken
  }
}

output fabricWorkspaceName string = fabricProvision.outputs.workspaceName
output fabricLakehouseName string = fabricProvision.outputs.lakehouseName
output fabricSqlServer string = fabricProvision.outputs.sqlServer
output fabricSqlDatabase string = fabricProvision.outputs.sqlDatabase
output fabricSeedDeploymentScriptName string = postDeployScripts.outputs.fabricSeedDeploymentScriptName
