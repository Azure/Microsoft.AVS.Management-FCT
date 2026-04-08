#!/usr/bin/env bash
#
# Manually trigger the FCT dispatch workflow against the Lab SDDC.
# Requires: GITHUB_TOKEN env var with repo scope on Azure/Microsoft.AVS.Management-FCT
#
# Usage:
#   export GITHUB_TOKEN="ghp_..."
#   ./docs/trigger-fct.sh 1.1.526
#   ./docs/trigger-fct.sh 1.1.526 my-resource-group my-private-cloud eastus
#

set -euo pipefail

PACKAGE_VERSION="${1:?Usage: $0 <package_version> [resource_group] [private_cloud] [location]}"
RESOURCE_GROUP="${2:-AVS-Management-FCT-Lab}"
PRIVATE_CLOUD="${3:-AVS-Management-FCT-Lab}"
LOCATION="${4:-canadacentral}"

: "${GITHUB_TOKEN:?Set GITHUB_TOKEN with repo scope on Azure/Microsoft.AVS.Management-FCT}"

REPO="Azure/Microsoft.AVS.Management-FCT"

echo "Dispatching FCT run:"
echo "  Package version: $PACKAGE_VERSION"
echo "  Resource group:  $RESOURCE_GROUP"
echo "  Private cloud:   $PRIVATE_CLOUD"
echo "  Location:        $LOCATION"

HTTP_STATUS=$(curl -s -o /dev/null -w "%{http_code}" -X POST \
  -H "Authorization: Bearer $GITHUB_TOKEN" \
  -H "Accept: application/vnd.github+json" \
  -H "X-GitHub-Api-Version: 2022-11-28" \
  "https://api.github.com/repos/${REPO}/dispatches" \
  -d @- <<EOF
{
  "event_type": "run-fct",
  "client_payload": {
    "package_version": "$PACKAGE_VERSION",
    "resource_group": "$RESOURCE_GROUP",
    "private_cloud": "$PRIVATE_CLOUD",
    "location": "$LOCATION"
  }
}
EOF
)

if [ "$HTTP_STATUS" = "204" ]; then
  echo ""
  echo "✓ Dispatch sent successfully."
  echo "  Monitor: https://github.com/${REPO}/actions"
else
  echo ""
  echo "✗ Dispatch failed with HTTP $HTTP_STATUS."
  echo "  Check your GITHUB_TOKEN has 'repo' scope on ${REPO}."
  exit 1
fi
