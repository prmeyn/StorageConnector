using EarthCountriesInfo;
using Google.Cloud.Iam.Credentials.V1;
using Microsoft.Extensions.Logging;
using StorageConnector.Common;
using StorageConnector.Common.DTOs;

namespace StorageConnector.Services.GCP
{
	public sealed class GCPStorageService : IStorageProvider
	{
		private readonly GCPStoragesInitializer _gcpStoragesInitializer;
		private readonly ILogger<GCPStorageService> _logger;

		public GCPStorageService(GCPStoragesInitializer gcpStoragesInitializer, ILogger<GCPStorageService> logger)
		{
			_gcpStoragesInitializer = gcpStoragesInitializer;
			_logger = logger;
		}

		// Requires both accounts and a usable signing client, not just configuration rows (finding C6).
		public bool HasAccounts =>
			_gcpStoragesInitializer.GCPStorageSettings?.Accounts.Count > 0
			&& _gcpStoragesInitializer.IamCredentialsClient is not null;

		public Task<DownloadInfo> GenerateDirectDownloadInfo(
			CountryIsoCode countryOfResidenceIsoCode,
			CloudFileName fileReferenceWithPath,
			int expiryInMinutes = IStorageProvider.DefaultExpiryInMinutes,
			CancellationToken cancellationToken = default)
		{
			throw new NotSupportedException(
				"Direct download URLs are not implemented for Google Cloud Storage. Configure Azure or AWS, " +
				"or track https://github.com/prmeyn/StorageConnector/issues for support.");
		}

		public async Task<UploadInfo> GenerateDirectUploadInfo(
			CountryIsoCode countryOfResidenceIsoCode,
			CloudFileName fileReferenceWithPath,
			string contentType,
			int expiryInMinutes = IStorageProvider.DefaultExpiryInMinutes,
			CancellationToken cancellationToken = default)
		{
			if (!HasAccounts)
			{
				_logger.LogError("No valid GCP accounts found");
				throw new InvalidOperationException("No valid GCP accounts found");
			}

			// HasAccounts guarantees both of these are non-null.
			var settings = _gcpStoragesInitializer.GCPStorageSettings!;
			IAMCredentialsClient iamCredentialsClient = _gcpStoragesInitializer.IamCredentialsClient!;

			var blobName = fileReferenceWithPath.Value;

			// Previously FirstOrDefault(...) could return null for an unmapped country and the next line
			// dereferenced it. Selection now falls back consistently (finding H10).
			var gcpStorageAccount = AccountSelector.Select(
				settings.CountryIsoCodeMapToAccountName,
				settings.Accounts,
				account => account.BucketName,
				countryOfResidenceIsoCode)!;

			var signedUrl = await GcpV4Signer.CreateSignedUrlAsync(
				GcpV4Signer.IamSigner(iamCredentialsClient, gcpStorageAccount.ServiceAccountEmail),
				gcpStorageAccount.ServiceAccountEmail,
				gcpStorageAccount.BucketName,
				blobName,
				httpVerb: "PUT",
				contentType,
				expiry: TimeSpan.FromMinutes(expiryInMinutes),
				utcNow: DateTimeOffset.UtcNow,
				cancellationToken).ConfigureAwait(false);

			return new UploadInfo()
			{
				FileName = fileReferenceWithPath,
				DirectUploadUrl = signedUrl,
				Headers = new Dictionary<string, string> { { "Content-Type", contentType } },
				HttpMethod = "PUT"
			};
		}

	}
}
