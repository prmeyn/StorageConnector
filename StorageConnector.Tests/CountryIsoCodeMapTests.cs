using EarthCountriesInfo;
using StorageConnector.Common;

namespace StorageConnector.Tests;

/// <summary>
/// Characterization tests for ParseCountryIsoCodeMap (SeviceCollectionExtensions).
/// This runs during provider initialization, so anything it throws is an application startup failure.
/// Findings C3 and M4.
/// </summary>
public class CountryIsoCodeMapTests
{
	[Fact]
	public void ParsesValidCountryCodes()
	{
		var parsed = new Dictionary<string, string>
		{
			["DK"] = "danish-bucket",
			["US"] = "us-bucket",
		}.ParseCountryIsoCodeMap();

		Assert.Equal("danish-bucket", parsed[CountryIsoCode.DK]);
		Assert.Equal("us-bucket", parsed[CountryIsoCode.US]);
	}

	[Fact]
	public void IsCaseInsensitive()
	{
		var parsed = new Dictionary<string, string> { ["dk"] = "danish-bucket" }.ParseCountryIsoCodeMap();
		Assert.Equal("danish-bucket", parsed[CountryIsoCode.DK]);
	}

	[Fact]
	public void ReturnsEmptyMap_ForNullInput()
	{
		Dictionary<string, string>? raw = null;
		Assert.Empty(raw.ParseCountryIsoCodeMap());
	}

	[Fact]
	public void ReturnsEmptyMap_ForEmptyInput()
	{
		Assert.Empty(new Dictionary<string, string>().ParseCountryIsoCodeMap());
	}

	/// <summary>
	/// C3: "EU" is a region grouping, not an ISO 3166-1 country code, so it is not a member of
	/// CountryIsoCode. The README used to document exactly this key; the message now explains the
	/// distinction rather than just reporting the code as invalid.
	/// </summary>
	[Fact]
	public void Throws_OnRegionGroupings_ExplainingWhy()
	{
		var raw = new Dictionary<string, string> { ["EU"] = "your-s3-bucket" };

		var ex = Assert.Throws<ArgumentException>(() => raw.ParseCountryIsoCodeMap());

		Assert.Contains("EU", ex.Message);
		Assert.Contains("ISO 3166-1", ex.Message);
	}

	/// <summary>
	/// M4 (fixed): with several providers configured, the message must say which section the bad code
	/// came from. It previously reported only the code, leaving an operator to guess.
	/// </summary>
	[Fact]
	public void ThrownMessage_IdentifiesTheConfigurationSection()
	{
		var raw = new Dictionary<string, string> { ["ZZZ"] = "some-bucket" };

		var ex = Assert.Throws<ArgumentException>(() =>
			raw.ParseCountryIsoCodeMap("StorageConnectors:GCP:CountryIsoCodeMapToAccountName"));

		Assert.Contains("StorageConnectors:GCP:CountryIsoCodeMapToAccountName", ex.Message);
	}
}
