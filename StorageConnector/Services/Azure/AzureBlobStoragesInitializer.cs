using Azure;
using Azure.AI.Vision.Face;
using Azure.Storage;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;
using StorageConnector.Common;

namespace StorageConnector.Services.Azure
{
	public sealed class AzureBlobStoragesInitializer
	{
		private const string SectionName = $"{ConstantStrings.StorageConnectorsConfigName}:Azure";

		internal readonly AzureBlobStorageSettings? AzureBlobStorageSettings;
		internal readonly Dictionary<string, BlobServiceClient> AccountNamesMappedToBlobServiceClient = [];

		/// <summary>
		/// Null unless a <c>VisionAccount</c> section is configured. Blob storage works without it;
		/// only the face APIs require it.
		/// </summary>
		internal readonly FaceClient? FaceClient;
		internal readonly FaceAdministrationClient? FaceAdministrationClient;

		public AzureBlobStoragesInitializer(IConfiguration configuration)
		{
			var azureConfig = configuration.GetSection(SectionName);
			if (!azureConfig.Exists())
			{
				return;
			}

			var accounts = azureConfig.GetSection(ConstantStrings.AccountsConfigName).Get<List<AzureAccount>>();
			if (accounts is null || accounts.Count == 0)
			{
				throw StorageConnectorConfigurationException.MissingSetting(
					$"{SectionName}:{ConstantStrings.AccountsConfigName}",
					"At least one Azure storage account must be configured.");
			}

			for (var i = 0; i < accounts.Count; i++)
			{
				ValidateAccount(accounts[i], i);
			}

			AzureBlobStorageSettings = new AzureBlobStorageSettings
			{
				CountryIsoCodeMapToAccountName = azureConfig
					.GetSection(ConstantStrings.CountryIsoCodeMapToAccountNameConfigName)
					.Get<Dictionary<string, string>>()
					.ParseCountryIsoCodeMap($"{SectionName}:{ConstantStrings.CountryIsoCodeMapToAccountNameConfigName}"),
				Accounts = accounts
			};

			// The Face API is optional: blob storage without face recognition is a supported setup, so a
			// missing or partially filled VisionAccount section leaves the face clients null rather than
			// failing start-up.
			var visionAccount = azureConfig.GetSection("VisionAccount").Get<AzureVisionAccountSettings>();
			if (visionAccount is not null
				&& !string.IsNullOrWhiteSpace(visionAccount.Endpoint)
				&& !string.IsNullOrWhiteSpace(visionAccount.ApiKey))
			{
				if (!Uri.TryCreate(visionAccount.Endpoint, UriKind.Absolute, out var faceEndpoint))
				{
					throw new StorageConnectorConfigurationException(
						$"Configuration '{SectionName}:VisionAccount:Endpoint' is not a valid absolute URI: '{visionAccount.Endpoint}'.");
				}

				var credentials = new AzureKeyCredential(visionAccount.ApiKey);
				FaceClient = new FaceClient(faceEndpoint, credentials);
				FaceAdministrationClient = new FaceAdministrationClient(faceEndpoint, credentials);
			}

			foreach (var account in accounts)
			{
				AccountNamesMappedToBlobServiceClient[account.AccountName] = new BlobServiceClient(
					new Uri($"https://{account.AccountName}.blob.core.windows.net"),
					new StorageSharedKeyCredential(account.AccountName, account.AccountKey)
				);
			}
		}

		private static void ValidateAccount(AzureAccount account, int index)
		{
			var path = $"{SectionName}:{ConstantStrings.AccountsConfigName}:{index}";

			// These are marked `required` on AzureAccount, but the configuration binder does not enforce
			// `required` -- it simply leaves them null. Validate explicitly.
			if (string.IsNullOrWhiteSpace(account.AccountName))
			{
				throw StorageConnectorConfigurationException.MissingSetting($"{path}:AccountName");
			}

			if (string.IsNullOrWhiteSpace(account.AccountKey))
			{
				throw StorageConnectorConfigurationException.MissingSetting($"{path}:AccountKey");
			}

			if (string.IsNullOrWhiteSpace(account.ContainerName))
			{
				throw StorageConnectorConfigurationException.MissingSetting($"{path}:ContainerName");
			}
		}
	}
}
