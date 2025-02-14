using EarthCountriesInfo;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Logging;
using StorageConnector.Common;
using StorageConnector.Common.DTOs;
using StorageConnector.Services.AWS;
using StorageConnector.Services.Azure;
using StorageConnector.Services.GCP;

namespace StorageConnector
{
	public sealed class StorageConnectorService : IStorageProvider
	{
		private readonly AzureBlobStorageService _azureBlobStorageService;
		private readonly AmazonS3BucketService _amazonS3BucketService;
		private readonly GCPStorageService _GCPStorageService;
		private readonly ILogger<StorageConnectorService> _logger;

		public StorageConnectorService(
			AzureBlobStorageService azureBlobStorageService,
			AmazonS3BucketService awsS3BucketService,
			GCPStorageService gCPStorageService,
			ILogger<StorageConnectorService> logger
			)
		{
			_azureBlobStorageService = azureBlobStorageService;
			_amazonS3BucketService = awsS3BucketService;
			_GCPStorageService = gCPStorageService;

			_logger = logger;
		}

		public async Task<UploadInfo> GenerateDirectUploadInfo(CountryIsoCode countryOfResidenceIsoCode, CloudFileName fileReferenceWithPath, string contentType, int expiryInMinutes = 1)
		{
			var extension = GetExtensionFromContentType(contentType);
			if (!string.IsNullOrWhiteSpace(extension))
			{
				fileReferenceWithPath = fileReferenceWithPath.ToString().EndsWith(extension) ? fileReferenceWithPath : new CloudFileName($"{fileReferenceWithPath}{extension}");

				if (await HasAccounts())
				{
					if (await _azureBlobStorageService.HasAccounts())
					{
						return await _azureBlobStorageService.GenerateDirectUploadInfo(countryOfResidenceIsoCode, fileReferenceWithPath, contentType, expiryInMinutes);
					}
					if (await _amazonS3BucketService.HasAccounts())
					{
						return await _amazonS3BucketService.GenerateDirectUploadInfo(countryOfResidenceIsoCode, fileReferenceWithPath, contentType, expiryInMinutes);
					}
					if (await _GCPStorageService.HasAccounts())
					{
						return await _GCPStorageService.GenerateDirectUploadInfo(countryOfResidenceIsoCode, fileReferenceWithPath, contentType, expiryInMinutes);
					}
				}
				_logger.LogError("StorageConnectorService has no accounts");
				throw new InvalidOperationException("StorageConnectorService has no accounts");
			}
			_logger.LogError($"Unknown Content Type: {contentType}");
			throw new InvalidOperationException($"Unknown Content Type: {contentType}");

		}

		public static string? GetExtensionFromContentType(string contentType)
		{
			var provider = new FileExtensionContentTypeProvider();
			foreach (var mapping in provider.Mappings)
			{
				if (mapping.Value.Equals(contentType, StringComparison.OrdinalIgnoreCase))
				{
					return mapping.Key; // Returns something like ".jpg"
				}
			}
			return null; // Return null if no match is found
		}

		public async Task<bool> HasAccounts()
		{
			return await _azureBlobStorageService.HasAccounts() || await _amazonS3BucketService.HasAccounts() || await _GCPStorageService.HasAccounts();
		}
	}
}
