namespace modaar.api.Common.Push;

public sealed class FirebaseSettings
{
    public const string SectionName = "Firebase";

    // Path to the service-account JSON from Firebase (Project settings → Service accounts).
    // The file is a secret: keep it outside the repository and outside the deploy folder, and set
    // the path on the server with the Firebase__CredentialsPath environment variable.
    // Empty = push is switched off; notifications still save and still reach open apps over
    // SignalR.
    public string? CredentialsPath { get; set; }
}
