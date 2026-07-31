using EarthCountriesInfo;
using StorageConnector.Common.DTOs;

namespace StorageConnector.Common
{
	/// <summary>
	/// Face recognition, split apart from storage.
	///
	/// The single <c>GetFaceInfo</c> method this replaces both counted faces AND permanently stored a
	/// biometric template, as a side effect of a call that read like a question (finding H5). Face
	/// templates are special-category personal data under GDPR Article 9, so storing one must be a
	/// deliberate act with a lawful basis, and it must be erasable.
	///
	/// The operations below are therefore separated by what they do to stored data:
	/// <list type="bullet">
	/// <item><see cref="CountFacesAsync"/> and <see cref="FindMatchingFacesAsync"/> only read.</item>
	/// <item><see cref="RegisterFaceAsync"/> is the only method that stores anything.</item>
	/// <item><see cref="DeleteRegisteredFacesAsync"/> exists so an erasure request can be honoured.</item>
	/// </list>
	/// </summary>
	public interface IFaceRecognitionProvider
	{
		/// <summary>
		/// Whether this provider is configured for face recognition. Azure additionally requires a
		/// <c>VisionAccount</c> section; Google Cloud Storage does not support it at all.
		/// </summary>
		bool SupportsFaceRecognition { get; }

		/// <summary>
		/// Counts the good-quality faces in a stored image. Read-only: stores nothing.
		/// </summary>
		Task<int> CountFacesAsync(
			CountryIsoCode regionCountryIsoCode,
			CloudFileName fileNameWithExtension,
			CancellationToken cancellationToken = default);

		/// <summary>
		/// Returns the subject identifiers of previously registered faces matching this image.
		/// Read-only: stores nothing, and never adds the queried face to the collection.
		/// </summary>
		/// <exception cref="NotSupportedException">
		/// Thrown by providers that cannot search a collection.
		/// </exception>
		Task<IReadOnlySet<string>> FindMatchingFacesAsync(
			string faceCollectionName,
			CountryIsoCode regionCountryIsoCode,
			CloudFileName fileNameWithExtension,
			CancellationToken cancellationToken = default);

		/// <summary>
		/// Stores a biometric template for this image under <paramref name="subjectId"/>.
		///
		/// This writes special-category personal data. Call it only where you have a lawful basis, and
		/// make sure <see cref="DeleteRegisteredFacesAsync"/> is wired into your erasure process.
		/// </summary>
		/// <param name="faceCollectionName">The collection the template is stored in.</param>
		/// <param name="regionCountryIsoCode">Selects which configured account holds the image.</param>
		/// <param name="fileNameWithExtension">The stored image to read the face from.</param>
		/// <param name="subjectId">
		/// Your identifier for the person. Restricted to letters, digits and <c>_ . - :</c> (max 255
		/// characters), which is what AWS Rekognition permits for an external image id.
		/// </param>
		/// <param name="cancellationToken">Cancels the operation.</param>
		Task<RegisteredFace> RegisterFaceAsync(
			string faceCollectionName,
			CountryIsoCode regionCountryIsoCode,
			CloudFileName fileNameWithExtension,
			string subjectId,
			CancellationToken cancellationToken = default);

		/// <summary>
		/// Deletes every stored face template for <paramref name="subjectId"/>, and returns how many
		/// were removed. Deleting a subject with nothing stored is not an error and returns zero.
		/// </summary>
		/// <param name="faceCollectionName">The collection to erase from.</param>
		/// <param name="regionCountryIsoCode">
		/// Must be the same country used when the face was registered. Templates live in the recognition
		/// service of the account that country maps to, so erasing with a different country would search
		/// the wrong collection, find nothing, and report a successful deletion that never happened.
		/// </param>
		/// <param name="subjectId">The identifier the templates were registered under.</param>
		/// <param name="cancellationToken">Cancels the operation.</param>
		Task<int> DeleteRegisteredFacesAsync(
			string faceCollectionName,
			CountryIsoCode regionCountryIsoCode,
			string subjectId,
			CancellationToken cancellationToken = default);
	}
}
