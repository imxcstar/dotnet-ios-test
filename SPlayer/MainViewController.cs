using System.Text.Json;
using System.Text.Json.Serialization;
using AVFoundation;
using CoreFoundation;
using CoreMedia;
using MobileCoreServices;

namespace SPlayer;

/// <summary>
/// 主页面：包含播放列表、添加视频、以及进入 WebDav 管理等操作
/// </summary>
public class MainViewController : UIViewController
{
    // 播放列表
    private List<VideoItem> _playlist;

    // 存储上次播放进度（key = 视频 Url, value = 秒数）
    private Dictionary<string, double> _playHistory;

    // 用于显示播放列表的表格
    private UITableView _tableView;

    // 右上角按钮：添加视频
    private UIBarButtonItem _addButton;

    // 原先的 WebDAV 浏览按钮，现在改为“管理 WebDAV”
    private UIBarButtonItem _webDavButton;

    // 播放列表本地存储 Key
    private const string PlaylistStorageKey = "VideoPlaylistStorageKey";

    // 播放进度本地存储 Key
    private const string PlayHistoryStorageKey = "PlayHistoryStorageKey";

    // 用于文件选择
    private UIDocumentPickerViewController _documentPicker;

    public override void ViewDidLoad()
    {
        base.ViewDidLoad();
        Title = "SPlayer";
        View.BackgroundColor = UIColor.White;

        // 初始化数据
        _playlist = LoadPlaylist();
        _playHistory = LoadPlayHistory();

        // 设置右上角的添加按钮
        _addButton = new UIBarButtonItem(UIBarButtonSystemItem.Add, (sender, e) => ShowAddOptionDialog());

        // 新增：WebDAV 按钮 -> 进入 WebDavListViewController
        _webDavButton = new UIBarButtonItem("管理 WebDAV", UIBarButtonItemStyle.Plain, (sender, e) =>
        {
            var listVC = new WebDavListViewController();
            NavigationController.PushViewController(listVC, true);
        });

        // 把按钮放在右上角，形成一个按钮组
        NavigationItem.RightBarButtonItems = new[] { _addButton, _webDavButton };

        // 初始化 TableView
        _tableView = new UITableView(View.Bounds)
        {
            AutoresizingMask = UIViewAutoresizing.FlexibleDimensions,
            Source = new PlaylistTableSource(_playlist, this)
        };
        View.AddSubview(_tableView);
    }

    /// <summary>
    /// 弹出选择对话框：本地视频 or 网络视频
    /// </summary>
    private void ShowAddOptionDialog()
    {
        var alert = UIAlertController.Create("添加视频", "请选择添加方式", UIAlertControllerStyle.ActionSheet);

        var localAction = UIAlertAction.Create("本地视频", UIAlertActionStyle.Default, action => { PickLocalVideo(); });

        var onlineAction =
            UIAlertAction.Create("网络视频", UIAlertActionStyle.Default, action => { ShowAddNetworkVideoDialog(); });

        var cancelAction = UIAlertAction.Create("取消", UIAlertActionStyle.Cancel, null);

        alert.AddAction(localAction);
        alert.AddAction(onlineAction);
        alert.AddAction(cancelAction);

        PresentViewController(alert, true, null);
    }

    /// <summary>
    /// 弹出对话框，添加网络视频地址
    /// </summary>
    private void ShowAddNetworkVideoDialog()
    {
        var alert = UIAlertController.Create("添加网络视频", "请输入视频标题和URL", UIAlertControllerStyle.Alert);

        // 标题 (可选)
        alert.AddTextField((field) => { field.Placeholder = "标题(可选)"; });

        // 视频URL
        alert.AddTextField((field) => { field.Placeholder = "输入视频的网络URL"; });

        var addAction = UIAlertAction.Create("添加", UIAlertActionStyle.Default, (action) =>
        {
            string title = alert.TextFields[0].Text;
            string url = alert.TextFields[1].Text;

            if (!string.IsNullOrWhiteSpace(url))
            {
                if (string.IsNullOrWhiteSpace(title))
                {
                    title = url; // 没有输入标题就用url
                }

                var videoItem = new VideoItem
                {
                    Title = title,
                    Url = url
                };
                _playlist.Add(videoItem);
                SavePlaylist(_playlist);
                _tableView.ReloadData();
            }
        });

        var cancelAction = UIAlertAction.Create("取消", UIAlertActionStyle.Cancel, null);

        alert.AddAction(addAction);
        alert.AddAction(cancelAction);

        PresentViewController(alert, true, null);
    }

