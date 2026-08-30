namespace Wanes.Shareds.Models.Base;

/// <summary>
/// Root of every persisted entity. Soft-delete everywhere — there is no global
/// query filter, so queries must add <c>.Where(x =&gt; !x.IsDeleted)</c> explicitly.
/// </summary>
public abstract class BaseEntity
{
    public int Id { get; set; }

    public bool IsDeleted { get; set; }
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public DateTime? ModificationDate { get; set; }
    public DateTime? DeletionDate { get; set; }
}

/// <summary>Adds the acting user id to the audit columns.</summary>
public abstract class AuditableEntity : BaseEntity
{
    public int? CreatedBy { get; set; }
    public int? ModifiedBy { get; set; }
}
