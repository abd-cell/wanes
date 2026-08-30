namespace Wanes.Areas.Services.Users.Presence.Models;

public class UpdateLocationInput
{
    public double Lat { get; set; }
    public double Lng { get; set; }
    public bool Online { get; set; } = true;
}
