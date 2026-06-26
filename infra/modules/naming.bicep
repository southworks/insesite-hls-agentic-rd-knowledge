param baseName string
param nameSuffix string

var deploymentSuffix = empty(nameSuffix) ? uniqueString(resourceGroup().id) : uniqueString(resourceGroup().id, nameSuffix)

output deploymentSuffix string = deploymentSuffix
