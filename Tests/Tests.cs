// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure;
using Azure.Core;
using NUnit.Framework;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.Avs;
using Azure.ResourceManager.Avs.Models;

namespace Tests
{
    /// <summary>
    /// Provides access to environment variables used in the tests.
    /// </summary>
    public class Variables
    {
        // The following environment variables are required to run the tests. They are protected so that they can only be accessed by the test classes.
        protected static readonly string? AzureSubscriptionId = Environment.GetEnvironmentVariable("SUBSCRIPTIONID");
        protected static readonly string? AzureResourceGroup = Environment.GetEnvironmentVariable("RESOURCEGROUPNAME");
        protected static readonly string? AzurePrivateCloudName = Environment.GetEnvironmentVariable("PRIVATECLOUDNAME");
        protected static readonly string? LDAPUsername = Environment.GetEnvironmentVariable("LDAPUSERNAME");
        protected static readonly string? LDAPPassword = Environment.GetEnvironmentVariable("LDAPPASSWORD");
        protected static readonly string? LDAPBaseDNGroups = Environment.GetEnvironmentVariable("BASEDNGROUPS");
        protected static readonly string? LDAPBaseDNUsers = Environment.GetEnvironmentVariable("BASEDNUSERS");
        protected static readonly string? LDAPPrimaryUrl = Environment.GetEnvironmentVariable("PRIMARYURL");
        protected static readonly string? LDAPSecondaryUrl = Environment.GetEnvironmentVariable("SECONDARYURL");
        protected static readonly string? SSLCertificatesSAS = Environment.GetEnvironmentVariable("SSLCERTIFICATESAS");
        protected static readonly string? LDAPDomainNetBIOSName = Environment.GetEnvironmentVariable("DOMAINNETBIOSNAME");
        protected static readonly string? LDAPDomainName = Environment.GetEnvironmentVariable("DOMAINNAME");
        protected static readonly string IdentityPackageVersion = Environment.GetEnvironmentVariable("IDENTITYPACKAGEVERSION") ?? "1.*";
        protected static readonly DefaultAzureCredential AzureCredential = new();
    }

    [TestFixture]
    public class EnvironmentTests : Variables
    {
        AccessToken Token;
        ArmClient? Client;
        ResourceIdentifier? PrivateCloudResourceIdentifier;
        AvsPrivateCloudResource? PrivateCloudResource;

        /// <summary>
        /// Sets up the test environment by getting the required environment variables and initializing any required resources.
        /// </summary>
        [SetUp]
        public async Task SetUp()
        {
            Assert.Multiple(() =>
                {
                    Assert.That(AzureSubscriptionId, Is.Not.Null, "AzureSubscriptionId is null.");
                    Assert.That(AzureResourceGroup, Is.Not.Null, "AzureResourceGroup is null.");
                    Assert.That(AzurePrivateCloudName, Is.Not.Null, "AzurePrivateCloudName is null.");
                    Assert.That(LDAPUsername, Is.Not.Null, "LDAPUsername is null.");
                    Assert.That(LDAPPassword, Is.Not.Null, "LDAPPassword is null.");
                    Assert.That(LDAPBaseDNGroups, Is.Not.Null, "LDAPBaseDNGroups is null.");
                    Assert.That(LDAPBaseDNUsers, Is.Not.Null, "LDAPBaseDNUsers is null.");
                    Assert.That(LDAPPrimaryUrl, Is.Not.Null, "LDAPPrimaryUrl is null.");
                    Assert.That(LDAPSecondaryUrl, Is.Not.Null, "LDAPSecondaryUrl is null.");
                    Assert.That(SSLCertificatesSAS, Is.Not.Null, "SSLCertificatesSAS is null.");
                    Assert.That(LDAPDomainNetBIOSName, Is.Not.Null, "LDAPDomainNetBIOSName is null.");
                    Assert.That(LDAPDomainName, Is.Not.Null, "LDAPDomainName is null.");
                    Assert.That(AzureCredential, Is.Not.Null, "Credentials is null.");
                });

            TokenRequestContext TokenRequest = new(new[] { "https://management.azure.com/.default" });

            await Task.Run(async () =>
            {
                Token = await AzureCredential.GetTokenAsync(TokenRequest);

                Assert.Multiple(() =>
                {
                    Assert.That(Token.Token, Is.Not.Null);
                    Assert.That(Token.ExpiresOn, Is.GreaterThan(DateTimeOffset.UtcNow));
                });
            });

            // set up the arm client and the avs private cloud resource
            Client = new ArmClient(AzureCredential, AzureSubscriptionId);

            PrivateCloudResourceIdentifier = AvsPrivateCloudResource.CreateResourceIdentifier(
                       AzureSubscriptionId,
                       AzureResourceGroup,
                       AzurePrivateCloudName);
            PrivateCloudResource = Client.GetAvsPrivateCloudResource(PrivateCloudResourceIdentifier);
        }

