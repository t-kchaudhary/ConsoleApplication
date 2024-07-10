using ArcForPublicCloud;


public class FetchToken
{
    public static async Task Main(String[] args)
    {
       await GetToken();
    }

    public static async Task GetToken()
    {
        var awsAccountId = "767397730009";
        var publicCloudConnectorArmId = "/subscriptions/4bd2aa0f-2bd2-4d67-91a8-5a4533d58600/resourceGroups/sakanwar/providers/microsoft.hybridconnectivity/publicCloudConnectors/publicCloudarcmigrate9921";
        var azureUserTenantId = "72f988bf-86f1-41af-91ab-2d7cd011db47";
        var solutionConfigurationArmId = "/subscriptions/4bd2aa0f-2bd2-4d67-91a8-5a4533d58600/resourceGroups/sakanwar/providers/microsoft.hybridconnectivity/publicCloudConnectors/publicCloudarcmigrate9921/providers/Microsoft.HybridConnectivity/solutionConfigurations/arcMigrate";
        var options = new ResourceProviderOptions();
        options.ArcServerGnsDPS2SAppId = "8ccb1e4b-1cee-45d3-ab35-71da6934ca94";
        options.GnsDPAppId = "958acf75-e54e-483f-95da-e44c1932b288";
        options.GnsDPAADInstance = "https://login.windows.net";
        options.GnsDPAADAuthorityTenantId = "33e01921-4d64-4f8c-a055-5bdaffd5e33d";
        options.GnsDPEndpoint = "https://canary.guestnotificationservice.azure.com";
        
        var client = new HttpClient();
        var gnsDataPlaneHelper = new GNSDataplaneHelper(options, client, "Prod");
        IDictionary<string, string> SolutionSettings = new Dictionary<string, string>();

        // Get AWS credentials.
        var awsCreds = await gnsDataPlaneHelper.GetAwsCredentialAsync(publicCloudConnectorArmId, awsAccountId, azureUserTenantId) ?? throw new UnauthorizedAccessException($"Failed to get AWS token for awsAccountId: {awsAccountId} solutionConfigurationArmId: {solutionConfigurationArmId}");
        Console.WriteLine(awsCreds);

        var azureRegion = "eastus2euap";

        // Fetch instances.
        var instances = await Ec2_Instance.GetAllResources(awsCreds, awsAccountId, publicCloudConnectorArmId, azureRegion);
        Console.WriteLine(instances);

        Console.ReadLine();
    }
}







