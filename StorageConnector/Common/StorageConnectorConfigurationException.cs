namespace StorageConnector.Common
{
	/// <summary>
	/// Thrown during start-up when the <c>StorageConnectors</c> configuration is incomplete or
	/// invalid. Every message names the exact configuration path at fault so an operator can act on
	/// it without reading the library's source.
	/// </summary>
	public sealed class StorageConnectorConfigurationException : Exception
	{
		public StorageConnectorConfigurationException(string message) : base(message)
		{
		}

		public StorageConnectorConfigurationException(string message, Exception innerException)
			: base(message, innerException)
		{
		}

		/// <summary>
		/// Builds an exception for a missing or empty setting, quoting its full configuration path.
		/// </summary>
		internal static StorageConnectorConfigurationException MissingSetting(string configurationPath, string? because = null)
			=> new($"Required configuration '{configurationPath}' is missing or empty.{(because is null ? string.Empty : $" {because}")}");
	}
}
