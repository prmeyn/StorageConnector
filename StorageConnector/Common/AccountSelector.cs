using EarthCountriesInfo;

namespace StorageConnector.Common
{
	/// <summary>
	/// The single account-selection rule shared by every provider.
	///
	/// Previously each provider resolved a country to an account differently: Azure fell back to the
	/// first configured account, AWS threw <c>InvalidOperationException</c> from <c>First(predicate)</c>,
	/// and GCP dereferenced a null from <c>FirstOrDefault</c>. One typo in a country map therefore
	/// produced three unrelated failures depending on which cloud was configured.
	/// </summary>
	internal static class AccountSelector
	{
		/// <summary>
		/// Resolves the account serving <paramref name="countryIsoCode"/>, falling back to the first
		/// configured account when the country is unmapped or maps to an account that does not exist.
		/// Returns <c>null</c> only when no accounts are configured at all.
		/// </summary>
		internal static TAccount? Select<TAccount>(
			IReadOnlyDictionary<CountryIsoCode, string> countryIsoCodeMapToAccountName,
			IReadOnlyList<TAccount> accounts,
			Func<TAccount, string> accountNameSelector,
			CountryIsoCode countryIsoCode)
			where TAccount : class
		{
			if (accounts.Count == 0)
			{
				return null;
			}

			if (countryIsoCodeMapToAccountName.TryGetValue(countryIsoCode, out var accountName))
			{
				var matched = accounts.FirstOrDefault(
					account => string.Equals(accountNameSelector(account), accountName, StringComparison.Ordinal));

				if (matched is not null)
				{
					return matched;
				}
			}

			return accounts[0];
		}
	}
}
