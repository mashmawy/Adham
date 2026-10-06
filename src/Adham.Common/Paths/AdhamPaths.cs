namespace Adham.Common.Paths;

// Where Adham keeps user-wide files: ~/.adham, or ADHAM_HOME if set.
public static class AdhamPaths
{
    public static string UserConfigDir(Func<string, string?> read, string userHome) =>
        read("ADHAM_HOME") is { Length: > 0 } custom ? custom : Path.Combine(userHome, ".adham");

    public static string UserConfigDir() =>
        UserConfigDir(Environment.GetEnvironmentVariable, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));

    // User-wide settings (e.g. the API key), used from any working directory.
    public static string UserDotEnv() => Path.Combine(UserConfigDir(), ".env");
}
