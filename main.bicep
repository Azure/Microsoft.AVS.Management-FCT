// Values inherited from azure-pipelines.yml variables & secrets
param privateCloudName string
param resourceGroupName string
param location string
@secure()
param adminPassword string
param adminUsername string
@secure()
param domainAdminPassword string
param domainAdminUsername string
param domainName string

// Values defined here
param tag string = 'TEST'
param vnetName string = '${privateCloudName}-VNET'
param nicName string = '${privateCloudName}-NIC'
param ldapExtensionName string = '${privateCloudName}-LDAP'
param vnetCloudPeeringName string = 'VNETtoCLOUD-PEERING'
param cloudVnetPeeringName string = 'CLOUDtoVNET-PEERING'
param vmName string = '${privateCloudName}-VM'

@description('Creates a virtual network with a single subnet for LDAP.')
resource vnet 'Microsoft.Network/virtualNetworks@2023-02-01' = {
  name: vnetName
  location: location
  properties: {
    addressSpace: {
      addressPrefixes: [
        '10.0.0.0/22' 
      ]
    }
    subnets: [
      {
        name: 'ldap-subnet'
        properties: {
          addressPrefix: '10.0.0.0/22'
        }
      }
    ]
  }
}

@description('Creates a virtual network peering between the AVS private cloud and the virtual network created for LDAP.')
resource vnetPrivateCloudPeering 'Microsoft.Network/virtualNetworks/virtualNetworkPeerings@2023-02-01' = {
  name: vnetCloudPeeringName
  dependsOn: [
    vnet
  ]
  properties: {
    remoteVirtualNetwork: {
      id:  resourceId(resourceGroupName, 'Microsoft.AVS/privateClouds', privateCloudName)
    }
    allowVirtualNetworkAccess: true
    allowForwardedTraffic: true
    allowGatewayTransit: true
    useRemoteGateways: false
  }
}

@description('Creates a virtual network peering between the AVS private cloud and the virtual network created for LDAP.')
resource privateCloudAndVnetPeering 'Microsoft.Network/virtualNetworks/virtualNetworkPeerings@2023-02-01' = {
  name: cloudVnetPeeringName
  dependsOn: [
    privateCloud
  ]
  properties: {
    remoteVirtualNetwork: {
      id:  resourceId(resourceGroupName, 'Microsoft.Network/virtualNetworks', vnetName)
    }
    allowVirtualNetworkAccess: true
    allowForwardedTraffic: true
    allowGatewayTransit: true
    useRemoteGateways: false
  }
}

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

@description('Creates a network interface for the LDAP VM.')
resource nic 'Microsoft.Network/networkInterfaces@2023-02-01' = {
  name: nicName
  location: location
  properties: {
    ipConfigurations: [
      {
        name: 'ipconfig1'
        properties: {
          privateIPAddress: '10.0.0.10' // IP address subnet: 10.0.0.0/22
          privateIPAllocationMethod: 'Static'
          subnet: {
            id: vnet.id
          }
        }
      }
    ]
  }
}

@description('Creates a VM for LDAP.')
resource vm 'Microsoft.Compute/virtualMachines@2022-11-01' = {
  name: vmName
  location: location
  properties: {
    hardwareProfile: {
      vmSize: 'Standard_A1_v2' // low-cost standard VM 
    }
    storageProfile: {
      imageReference: {
        publisher: 'MicrosoftWindowsServer'
        offer: 'WindowsServer'
        sku: '2022-Datacenter'
        version: 'latest'
      }
      osDisk: {
        name: '${vmName}-osdisk'
        createOption: 'FromImage'
      }
    }
    osProfile: {
      computerName: vmName
      adminUsername: adminUsername
      adminPassword: adminPassword
      windowsConfiguration: {
        provisionVMAgent: true
      }
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

@description('Creates an extension for the LDAP VM to join an Active Directory domain.')
resource ldapExtension 'Microsoft.Compute/virtualMachines/extensions@2022-11-01' = {
  name: ldapExtensionName
  location: location
  properties: {
    publisher: 'Microsoft.Compute'
    type: 'JsonADDomainExtension'
    typeHandlerVersion: '1.3'
    autoUpgradeMinorVersion: true
    settings: {
      Name: domainName
      User: domainAdminUsername
      Password: domainAdminPassword
      Restart: 'true'
      OUPath: 'OU=AVS,DC=mydomain,DC=com' // example OU path
    }
    protectedSettings: {
      // commandToExecute: 'powershell.exe Install-ADDSForest -DomainName ${domainName} -DomainNetbiosName ${domainName} -SafeModeAdministratorPassword (ConvertTo-SecureString -AsPlainText "${domainAdminPassword}" -Force) -Force:$true'
      vmResourceId: vm.id
    }
  }
}