        /// <summary>
        /// Async method that tests the script execution CRUD operations of Get-CloudAdminGroups.
        /// </summary>
        [Test]
        public async Task ScriptExecution_CRUD()
        {
            // set up the cmdlet and cmldet resource
            string packageName = "Microsoft.AVS.Identity";
            string armPackageName = $"{packageName}@{IdentityPackageVersion}";
            string cmdletName = "Get-CloudAdminGroups";
            var resourceId = $"/subscriptions/{AzureSubscriptionId}/resourceGroups/{AzureResourceGroup}/providers/Microsoft.AVS/privateClouds/{AzurePrivateCloudName}/scriptPackages/{armPackageName}/scriptCmdlets/{cmdletName}";
            ResourceIdentifier CmdletResourceId = new(resourceId);

            // set up the script execution name
            Random r = new();
            int randomNumber = r.Next(1, 5000);
            ResourceIdentifier ExecutionNameId = new($"FCT:{AzureResourceGroup}-execution-{randomNumber}");
            var executionResourceString = $"/subscriptions/{AzureSubscriptionId}/resourceGroups/{AzureResourceGroup}/providers/Microsoft.AVS/privateClouds/{AzurePrivateCloudName}/scriptExecutions/{ExecutionNameId}";

            // set up the execution data
            var executionData = new ScriptExecutionData
            {
                ScriptCmdletId = CmdletResourceId,
                Retention = System.Xml.XmlConvert.ToString(TimeSpan.FromMinutes(30)), // script execution will be deleted after X minute(s)
                Timeout = System.Xml.XmlConvert.ToString(TimeSpan.FromMinutes(2)) // script execution will timeout after X minute(s) if it does not complete
            };

            ScriptExecutionCollection Executions = PrivateCloudResource!.GetScriptExecutions();
            var executionResource = (await Executions.CreateOrUpdateAsync(WaitUntil.Completed, ExecutionNameId, executionData)).Value;
            var executionResponse = executionResource.Data;

            Assert.That(executionResponse.ProvisioningState, Is.EqualTo(ScriptExecutionProvisioningState.Succeeded), $"{cmdletName} should always succeed but instead its state is: {executionData.ProvisioningState}");
        }

