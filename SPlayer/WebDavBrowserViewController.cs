using System.Net;
using WebDav;

namespace SPlayer;

public class WebDavBrowserViewController : UITableViewController
{
    private readonly WebDavServer _server;
    private readonly string _currentPath;  // 当前位置（相对于 serverUrl 的相对路径）

    private List<WebDavResource> _resources; // 当前目录下的资源列表

    public WebDavBrowserViewController(WebDavServer server, string path)
    {
        _server = server;
        _currentPath = path;

        Title = path == "/" ? server.Title : path;
    }

    public override async void ViewDidLoad()
    {
        base.ViewDidLoad();

        // 加载当前目录内容
        var clientParams = new WebDavClientParams
        {
            BaseAddress = new Uri(_server.ServerUrl),
            Credentials = new NetworkCredential(_server.UserName, _server.Password)
        };
        var webDavClient = new WebDavClient(clientParams);

        try
        {
            // 假设当前路径不是绝对 URL，而是 "/xxx/yyy"
            // 要组合出完整的 URL
            var fullUrl = CombineUrl(_server.ServerUrl, _currentPath);

            Logger.Log($"WebDavBrowser: listing {fullUrl}");
            var result = await webDavClient.Propfind(fullUrl);

            if (result.IsSuccessful)
            {
                // 过滤出有效的资源（排除最顶级本身那个条目，以及空 DisplayName 的）
                _resources = result.Resources
                    .Where(r => r.Uri.TrimEnd('/') != fullUrl.TrimEnd('/')) // 不包含当前目录自身
                    .Where(r => !string.IsNullOrEmpty(r.DisplayName))
                    .OrderBy(r => r.IsCollection) // 先文件夹后文件，这里可根据需求调整
                    .ThenBy(r => r.DisplayName)
                    .ToList();

                TableView.ReloadData();
            }
            else
            {
                Logger.Log($"WebDAV PropFind request failed: {result.StatusCode}");
                ShowErrorAlert("WebDAV 连接失败", "请检查配置或网络。");
            }
        }
        catch (Exception ex)
        {
            Logger.Log($"WebDAV error: {ex.Message}");
            ShowErrorAlert("WebDAV 异常", ex.Message);
        }
    }

    public override nint RowsInSection(UITableView tableView, nint section)
    {
        if (_resources == null) return 0;
        return _resources.Count;
    }

    public override UITableViewCell GetCell(UITableView tableView, NSIndexPath indexPath)
    {
        const string cellId = "WebDavResourceCell";
        var cell = tableView.DequeueReusableCell(cellId) 
                   ?? new UITableViewCell(UITableViewCellStyle.Subtitle, cellId);

        var item = _resources[indexPath.Row];

        // 如果是文件夹
        if (item.IsCollection)
        {
            cell.TextLabel.Text = $"📁 {item.DisplayName}";
            cell.DetailTextLabel.Text = item.Uri;
            cell.Accessory = UITableViewCellAccessory.DisclosureIndicator;
        }
        else
        {
            cell.TextLabel.Text = item.DisplayName;
            cell.DetailTextLabel.Text = item.Uri;
            cell.Accessory = UITableViewCellAccessory.None;
        }

        cell.DetailTextLabel.TextColor = UIColor.Gray;

        return cell;
    }

    public override void RowSelected(UITableView tableView, NSIndexPath indexPath)
    {
        var item = _resources[indexPath.Row];
        tableView.DeselectRow(indexPath, true);

        if (item.IsCollection)
        {
            // 如果是文件夹，进入下一级
            var ap = new Uri(_server.ServerUrl).AbsolutePath;
            var relativePath = $"/{item.Uri[ap.Length..].TrimStart('/')}";
            
            var browserVC = new WebDavBrowserViewController(_server, relativePath);
            NavigationController.PushViewController(browserVC, true);
        }
        else
        {
            // 如果是文件，尝试把它当成视频添加到播放列表
            AddToPlaylist(item);
        }
    }

    private void AddToPlaylist(WebDavResource item)
    {
        Logger.Log($"尝试添加文件到播放列表: {item.Uri}");

        // 找到主界面 (MainViewController)
        var mainVC = NavigationController?.ViewControllers
            .FirstOrDefault(vc => vc is MainViewController) as MainViewController;
        if (mainVC == null)
        {
            ShowErrorAlert("操作失败", "未找到主页面实例。");
            return;
        }

        // 创建 VideoItem
        var videoItem = new MainViewController.VideoItem
        {
            Title = item.DisplayName,
            Url = item.Uri // 这个是完整 URL
        };

        mainVC.AddVideoItemAndRefresh(videoItem);

        // 提示成功
        var alert = UIAlertController.Create("已添加", $"已将 {item.DisplayName} 添加到播放列表", 
            UIAlertControllerStyle.Alert);
        alert.AddAction(UIAlertAction.Create("确定", UIAlertActionStyle.Default, null));
        PresentViewController(alert, true, null);
    }

    private void ShowErrorAlert(string title, string message)
    {
        var alert = UIAlertController.Create(title, message, UIAlertControllerStyle.Alert);
        alert.AddAction(UIAlertAction.Create("确定", UIAlertActionStyle.Default, null));
        PresentViewController(alert, true, null);
    }

    /// <summary>
    /// 根据 baseUrl 与 path 拼出完整地址
    /// </summary>
    private string CombineUrl(string baseUrl, string path)
    {
        if (!baseUrl.EndsWith("/")) baseUrl += "/";
        if (path.StartsWith("/")) path = path.Substring(1);
        return baseUrl + path;
    }
}