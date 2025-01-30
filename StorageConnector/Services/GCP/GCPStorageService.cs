using EarthCountriesInfo;
using Microsoft.Extensions.Logging;
using StorageConnector.Common;
using StorageConnector.Common.DTOs;
using System.Security.Cryptography;
using System.Text;

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

		public Task<string> GenerateDirectUploadURL(CountryIsoCode countryOfResidenceIsoCode, CloudFileName fileReferenceWithPath, int expiryInMinutes = 60)
		{

			if (_gcpStoragesInitializer.GCPStorageSettings.Accounts.Any())
			{
				var blobName = fileReferenceWithPath.ToString();
				var gcpStorageAccount = _gcpStoragesInitializer.GCPStorageSettings.Accounts.FirstOrDefault(); //todo

				var storageUri = $"https://storage.googleapis.com/{gcpStorageAccount.BucketName}/{blobName}";

				var expiration = DateTimeOffset.UtcNow.AddMinutes(expiryInMinutes).ToUnixTimeSeconds();
				var signatureString = $"PUT\n\n\n{expiration}\n/{gcpStorageAccount.BucketName}/{blobName}";

				using var rsa = new RSACryptoServiceProvider();
				rsa.ImportFromPem(gcpStorageAccount.PrivateKey);

				var signatureBytes = rsa.SignData(Encoding.UTF8.GetBytes(signatureString), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
				var signature = Convert.ToBase64String(signatureBytes);

				var signedUrl = $"{storageUri}?GoogleAccessId={gcpStorageAccount.ServiceAccountEmail}&Expires={expiration}&Signature={Uri.EscapeDataString(signature)}";

				return Task.FromResult(signedUrl);
			}

			_logger.LogError($"No GCP account found for country ISO code: {countryOfResidenceIsoCode}");
			throw new InvalidOperationException($"No GCP account found for country ISO code: {countryOfResidenceIsoCode}");
		}
	}
}