        /// <summary>
        /// Async method that tests the script execution of New-LDAPIdentitySource. This test must be run before the Remove-ExternalIdentitySources test.
        /// </summary>
        [Test, Order(1)]
        public async Task ScriptExecution_NewLDAPIdentitySource()
        {
            // set up the cmdlet and cmldet resource
            string packageName = "Microsoft.AVS.Identity";
            string armPackageName = $"{packageName}@{IdentityPackageVersion}";
            string cmdletName = "New-LDAPIdentitySource";
            var resourceId = $"/subscriptions/{AzureSubscriptionId}/resourceGroups/{AzureResourceGroup}/providers/Microsoft.AVS/privateClouds/{AzurePrivateCloudName}/scriptPackages/{armPackageName}/scriptCmdlets/{cmdletName}";
            ResourceIdentifier CmdletResourceId = new(resourceId);

            // set up the script execution name
            Random r = new();
            int randomNumber = r.Next(1, 5000);
            ResourceIdentifier ExecutionNameId = new($"FCT:{AzureResourceGroup}-execution-{randomNumber}");
            var executionResourceString = $"/subscriptions/{AzureSubscriptionId}/resourceGroups/{AzureResourceGroup}/providers/Microsoft.AVS/privateClouds/{AzurePrivateCloudName}/scriptExecutions/{ExecutionNameId}";

            // set up the execution data
            var executionData = new ScriptExecutionData
            {
                ScriptCmdletId = CmdletResourceId,
                Retention = System.Xml.XmlConvert.ToString(TimeSpan.FromMinutes(30)), // script execution will be deleted after X minute(s)
                Timeout = System.Xml.XmlConvert.ToString(TimeSpan.FromMinutes(2)) // script execution will timeout after X minute(s) if it does not complete
            };

            // set up the execution parameters
            ScriptExecutionParameterDetails[] parameters = new ScriptExecutionParameterDetails[]
            {
                new PSCredentialExecutionParameterDetails("Credential") { Username = $"{LDAPUsername}@{LDAPDomainName}", Password = LDAPPassword },
                new ScriptStringExecutionParameterDetails("BaseDNGroups") { Value = LDAPBaseDNGroups },
                new ScriptStringExecutionParameterDetails("BaseDNUsers") { Value = LDAPBaseDNUsers },
                new ScriptStringExecutionParameterDetails("PrimaryUrl") { Value = LDAPPrimaryUrl },
                new ScriptStringExecutionParameterDetails("DomainName") { Value = LDAPDomainName },
                new ScriptStringExecutionParameterDetails("DomainAlias") { Value = LDAPDomainNetBIOSName },
                new ScriptStringExecutionParameterDetails("Name") { Value = "FCT:New-LDAPIdentitySource" },
            };

            // add the parameters to the execution data
            foreach (var p in parameters) executionData.Parameters.Add(p);

            // create the script execution, wait for it to complete, and assert a successful response
            ScriptExecutionCollection Executions = PrivateCloudResource!.GetScriptExecutions();
            var executionResource = (await Executions.CreateOrUpdateAsync(WaitUntil.Completed, ExecutionNameId, executionData)).Value;
            var executionResponse = executionResource.Data;

            Assert.That(executionResponse.ProvisioningState, Is.EqualTo(ScriptExecutionProvisioningState.Succeeded), $"{cmdletName} should always succeed but instead its state is: {executionData.ProvisioningState}");
        }

        /// <summary>
        /// Async method that tests the script execution of Remove-ExternalIdentitySources.
        /// </summary>
        [Test, Order(2)]
        public async Task ScriptExecution_RemoveExternalIdentitySources()
        {
            // set up the cmdlet and cmldet resource
            string packageName = "Microsoft.AVS.Identity";
            string armPackageName = $"{packageName}@{IdentityPackageVersion}";
            string cmdletName = "Remove-ExternalIdentitySources";
            var resourceId = $"/subscriptions/{AzureSubscriptionId}/resourceGroups/{AzureResourceGroup}/providers/Microsoft.AVS/privateClouds/{AzurePrivateCloudName}/scriptPackages/{armPackageName}/scriptCmdlets/{cmdletName}";
            ResourceIdentifier CmdletResourceId = new(resourceId);

            // set up the script execution name
            Random r = new();
            int randomNumber = r.Next(1, 5000);
            ResourceIdentifier ExecutionNameId = new($"FCT:{AzureResourceGroup}-execution-{randomNumber}");
            var executionResourceString = $"/subscriptions/{AzureSubscriptionId}/resourceGroups/{AzureResourceGroup}/providers/Microsoft.AVS/privateClouds/{AzurePrivateCloudName}/scriptExecutions/{ExecutionNameId}";

            // set up the execution data
            var executionData = new ScriptExecutionData
            {
                ScriptCmdletId = CmdletResourceId,
                Retention = System.Xml.XmlConvert.ToString(TimeSpan.FromMinutes(30)), // script execution will be deleted after X minute(s)
                Timeout = System.Xml.XmlConvert.ToString(TimeSpan.FromMinutes(2)) // script execution will timeout after X minute(s) if it does not complete
            };

            // create the script execution, wait for it to complete, and assert a successful response
            ScriptExecutionCollection Executions = PrivateCloudResource!.GetScriptExecutions();
            var executionResource = (await Executions.CreateOrUpdateAsync(WaitUntil.Completed, ExecutionNameId, executionData)).Value;
            var executionResponse = executionResource.Data;

            Assert.That(executionResponse.ProvisioningState, Is.EqualTo(ScriptExecutionProvisioningState.Succeeded), $"{cmdletName} should always succeed but instead its state is: {executionData.ProvisioningState}");
        }

