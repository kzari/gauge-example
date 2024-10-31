namespace AgileContent.Itaas.E2E.Models;

public static class WebhookProcessStatus
{
    public const string Downloading = "Downloading";
    public const string Waiting = "Waiting";
    public const string Processing = "Processing";
    public const string Uploaded = "Uploaded";
    public const string RestoringSchemaAndData = "RestoringSchemaAndData";
    public const string RestoringIndexesAndConstraints = "RestoringIndexesAndConstraints";
    public const string Creating = "Creating";
    public const string Activating = "Activating";
    public const string Processed = "Processed";
    public const string Finished = "Finished";
    public const string Info = "Info";
    public const string OldCatalogsInactivated = "OldCatalogsInactivated";
    public const string Error = "Error";
}