    /// <summary>
    /// 选择本地视频
    /// </summary>
    private void PickLocalVideo()
    {
        var allowedTypes = new string[] { UTType.Movie };
        _documentPicker = new UIDocumentPickerViewController(allowedTypes, UIDocumentPickerMode.Open);
        _documentPicker.DidPickDocument += (s, e) =>
        {
            var securityScoped = e.Url.StartAccessingSecurityScopedResource();
            try
            {
                var path = e.Url.Path;
                var fileName = e.Url.LastPathComponent;

                var videoItem = new VideoItem
                {
                    Title = fileName,
                    Url = path
                };
                _playlist.Add(videoItem);
                SavePlaylist(_playlist);
                _tableView.ReloadData();
            }
            catch (Exception ex)
            {
                Logger.Log($"Error picking local video: {ex.Message}");
            }
            finally
            {
                if (securityScoped)
                    e.Url.StopAccessingSecurityScopedResource();
            }
        };
        _documentPicker.ModalPresentationStyle = UIModalPresentationStyle.FormSheet;

        PresentViewController(_documentPicker, true, null);
    }

    /// <summary>
    /// 从其他界面回调，往播放列表添加视频，并刷新
    /// </summary>
    public void AddVideoItemAndRefresh(VideoItem item)
    {
        _playlist.Add(item);
        SavePlaylist(_playlist);
        _tableView.ReloadData();
    }

    /// <summary>
    /// 进入播放器页面播放指定视频
    /// </summary>
    public void PlayVideoAtIndex(int index)
    {
        if (index < 0 || index >= _playlist.Count) return;

        var playerVC = new PlayerViewController(_playlist, index, _playHistory);
        NavigationController.PushViewController(playerVC, true);
    }

    #region 播放列表的加载与保存

    private List<VideoItem> LoadPlaylist()
    {
        var userDefaults = NSUserDefaults.StandardUserDefaults;
        var serialized = userDefaults.StringForKey(PlaylistStorageKey);
        if (string.IsNullOrEmpty(serialized))
        {
            return new List<VideoItem>();
        }

        try
        {
            var data = JsonSerializer.Deserialize<List<VideoItem>>(serialized);
            return data ?? new List<VideoItem>();
        }
        catch (Exception ex)
        {
            Logger.Log($"Error loading playlist: {ex.Message}");
            return new List<VideoItem>();
        }
    }

    private void SavePlaylist(List<VideoItem> playlist)
    {
        var userDefaults = NSUserDefaults.StandardUserDefaults;
        try
        {
            var serialized = JsonSerializer.Serialize(playlist);
            userDefaults.SetString(serialized, PlaylistStorageKey);
            userDefaults.Synchronize();
        }
        catch (Exception ex)
        {
            Logger.Log($"Error saving playlist: {ex.Message}");
        }
    }

    #endregion

    #region 播放历史记录的加载与保存

    private Dictionary<string, double> LoadPlayHistory()
    {
        var userDefaults = NSUserDefaults.StandardUserDefaults;
        var serialized = userDefaults.StringForKey(PlayHistoryStorageKey);
        if (string.IsNullOrEmpty(serialized))
        {
            return new Dictionary<string, double>();
        }

        try
        {
            var data = JsonSerializer.Deserialize<Dictionary<string, double>>(serialized);
            return data ?? new Dictionary<string, double>();
        }
        catch (Exception ex)
        {
            Logger.Log($"Error loading play history: {ex.Message}");
            return new Dictionary<string, double>();
        }
    }

    public void SavePlayHistory(Dictionary<string, double> history)
    {
        var userDefaults = NSUserDefaults.StandardUserDefaults;
        try
        {
            var serialized = JsonSerializer.Serialize(history);
            userDefaults.SetString(serialized, PlayHistoryStorageKey);
            userDefaults.Synchronize();
        }
        catch (Exception ex)
        {
            Logger.Log($"Error saving play history: {ex.Message}");
        }
    }

