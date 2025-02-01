﻿using EarthCountriesInfo;
using Google.Cloud.Iam.Credentials.V1;
using Microsoft.Extensions.Logging;
using StorageConnector.Common;
using StorageConnector.Common.DTOs;

namespace StorageConnector.Services.GCP
{
	public sealed class GCPStorageService : IStorageProvidor
	{
		private readonly GCPStoragesInitializer _gcpStoragesInitializer;
		private readonly ILogger<GCPStorageService> _logger;

		public GCPStorageService(GCPStoragesInitializer gcpStoragesInitializer, ILogger<GCPStorageService> logger)
		{
			_gcpStoragesInitializer = gcpStoragesInitializer;
			_logger = logger;
		}

		public async Task<UploadInfo> GenerateDirectUploadInfo(CountryIsoCode countryOfResidenceIsoCode, CloudFileName fileReferenceWithPath, string contentType, int expiryInMinutes = 60)
		{
			IAMCredentialsClient iamCredentialsClient = _gcpStoragesInitializer._iamCredentialsClient;

			if (_gcpStoragesInitializer.GCPStorageSettings.Accounts.Any())
			{
				var blobName = fileReferenceWithPath.ToString();
				var gcpStorageAccount = _gcpStoragesInitializer.GCPStorageSettings.Accounts.FirstOrDefault(); //todo

				var storageUri = $"https://storage.googleapis.com/{gcpStorageAccount.BucketName}/{blobName}";
				var expiration = DateTimeOffset.UtcNow.AddMinutes(expiryInMinutes).ToUnixTimeSeconds();
				var stringToSign = $"PUT\n\n{contentType}\n{expiration}\n/{gcpStorageAccount.BucketName}/{blobName}";

				// ✅ Sign the string using IAMCredentialsClient instead of PrivateKey
				var signBlobResponse = await iamCredentialsClient.SignBlobAsync(new SignBlobRequest
				{
					Name = $"projects/-/serviceAccounts/{gcpStorageAccount.ServiceAccountEmail}",
					Payload = Google.Protobuf.ByteString.CopyFromUtf8(stringToSign)
				});

				var signature = Convert.ToBase64String(signBlobResponse.SignedBlob.ToByteArray());

				var signedUrl = $"{storageUri}?GoogleAccessId={gcpStorageAccount.ServiceAccountEmail}&Expires={expiration}&Signature={Uri.EscapeDataString(signature)}";

				return new UploadInfo()
				{
					DirectUploadUrl = signedUrl,
					Headers = new Dictionary<string, string> { { "Content-Type", contentType } },
					HttpMethod = "PUT"
				};
			}

			_logger.LogError($"No GCP account found for country ISO code: {countryOfResidenceIsoCode}");
			throw new InvalidOperationException($"No GCP account found for country ISO code: {countryOfResidenceIsoCode}");
		}
	}
}
