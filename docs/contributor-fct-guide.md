# Identity Package — FCT Validation Guide

This guide explains how to validate Identity package changes using the
Functional Compliance Tests (FCT) pipeline.

## Overview

The FCT pipeline runs 6 end-to-end tests against a live AVS SDDC:

| # | Test | Cmdlet | Description |
|---|------|--------|-------------|
| 0 | ScriptExecution_CRUD | Get-CloudAdminGroups | Validates script execution CRUD |
| 1 | ScriptExecution_NewLDAPIdentitySource | New-LDAPIdentitySource | Adds LDAP identity source |
| 2 | ScriptExecution_RemoveExternalIdentitySources | Remove-ExternalIdentitySources | Removes identity sources (post-LDAP) |
| 3 | ScriptExecution_NewLDAPSIdentitySource | New-LDAPSIdentitySource | Adds LDAPS identity source |
| 4 | ScriptExecution_DebugLDAPSIdentitySources | Debug-LDAPSIdentitySources | Verifies LDAPS config health |
| 5 | ScriptExecution_RemoveLDAPSExternalIdentitySources | Remove-ExternalIdentitySources | Removes identity sources (post-LDAPS) |

## Automated PR Validation

When you open a PR in the Identity package repository, the CI pipeline
automatically:

1. Builds your package (e.g. version `1.1.526`)
2. Fires a `repository_dispatch` to `Azure/Microsoft.AVS.Management-FCT`
3. The FCT workflow runs all 6 tests against the **Lab SDDC** using your
   package version
4. Results appear as a commit status check on your PR

No action required — just push your changes and watch the checks.

## Manual Trigger

To manually trigger FCT with a specific package version:

### Option A: GitHub UI

1. Go to [Actions → FCT – Identity PR Validation](https://github.com/Azure/Microsoft.AVS.Management-FCT/actions/workflows/fct-dispatch.yml)
2. Click **Run workflow**
3. Fill in the package version (e.g. `1.1.526`)
4. Optionally override SDDC parameters
5. Click **Run workflow**

### Option B: CLI

```bash
export GITHUB_TOKEN="ghp_..."  # needs repo scope
./docs/trigger-fct.sh 1.1.526
```

### Option C: curl

```bash
curl -X POST \
  -H "Authorization: Bearer $GITHUB_TOKEN" \
  -H "Accept: application/vnd.github+json" \
  https://api.github.com/repos/Azure/Microsoft.AVS.Management-FCT/dispatches \
  -d '{"event_type":"run-fct","client_payload":{"package_version":"1.1.526"}}'
```

### Option D: PowerShell (from Identity ADO pipeline)

```powershell
$headers = @{
    "Authorization" = "Bearer $env:FCT_GITHUB_TOKEN"
    "Accept" = "application/vnd.github+json"
    "X-GitHub-Api-Version" = "2022-11-28"
}
$body = @{
    event_type = "run-fct"
    client_payload = @{
        package_version = "1.1.526"
        sha = $env:BUILD_SOURCEVERSION
    }
} | ConvertTo-Json -Depth 3

Invoke-RestMethod -Uri "https://api.github.com/repos/Azure/Microsoft.AVS.Management-FCT/dispatches" `
    -Method POST -Headers $headers -Body $body -ContentType "application/json"
```

## SDDC Environments

| Environment | Trigger | Resource Group | Private Cloud | Usage |
|-------------|---------|---------------|---------------|-------|
| **Lab** | `repository_dispatch`, `workflow_dispatch` | AVS-Management-FCT-Lab | AVS-Management-FCT-Lab | PR validation, pre-release testing |
| **Prod** | Push to `main` (ADO pipeline) | AVS-Management-FCT | AVS-Management-FCT | Release validation |

## Adding the Dispatch Step to Identity CI

Add this step to the Identity package ADO pipeline, after the package build step:

```yaml
- task: PowerShell@2
  displayName: Trigger FCT Validation
  condition: and(succeeded(), eq(variables['Build.Reason'], 'PullRequest'))
  inputs:
    targetType: inline
    script: |
      $headers = @{
        "Authorization" = "Bearer $(FCT_GITHUB_TOKEN)"
        "Accept" = "application/vnd.github+json"
        "X-GitHub-Api-Version" = "2022-11-28"
      }
      $body = @{
        event_type = "run-fct"
        client_payload = @{
          package_version = "$(PackageVersion)"
          sha = "$(Build.SourceVersion)"
          build_id = "$(Build.BuildId)"
          pr_number = "$(System.PullRequest.PullRequestNumber)"
        }
      } | ConvertTo-Json -Depth 3

      Invoke-RestMethod `
        -Uri "https://api.github.com/repos/Azure/Microsoft.AVS.Management-FCT/dispatches" `
        -Method POST -Headers $headers -Body $body -ContentType "application/json"

      Write-Host "FCT dispatch sent for package version $(PackageVersion)"
      Write-Host "Monitor: https://github.com/Azure/Microsoft.AVS.Management-FCT/actions"
```

**Required**: Secret variable `FCT_GITHUB_TOKEN` — a GitHub PAT with `repo` scope
on `Azure/Microsoft.AVS.Management-FCT`.

## Prerequisites Setup

### 1. Lab SDDC Provisioning

Deploy using the existing `main.bicep`:

```bash
# Create resource group
az group create --name AVS-Management-FCT-Lab --location eastus

# Deploy infrastructure (~5-6 hours for initial provisioning)
az deployment group create \
    --resource-group AVS-Management-FCT-Lab \
    --template-file main.bicep \
    --parameters location=eastus \
                 username=<LDAP_USERNAME> \
                 password=<LDAP_PASSWORD> \
                 domain=contoso.com \
                 private_cloud_name=AVS-Management-FCT-Lab

# After deployment, run the ADO pipeline once with isFirstRun=true
# to configure ADDS, DNS forwarder, and LDAPS certificate
```

### 2. Azure AD App for GitHub Actions OIDC

```bash
# Create app registration
az ad app create --display-name "fct-github-actions"
APP_ID=$(az ad app list --display-name "fct-github-actions" --query "[0].appId" -o tsv)

# Create service principal
az ad sp create --id $APP_ID

# Add federated credential for GitHub Actions
az ad app federated-credential create --id $APP_ID --parameters '{
  "name": "fct-dispatch",
  "issuer": "https://token.actions.githubusercontent.com",
  "subject": "repo:Azure/Microsoft.AVS.Management-FCT:ref:refs/heads/main",
  "audiences": ["api://AzureADTokenExchange"]
}'