    #endregion

    /// <summary>
    /// 自定义 UITableViewSource，用于播放列表显示和点击处理
    /// </summary>
    private class PlaylistTableSource : UITableViewSource
    {
        private readonly List<VideoItem> _items;
        private readonly MainViewController _owner;
        private const string CellIdentifier = "PlaylistCell";

        public PlaylistTableSource(List<VideoItem> items, MainViewController owner)
        {
            _items = items;
            _owner = owner;
        }

        public override nint RowsInSection(UITableView tableview, nint section)
        {
            return _items.Count;
        }

        public override UITableViewCell GetCell(UITableView tableView, NSIndexPath indexPath)
        {
            var cell = tableView.DequeueReusableCell(CellIdentifier)
                       ?? new UITableViewCell(UITableViewCellStyle.Subtitle, CellIdentifier);

            var item = _items[indexPath.Row];

            // 设置标题和URL
            cell.TextLabel.Text = item.Title;
            cell.DetailTextLabel.Text = item.Url;
            cell.DetailTextLabel.TextColor = UIColor.Gray;

            // 如果还没生成过缩略图，尝试自动获取并缓存
            if (item.Thumbnail == null)
            {
                // 异步获取首帧图，避免阻塞 UI
                DispatchQueue.GetGlobalQueue(DispatchQueuePriority.Background).DispatchAsync(() =>
                {
                    try
                    {
                        NSUrl nsUrl = null;
                        if (item.Url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                        {
                            nsUrl = NSUrl.FromString(item.Url);
                        }
                        else
                        {
                            // 本地文件
                            nsUrl = NSUrl.FromFilename(item.Url);
                        }

                        if (nsUrl == null) return;

                        var asset = AVAsset.FromUrl(nsUrl);
                        if (asset == null) return;

                        var generator = AVAssetImageGenerator.FromAsset(asset);
                        generator.AppliesPreferredTrackTransform = true;

                        CMTime actualTime;
                        NSError error;
                        using var imageRef = generator.CopyCGImageAtTime(new CMTime(1, 1), out actualTime, out error);
                        if (imageRef != null && error == null)
                        {
                            var uiImage = UIImage.FromImage(imageRef);
                            DispatchQueue.MainQueue.DispatchAsync(() =>
                            {
                                item.Thumbnail = uiImage;
                                // 更新对应 cell
                                var currentCell = tableView.CellAt(indexPath);
                                if (currentCell != null)
                                {
                                    currentCell.ImageView.Image = uiImage;
                                    currentCell.SetNeedsLayout();
                                }
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Log($"Error generating thumbnail: {ex.Message}");
                    }
                });

                // 先用占位图
                cell.ImageView.Image = UIImage.FromBundle("placeholder.png");
            }
            else
            {
                // 已有缓存图
                cell.ImageView.Image = item.Thumbnail;
            }

            return cell;
        }

        public override void RowSelected(UITableView tableView, NSIndexPath indexPath)
        {
            tableView.DeselectRow(indexPath, true);
            _owner.PlayVideoAtIndex(indexPath.Row);
        }

        // 支持删除功能
        public override bool CanEditRow(UITableView tableView, NSIndexPath indexPath)
        {
            return true;
        }

        public override void CommitEditingStyle(UITableView tableView,
            UITableViewCellEditingStyle editingStyle, NSIndexPath indexPath)
        {
            if (editingStyle == UITableViewCellEditingStyle.Delete)
            {
                _items.RemoveAt(indexPath.Row);
                // 更新存储
                _owner.SavePlaylist(_items);
                tableView.DeleteRows(new[] { indexPath }, UITableViewRowAnimation.Automatic);
            }
        }
    }

    /// <summary>
    /// 播放列表 Item
    /// </summary>
    public class VideoItem
    {
        /// <summary>
        /// 视频标题
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// 视频 URL（本地路径或网络地址）
        /// </summary>
        public string Url { get; set; }

        /// <summary>
        /// 视频缩略图（自动获取后缓存）
        /// </summary>
        [JsonIgnore]
        public UIImage Thumbnail { get; set; }
    }
}