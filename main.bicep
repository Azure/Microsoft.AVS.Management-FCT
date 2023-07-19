@description('privateCloud resource fields.')
param name string = 'Microsoft-AVS-Management'
param location string = 'northcentralus'
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
