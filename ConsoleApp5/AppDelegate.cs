namespace ConsoleApp5;

[Register("AppDelegate")]
public class AppDelegate : UIApplicationDelegate
{
    // iOS 应用的窗口对象
    public override UIWindow Window { get; set; }

    public override bool FinishedLaunching(UIApplication application, NSDictionary launchOptions)
    {
        // 创建并设置主窗口
        Window = new UIWindow(UIScreen.MainScreen.Bounds);

        // 创建一个简单的 UIViewController 并设置背景色
        var viewController = new UIViewController();
        viewController.View.BackgroundColor = UIColor.White;

        // 创建一个原生 UILabel
        var label = new UILabel
        {
            Frame = new CGRect(20, 80, Window.Bounds.Width - 40, 40),
            Text = "Hello, iOS from .NET 9!",
            TextAlignment = UITextAlignment.Center,
            TextColor = UIColor.Black,
            Font = UIFont.SystemFontOfSize(24, UIFontWeight.Semibold)
        };
        viewController.View.AddSubview(label);

        // 将此 UIViewController 作为 RootViewController
        Window.RootViewController = viewController;
        // 显示窗口
        Window.MakeKeyAndVisible();

        return true;
    }
}