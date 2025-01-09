namespace SPlayer;

[Register("AppDelegate")]
public class AppDelegate : UIApplicationDelegate
{
    public override UIWindow Window { get; set; }

    public override bool FinishedLaunching(UIApplication application, NSDictionary launchOptions)
    {
        Logger.Log("App Launching...");

        Window = new UIWindow(UIScreen.MainScreen.Bounds);

        var mainViewController = new MainViewController();
        var navController = new UINavigationController(mainViewController);
        Window.RootViewController = navController;

        Window.MakeKeyAndVisible();

        Logger.Log("App Launched Successfully.");
        return true;
    }
}