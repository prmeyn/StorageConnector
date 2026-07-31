using System.Globalization;
using System.Text;
using System.Web;
using StorageConnector.Services.GCP;

namespace StorageConnector.Tests;

/// <summary>
/// Tests for Google Cloud Storage V4 URL signing (finding H8).
///
/// The previous implementation hand-rolled Google's deprecated V2 scheme
/// (<c>GoogleAccessId</c>/<c>Expires</c>/<c>Signature</c>). V4 additionally signs the request headers,
/// which is what makes the advertised Content-Type binding rather than advisory (finding M11).
///
/// The signing step is injected, so these verify the URL structure without a Google account.
/// </summary>
public class GcpV4SignerTests
{
	private static readonly DateTimeOffset FixedNow = new(2026, 7, 31, 12, 0, 0, TimeSpan.Zero);
	private const string ServiceAccount = "uploader@my-project.iam.gserviceaccount.com";

	private static Task<string> Sign(
		string objectName = "uploads/photo.jpg",
		string contentType = "image/jpeg",
		TimeSpan? expiry = null,
		Action<string>? captureStringToSign = null)
	{
		return GcpV4Signer.CreateSignedUrlAsync(
			(stringToSign, _) =>
			{
				captureStringToSign?.Invoke(stringToSign);
				return Task.FromResult(new byte[] { 0xDE, 0xAD, 0xBE, 0xEF });
			},
			ServiceAccount,
			"my-bucket",
			objectName,
			httpVerb: "PUT",
			contentType,
			expiry ?? TimeSpan.FromMinutes(15),
			FixedNow,
			CancellationToken.None);
	}

	private static Dictionary<string, string> QueryOf(string url)
	{
		var query = HttpUtility.ParseQueryString(new Uri(url).Query);
		return query.AllKeys
			.Where(k => k is not null)
			.ToDictionary(k => k!, k => query[k] ?? string.Empty, StringComparer.Ordinal);
	}

	[Fact]
	public async Task UsesTheV4Scheme_NotTheDeprecatedV2Parameters()
	{
		var query = QueryOf(await Sign());

		Assert.Equal("GOOG4-RSA-SHA256", query["X-Goog-Algorithm"]);
		Assert.Contains("X-Goog-Credential", query.Keys);
		Assert.Contains("X-Goog-Signature", query.Keys);

		// V2 parameters must be gone entirely.
		Assert.DoesNotContain("GoogleAccessId", query.Keys);
		Assert.DoesNotContain("Expires", query.Keys);
		Assert.DoesNotContain("Signature", query.Keys);
	}

	/// <summary>
	/// M11: content-type is among the signed headers, so a client cannot substitute a different type.
	/// </summary>
	[Fact]
	public async Task SignsTheContentTypeHeader()
	{
		string? stringToSign = null;
		var url = await Sign(contentType: "image/png", captureStringToSign: s => stringToSign = s);

		Assert.Equal("content-type;host", QueryOf(url)["X-Goog-SignedHeaders"]);
		Assert.NotNull(stringToSign);
	}

	/// <summary>
	/// M10: the URL is valid from X-Goog-Date, so that timestamp is backdated and the lifetime extended
	/// to match. A client whose clock trails the signing host is otherwise refused.
	/// </summary>
	[Fact]
	public async Task BackdatesTheTimestamp_ToTolerateClockSkew()
	{
		var query = QueryOf(await Sign(expiry: TimeSpan.FromMinutes(15)));

		var expectedStart = (FixedNow - GcpV4Signer.ClockSkewAllowance)
			.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);

		Assert.Equal(expectedStart, query["X-Goog-Date"]);

		// The requested 15 minutes still remain after the backdating is accounted for.
		var seconds = int.Parse(query["X-Goog-Expires"], CultureInfo.InvariantCulture);
		Assert.Equal((int)(TimeSpan.FromMinutes(15) + GcpV4Signer.ClockSkewAllowance).TotalSeconds, seconds);
	}

	[Fact]
	public async Task SignatureIsLowercaseHex()
	{
		var signature = QueryOf(await Sign())["X-Goog-Signature"];

		Assert.Equal("deadbeef", signature);
	}

	[Fact]
	public async Task PreservesPathSeparators_ButEncodesEverythingElse()
	{
		var url = await Sign(objectName: "uploads/2026/holiday photo.jpg");

		Assert.Contains("/my-bucket/uploads/2026/holiday%20photo.jpg", url, StringComparison.Ordinal);
	}

	[Fact]
	public async Task CredentialCarriesTheServiceAccountAndScope()
	{
		var query = QueryOf(await Sign());

		Assert.Equal($"{ServiceAccount}/20260731/auto/storage/goog4_request", query["X-Goog-Credential"]);
	}

	/// <summary>
	/// Google rejects V4 URLs valid for more than seven days, so catch it before the round trip.
	/// </summary>
	[Theory]
	[InlineData(0)]
	[InlineData(-5)]
	[InlineData(60 * 24 * 8)]
	public async Task RejectsExpiryOutsideTheSupportedRange(int minutes)
	{
		await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => Sign(expiry: TimeSpan.FromMinutes(minutes)));
	}

	/// <summary>
	/// The backdating counts towards Google's seven-day ceiling, because the signed lifetime starts at
	/// the backdated timestamp. Validating the requested expiry alone let exactly seven days through and
	/// then signed 7 days + 5 minutes, which Google rejects when the URL is used -- a URL that looks
	/// valid on creation and fails in the client's hands.
	/// </summary>
	[Fact]
	public async Task RejectsExpiry_ThatOnlyExceedsTheLimitOnceSkewIsAdded()
	{
		await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
			() => Sign(expiry: GcpV4Signer.MaximumExpiry));

		// Just inside the ceiling once the allowance is accounted for.
		var url = await Sign(expiry: GcpV4Signer.MaximumExpiry - GcpV4Signer.ClockSkewAllowance);

		Assert.Equal(
			((int)GcpV4Signer.MaximumExpiry.TotalSeconds).ToString(CultureInfo.InvariantCulture),
			QueryOf(url)["X-Goog-Expires"]);
	}

	[Fact]
	public async Task StringToSign_FollowsTheV4Layout()
	{
		string? stringToSign = null;
		await Sign(captureStringToSign: s => stringToSign = s);

		var lines = stringToSign!.Split('\n');

		Assert.Equal(4, lines.Length);
		Assert.Equal("GOOG4-RSA-SHA256", lines[0]);
		Assert.Equal("20260731T115500Z", lines[1]);              // backdated by five minutes
		Assert.Equal("20260731/auto/storage/goog4_request", lines[2]);
		Assert.Matches("^[0-9a-f]{64}$", lines[3]);              // SHA-256 of the canonical request
	}

	/// <summary>Signing is deterministic for a fixed clock, so URLs are reproducible in tests.</summary>
	[Fact]
	public async Task IsDeterministic_ForAFixedClock()
	{
		Assert.Equal(await Sign(), await Sign());
	}
}