# Grant Contributor on Lab resource group
SP_ID=$(az ad sp show --id $APP_ID --query "id" -o tsv)
az role assignment create --assignee $SP_ID \
    --role Contributor \
    --scope /subscriptions/<SUB_ID>/resourceGroups/AVS-Management-FCT-Lab

az role assignment create --assignee $SP_ID \
    --role "Storage Blob Data Contributor" \
    --scope /subscriptions/<SUB_ID>/resourceGroups/AVS-Management-FCT-Lab
```

### 3. GitHub Repository Secrets

Configure on `Azure/Microsoft.AVS.Management-FCT` → Settings → Secrets:

| Secret | Value |
|--------|-------|
| `AZURE_CLIENT_ID` | App registration client ID |
| `AZURE_TENANT_ID` | Azure AD tenant ID |
| `AZURE_SUBSCRIPTION_ID` | Lab subscription ID |
| `LDAP_USERNAME` | LDAP admin username |
| `LDAP_PASSWORD` | LDAP admin password |

## Troubleshooting

- **Tests timeout**: Identity cmdlets with SSH sessions (LDAPS, Debug) need
  up to 5 minutes. Check that the Lab SDDC is healthy and the domain controller
  VM is running (`az vm show -g AVS-Management-FCT-Lab -n VM --query powerState`).
- **Azure login fails**: Verify the OIDC federated credential subject matches
  the workflow trigger. For `repository_dispatch`, use subject filter
  `repo:Azure/Microsoft.AVS.Management-FCT:ref:refs/heads/main`.
- **Certificate errors**: Run the LDAPS setup script on the Lab domain
  controller if the self-signed cert has expired (certs are valid for 2 years).
- **Package version not found**: Ensure the version string matches an available
  `Microsoft.AVS.Identity` script package on the target SDDC. Use `1.*` for
  latest stable.
