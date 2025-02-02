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
	public sealed class StorageConnectorService : IStorageProvidor
	{
		private readonly AzureBlobStorageService _azureBlobStorageService;
		private readonly AmazonS3BucketService _amazonS3BucketService;
		private readonly GCPStorageService _GCPStorageService;
		private readonly ILogger<StorageConnectorService> _logger;

		public StorageConnectorService(
			AzureBlobStorageService azureBlobStorageService,
			AmazonS3BucketService awsS3BucketService,
			GCPStorageService gCPStorageService
			)
		{
			_azureBlobStorageService = azureBlobStorageService;
			_amazonS3BucketService = awsS3BucketService;
			_GCPStorageService = gCPStorageService;
		}

		public Task<UploadInfo> GenerateDirectUploadInfo(CountryIsoCode countryOfResidenceIsoCode, CloudFileName fileReferenceWithPath, string contentType, int expiryInMinutes = 1)
		{
			var extension = GetExtensionFromContentType(contentType);
			if (!string.IsNullOrWhiteSpace(extension))
			{
				fileReferenceWithPath = fileReferenceWithPath.ToString().EndsWith(extension) ? fileReferenceWithPath : new CloudFileName($"{fileReferenceWithPath}{extension}");


				//return _GCPStorageService.GenerateDirectUploadInfo(countryOfResidenceIsoCode, fileReferenceWithPath, contentType, expiryInMinutes);
				//return _azureBlobStorageService.GenerateDirectUploadInfo(countryOfResidenceIsoCode, fileReferenceWithPath, contentType, expiryInMinutes);
				return _amazonS3BucketService.GenerateDirectUploadInfo(countryOfResidenceIsoCode, fileReferenceWithPath, contentType, expiryInMinutes);
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
	}
}
