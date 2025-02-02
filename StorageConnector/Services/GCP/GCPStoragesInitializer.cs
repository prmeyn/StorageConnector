using Google.Apis.Auth.OAuth2;
using Google.Cloud.Iam.Credentials.V1;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using StorageConnector.Common;

namespace StorageConnector.Services.GCP
{
	public sealed class GCPStoragesInitializer
	{
		internal readonly GCPStorageSettings? GCPStorageSettings;
		internal readonly IAMCredentialsClient? _iamCredentialsClient;

		public GCPStoragesInitializer(IConfiguration configuration)
		{
			var gcpConfig = configuration.GetSection($"{ConstantStrings.StorageConnectorsConfigName}:GCP");
			if (gcpConfig.Exists())
			{
				GCPStorageSettings = new GCPStorageSettings
				{
					CountryIsoCodeMapToAccountName = (gcpConfig.GetSection(ConstantStrings.CountryIsoCodeMapToAccountNameConfigName).Get<Dictionary<string, string>>()).ParseCountryIsoCodeMap(),
					Accounts = gcpConfig.GetRequiredSection(ConstantStrings.AccountsConfigName).Get<List<GCPAccount>>()
				};
				var gcpCredentialsJson = JsonConvert.SerializeObject(gcpConfig.GetSection("GcpCredentials").Get<Dictionary<string, string>>());
				var googleCredential = GoogleCredential.FromJson(gcpCredentialsJson);
				_iamCredentialsClient = new IAMCredentialsClientBuilder { Credential = googleCredential }.Build();
			}
		}
	}
}
