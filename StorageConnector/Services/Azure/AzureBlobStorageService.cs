using Azure.Storage.Blobs;
using Azure.Storage.Sas;
using EarthCountriesInfo;
using Microsoft.Extensions.Logging;
using StorageConnector.Common;
using StorageConnector.Common.DTOs;

namespace StorageConnector.Services.Azure
{
	public sealed class AzureBlobStorageService : IStorageProvidor
	{
		private readonly AzureBlobStoragesInitializer _azureBlobStoragesInitializer;
		private readonly ILogger<AzureBlobStorageService> _logger;

		public AzureBlobStorageService(AzureBlobStoragesInitializer azureBlobStoragesInitializer, ILogger<AzureBlobStorageService> logger)
		{
			_azureBlobStoragesInitializer = azureBlobStoragesInitializer;
			_logger = logger;
		}

		public Task<UploadInfo> GenerateDirectUploadInfo(CountryIsoCode countryOfResidenceIsoCode, CloudFileName fileReferenceWithPath, string contentType, int expiryInMinutes = 60)
		{
			if (_azureBlobStoragesInitializer.AzureBlobStorageSettings.Accounts.Any())
			{
				AzureAccount? azureAccount = null;
				if (_azureBlobStoragesInitializer.AzureBlobStorageSettings.CountryIsoCodeMapToAccountName.TryGetValue(countryOfResidenceIsoCode, out string? accountName))
				{
					azureAccount = _azureBlobStoragesInitializer.AzureBlobStorageSettings.Accounts.Find(a => a.AccountName == accountName);
				}

				if (azureAccount == null)
				{
					azureAccount = _azureBlobStoragesInitializer.AzureBlobStorageSettings.Accounts.FirstOrDefault();
				}

				//AccountNamesMappedToBlobServiceClient

				if (!string.IsNullOrWhiteSpace(azureAccount?.AccountName) && _azureBlobStoragesInitializer.AccountNamesMappedToBlobServiceClient.TryGetValue(azureAccount.AccountName, out var blobServiceClient))
				{
					var blobName = fileReferenceWithPath.ToString();
					// Create a BlobClient for the specific blob
					BlobClient blobClient = blobServiceClient
						.GetBlobContainerClient(azureAccount.ContainerName)
						.GetBlobClient(blobName);

					var startsOn = DateTimeOffset.UtcNow;

					// Generate SAS Token valid for "expiryInMinutes" minutes
					BlobSasBuilder sasBuilder = new()
					{
						BlobContainerName = azureAccount.ContainerName,
						BlobName = blobName,
						Resource = "b", // b = blob
						StartsOn = startsOn,
						ExpiresOn = startsOn.AddMinutes(expiryInMinutes),
					};

					sasBuilder.SetPermissions(BlobSasPermissions.Write | BlobSasPermissions.Create);

					Uri sasUri = blobClient.GenerateSasUri(sasBuilder);

					return Task.FromResult(new UploadInfo() { 
						DirectUploadUrl = sasUri.ToString(),
						Headers = new Dictionary<string, string> { { "Content-Type", contentType }, { "x-ms-blob-type", "BlockBlob" } },
						HttpMethod = "PUT"
					});
				}
			}

			_logger.LogError($"No Azure account found for country ISO code: {countryOfResidenceIsoCode}");
			throw new InvalidOperationException($"No Azure account found for country ISO code: {countryOfResidenceIsoCode}");
		}
	}
}
