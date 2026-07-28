namespace DistroNexus.Tests.ViewModels;

public sealed class InstallWizardRoutingGuardTests
{
    [Theory]
    [InlineData("src/Client/DistroNexus.Desktop/ViewModels/InstallWizardViewModel.cs")]
    [InlineData("src/Client/DistroNexus.Desktop/Wizard/Steps/ProgressStep.cs")]
    public void InstallWizards_UseOnlyTheTypedVerifiedInstallFlow(string relativePath)
    {
        var root = Directory.GetCurrentDirectory();
        while (!File.Exists(Path.Combine(root, "AGENTS.md"))) root = Directory.GetParent(root)!.FullName;
        var source = File.ReadAllText(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));

        Assert.DoesNotContain("IWslManagerService", source, StringComparison.Ordinal);
        Assert.DoesNotContain("InstallInstanceAsync", source, StringComparison.Ordinal);
        Assert.Contains("ResolveInstallSourceAsync", source, StringComparison.Ordinal);
        Assert.Contains("PreviewPackageAcquisitionAsync", source, StringComparison.Ordinal);
        Assert.Contains("AcquirePackageAsync", source, StringComparison.Ordinal);
        Assert.Contains("InstallVerifiedInstanceAsync", source, StringComparison.Ordinal);
    }

    [Fact]
    public void WizardContext_DoesNotRetainSecrets()
    {
        var root = Directory.GetCurrentDirectory();
        while (!File.Exists(Path.Combine(root, "AGENTS.md"))) root = Directory.GetParent(root)!.FullName;
        var source = File.ReadAllText(Path.Combine(root, "src", "Client", "DistroNexus.Desktop", "Wizard", "WizardContext.cs"));

        Assert.DoesNotContain("SecureString", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Password", source, StringComparison.Ordinal);
    }

    [Fact]
    public void UserConfigurationNavigationAway_ClearsBothPasswordControls()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var step = new DistroNexus.Desktop.Wizard.Steps.UserConfigurationStep(
                    Moq.Mock.Of<DistroNexus.Core.Interfaces.ISettingsService>(), Moq.Mock.Of<Microsoft.Extensions.Logging.ILogger>())
                { Context = new DistroNexus.Desktop.Wizard.WizardContext() };
                var view = (DistroNexus.Desktop.Wizard.Steps.UserConfigurationStepView)step.Content;
                var password = (System.Windows.Controls.PasswordBox)view.FindName("PasswordInput");
                var confirmation = (System.Windows.Controls.PasswordBox)view.FindName("ConfirmPasswordInput");
                password.Password = "test-secret";
                confirmation.Password = "test-secret";

                step.OnExitAsync().GetAwaiter().GetResult();

                Assert.Equal(0, password.SecurePassword.Length);
                Assert.Equal(0, confirmation.SecurePassword.Length);
            }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        Assert.Null(failure);
    }
}
