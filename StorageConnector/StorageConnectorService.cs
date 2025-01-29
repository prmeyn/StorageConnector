using EarthCountriesInfo;
using StorageConnector.Common;
using StorageConnector.Common.DTOs;
using StorageConnector.Services.Azure;

namespace StorageConnector
{
	public sealed class StorageConnectorService : IStorageProvidor
	{
		private readonly AzureBlobStorageService _azureBlobStorageService;

		public StorageConnectorService(AzureBlobStorageService azureBlobStorageService)
		{
			_azureBlobStorageService = azureBlobStorageService;
		}

		public Task<string> GenerateDirectUploadURL(CountryIsoCode countryOfResidenceIsoCode, CloudFileName fileReferenceWithPath, int expiryInMinutes = 1)
		{
			return _azureBlobStorageService.GenerateDirectUploadURL(countryOfResidenceIsoCode, fileReferenceWithPath, expiryInMinutes);
		}
	}
}
