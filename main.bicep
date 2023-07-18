@description('Specifies the location for resources.')
param location string = 'northcentralus'

@description('Specifies the name of the private cloud.')
param name string = 'Microsoft-AVS-Management'

@description('Specifies the tag for the private cloud.')
param tag string = 'Test'

resource myResource 'Microsoft.AVS/privateClouds@2022-05-01' = {
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
