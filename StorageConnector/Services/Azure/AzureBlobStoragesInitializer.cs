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
		public AzureBlobStoragesInitializer(IConfiguration configuration)
        {
            var azureConfig = configuration.GetSection($"{ConstantStrings.StorageConnectorsConfigName}:Azure");
			AzureBlobStorageSettings = new AzureBlobStorageSettings
			{
				CountryIsoCodeMapToAccountName = (azureConfig.GetSection(ConstantStrings.CountryIsoCodeMapToAccountNameConfigName).Get<Dictionary<string, string>>()).ParseCountryIsoCodeMap(),
				Accounts = azureConfig.GetRequiredSection("Accounts").Get<List<AzureAccount>>()
			};
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