        /// <summary>
        /// Async method that tests the script execution of New-LDAPSIdentitySource. This test must be run after the Remove-ExternalIdentitySources test.
        /// </summary>
        [Test, Order(3)]
        public async Task ScriptExecution_NewLDAPSIdentitySource()
        {
            // set up the cmdlet and cmldet resource
            string packageName = "Microsoft.AVS.Identity";
            string armPackageName = $"{packageName}@{IdentityPackageVersion}";
            string cmdletName = "New-LDAPSIdentitySource";
            var resourceId = $"/subscriptions/{AzureSubscriptionId}/resourceGroups/{AzureResourceGroup}/providers/Microsoft.AVS/privateClouds/{AzurePrivateCloudName}/scriptPackages/{armPackageName}/scriptCmdlets/{cmdletName}";
            ResourceIdentifier CmdletResourceId = new(resourceId);

            // set up the script execution name
            Random r = new();
            int randomNumber = r.Next(1, 5000);
            ResourceIdentifier ExecutionNameId = new($"FCT:{AzureResourceGroup}-execution-{randomNumber}");
            var executionResourceString = $"/subscriptions/{AzureSubscriptionId}/resourceGroups/{AzureResourceGroup}/providers/Microsoft.AVS/privateClouds/{AzurePrivateCloudName}/scriptExecutions/{ExecutionNameId}";

            // set up the execution data — timeout increased to 5 minutes because LDAPS requires SSH session
            // initialization (Posh-SSH lazy connect to each ESXi host) on top of the identity source configuration
            var executionData = new ScriptExecutionData
            {
                ScriptCmdletId = CmdletResourceId,
                Retention = System.Xml.XmlConvert.ToString(TimeSpan.FromMinutes(30)), // script execution will be deleted after X minute(s)
                Timeout = System.Xml.XmlConvert.ToString(TimeSpan.FromMinutes(5)) // script execution will timeout after X minute(s) if it does not complete
            };

            // set up the execution parameters
            ScriptExecutionParameterDetails[] parameters = new ScriptExecutionParameterDetails[]
            {
                new PSCredentialExecutionParameterDetails("Credential") { Username = $"{LDAPUsername}@{LDAPDomainName}", Password = LDAPPassword },
                new ScriptStringExecutionParameterDetails("BaseDNGroups") { Value = LDAPBaseDNGroups },
                new ScriptStringExecutionParameterDetails("BaseDNUsers") { Value = LDAPBaseDNUsers },
                new ScriptStringExecutionParameterDetails("PrimaryUrl") { Value = LDAPSecondaryUrl },
                new ScriptStringExecutionParameterDetails("DomainName") { Value = LDAPDomainName },
                new ScriptStringExecutionParameterDetails("DomainAlias") { Value = LDAPDomainNetBIOSName },
                new ScriptStringExecutionParameterDetails("Name") { Value = "FCT:New-LDAPSIdentitySource" },
            };

            // SSLCertificatesSasUrl is intentionally omitted — the cmdlet will fetch certs
            // directly from the domain controllers, which is the more reliable path
            // (confirmed working via Debug-LDAPSIdentitySources).

            // add the parameters to the execution data
            foreach (var p in parameters) executionData.Parameters.Add(p);

            // create the script execution, wait for it to complete, and assert a successful response
            ScriptExecutionCollection Executions = PrivateCloudResource!.GetScriptExecutions();
            ScriptExecutionData executionResponse;

            try
            {
                var executionResource = (await Executions.CreateOrUpdateAsync(WaitUntil.Completed, ExecutionNameId, executionData)).Value;
                executionResponse = executionResource.Data;
            }
            catch (Azure.RequestFailedException ex)
            {
                // If the ARM long-running operation itself fails, retrieve the execution to get server-side diagnostics
                ScriptExecutionResource? failedExecution = null;
                try
                {
                    failedExecution = (await PrivateCloudResource!.GetScriptExecutionAsync(ExecutionNameId)).Value;
                }
                catch { /* execution may not exist yet */ }

                string diagnostics = failedExecution != null
                    ? FormatExecutionDiagnostics(failedExecution.Data)
                    : "No execution resource available for diagnostics.";

                Assert.Fail($"{cmdletName} ARM operation failed: {ex.Message}\n\nServer-side diagnostics:\n{diagnostics}");
                return;
            }

            string output = FormatExecutionDiagnostics(executionResponse);
            Assert.That(executionResponse.ProvisioningState, Is.EqualTo(ScriptExecutionProvisioningState.Succeeded),
                $"{cmdletName} should always succeed but instead its state is: {executionResponse.ProvisioningState}\n\nExecution output:\n{output}");
        }

