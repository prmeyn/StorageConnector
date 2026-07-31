using EarthCountriesInfo;

namespace StorageConnector.Common
{
	/// <summary>
	/// Parses country-code-to-account-name maps from configuration.
	///
	/// This was a <c>public</c> extension method on <c>Dictionary&lt;string, string&gt;</c> in the root
	/// namespace, so it appeared in IntelliSense on every dictionary for anyone with a
	/// <c>using StorageConnector;</c>. It is an implementation detail (finding M4).
	/// </summary>
	internal static class CountryIsoCodeMapParser
	{
		/// <param name="rawMap">The raw country-code keyed map as bound from configuration.</param>
		/// <param name="configurationPath">
		/// The full configuration path this map came from. Included in the exception when a code is
		/// invalid, so an operator with several providers configured knows which section to fix.
		/// </param>
		internal static Dictionary<CountryIsoCode, string> ParseCountryIsoCodeMap(
			this Dictionary<string, string>? rawMap,
			string? configurationPath = null)
		{
			var parsedMap = new Dictionary<CountryIsoCode, string>();

			if (rawMap is null)
			{
				return parsedMap;
			}

			foreach (var kvp in rawMap)
			{
				if (!Enum.TryParse(kvp.Key, ignoreCase: true, out CountryIsoCode countryIsoCode))
				{
					var where = configurationPath is null ? string.Empty : $" in '{configurationPath}'";
					throw new ArgumentException(
						$"Invalid country ISO code '{kvp.Key}'{where}. Expected an ISO 3166-1 alpha-2 country " +
						$"code such as 'DK' or 'US'; note that region groupings like 'EU' are not country codes.");
				}

				parsedMap[countryIsoCode] = kvp.Value;
			}

			return parsedMap;
		}
	}
}
