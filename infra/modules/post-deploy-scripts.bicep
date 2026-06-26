param location string
param resourceTags object
param deploymentSuffix string
param enableFabricSeed bool
param fabricUamiResourceId string
param fabricWorkspaceId string
param fabricWorkspaceName string
param fabricLakehouseId string
param fabricLakehouseName string
param fabricRepositoryArchiveUrl string
@secure()
param fabricGithubToken string

resource fabricUami 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' existing = {
  name: last(split(fabricUamiResourceId, '/'))
}

resource runFabricSeed 'Microsoft.Resources/deploymentScripts@2023-08-01' = if (enableFabricSeed) {
  name: 'run-hls-fabric-seed-${deploymentSuffix}'
  location: location
  tags: resourceTags
  kind: 'AzurePowerShell'
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${fabricUami.id}': {}
    }
  }
  properties: {
    azPowerShellVersion: '11.0'
    retentionInterval: 'P1D'
    timeout: 'PT60M'
    cleanupPreference: 'OnSuccess'
    forceUpdateTag: deploymentSuffix
    scriptContent: loadTextContent('../scripts/seed-fabric-data.ps1')
    environmentVariables: [
      { name: 'AZURE_CLIENT_ID',         value: fabricUami.properties.clientId }
      { name: 'FABRIC_WORKSPACE_ID',     value: fabricWorkspaceId }
      { name: 'FABRIC_WORKSPACE_NAME',   value: fabricWorkspaceName }
      { name: 'FABRIC_LAKEHOUSE_ID',     value: fabricLakehouseId }
      { name: 'FABRIC_LAKEHOUSE_NAME',   value: fabricLakehouseName }
      { name: 'RESOURCE_GROUP_NAME',     value: resourceGroup().name }
      { name: 'REPOSITORY_ARCHIVE_URL',  value: fabricRepositoryArchiveUrl }
      { name: 'GITHUB_TOKEN',            secureValue: fabricGithubToken }
    ]
  }
}

output fabricSeedDeploymentScriptName string = enableFabricSeed ? runFabricSeed.name : ''
