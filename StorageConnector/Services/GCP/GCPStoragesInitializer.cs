using Google.Apis.Auth.OAuth2;
using Google.Cloud.Iam.Credentials.V1;
using Microsoft.Extensions.Configuration;
using StorageConnector.Common;
using System.Text.Json;

namespace StorageConnector.Services.GCP
{
	public sealed class GCPStoragesInitializer
	{
		private const string SectionName = $"{ConstantStrings.StorageConnectorsConfigName}:GCP";

		internal readonly GCPStorageSettings? GCPStorageSettings;
		internal readonly IAMCredentialsClient? IamCredentialsClient;

		public GCPStoragesInitializer(IConfiguration configuration)
		{
			var gcpConfig = configuration.GetSection(SectionName);
			if (!gcpConfig.Exists())
			{
				return;
			}

			var accounts = gcpConfig.GetSection(ConstantStrings.AccountsConfigName).Get<List<GCPAccount>>();
			if (accounts is null || accounts.Count == 0)
			{
				throw StorageConnectorConfigurationException.MissingSetting(
					$"{SectionName}:{ConstantStrings.AccountsConfigName}",
					"At least one GCP bucket must be configured.");
			}

			for (var i = 0; i < accounts.Count; i++)
			{
				var path = $"{SectionName}:{ConstantStrings.AccountsConfigName}:{i}";

				// `required` on GCPAccount is not enforced by the configuration binder.
				if (string.IsNullOrWhiteSpace(accounts[i].BucketName))
				{
					throw StorageConnectorConfigurationException.MissingSetting($"{path}:BucketName");
				}

				if (string.IsNullOrWhiteSpace(accounts[i].ServiceAccountEmail))
				{
					throw StorageConnectorConfigurationException.MissingSetting($"{path}:ServiceAccountEmail");
				}
			}

			GCPStorageSettings = new GCPStorageSettings
			{
				CountryIsoCodeMapToAccountName = gcpConfig
					.GetSection(ConstantStrings.CountryIsoCodeMapToAccountNameConfigName)
					.Get<Dictionary<string, string>>()
					.ParseCountryIsoCodeMap($"{SectionName}:{ConstantStrings.CountryIsoCodeMapToAccountNameConfigName}"),
				Accounts = accounts
			};

			// Without this check the section binds to null, is serialized to the literal string "null",
			// and credential parsing fails with an error that says nothing about configuration.
			var gcpCredentials = gcpConfig.GetSection("GcpCredentials").Get<Dictionary<string, string>>();
			if (gcpCredentials is null || gcpCredentials.Count == 0)
			{
				throw StorageConnectorConfigurationException.MissingSetting(
					$"{SectionName}:GcpCredentials",
					"Provide the service account key JSON.");
			}

			try
			{
				// CredentialFactory replaces GoogleCredential.FromJson, which Google deprecated citing a
				// potential security risk (finding H9). Requesting ServiceAccountCredential explicitly
				// means a key of the wrong type is rejected here rather than failing later at the first
				// signing call.
				var googleCredential = CredentialFactory
					.FromJson<ServiceAccountCredential>(JsonSerializer.Serialize(gcpCredentials))
					.ToGoogleCredential();

				IamCredentialsClient = new IAMCredentialsClientBuilder { Credential = googleCredential }.Build();
			}
			catch (Exception ex) when (ex is not StorageConnectorConfigurationException)
			{
				throw new StorageConnectorConfigurationException(
					$"Configuration '{SectionName}:GcpCredentials' is not a usable service account key. " +
					$"Expected a service account key JSON with \"type\": \"service_account\". {ex.Message}", ex);
			}
		}
	}
}
