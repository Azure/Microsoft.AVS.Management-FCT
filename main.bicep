// variables inherited from Azure Pipeline Secrets
param location string
param username string
@secure()
param password string
param private_cloud_name string
param domain string

@description('Creates an AVS private cloud and a DNS FQDN zone.')
resource private_cloud 'Microsoft.AVS/privateClouds@2022-05-01' = {
  name: private_cloud_name
  location: location
  sku: {
    name: 'av36p'
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
    ENV: 'TEST'
  }

  resource private_cloud_networking 'workloadNetworks@2022-05-01' existing = {
    name: 'default' // please keep this named 'default' 

    resource dns_zone 'dnsZones@2022-05-01' = {
      name: domain
      properties: {
        displayName: domain
        dnsServerIps: [
          '172.36.0.4'
        ]
        domain: [domain]
      }
    }
}}

@description('Creates an ExpressRoute resource for the private cloud.')
resource expressroute 'Microsoft.AVS/privateClouds/authorizations@2022-05-01' = {
  parent: private_cloud
  name: 'er-auth-key'
  properties: {} // please do not remove this field
}

@description('Creates a NIC for the VNet.')
resource nic 'Microsoft.Network/networkInterfaces@2023-04-01' = {
  name: 'Network_Interface'
  location: location
  properties: {
    ipConfigurations: [
      {
        name: 'ipconfig1'
        properties: {
          privateIPAddress: '172.36.0.4'
          privateIPAllocationMethod: 'Static'
          primary: true
          subnet: {
            id: vnet.properties.subnets[1].id
          }
        }
      }
    ]
  }
}

@description('Creates a VM that acts as a domain controller with AD DS.') 
resource vm 'Microsoft.Compute/virtualMachines@2023-03-01' = {
  name: 'VM'
  location: location
  properties: {
    hardwareProfile: {
      vmSize: 'Standard_B2als_v2'
    }
    storageProfile: {
      imageReference: {
        publisher: 'MicrosoftWindowsServer'
        offer: 'WindowsServer'
        sku: '2022-datacenter-azure-edition-hotpatch'
        version: 'latest'
      }
      osDisk: {
        osType: 'Windows'
        name: 'Domain_Controller_OsDisk'
        createOption: 'FromImage'
      }
    }
    osProfile: {
      adminUsername: username
      adminPassword: password
      computerName: 'VM'
    }
    networkProfile: {
      networkInterfaces: [
        {
          id: nic.id
        }
      ]
    }
  }
}

@description('Creates a VNet that connects the VM and the private cloud.')
resource vnet 'Microsoft.Network/virtualNetworks@2023-04-01' = {
  name: 'VNet'
  location: location
  properties: {
    addressSpace: {
      addressPrefixes: [
        '172.36.0.0/16'
      ]
    }
    subnets: [
      {
        name: 'GatewaySubnet'
        properties: {
          addressPrefix: '172.36.1.0/24'
        }
      }
      {
        name: 'Default'
        properties: {
          addressPrefix: '172.36.0.0/24'
        }
      }
    ]
  }
}

@description('Creates an ExpressRoute connection between the VNet and private cloud.')
resource expressroute_connection 'Microsoft.Network/connections@2023-04-01' = {
  name: 'ExpressRoute_Connection'
  location: location
  properties: {
    connectionType: 'ExpressRoute'
    virtualNetworkGateway1: {
      id: vnet_gateway.id
      properties: {}
    }
    useLocalAzureIpAddress: false
    enableBgp: false
    connectionProtocol: 'IKEv1'
    authorizationKey: expressroute.properties.expressRouteAuthorizationKey
    routingWeight: 0
    expressRouteGatewayBypass: false
    peer: {
      id: expressroute.properties.expressRouteId
    }
  }
}
  
resource gateway_pip 'Microsoft.Network/publicIPAddresses@2020-08-01' = {
  name: 'Gateway_Public_IP'
  location: location
  sku: {
    name: 'Basic'
    tier: 'Regional'
  }
  properties: {
    publicIPAllocationMethod: 'Dynamic'
  }
}

@description('Creates a VNet gateway.')
resource vnet_gateway 'Microsoft.Network/virtualNetworkGateways@2023-04-01' = {
  name: 'VNet_Gateway'
  location: location
  properties: {
    enablePrivateIpAddress: false
    ipConfigurations: [
      {
        name: 'default'
        properties: {
          privateIPAllocationMethod: 'Dynamic'
          publicIPAddress: {
            id: gateway_pip.id
          }
          subnet: {
            id: vnet.properties.subnets[0].id
          }
        }
      }
    ]
    gatewayType: 'ExpressRoute'
    vpnType: 'PolicyBased'
    enableBgp: false
    activeActive: false
    vpnGatewayGeneration: 'None'
    allowRemoteVnetTraffic: false
    allowVirtualWanTraffic: false
    adminState: 'Enabled'
    sku: {
      name: 'UltraPerformance'
      tier: 'UltraPerformance'
    }
  }
}
