namespace Jbx.Testing;

public static class Fixture
{
    public static byte[] ReadBytes(string relativePath)
    {
        return File.ReadAllBytes(PathOf(relativePath));
    }

    public static string ReadText(string relativePath)
    {
        return File.ReadAllText(PathOf(relativePath));
    }

    public static string PathOf(string relativePath)
    {
        return Path.Combine(AppContext.BaseDirectory, "fixtures", relativePath);
    }
}
