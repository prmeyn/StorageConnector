using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Google.Cloud.Iam.Credentials.V1;

namespace StorageConnector.Services.GCP
{
	/// <summary>
	/// Builds Google Cloud Storage V4 signed URLs.
	///
	/// This replaces the hand-rolled V2 scheme (<c>GoogleAccessId</c>/<c>Expires</c>/<c>Signature</c>),
	/// which Google has superseded (finding H8). V4 also signs the request headers, so unlike the old
	/// scheme the <c>Content-Type</c> the client sends is actually enforced rather than merely
	/// suggested (finding M11).
	///
	/// Signing goes through the IAM <c>SignBlob</c> API, so no private key is ever held by the
	/// application -- only the service account's identity.
	/// </summary>
	internal static class GcpV4Signer
	{
		private const string Algorithm = "GOOG4-RSA-SHA256";
		private const string Host = "storage.googleapis.com";

		/// <summary>V4 signed URLs may not outlive seven days.</summary>
		internal static readonly TimeSpan MaximumExpiry = TimeSpan.FromDays(7);

		/// <summary>
		/// Signed URLs are valid from <c>X-Goog-Date</c>, so that timestamp is backdated slightly.
		/// Without it, a client whose clock is a little behind the signing host is refused (finding M10).
		/// </summary>
		internal static readonly TimeSpan ClockSkewAllowance = TimeSpan.FromMinutes(5);

		/// <summary>
		/// Signs the V4 string-to-sign, returning the raw signature bytes.
		/// </summary>
		internal delegate Task<byte[]> SignAsync(string stringToSign, CancellationToken cancellationToken);

		/// <summary>
		/// Signs via the IAM <c>SignBlob</c> API, so the application never holds a private key.
		/// </summary>
		internal static SignAsync IamSigner(IAMCredentialsClient client, string serviceAccountEmail) =>
			async (stringToSign, cancellationToken) =>
			{
				var response = await client.SignBlobAsync(new SignBlobRequest
				{
					Name = $"projects/-/serviceAccounts/{serviceAccountEmail}",
					Payload = Google.Protobuf.ByteString.CopyFromUtf8(stringToSign)
				}, cancellationToken).ConfigureAwait(false);

				return response.SignedBlob.ToByteArray();
			};

		internal static async Task<string> CreateSignedUrlAsync(
			SignAsync signAsync,
			string serviceAccountEmail,
			string bucketName,
			string objectName,
			string httpVerb,
			string contentType,
			TimeSpan expiry,
			DateTimeOffset utcNow,
			CancellationToken cancellationToken)
		{
			// The signed lifetime starts at the backdated timestamp, so the skew allowance counts towards
			// Google's seven-day ceiling. Validating `expiry` alone would let exactly seven days through
			// and then sign 7 days + 5 minutes, which Google rejects when the URL is used.
			var signedLifetime = expiry + ClockSkewAllowance;

			if (expiry <= TimeSpan.Zero || signedLifetime > MaximumExpiry)
			{
				throw new ArgumentOutOfRangeException(
					nameof(expiry),
					$"Google Cloud Storage signed URLs must expire within {MaximumExpiry.TotalDays} days, including " +
					$"the {ClockSkewAllowance.TotalMinutes}-minute clock-skew allowance; got {expiry}.");
			}

			var issuedAt = utcNow - ClockSkewAllowance;
			var requestTimestamp = issuedAt.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);
			var requestDate = issuedAt.ToString("yyyyMMdd", CultureInfo.InvariantCulture);

			var credentialScope = $"{requestDate}/auto/storage/goog4_request";
			var totalSeconds = (long)signedLifetime.TotalSeconds;

			// Headers included in the signature. Signing content-type is what makes it enforceable.
			var canonicalHeaders = $"content-type:{contentType}\nhost:{Host}\n";
			const string signedHeaders = "content-type;host";

			var canonicalQueryString = string.Join('&',
			[
				$"X-Goog-Algorithm={Encode(Algorithm)}",
				$"X-Goog-Credential={Encode($"{serviceAccountEmail}/{credentialScope}")}",
				$"X-Goog-Date={Encode(requestTimestamp)}",
				$"X-Goog-Expires={totalSeconds.ToString(CultureInfo.InvariantCulture)}",
				$"X-Goog-SignedHeaders={Encode(signedHeaders)}",
			]);

			var canonicalResource = $"/{bucketName}/{EncodeObjectName(objectName)}";

			var canonicalRequest = string.Join('\n',
			[
				httpVerb,
				canonicalResource,
				canonicalQueryString,
				canonicalHeaders,
				signedHeaders,
				"UNSIGNED-PAYLOAD",
			]);

			var stringToSign = string.Join('\n',
			[
				Algorithm,
				requestTimestamp,
				credentialScope,
				ToHex(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalRequest))),
			]);

			var signature = ToHex(await signAsync(stringToSign, cancellationToken).ConfigureAwait(false));

			return $"https://{Host}{canonicalResource}?{canonicalQueryString}&X-Goog-Signature={signature}";
		}

		/// <summary>
		/// Percent-encodes an object key while leaving path separators intact, since they are part of
		/// the resource path rather than data.
		/// </summary>
		private static string EncodeObjectName(string objectName) =>
			string.Join('/', objectName.Split('/').Select(Encode));

		private static string Encode(string value) => Uri.EscapeDataString(value);

		private static string ToHex(byte[] bytes) => Convert.ToHexStringLower(bytes);
	}
}
