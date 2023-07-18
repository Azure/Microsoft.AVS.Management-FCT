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
            string packageName = "Microsoft.AVS.Management";
            string majorPackageVersion = "5";
            string packageVersion = $"{majorPackageVersion}.*";
            string armPackageName = $"{packageName}@{packageVersion}";
            string cmdletName = "Get-CloudAdminGroups";
            var resourceId = $"/subscriptions/{AzureSubscriptionId}/resourceGroups/{AzureResourceGroup}/providers/Microsoft.AVS/privateClouds/{AzurePrivateCloudName}/scriptPackages/{armPackageName}/scriptCmdlets/{cmdletName}";
            ResourceIdentifier CmdletResourceId = new(resourceId);

            // set up the script execution name
            Random r = new();
            int randomNumber = r.Next(1, 1000);
            ResourceIdentifier ExecutionNameId = new($"{AzureResourceGroup}-execution-{randomNumber}");
            var executionResourceString = $"/subscriptions/{AzureSubscriptionId}/resourceGroups/{AzureResourceGroup}/providers/Microsoft.AVS/privateClouds/{AzurePrivateCloudName}/scriptExecutions/{ExecutionNameId}";
            ResourceIdentifier ExecutionName = new(executionResourceString);

            // set up the execution data
            var executionData = new ScriptExecutionData
            {
                ScriptCmdletId = CmdletResourceId,
                Retention = System.Xml.XmlConvert.ToString(TimeSpan.FromMinutes(1)), // script execution will be deleted after X minute(s)
                Timeout = System.Xml.XmlConvert.ToString(TimeSpan.FromMinutes(2)) // script execution will timeout after X minute(s) if it does not complete
            };

            var executionResource = Client.GetScriptExecutionResource(ExecutionName);

            ScriptExecutionCollection Executions = PrivateCloudResource!.GetScriptExecutions();
            executionResource = (await Executions.CreateOrUpdateAsync(WaitUntil.Completed, ExecutionNameId, executionData)).Value;
            var executionResponse = executionResource.Data;

            Assert.That(executionResponse.ProvisioningState, Is.EqualTo(ScriptExecutionProvisioningState.Succeeded), $"{cmdletName} should always succeed but instead its state is: {executionData.ProvisioningState}");
        }
    }
}