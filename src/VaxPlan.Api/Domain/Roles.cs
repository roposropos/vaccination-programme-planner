namespace VaxPlan.Api.Domain;

public static class Roles
{
    public const string Patient = "Patient";
    public const string Nurse = "Nurse";
    public const string Doctor = "Doctor";
    public const string Admin = "Admin";

    public const string Clinical = Nurse + "," + Doctor;

    public static readonly string[] All = [Patient, Nurse, Doctor, Admin];
}
