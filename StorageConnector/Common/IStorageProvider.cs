using EarthCountriesInfo;
using StorageConnector.Common.DTOs;

namespace StorageConnector.Common
{
	public interface IStorageProvider
	{
		Task<UploadInfo> GenerateDirectUploadInfo(CountryIsoCode countryOfResidenceIsoCode, CloudFileName fileReferenceWithPath, string contentType, int expiryInMinutes = 60);
		Task<bool> HasAccounts();
		Task<FaceInfo> GetFaceInfo(string faceListName, CountryIsoCode regionCountryIsoCode, CloudFileName fileNameWithExtension, string userData);
	}
}
