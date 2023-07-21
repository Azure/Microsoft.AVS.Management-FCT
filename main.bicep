// Values inherited from azure-pipelines.yml variables & secrets
param privateCloudName string
param location string
@secure()
param adminPassword string
param adminUsername string
@secure()
param domainAdminPassword string
param domainAdminUsername string
param domainName string

param tag string = 'TEST'

@description('Creates an AVS private cloud.')
resource privateCloud 'Microsoft.AVS/privateClouds@2022-05-01' = {
  name: privateCloudName
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
