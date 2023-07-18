# [Microsoft.AVS.Management](https://github.com/Azure/Microsoft.AVS.Management/) Functional Testing Pipeline [![Build Status](https://dev.azure.com/avs-oss/Public/_apis/build/status%2FAVS.Management.FCT?branchName=main)](https://dev.azure.com/avs-oss/Public/_build/latest?definitionId=8&branchName=main)

This project provides a publicly available functional testing pipeline for the [Microsoft.AVS.Management](https://github.com/Azure/Microsoft.AVS.Management/) repository. You can use this as a template for continuous integration testing for scenarios that require running an [Azure VMware Solution (AVS)](https://azure.microsoft.com/en-us/services/azure-vmware/) environment.

## **Table of Contents**

- [**Setup**](#setup)
  - [**Configuring Azure**](#configuring-azure)
  - [**Configuring the pipeline**](#configuring-the-pipeline)
  - [**Connecting the two**](#connecting-the-two)
  - [**Local development (optional)**](#local-development-optional)
- [**Execution**](#execution)
- [**Additional Resources**](#additional-resources)
- [**Contributing**](contributing.md)
- [**License**](license.txt)
- [**Code of Conduct**](code-of-conduct.md)
- [**Security**](security.md)
- [**Support**](support.md)

Overview of important files in this repository:

| File/folder           | Description                                     |
|-----------------------|-------------------------------------------------|
| `Tests/Tests.cs`      | C# file containing the tests to be run.         |
| `azure-pipelines.yml` | Azure DevOps pipeline definition file.          |
| `main.bicep`          | Bicep file for deploying an AVS private cloud.  |
| `main.json`           | JSON file generated from the `main.bicep` file. |

## **Setup**

To use this project, you need to have the following installed:

- [.NET 7.0 or later](https://dotnet.microsoft.com/download/dotnet/7.0)
- [Azure CLI](https://docs.microsoft.com/en-us/cli/azure/install-azure-cli?view=azure-cli-latest) 
- [Visual Studio Code](https://code.visualstudio.com/)
- [Visual Studio 2022 or later](https://visualstudio.microsoft.com/vs/preview/)

After installing all of those, please clone this repository to your local machine and open it in your choice of IDE. 

### **Configuring Azure** 

In order to run the tests, you will need to have an AVS Private Cloud deployed in your Azure Subscription. 

Please have your desired subscription ID ready for the next steps. If you don't have your subscription set up, please [follow these steps to set it up](https://docs.microsoft.com/en-us/azure/cost-management-billing/manage/create-subscription?tabs=azure-portal). 

You can check your AVS quotas [here](https://ms.portal.azure.com/#view/Microsoft_Azure_Capacity/QuotaMenuBlade/~/myQuotas). If you don't have enough quota to deploy an AVS private cloud, please [follow these steps](https://docs.microsoft.com/en-us/azure/virtual-machines/quotas) to request an increase. 

After making sure you have enough quota on your desired subscription, please follow these steps:

1. Log into Azure in your default browser on your local machine.
2. Navigate to project folder in terminal and run `az login`. This will log you in to your Azure account.
2. Run `az account set --subscription <subscription_id>`. This will set the subscription you want to use for your pipeline. Please replace `<subscription_id>` with your subscription ID mentioned above.
3. Change up the `main.bicep` to fit your needs. You can change the location, name, tags, cloud version, etc. ([click here to learn more about Bicep](https://learn.microsoft.com/en-us/azure/templates/Microsoft.AVS/2022-05-01/privateclouds?pivots=deployment-language-bicep)). After changing and saving the file please run `az bicep build --file main.bicep` to build your `main.json` file. Please verify your `main.json` got changed accordingly by checking the file. 

### **Configuring the pipeline**

1. Open `azure-pipelines.yml` and update the `azureSubscription` and `resourceGroupName` variables with your own Azure subscription and resource group name.
2. Create a new [Azure DevOps](https://learn.microsoft.com/en-us/azure/devops/user-guide/sign-up-invite-teammates?view=azure-devops) project or open an existing one. 
2. Make a new repository in your Azure DevOps project, or a new GitHub repository, and push your code to it (GitHub repository can be private).
2. Push the cloned and modified repository to the new Azure DevOps repository or GitHub repository.
6. Create a new Azure DevOps pipeline by navigating to Pipelines section in your new DevOps project, and follow the instructions to connect it to your repository.
7. Select the `azure-pipelines.yml` file as the pipeline configuration file.

### **Connecting the two**

1. Connect your Azure DevOps project to your Azure subscription.
      * Go to your Azure DevOps project, click on the `Project Settings` and then click on `Service Connections`.
      * Click on `New service connection` and select `Azure Resource Manager`.
      * Select `Service principal (automatic)` and follow the instructions to create a new service principal.
2. Connect your Azure DevOps project to your Azure Resource Manager (ARM) resource group.
      * Go to your Azure DevOps project, click on the `Project Settings` and then click on `Service Connections`.
      * Click on `New service connection` and select `GitHub` or `Azure DevOps Repository`.

After completing these steps, you should be able to run the pipeline. Please double check both connections show up under service connections in your project settings. 

**The Service Principal secrets have an expiration date and will need to be updated when they expire!** To check the expiration date of your secrets, go to your Azure DevOps project, click on the `Project Settings`, and then click on `Service Connections`. You will see a list of connections, including the ones you have set up. Click on your ARM connection (cloud icon) button next to the variable you want to check, and then click on `Manage Service Principal`. On the new page, click on `Client credentials` and you will see the expiration date of your secret under `Expires`. To update the secret, click on `New client secret` and follow the instructions.

### **Local development (optional)**

While not necessary, you can run the tests locally to make sure they work before running them in the pipeline.

To do so, you need to set up the following environment variables in your terminal (naming is important):
* `SUBSCRIPTIONID=$(az account show --query 'id' -o tsv)` - keep in mind that this will set the subscription ID to the one you are currently using, so if you want to use a different subscription, you need to set it manually. Keep the `$()` syntax and do not add parenthesis around the command.
* `RESOURCEGROUPNAME="<your_resource_group_name>"`
* `PRIVATECLOUDNAME="<your_private_cloud_name>"`

#### Setting Environment Variables on **_Windows_**:

1. Open PowerShell.
2. Run the following command for each variable: `[Environment]::SetEnvironmentVariable("<VARIABLE_NAME>", <VALUE>, "User")`, replacing variable names and values with the ones mentioned above.
3. Verify that the environment variables are set by running this command: 
      * `Write-Host "SUBSCRIPTIONID: $env:SUBSCRIPTIONID"; Write-Host "RESOURCEGROUPNAME: $env:RESOURCEGROUPNAME"; Write-Host "PRIVATECLOUDNAME: $env:PRIVATECLOUDNAME"`

#### Setting Environment Variables on **_Linux_** or **_macOS_** or **_WSL_**:

1. Open the Terminal.
2. Enter `nano ~/.bash_profile` if you are using Bash and `nano ~/.zshrc` for ZSH. 
3. Add the following line for each variable to the file: `export <VARIABLE_NAME>=<VALUE>`, replacing variable names and values with the ones mentioned above.
4. Press *Ctrl + S* to save, then *Ctrl + X* to exit nano and save the file. If you encounter any problems with nano, you can use different text editors, such as Vim or Emacs, to do the same.
5. Type `source ~/.bash_profile` or `source ~/.zshrc` to reload the environment variables (or open a new terminal session).
6. Verify that the environment variables are set by running this command: 
      * `echo "SUBSCRIPTIONID: $SUBSCRIPTIONID"; echo "RESOURCEGROUPNAME: $RESOURCEGROUPNAME"; echo "PRIVATECLOUDNAME: $PRIVATECLOUDNAME"`.

To execute the tests:

1. Navigate to the `Tests` folder in your terminal.
2. Run the tests by selecting the `Run Tests` option from the `Test` menu in Visual Studio or by running the `dotnet test` command in your terminal. This will restore the NuGet packages and build the project, then run the tests.

In the case of *WSL*, you might get time *out-of-sync* errors when running AZ commands. To fix this, please run `sudo hwclock -s` in your terminal to sync your time with the hardware clock.

## **Execution**

Execution of the pipeline can be triggered in the following ways:

* By pushing changes to the `main` branch. The pipeline will run automatically and execute the tests. 
* By creating a pull request. The pipeline will run automatically and execute the tests.
* By manually running the pipeline. You can do this by going to your Azure DevOps project, clicking on `Pipelines`, and then clicking on `Run pipeline`. You will be prompted to select the branch you want to run the pipeline on. Select `main` and click on `Run`.
* By running the pipeline as a cronjob. You can do this by going to your Azure DevOps project, clicking on `Pipelines`, and then clicking on `Run pipeline`. You will be prompted to select the branch you want to run the pipeline on. Select `main` and click on `Run`. Then click on `Triggers` and select `Scheduled`. You can then set up the schedule you want to run the pipeline on.

After executing the pipeline, you should be able to see the results in the `Tests` tab of your pipeline run, with any failed tests highlighted in red. You can also see the results of the pipeline run in the `Runs` tab of your pipeline.

## **Additional Resources**

* [.NET Official Tutorials](https://learn.microsoft.com/en-us/dotnet/core/tutorials/)
* [Azure CLI documentation](https://docs.microsoft.com/en-us/cli/azure/?view=azure-cli-latest)
* [Azure DevOps documentation](https://docs.microsoft.com/en-us/azure/devops/?view=azure-devops)
* [Azure DevOps Pipelines documentation](https://docs.microsoft.com/en-us/azure/devops/pipelines/?view=azure-devops)
* [Bicep documentation](https://docs.microsoft.com/en-us/azure/azure-resource-manager/bicep/)
* [Azure DevOps Pipelines YAML schema reference](https://docs.microsoft.com/en-us/azure/devops/pipelines/yaml-schema?view=azure-devops&tabs=schema)
* [Pipeline Scheduling](https://learn.microsoft.com/en-us/azure/devops/pipelines/process/scheduled-triggers?view=azure-devops&tabs=yaml)
* [NUnit documentation](https://docs.nunit.org/)
* [NuGet documentation](https://docs.microsoft.com/en-us/nuget/)
* [Azure .NET SDK documentation](https://docs.microsoft.com/en-us/dotnet/api/overview/azure/?view=azure-dotnet)

## **Trademarks**

This project may contain trademarks or logos for projects, products, or services. Authorized use of Microsoft trademarks or logos is subject to and must follow Microsoft’s Trademark & Brand Guidelines. Use of Microsoft trademarks or logos in modified versions of this project must not cause confusion or imply Microsoft sponsorship. Any use of third-party trademarks or logos is subject to those third-party’s policies.

