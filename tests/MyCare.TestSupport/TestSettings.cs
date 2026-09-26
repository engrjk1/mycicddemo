namespace MyCare.TestSupport;

public static class TestSettings
{
    public const string BaseUrlVariable = "MYCARE_BASE_URL";

    /// <summary>
    /// The environment under test. Required input with no default: if it is unset the test fails
    /// instead of silently hitting the wrong environment (strategy section 5).
    /// </summary>
    public static string BaseUrl
    {
        get
        {
            var url = Environment.GetEnvironmentVariable(BaseUrlVariable);
            if (string.IsNullOrWhiteSpace(url))
                throw new InvalidOperationException(
                    $"{BaseUrlVariable} is not set. Base URLs are required inputs with no default. " +
                    $"Example: set {BaseUrlVariable}=http://localhost:5080");
            return url.TrimEnd('/');
        }
    }
}
