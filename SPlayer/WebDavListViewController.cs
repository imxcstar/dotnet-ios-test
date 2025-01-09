using System.Text.Json;

namespace SPlayer;

public class WebDavListViewController : UITableViewController
{
    // 存储所有 WebDavServer 的本地存储 Key
    private const string WebDavServersStorageKey = "WebDavServersStorageKey";

    // 内存中的服务器列表
    private List<WebDavServer> _servers;

    public WebDavListViewController()
    {
        Title = "WebDAV 管理";
    }

    public override void ViewDidLoad()
    {
        base.ViewDidLoad();

        // 从本地加载服务器列表
        _servers = LoadWebDavServers();

        // 设置右上角按钮“添加”
        NavigationItem.RightBarButtonItem = new UIBarButtonItem(UIBarButtonSystemItem.Add, (s, e) =>
        {
            ShowAddServerDialog();
        });

        // 编辑功能（左上角） - 用于快速删除
        NavigationItem.LeftBarButtonItem = EditButtonItem;
    }

    public override nint RowsInSection(UITableView tableView, nint section)
    {
        return _servers.Count;
    }

    public override UITableViewCell GetCell(UITableView tableView, NSIndexPath indexPath)
    {
        const string cellId = "WebDavServerCell";
        var cell = tableView.DequeueReusableCell(cellId) 
                   ?? new UITableViewCell(UITableViewCellStyle.Subtitle, cellId);

        var server = _servers[indexPath.Row];
        cell.TextLabel.Text = server.Title;
        cell.DetailTextLabel.Text = server.ServerUrl;
        cell.DetailTextLabel.TextColor = UIColor.Gray;

        return cell;
    }

    public override void RowSelected(UITableView tableView, NSIndexPath indexPath)
    {
        tableView.DeselectRow(indexPath, true);

        // 点击某服务器，进入“浏览器”
        var selectedServer = _servers[indexPath.Row];
        var browserVC = new WebDavBrowserViewController(selectedServer, "/"); // 进入根目录
        NavigationController.PushViewController(browserVC, true);
    }

    // 删除功能
    public override bool CanEditRow(UITableView tableView, NSIndexPath indexPath)
    {
        return true;
    }

    public override void CommitEditingStyle(UITableView tableView, 
        UITableViewCellEditingStyle editingStyle, NSIndexPath indexPath)
    {
        if (editingStyle == UITableViewCellEditingStyle.Delete)
        {
            _servers.RemoveAt(indexPath.Row);
            SaveWebDavServers(_servers);
            tableView.DeleteRows(new[] { indexPath }, UITableViewRowAnimation.Automatic);
        }
    }

    /// <summary>
    /// 弹出对话框，用于新增一个 WebDavServer
    /// </summary>
    private void ShowAddServerDialog()
    {
        var alert = UIAlertController.Create("添加 WebDAV 服务器", null, UIAlertControllerStyle.Alert);

        // 服务器名称
        alert.AddTextField((field) =>
        {
            field.Placeholder = "服务器名称(自定义)";
        });
        // 服务器URL
        alert.AddTextField((field) =>
        {
            field.Placeholder = "WebDAV 地址(含 https://...)";
        });
        // 用户名
        alert.AddTextField((field) =>
        {
            field.Placeholder = "登录用户名";
        });
        // 密码
        alert.AddTextField((field) =>
        {
            field.Placeholder = "登录密码";
            field.SecureTextEntry = true;
        });

        var addAction = UIAlertAction.Create("添加", UIAlertActionStyle.Default, (action) =>
        {
            var title = alert.TextFields[0].Text?.Trim();
            var url = alert.TextFields[1].Text?.Trim();
            var user = alert.TextFields[2].Text?.Trim();
            var pwd = alert.TextFields[3].Text?.Trim();

            if (!string.IsNullOrEmpty(url))
            {
                if (string.IsNullOrEmpty(title)) title = url;
                var newServer = new WebDavServer
                {
                    Title = title,
                    ServerUrl = url,
                    UserName = user ?? string.Empty,
                    Password = pwd ?? string.Empty
                };
                _servers.Add(newServer);
                SaveWebDavServers(_servers);
                TableView.ReloadData();
            }
        });

        var cancelAction = UIAlertAction.Create("取消", UIAlertActionStyle.Cancel, null);

        alert.AddAction(addAction);
        alert.AddAction(cancelAction);

        PresentViewController(alert, true, null);
    }

    /// <summary>
    /// 从 NSUserDefaults 加载 WebDAV 服务器列表
    /// </summary>
    private List<WebDavServer> LoadWebDavServers()
    {
        var userDefaults = NSUserDefaults.StandardUserDefaults;
        var serialized = userDefaults.StringForKey(WebDavServersStorageKey);
        if (string.IsNullOrEmpty(serialized))
        {
            return new List<WebDavServer>();
        }
        try
        {
            var list = JsonSerializer.Deserialize<List<WebDavServer>>(serialized);
            return list ?? new List<WebDavServer>();
        }
        catch (Exception ex)
        {
            Logger.Log($"Error loading WebDavServers: {ex.Message}");
            return new List<WebDavServer>();
        }
    }

    /// <summary>
    /// 保存 WebDavServers 列表到 NSUserDefaults
    /// </summary>
    private void SaveWebDavServers(List<WebDavServer> servers)
    {
        try
        {
            var serialized = JsonSerializer.Serialize(servers);
            NSUserDefaults.StandardUserDefaults.SetString(serialized, WebDavServersStorageKey);
            NSUserDefaults.StandardUserDefaults.Synchronize();
        }
        catch (Exception ex)
        {
            Logger.Log($"Error saving WebDavServers: {ex.Message}");
        }
    }
}