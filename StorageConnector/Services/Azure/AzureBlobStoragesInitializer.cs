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
					FaceClient = new FaceClient(new Uri(azureVisionAccountSettings.Endpoint), new AzureKeyCredential(azureVisionAccountSettings.ApiKey));
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
