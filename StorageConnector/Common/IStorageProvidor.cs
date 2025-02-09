﻿using EarthCountriesInfo;
using StorageConnector.Common.DTOs;

namespace StorageConnector.Common
{
	public interface IStorageProvidor
	{
		Task<UploadInfo> GenerateDirectUploadInfo(CountryIsoCode countryOfResidenceIsoCode, CloudFileName fileReferenceWithPath, string contentType, int expiryInMinutes = 60);

		Task<bool> HasAccounts();

		Task<byte?> GetNumberOfFacesOnImage(CountryIsoCode regionCountryIsoCode, CloudFileName fileNameWithExtension);

		Task<HashSet<string>> GetMatchingFacesUserDataHashSet(string faceListName, CountryIsoCode regionCountryIsoCode, CloudFileName fileNameWithExtension, string userData);
	}
}
