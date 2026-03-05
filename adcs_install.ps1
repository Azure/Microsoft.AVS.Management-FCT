<#
.SYNOPSIS
    Installs AD Certificate Services as Enterprise Root CA and configures DNS for LDAPS.
.DESCRIPTION
    This script configures the domain controller for LDAPS by:
    1. Configuring DNS client to use the local DNS server
    2. Registering the DC's DNS records
    3. Installing AD CS and configuring an Enterprise Root CA
    4. Triggering certificate auto-enrollment for the DC
    5. Verifying LDAPS is available on port 636
    6. Exporting the root CA certificate as base64 to stdout
.PARAMETER DomainName
    The fully qualified domain name (FQDN) of the domain.
.EXAMPLE
    .\adcs_install.ps1 -DomainName "contoso.com"
#>
Param(
    [Parameter(Mandatory = $true)]
    [string]$DomainName
)

function Configure-DNS {
    Write-Host "[Configure-DNS] Setting DNS client to use local DNS server..."
    $adapter = Get-NetAdapter | Where-Object { $_.Status -eq 'Up' } | Select-Object -First 1
    Set-DnsClientServerAddress -InterfaceIndex $adapter.ifIndex -ServerAddresses 127.0.0.1

    Write-Host "[Configure-DNS] Registering DNS records..."
    ipconfig /registerdns | Out-Null
    Start-Sleep -Seconds 10

    # Verify DNS resolution
    $hostname = "$env:COMPUTERNAME.$DomainName"
    try {
        $resolved = Resolve-DnsName -Name $hostname -ErrorAction Stop
        Write-Host "[Configure-DNS] DNS resolution verified: $hostname -> $($resolved.IPAddress)"
    }
    catch {
        Write-Host "[Configure-DNS] WARNING: Could not resolve $hostname. DNS may need time to propagate."
    }
}

function Install-ADCS {
    if ((Get-WindowsFeature -Name ADCS-Cert-Authority).Installed) {
        Write-Host "[Install-ADCS] AD CS is already installed."
    }
    else {
        Write-Host "[Install-ADCS] Installing AD Certificate Services..."
        Install-WindowsFeature -Name ADCS-Cert-Authority -IncludeManagementTools

        Write-Host "[Install-ADCS] Configuring Enterprise Root CA..."
        Install-AdcsCertificationAuthority `
            -CAType EnterpriseRootCA `
            -CryptoProviderName "RSA#Microsoft Software Key Storage Provider" `
            -KeyLength 2048 `
            -HashAlgorithmName SHA256 `
            -ValidityPeriod Years `
            -ValidityPeriodUnits 5 `
            -Force

        Write-Host "[Install-ADCS] Enterprise Root CA configured."
    }

    # Force certificate auto-enrollment for the DC
    Write-Host "[Install-ADCS] Triggering certificate enrollment..."
    certutil -pulse
    Start-Sleep -Seconds 30
    certutil -pulse

    # Verify LDAPS is available
    $ldapsTest = Test-NetConnection -ComputerName localhost -Port 636 -WarningAction SilentlyContinue
    if ($ldapsTest.TcpTestSucceeded) {
        Write-Host "[Install-ADCS] LDAPS is available on port 636."
    }
    else {
        Write-Host "[Install-ADCS] WARNING: LDAPS not yet available on port 636. A VM restart may be required."
    }
}

function Export-RootCACert {
    Write-Host "[Export-RootCACert] Exporting root CA certificate..."
    $certPath = "C:\rootCA.cer"
    certutil -ca.cert $certPath

    if (Test-Path $certPath) {
        $bytes = [System.IO.File]::ReadAllBytes($certPath)
        $base64 = [Convert]::ToBase64String($bytes)
        Write-Host "[Export-RootCACert] ROOT_CA_CERT_BASE64:$base64"
        Write-Host "[Export-RootCACert] Certificate exported successfully."
    }
    else {
        Write-Host "[Export-RootCACert] ERROR: Failed to export root CA certificate."
    }
}

Configure-DNS
Install-ADCS
Export-RootCACert
