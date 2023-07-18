@description('PrivateCloud deployment variables.')
param name string = deployment().name
param location string = resourceGroup().location
param tag string = 'TEST'

resource privateCloud 'Microsoft.AVS/privateClouds@2022-05-01' = {
  name: name
  location: location
  sku: {
    name: 'av36'
  }
  properties: {
    availability: {
            strategy: 'singleZone'
        }
    internet: 'Enabled'
    managementCluster: {
      clusterSize: 3
    }
    networkBlock: '10.0.0.0/22'
  }
  tags: {
    ENV: tag
  }
}
