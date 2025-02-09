using Azure.AI.Vision.Face;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using EarthCountriesInfo;
using Microsoft.Extensions.Logging;
using StorageConnector.Common;
using StorageConnector.Common.DTOs;
using System.Drawing;

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

		public async Task<UploadInfo> GenerateDirectUploadInfo(CountryIsoCode countryOfResidenceIsoCode, CloudFileName fileReferenceWithPath, string contentType, int expiryInMinutes = 60)
		{
			if (await HasAccounts())
			{
				AzureAccount? azureAccount = selectAzureAccount(countryOfResidenceIsoCode);
				

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

					return new UploadInfo() { 
						DirectUploadUrl = sasUri.ToString(),
						Headers = new Dictionary<string, string> { { "Content-Type", contentType }, { "x-ms-blob-type", "BlockBlob" } },
						HttpMethod = "PUT"
					};
				}
			}

			_logger.LogError("No Azure account found");
			throw new InvalidOperationException("No Azure account found");
		}

		private AzureAccount? selectAzureAccount(CountryIsoCode regionCountryIsoCode)
		{
			AzureAccount? azureAccount = null;
			if (_azureBlobStoragesInitializer.AzureBlobStorageSettings.CountryIsoCodeMapToAccountName.TryGetValue(regionCountryIsoCode, out string? accountName))
			{
				azureAccount = _azureBlobStoragesInitializer.AzureBlobStorageSettings.Accounts.Find(a => a.AccountName == accountName);
			}

			if (azureAccount == null)
			{
				return _azureBlobStoragesInitializer.AzureBlobStorageSettings.Accounts.FirstOrDefault();
			}
			return azureAccount;
		}

		public async Task<HashSet<string>> GetMatchingFacesUserDataHashSet(string faceListName, CountryIsoCode regionCountryIsoCode, CloudFileName fileNameWithExtension, string userData)
		{
			return [];
		}

		public async Task<byte?> GetNumberOfFacesOnImage(CountryIsoCode regionCountryIsoCode, CloudFileName fileNameWithExtension)
		{
			if (await HasAccounts())
			{
				AzureAccount? azureAccount = selectAzureAccount(regionCountryIsoCode);
				if (azureAccount == null)
				{
					_logger.LogError("No Azure account found");
					throw new InvalidOperationException("No Azure account found");
				}
				if (!string.IsNullOrWhiteSpace(azureAccount?.AccountName) && _azureBlobStoragesInitializer.AccountNamesMappedToBlobServiceClient.TryGetValue(azureAccount.AccountName, out var blobServiceClient))
				{
					try
					{
						var blobName = fileNameWithExtension.ToString();
						// Create a BlobClient for the specific blob
						BlobClient blobClient = blobServiceClient
							.GetBlobContainerClient(azureAccount.ContainerName)
							.GetBlobClient(blobName);

						// Step 2: Download the blob content
						BlobDownloadInfo blobDownload = await blobClient.DownloadAsync();

						// Step 3: Convert the content to BinaryData
						using var memoryStream = new MemoryStream();
						await blobDownload.Content.CopyToAsync(memoryStream);
						using var image = Image.FromStream(memoryStream);

						if (image.Width < 36 || image.Height < 36)
						{
							_logger.LogInformation("Image too small: {Width} X {Height}", image.Width, image.Height);
							// Resize the image to a minimum acceptable size, e.g., 200x200
							var resizedImage = new Bitmap(image, new Size(200, 200));
							using (var resizedStream = new MemoryStream())
							{
								resizedImage.Save(resizedStream, image.RawFormat);
								BinaryData imageContent = BinaryData.FromStream(resizedStream);

								// Now call the Face API with the resized image
								var result = await _azureBlobStoragesInitializer.FaceClient.DetectAsync(
									imageContent,
									detectionModel: FaceDetectionModel.Detection03,
									recognitionModel: FaceRecognitionModel.Recognition04,
									returnFaceId: false
								);
								return (byte)result.Value.Count;
							}
						}
						else
						{
							// If the image is large enough, just send it
							BinaryData imageContent = BinaryData.FromStream(memoryStream);
							var result = await _azureBlobStoragesInitializer.FaceClient.DetectAsync(
								imageContent,
								detectionModel: FaceDetectionModel.Detection03,
								recognitionModel: FaceRecognitionModel.Recognition04,
								returnFaceId: false
							);
							return (byte)result.Value.Count;
						}
					}
					catch (Exception ex)
					{
						_logger.LogError(ex, "Error while detecting faces in {FileNameWithExtension} ERROR: {errorMessage}", fileNameWithExtension, ex.Message);
					}
				}
			}
			return 0;
		}

		public async Task<bool> HasAccounts() => _azureBlobStoragesInitializer?.AzureBlobStorageSettings?.Accounts?.Any() ?? false;
	}
}
