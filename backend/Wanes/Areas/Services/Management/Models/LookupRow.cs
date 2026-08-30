namespace Wanes.Areas.Services.Management.Models;

/// <summary>
/// One option in an admin reference picker. <see cref="Label"/> is what the admin
/// reads ("Ahmad Ali", "Amman → Zarqa"); <see cref="Description"/> is the secondary
/// line that disambiguates it (phone, departure time, record id); <see cref="Id"/>
/// is the foreign key actually stored.
/// </summary>
public class LookupRow
{
    public int Id { get; set; }
    public string Label { get; set; } = string.Empty;
    public string? Description { get; set; }

    public LookupRow() { }

    public LookupRow(int id, string? label, string? description)
    {
        Id = id;
        Label = string.IsNullOrWhiteSpace(label) ? $"#{id}" : label;
        Description = description;
    }
}