        /// <summary>
        /// Async method that tests the script execution of Debug-LDAPSIdentitySources.
        /// Runs after LDAPS identity source has been added to verify the configuration is healthy.
        /// </summary>
        [Test, Order(4)]
        public async Task ScriptExecution_DebugLDAPSIdentitySources()
        {
            // set up the cmdlet and cmldet resource
            string packageName = "Microsoft.AVS.Identity";
            string armPackageName = $"{packageName}@{IdentityPackageVersion}";
            string cmdletName = "Debug-LDAPSIdentitySources";
            var resourceId = $"/subscriptions/{AzureSubscriptionId}/resourceGroups/{AzureResourceGroup}/providers/Microsoft.AVS/privateClouds/{AzurePrivateCloudName}/scriptPackages/{armPackageName}/scriptCmdlets/{cmdletName}";
            ResourceIdentifier CmdletResourceId = new(resourceId);

            // set up the script execution name
            Random r = new();
            int randomNumber = r.Next(1, 5000);
            ResourceIdentifier ExecutionNameId = new($"FCT:{AzureResourceGroup}-execution-{randomNumber}");
            var executionResourceString = $"/subscriptions/{AzureSubscriptionId}/resourceGroups/{AzureResourceGroup}/providers/Microsoft.AVS/privateClouds/{AzurePrivateCloudName}/scriptExecutions/{ExecutionNameId}";

            // set up the execution data — 5 minute timeout because Debug-LDAPS uses SSH sessions
            var executionData = new ScriptExecutionData
            {
                ScriptCmdletId = CmdletResourceId,
                Retention = System.Xml.XmlConvert.ToString(TimeSpan.FromMinutes(30)), // script execution will be deleted after X minute(s)
                Timeout = System.Xml.XmlConvert.ToString(TimeSpan.FromMinutes(5)) // script execution will timeout after X minute(s) if it does not complete
            };

            // create the script execution, wait for it to complete, and assert a successful response
            ScriptExecutionCollection Executions = PrivateCloudResource!.GetScriptExecutions();
            ScriptExecutionData executionResponse;

            try
            {
                var executionResource = (await Executions.CreateOrUpdateAsync(WaitUntil.Completed, ExecutionNameId, executionData)).Value;
                executionResponse = executionResource.Data;
            }
            catch (Azure.RequestFailedException ex)
            {
                ScriptExecutionResource? failedExecution = null;
                try
                {
                    failedExecution = (await PrivateCloudResource!.GetScriptExecutionAsync(ExecutionNameId)).Value;
                }
                catch { /* execution may not exist yet */ }

                string diagnostics = failedExecution != null
                    ? FormatExecutionDiagnostics(failedExecution.Data)
                    : "No execution resource available for diagnostics.";

                Assert.Fail($"{cmdletName} ARM operation failed: {ex.Message}\n\nServer-side diagnostics:\n{diagnostics}");
                return;
            }

            string output = FormatExecutionDiagnostics(executionResponse);
            Assert.That(executionResponse.ProvisioningState, Is.EqualTo(ScriptExecutionProvisioningState.Succeeded),
                $"{cmdletName} should always succeed but instead its state is: {executionResponse.ProvisioningState}\n\nExecution output:\n{output}");
        }

