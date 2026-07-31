namespace StorageConnector.Common
{
	public static class ConstantStrings
	{
		// These were mutable `public static string` fields: any consumer could reassign them and break
		// configuration resolution process-wide (finding M3).
		public const string StorageConnectorsConfigName = "StorageConnectors";
		public const string CountryIsoCodeMapToAccountNameConfigName = "CountryIsoCodeMapToAccountName";
		public const string AccountsConfigName = "Accounts";
	}
}
