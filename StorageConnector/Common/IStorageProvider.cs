using EarthCountriesInfo;
using StorageConnector.Common.DTOs;

namespace StorageConnector.Common
{
	public interface IStorageProvider
	{
		/// <summary>
		/// Default lifetime of a generated pre-signed URL.
		///
		/// Declared once and referenced by every implementation. C# binds optional-argument defaults at
		/// the call site, so while the interface said 60 and StorageConnectorService said 1, the expiry
		/// a caller got depended silently on whether they held the interface or the concrete type
		/// (finding H1). Referencing this constant everywhere stops the two drifting apart again.
		/// </summary>
		public const int DefaultExpiryInMinutes = 60;

		/// <summary>
		/// Whether this provider has at least one usable account.
		///
		/// A synchronous property rather than <c>Task&lt;bool&gt;</c>: it only reads an in-memory
		/// dictionary populated at start-up, so the previous <c>async</c>-without-<c>await</c> signature
		/// allocated a state machine per call while the aggregate service invoked it up to six times per
		/// request (finding M1).
		/// </summary>
		bool HasAccounts { get; }

		Task<UploadInfo> GenerateDirectUploadInfo(
			CountryIsoCode countryOfResidenceIsoCode,
			CloudFileName fileReferenceWithPath,
			string contentType,
			int expiryInMinutes = DefaultExpiryInMinutes,
			CancellationToken cancellationToken = default);

		Task<DownloadInfo> GenerateDirectDownloadInfo(
			CountryIsoCode countryOfResidenceIsoCode,
			CloudFileName fileReferenceWithPath,
			int expiryInMinutes = DefaultExpiryInMinutes,
			CancellationToken cancellationToken = default);

		// Face recognition moved to IFaceRecognitionProvider. It had no business on a storage interface,
		// and bundling it here is what let a read-shaped call quietly store biometric data (finding H5).
	}
}
