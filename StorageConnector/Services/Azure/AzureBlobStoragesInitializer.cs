using Azure;
using Azure.AI.Vision.Face;
using Azure.Storage;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;
using StorageConnector.Common;


namespace StorageConnector.Services.Azure
{
	public sealed class AzureBlobStoragesInitializer
    {
		internal readonly AzureBlobStorageSettings AzureBlobStorageSettings;
		internal readonly Dictionary<string, BlobServiceClient> AccountNamesMappedToBlobServiceClient = [];
		internal readonly FaceClient FaceClient;
		internal readonly FaceAdministrationClient FaceAdministrationClient;

		public AzureBlobStoragesInitializer(IConfiguration configuration)
        {
            var azureConfig = configuration.GetSection($"{ConstantStrings.StorageConnectorsConfigName}:Azure");
			if (azureConfig.Exists())
			{
				AzureBlobStorageSettings = new AzureBlobStorageSettings
				{
					CountryIsoCodeMapToAccountName = (azureConfig.GetSection(ConstantStrings.CountryIsoCodeMapToAccountNameConfigName).Get<Dictionary<string, string>>()).ParseCountryIsoCodeMap(),
					Accounts = azureConfig.GetRequiredSection(ConstantStrings.AccountsConfigName).Get<List<AzureAccount>>()
				};
				var azureVisionAccountSettings = azureConfig.GetSection("VisionAccount").Get<AzureVisionAccountSettings>();

				if (!string.IsNullOrWhiteSpace(azureVisionAccountSettings.ApiKey) && !string.IsNullOrWhiteSpace(azureVisionAccountSettings.Endpoint))
				{
					//FaceHttpClientEndpoint = azureVisionAccountSettings.Endpoint;
					var faceEndpoint = new Uri(azureVisionAccountSettings.Endpoint);
					var credentials = new AzureKeyCredential(azureVisionAccountSettings.ApiKey);
					FaceClient = new FaceClient(faceEndpoint, credentials);
					FaceAdministrationClient = new FaceAdministrationClient(faceEndpoint, credentials);
					//FaceHttpClient = httpClient;
					//FaceHttpClient.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", azureVisionAccountSettings.ApiKey);
				}

				foreach (var account in AzureBlobStorageSettings.Accounts)
				{
					AccountNamesMappedToBlobServiceClient[account.AccountName] = new BlobServiceClient(
						new Uri($"https://{account.AccountName}.blob.core.windows.net"),
						new StorageSharedKeyCredential(account.AccountName, account.AccountKey)
					);
				}
			}
		}
	}
}
