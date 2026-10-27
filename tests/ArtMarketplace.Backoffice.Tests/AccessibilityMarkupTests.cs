namespace ArtMarketplace.Backoffice.Tests;

public sealed class AccessibilityMarkupTests
{
    private static readonly string RepositoryRoot = Path.GetFullPath("../../../../..", AppContext.BaseDirectory);

    [Fact]
    public void ShellDeclaresPersianRtlAndKeyboardSkipTarget()
    {
        var app = Read("src/Frontend/Backoffice/Components/App.razor");
        var layout = Read("src/Frontend/Backoffice/Components/Layout/MainLayout.razor");

        Assert.Contains("lang=\"fa\" dir=\"rtl\"", app, StringComparison.Ordinal);
        Assert.Contains("href=\"#main-content\"", layout, StringComparison.Ordinal);
        Assert.Contains("id=\"main-content\"", layout, StringComparison.Ordinal);
        Assert.Contains("tabindex=\"-1\"", layout, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Users.razor")]
    [InlineData("Operations.razor")]
    [InlineData("Audit.razor")]
    public void DataTablesHaveAccessibleCaptions(string page)
    {
        var markup = Read($"src/Frontend/Backoffice/Components/Pages/{page}");

        Assert.Equal(Count(markup, "<table"), Count(markup, "<caption"));
    }

    [Fact]
    public void GlobalStylesExposeFocusAndReducedMotionRules()
    {
        var css = Read("src/Frontend/Backoffice/wwwroot/app.css");

        Assert.Contains(":focus-visible", css, StringComparison.Ordinal);
        Assert.Contains("prefers-reduced-motion:reduce", css, StringComparison.Ordinal);
    }

    private static string Read(string relativePath) => File.ReadAllText(Path.Combine(RepositoryRoot, relativePath));
    private static int Count(string value, string token) => value.Split(token, StringSplitOptions.None).Length - 1;
}
