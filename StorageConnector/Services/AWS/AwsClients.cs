using Amazon.Rekognition;
using Amazon.S3;

namespace StorageConnector.Services.AWS
{
	public sealed class AwsClients
	{
		public required AmazonS3Client AmazonS3Client { get; init; }
		public required AmazonRekognitionClient AmazonRekognitionClient { get; init; }
	}
}
