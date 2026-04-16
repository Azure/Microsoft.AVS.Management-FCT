<#
.SYNOPSIS
    Setting the right parameters for setting up the AD DS.
.PARAMETER DomainName
    The fully qualified domain name (FQDN) of the new domain.
.PARAMETER DomainNetBIOSName
    The NetBIOS name of the new domain.
.PARAMETER Username
    The username of an account with sufficient privileges to add a new domain.
.PARAMETER Password
    The password of the account specified in the Username parameter.
.EXAMPLE
    .\adds_install.ps1 -DomainName "VM.contoso.com" -DomainNetBIOSName "contoso" -Username "bob" -Password "P@ssw0rd1"
#>
Param(
    [Parameter(Mandatory = $true)]
    [string]$DomainName,

    [Parameter(Mandatory = $true)]
    [string]$DomainNetBIOSName,

    [Parameter(Mandatory = $true)]
    [string]$Username,

    [Parameter(Mandatory = $true)]
    [String]$Password
)

<#
.SYNOPSIS
    Main function that installs and sets up ADDS on a server.
.DESCRIPTION
    This function installs the Active Directory module and sets up ADDS on a server. 
.INPUTS 
    None
.PARAMETER None
.EXAMPLE
    Install-ADDS
#>
function Install-ADDS {
    if ((Get-WindowsFeature -Name AD-Domain-Services).Installed) {
        try {
            Get-ADUser -Identity $Username
            Write-Host "[Install-ADDS] User $Username exists."
        }
        catch {
            # Create a new user
            New-ADUser -Name $Username -AccountPassword (ConvertTo-SecureString -String $Password -AsPlainText -Force) -Enabled $true -ChangePasswordAtLogon $false
            Write-Host "[New-ADDS] A new user has been created."
        }
    }
    else {
        Install-WindowsFeature -Name AD-Domain-Services -IncludeManagementTools -Restart:$false -Confirm:$false
        Write-Host "[Install-ADModules] Active Directory module has been installed."
        Install-ADDSForest -DomainName $DomainName -DomainNetBIOSName $DomainNetBIOSName -SafeModeAdministratorPassword (ConvertTo-SecureString -String $Password -AsPlainText -Force) -SkipPreChecks -NoRebootOnCompletion:$true -Force:$true -SkipAutoConfigureDns:$true
        Write-Host "[Install-ADDS] Server has been promoted to domain controller. The server must be restarted before the Active Directory module can be used."
    }
}

<#
.SYNOPSIS
    Hardens LDAP channel binding on the domain controller (ADV190023 / KB4034879).
.DESCRIPTION
    Sets the LdapEnforceChannelBinding registry value to 1 (when supported) under
    HKLM\SYSTEM\CurrentControlSet\Services\NTDS\Parameters. This mitigates
    CVE ADV190023 (QID 91564) by requiring LDAP channel binding tokens when
    TLS is negotiated, preventing relay attacks.
    Value meanings:
      0 = Disabled (vulnerable)
      1 = When supported (recommended — compatible with all AVS identity sources)
      2 = Always required (may break clients that don't send CBT)
#>
function Set-LdapChannelBinding {
    $RegPath = "HKLM:\SYSTEM\CurrentControlSet\Services\NTDS\Parameters"
    $RegName = "LdapEnforceChannelBinding"

    if (-not (Test-Path $RegPath)) {
        Write-Host "[Set-LdapChannelBinding] NTDS Parameters key not found — skipping (AD DS may not be installed yet)."
        return
    }

    $current = Get-ItemProperty -Path $RegPath -Name $RegName -ErrorAction SilentlyContinue
    if ($current -and $current.$RegName -ge 1) {
        Write-Host "[Set-LdapChannelBinding] Already set to $($current.$RegName) — no change needed."
        return
    }

    Set-ItemProperty -Path $RegPath -Name $RegName -Value 1 -Type DWord
    Write-Host "[Set-LdapChannelBinding] LdapEnforceChannelBinding set to 1 (when supported). ADV190023 mitigated."
}

Install-ADDS
Set-LdapChannelBinding
