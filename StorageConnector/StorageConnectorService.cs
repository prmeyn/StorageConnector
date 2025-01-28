using EarthCountriesInfo;
using StorageConnector.Common;
using StorageConnector.Common.DTOs;

namespace StorageConnector
{
	public sealed class StorageConnectorService : IStorageProvidor
	{
		public Task<string> GenerateDirectUploadURL(CountryIsoCode countryOfResidenceIsoCode, CloudFileName fileReferenceWithPath)
		{
			throw new NotImplementedException();
		}
	}
}
