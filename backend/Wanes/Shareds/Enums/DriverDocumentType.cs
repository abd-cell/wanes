namespace Wanes.Shareds.Enums;

/// <summary>
/// The kind of paperwork a driver document holds.
///
/// Typed rather than free text because the admin queue is read at a glance and
/// the required set is checked in code: an application is only complete once a
/// licence and an identity document are both on file
/// (<see cref="Wanes.Shareds.Constants.DriverDocumentRules.Required"/>).
///
/// Front and back of the licence are separate entries. They are two photographs
/// of two different faces, and a reviewer needs both — folding them into one
/// type would mean the second upload silently replacing the first.
/// </summary>
public enum DriverDocumentType
{
    LicenseFront = 1,
    LicenseBack = 2,

    /// <summary>National id, residency card or passport page — whatever proves who they are.</summary>
    IdDocument = 3,

    /// <summary>Vehicle registration ("licence of the car"). Optional.</summary>
    VehicleRegistration = 4,

    /// <summary>Insurance certificate. Optional.</summary>
    Insurance = 5,
}
