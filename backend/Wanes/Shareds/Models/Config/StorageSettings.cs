namespace Wanes.Shareds.Models.Config;

public class StorageSettings
{
    /// <summary>
    /// Where uploaded files live. Relative paths resolve against the content
    /// root. Deliberately outside <c>wwwroot</c>: a driver's licence and id are
    /// served through an authorized endpoint, never as static content anyone
    /// with the URL could fetch.
    /// </summary>
    public string Root { get; set; } = "App_Data/uploads";
}
