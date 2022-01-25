using System;
using System.Threading.Tasks;

using ProgrammerAl.OnetugExample.Config;

using Pulumi;
using Pulumi.AzureNative.Resources;
using Pulumi.AzureNative.Storage;
using Pulumi.AzureNative.Storage.Inputs;
using Pulumi.AzureNative.Web;
using Pulumi.AzureNative.Web.Inputs;

class MyStack : Stack
{
    public MyStack()
    {
        var config = new Config();
        var globalConfig = LoadGlobalConfig(config);
        var appServiceConfig = config.RequireObject<AppServiceConfigDto>("app-service").GenerateValidConfigObject();

        // Create an Azure Resource Group
        var resourceGroup = new ResourceGroup(globalConfig.ResourceGroupName);

        CreateAppService(resourceGroup, appServiceConfig);

        // Create an Azure resource (Storage Account)
        var storageAccount = new StorageAccount("sa", new StorageAccountArgs
        {
            ResourceGroupName = resourceGroup.Name,
            Sku = new SkuArgs
            {
                Name = SkuName.Standard_LRS
            },
            Kind = Kind.StorageV2
        });

        // Export the primary key of the Storage Account
        this.PrimaryStorageKey = Output.Tuple(resourceGroup.Name, storageAccount.Name).Apply(names =>
            Output.CreateSecret(GetStorageAccountPrimaryKey(names.Item1, names.Item2)));
    }

    private void CreateAppService(ResourceGroup resourceGroup, AppServiceConfig appServiceConfig)
    {
        var appServicePlan = new AppServicePlan(appServiceConfig.ServicePlanName, new AppServicePlanArgs
        {
            ResourceGroupName = resourceGroup.Name,
            Kind = "App",
            Sku = new SkuDescriptionArgs
            {
                Tier = appServiceConfig.Tier,
                Name = appServiceConfig.TierName
            },
            // For Linux, you need to change the plan to have Reserved = true property.
            Reserved = true,
        });

        var webApp = new WebApp(appServiceConfig.WebAppName, new WebAppArgs
        {
            Kind = "App",
            ResourceGroupName = resourceGroup.Name,
            ServerFarmId = appServicePlan.Id,
            HttpsOnly = true,
            //SiteConfig = functionAppSiteConfig,
            ClientAffinityEnabled = false
        });

        WebAppEndpoint = webApp.DefaultHostName;
    }

    private static async Task<string> GetStorageAccountPrimaryKey(string resourceGroupName, string accountName)
    {
        var accountKeys = await ListStorageAccountKeys.InvokeAsync(new ListStorageAccountKeysArgs
        {
            ResourceGroupName = resourceGroupName,
            AccountName = accountName
        });
        return accountKeys.Keys[0].Value;
    }

    private GlobalConfig LoadGlobalConfig(Config config)
    {
        string location = config.Require("location");
        string resourceGroupName = config.Require("resource-group-name");
        string environment = config.Require("environment");

        return new GlobalConfig(location, resourceGroupName, environment);
    }

    [Output]
    public Output<string>? PrimaryStorageKey { get; set; }

    [Output]
    public Output<string>? WebAppEndpoint { get; set; }
}
