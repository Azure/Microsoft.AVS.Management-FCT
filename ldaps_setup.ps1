<#
.SYNOPSIS
    Sets up LDAPS on the domain controller by generating a self-signed certificate.
.DESCRIPTION
    This script creates a self-signed certificate for the domain controller's FQDN,
    enabling LDAPS (LDAP over SSL) on port 636. The certificate is exported as a
    Base64-encoded .cer file for upload to Azure Blob Storage.
.PARAMETER DomainName
    The fully qualified domain name (FQDN) of the domain.
.EXAMPLE
    .\ldaps_setup.ps1 -DomainName "contoso.com"
#>
Param(
    [Parameter(Mandatory = $true)]
    [string]$DomainName
)

<#
.SYNOPSIS
    Creates a self-signed certificate for LDAPS and exports it.
.DESCRIPTION
    Generates a self-signed certificate for the domain controller, places it in the
    machine's personal certificate store (which auto-enables LDAPS on port 636),
    and exports it as a Base64-encoded .cer file.
#>
function Install-LDAPSCertificate {
    $ComputerName = $env:COMPUTERNAME
    $FQDN = "$ComputerName.$DomainName"
    $CertPath = "C:\ldaps_cert.cer"

    # Remove any existing LDAPS self-signed certificates to avoid conflicts
    Get-ChildItem Cert:\LocalMachine\My | Where-Object { $_.Subject -like "*$DomainName*" -and $_.Issuer -eq $_.Subject } | Remove-Item -Force -ErrorAction SilentlyContinue

    # Create self-signed certificate with Server Authentication EKU required for LDAPS
    $Certificate = New-SelfSignedCertificate `
        -DnsName $FQDN, $DomainName `
        -CertStoreLocation "Cert:\LocalMachine\My" `
        -NotAfter (Get-Date).AddYears(2) `
        -KeyAlgorithm RSA `
        -KeyLength 2048 `
        -KeyUsage DigitalSignature, KeyEncipherment `
        -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.1") `
        -KeyExportPolicy Exportable `
        -Provider "Microsoft RSA SChannel Cryptographic Provider"

    Write-Host "[Install-LDAPSCertificate] Certificate created with thumbprint: $($Certificate.Thumbprint)"

    # Export the certificate as Base64-encoded .cer file
    Export-Certificate -Cert $Certificate -FilePath $CertPath -Type CERT
    $CertBase64 = [Convert]::ToBase64String((Get-Content $CertPath -Encoding Byte))
    $CertPem = "-----BEGIN CERTIFICATE-----`n$CertBase64`n-----END CERTIFICATE-----"
    Set-Content -Path $CertPath -Value $CertPem

    Write-Host "[Install-LDAPSCertificate] Certificate exported to $CertPath"

    # Open Windows Firewall for LDAPS (port 636)
    New-NetFirewallRule -DisplayName "LDAPS" -Direction Inbound -Protocol TCP -LocalPort 636 -Action Allow -ErrorAction SilentlyContinue
    Write-Host "[Install-LDAPSCertificate] Firewall rule for port 636 added."

    # Restart NTDS service to force LDAPS to bind to port 636 with the new certificate
    Restart-Service NTDS -Force
    Start-Sleep -Seconds 10

    # Verify LDAPS is listening on port 636
    $Listener = Get-NetTCPConnection -LocalPort 636 -State Listen -ErrorAction SilentlyContinue
    if ($Listener) {
        Write-Host "[Install-LDAPSCertificate] LDAPS is confirmed listening on port 636."
    }
    else {
        Write-Warning "[Install-LDAPSCertificate] Port 636 not yet listening. Attempting full NTDS restart..."
        Stop-Service NTDS -Force
        Start-Sleep -Seconds 5
        Start-Service NTDS
        Start-Sleep -Seconds 10
        $Listener = Get-NetTCPConnection -LocalPort 636 -State Listen -ErrorAction SilentlyContinue
        if ($Listener) {
            Write-Host "[Install-LDAPSCertificate] LDAPS is confirmed listening on port 636 after retry."
        }
        else {
            Write-Error "[Install-LDAPSCertificate] LDAPS is NOT listening on port 636. Certificate may not meet LDAPS requirements."
        }
    }
}

Install-LDAPSCertificate
