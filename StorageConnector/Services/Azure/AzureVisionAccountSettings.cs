namespace StorageConnector.Services.Azure
{
	public sealed class AzureVisionAccountSettings
	{
		public required string Endpoint { get; init; }
		public required string ApiKey { get; init; }
	}
}
