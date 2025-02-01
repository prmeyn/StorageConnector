using EarthCountriesInfo;
using Microsoft.AspNetCore.StaticFiles;
using StorageConnector.Common;
using StorageConnector.Common.DTOs;
using StorageConnector.Services.Azure;
using StorageConnector.Services.GCP;

namespace StorageConnector
{
	public sealed class StorageConnectorService : IStorageProvidor
	{
		private readonly AzureBlobStorageService _azureBlobStorageService;
		private readonly GCPStorageService _GCPStorageService;

		public StorageConnectorService(
			AzureBlobStorageService azureBlobStorageService,
			GCPStorageService gCPStorageService
			)
		{
			_azureBlobStorageService = azureBlobStorageService;
			_GCPStorageService = gCPStorageService;
		}

		public Task<UploadInfo> GenerateDirectUploadInfo(CountryIsoCode countryOfResidenceIsoCode, CloudFileName fileReferenceWithPath, string contentType, int expiryInMinutes = 1)
		{
			var extension = GetExtensionFromContentType(contentType);
			if (!string.IsNullOrWhiteSpace(extension))
			{
				fileReferenceWithPath = fileReferenceWithPath.ToString().EndsWith(extension) ? fileReferenceWithPath : new CloudFileName($"{fileReferenceWithPath}{extension}");


				//return _GCPStorageService.GenerateDirectUploadInfo(countryOfResidenceIsoCode, fileReferenceWithPath, contentType, expiryInMinutes);
				return _azureBlobStorageService.GenerateDirectUploadInfo(countryOfResidenceIsoCode, fileReferenceWithPath, contentType, expiryInMinutes);
			}
			return null;
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
