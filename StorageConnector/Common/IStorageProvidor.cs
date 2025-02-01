using EarthCountriesInfo;
using StorageConnector.Common.DTOs;

namespace StorageConnector.Common
{
	public interface IStorageProvidor
	{
		Task<string> GenerateDirectUploadURL(CountryIsoCode countryOfResidenceIsoCode, CloudFileName fileReferenceWithPath, string contentType, int expiryInMinutes = 60);
	}
}