        /// <summary>
        /// Async method that tests the script execution of Remove-ExternalIdentitySources after LDAPS identity source has been added.
        /// </summary>
        [Test, Order(5)]
        public async Task ScriptExecution_RemoveLDAPSExternalIdentitySources()
        {
            // set up the cmdlet and cmldet resource
            string packageName = "Microsoft.AVS.Identity";
            string armPackageName = $"{packageName}@{IdentityPackageVersion}";
            string cmdletName = "Remove-ExternalIdentitySources";
            var resourceId = $"/subscriptions/{AzureSubscriptionId}/resourceGroups/{AzureResourceGroup}/providers/Microsoft.AVS/privateClouds/{AzurePrivateCloudName}/scriptPackages/{armPackageName}/scriptCmdlets/{cmdletName}";
            ResourceIdentifier CmdletResourceId = new(resourceId);

            // set up the script execution name
            Random r = new();
            int randomNumber = r.Next(1, 5000);
            ResourceIdentifier ExecutionNameId = new($"FCT:{AzureResourceGroup}-execution-{randomNumber}");
            var executionResourceString = $"/subscriptions/{AzureSubscriptionId}/resourceGroups/{AzureResourceGroup}/providers/Microsoft.AVS/privateClouds/{AzurePrivateCloudName}/scriptExecutions/{ExecutionNameId}";

            // set up the execution data
            var executionData = new ScriptExecutionData
            {
                ScriptCmdletId = CmdletResourceId,
                Retention = System.Xml.XmlConvert.ToString(TimeSpan.FromMinutes(30)), // script execution will be deleted after X minute(s)
                Timeout = System.Xml.XmlConvert.ToString(TimeSpan.FromMinutes(2)) // script execution will timeout after X minute(s) if it does not complete
            };

            // create the script execution, wait for it to complete, and assert a successful response
            ScriptExecutionCollection Executions = PrivateCloudResource!.GetScriptExecutions();
            var executionResource = (await Executions.CreateOrUpdateAsync(WaitUntil.Completed, ExecutionNameId, executionData)).Value;
            var executionResponse = executionResource.Data;

            Assert.That(executionResponse.ProvisioningState, Is.EqualTo(ScriptExecutionProvisioningState.Succeeded), $"{cmdletName} should always succeed but instead its state is: {executionData.ProvisioningState}");
        }

        /// <summary>
        /// Formats script execution diagnostics (errors, warnings, information, failure reason) into a readable string.
        /// </summary>
        private static string FormatExecutionDiagnostics(ScriptExecutionData data)
        {
            var parts = new List<string>();

            if (!string.IsNullOrEmpty(data.FailureReason))
                parts.Add($"[FailureReason] {data.FailureReason}");

            if (data.Errors?.Count > 0)
                parts.Add($"[Errors]\n{string.Join("\n", data.Errors)}");

            if (data.Warnings?.Count > 0)
                parts.Add($"[Warnings]\n{string.Join("\n", data.Warnings)}");

            if (data.Information?.Count > 0)
                parts.Add($"[Information]\n{string.Join("\n", data.Information)}");

            if (data.Output?.Count > 0)
                parts.Add($"[Output]\n{string.Join("\n", data.Output)}");

            return parts.Count > 0 ? string.Join("\n\n", parts) : "No diagnostic output available.";
        }
    }
}