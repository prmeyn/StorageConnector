using EarthCountriesInfo;
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

		public Task<string> GenerateDirectUploadURL(CountryIsoCode countryOfResidenceIsoCode, CloudFileName fileReferenceWithPath, int expiryInMinutes = 1)
		{
			return _GCPStorageService.GenerateDirectUploadURL(countryOfResidenceIsoCode, fileReferenceWithPath, expiryInMinutes);
			//return _azureBlobStorageService.GenerateDirectUploadURL(countryOfResidenceIsoCode, fileReferenceWithPath, expiryInMinutes);
		}
	}
}
