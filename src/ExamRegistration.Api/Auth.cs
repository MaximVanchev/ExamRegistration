namespace ExamRegistration.Api;

public static class Roles
{
    public const string Student = "Student";
    public const string Admin = "Admin";
}

public static class Policies
{
    public const string CanRegister = nameof(CanRegister);
    public const string CanViewAll = nameof(CanViewAll);
}
