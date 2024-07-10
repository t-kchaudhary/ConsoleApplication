namespace ArcForPublicCloud
{
    using ArcForPublicCloud;
#if !ONEBOX_TESTING
    using Azure.Identity;
#endif
    using Azure.Security.KeyVault.Secrets;

    using Newtonsoft.Json.Linq;
    using System;
    using System.Globalization;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Runtime.CompilerServices;
    using System.Security.Cryptography.X509Certificates;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Helper class to interact with GNS dataplane APIs.
    /// [PublicClouds_TODO] - Move the logic in this file and remove the file after fixing 
    /// Azure function DLL versioning loading issue: https://msazure.visualstudio.com/AzureArcPlatform/_workitems/edit/26491007
    /// </summary> 
    public class GNSDataplaneHelper
    {
        private readonly HttpClient httpClient;
        private readonly string environment;
        private readonly ResourceProviderOptions options;

        private static readonly string gnsDataplaneApiVersion = "2024-08-01-Preview";
        private static readonly string fetchTokenAPIName = "fetchToken"; // fetchToken GNS dataplane API name.
        private static readonly string pathSeparator = "/";

        /// <summary>
        /// Initializer
        /// </summary>
        /// <param name="log">Logger object</param>
        /// <param name="resourceProviderOptions">Resource provider configuration</param>
        /// <param name="httpClient"></param> 
        /// <param name="environment"> The environment name</param> 
        public GNSDataplaneHelper(
               ResourceProviderOptions resourceProviderOptions,
               HttpClient httpClient,
               string environment)
        {
            this.options = resourceProviderOptions;
            this.httpClient = httpClient;
            this.environment = environment;
        }
     
        

        /// <summary>
        /// This method returns AwsCredentials.
        /// <returns>AwsCredentials</returns>
        /// </summary>
        public async Task<AwsCredentials> GetAwsCredentialAsync(string publicCloudConnectorArmId, string awsAccountId, string azureUserTenantId)
        {
            var methodName = GetAsyncMethodName();
            Console.WriteLine($"{methodName}(): publicCloudConnectorArmId:{publicCloudConnectorArmId} AwsAccountId: {awsAccountId}, UserTenant: {azureUserTenantId}");
            var fetchTokenQueryParams = $"resourceId={publicCloudConnectorArmId}&awsAccountId={awsAccountId}&azureUserTenantId={azureUserTenantId}";
            var result = await this.GNSDataplanePostAsync(fetchTokenAPIName, null, fetchTokenQueryParams);

            if (string.IsNullOrWhiteSpace(result))
            {
                Console.WriteLine($"{methodName}(): Unable to fetch token for publicCloudConnectorArmId:{publicCloudConnectorArmId} awsAccountId: {awsAccountId}");

                return null;
            }

            var json = JObject.Parse(result);
            var accessKeyId = json.GetValue("accessKeyId").ToString();
            var secretAccessKey = json.GetValue("secretAccessKey").ToString();
            var sessionToken = json.GetValue("sessionToken").ToString();
            var expirationDate = DateTime.Parse(json.GetValue("expirationDate").ToString(), CultureInfo.CurrentCulture);
            var amazonAwsCreds = new AwsCredentials(accessKeyId, secretAccessKey, sessionToken, expirationDate);

            Console.WriteLine($"{methodName}(). Successfully able to fetch token for publicCloudConnectorArmId:{publicCloudConnectorArmId} awsAccountId: {awsAccountId}.");

            return amazonAwsCreds;
        }

        /// <inheritdoc />
        public async Task<string> GNSDataplanePostAsync(string apiName, string jsonSerializedBody, string queryParameters)
        {
            var methodName = GetAsyncMethodName();

            Console.WriteLine($"{methodName}() apiName:{apiName} jsonSearlizedBody:{jsonSerializedBody} queryParameters:{queryParameters}");

            if (string.IsNullOrWhiteSpace(this.options.GnsDPAADAuthorityTenantId) ||
                string.IsNullOrWhiteSpace(apiName) ||
                (string.IsNullOrWhiteSpace(queryParameters) && string.IsNullOrWhiteSpace(jsonSerializedBody)))
            {
                Console.WriteLine($"{methodName}() invalid parameter");
                return null;
            }

            var resourceUri = $"{this.options.GnsDPEndpoint}/solutionConfigurations/{apiName}?api-version={gnsDataplaneApiVersion}";

            if (!string.IsNullOrWhiteSpace(queryParameters))
            {
                resourceUri += $"&{queryParameters}";
            }

            Console.WriteLine($"{methodName}() resourceUri:{resourceUri}");

            var request = new HttpRequestMessage(HttpMethod.Post, resourceUri);
            request.Headers.Authorization = await this.CreateAuthenticationHeader();
            if (!string.IsNullOrWhiteSpace(jsonSerializedBody))
            {
                request.Content = new StringContent(jsonSerializedBody, Encoding.UTF8, "application/json");
            }

             var response = this.httpClient.SendAsync(request, CancellationToken.None);
            var res = response.GetAwaiter().GetResult();
            var result = await res.Content.ReadAsStringAsync();

            if (res.StatusCode != System.Net.HttpStatusCode.OK)
            {
                Console.WriteLine($"{methodName}() resourceUri:{resourceUri} responseCode:{res.StatusCode} error:{result}");
                result = null;
            }

            return result;
        }

        /// <summary>
        /// Create AAD token to authorize http request.
        /// </summary>
        /// <returns>Authorized head value.</returns>
        public async Task<AuthenticationHeaderValue> CreateAuthenticationHeader()
        {
            var methodName = GetAsyncMethodName();

            //            SecretClient client;

            //#if ONEBOX_TESTING
            //            client = CommonUtils.GetKeyVaultClientWithAppSecret(this.options.GnsKeyVaultUri,
            //                this.options.OneboxTesting.GnsKeyVaultTenantId,
            //                this.options.OneboxTesting.OneboxTestingAppId,
            //                this.options.OneboxTesting.OneboxTestingAppSecret);
            //#else
            //            client = new SecretClient(new Uri(this.options.GnsKeyVaultUri), new DefaultAzureCredential());
            //#endif

            // [PublicCloud_TODO] - https://msazure.visualstudio.com/AzureArcPlatform/_workitems/edit/28301580. Improvement: We can retrive the certificate in Startup.cs. We do not need to get the cert for each call.
            //var response = client.GetSecret(this.options.PubliccloudArcserverGnsDPAuthCertName);

            var certificate = new X509Certificate2(@"C:\Users\t-kchaudhary\Downloads\arcforpubliccloud-arcforpuliccloudmigrate-20240627 (2).pfx");


            // Since DF doesn't support SNI based validation, thumbprint based validation should be used.
            var sendX5C = true;
            //if (CommonUtils.IsDogfoodEnvironment(this.environment) || CommonUtils.IsDevelopmentEnvironment(this.environment))
            //{
            //    sendX5C = false;
            //}

            Console.WriteLine($"{methodName}(): sendX5C: {sendX5C}" +
                    $" GnsDPAADInstance: {this.options.GnsDPAADInstance}" +
                    $" GnsDPAADAuthorityTenantId: {this.options.GnsDPAADAuthorityTenantId}" +
                    $" ArcServerGnsDPS2SAppId: {this.options.ArcServerGnsDPS2SAppId}" +
                    $" Audience: {this.options.GnsDPAppId + pathSeparator}" +
                    $" AADInstanceAzureRegion: {this.options.AADInstanceAzureRegion}");

            // For local test, please use options.Certificate to replace certificate.
            var authenticationResult = await TokenUtils.GetAccessTokenAsync(
                this.options.GnsDPAADInstance,
                this.options.GnsDPAADAuthorityTenantId,
                this.options.ArcServerGnsDPS2SAppId,
                this.options.GnsDPAppId + pathSeparator,
                certificate,
                this.options.AADInstanceAzureRegion,
                sendX5C,
                this.environment);

            return new AuthenticationHeaderValue("Bearer", authenticationResult.AccessToken);
        }

        private static string GetAsyncMethodName([CallerMemberName] string name = null) => name;
    }
}
