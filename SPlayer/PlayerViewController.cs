using AVFoundation;
using CoreFoundation;
using CoreMedia;

namespace SPlayer;

public class PlayerViewController : UIViewController
{
    private List<MainViewController.VideoItem> _playlist;
    private int _currentIndex;
    private Dictionary<string, double> _playHistory;

    private AVPlayer _player;
    private AVPlayerLayer _playerLayer;

    // UI 控件
    private UIButton _playPauseButton;
    private UISlider _progressSlider;
    private UILabel _currentTimeLabel;
    private UILabel _durationLabel;
    private UIButton _orientationToggleButton;

    // 播放进度观察
    private NSObject _timeObserver;
    private bool _isSeeking = false;

    public PlayerViewController(
        List<MainViewController.VideoItem> playlist,
        int currentIndex,
        Dictionary<string, double> playHistory)
    {
        _playlist = playlist;
        _currentIndex = currentIndex;
        _playHistory = playHistory;
    }

    public override void ViewDidLoad()
    {
        base.ViewDidLoad();
        View.BackgroundColor = UIColor.Black;

        // 初始化播放器
        InitializePlayer();
        SetupUI();
        LayoutUI();
        UpdateUIState();
    }

    public override void ViewWillDisappear(bool animated)
    {
        base.ViewWillDisappear(animated);

        // 退出时移除进度观察
        if (_timeObserver != null)
        {
            _player.RemoveTimeObserver(_timeObserver);
            _timeObserver = null;
        }

        // 保存播放记录
        SaveCurrentPlayPosition();
        // 强制同步到 UserDefaults
        var mainVC = NavigationController?.ViewControllers
            .FirstOrDefault(vc => vc is MainViewController) as MainViewController;
        mainVC?.SavePlayHistory(_playHistory);
    }

    #region 播放器初始化

    private void InitializePlayer()
    {
        var currentItem = _playlist[_currentIndex];
        NSUrl nsUrl = NSUrl.FromString(currentItem.Url);

        // 如果无法正常转换为网络 URL，则尝试当作本地文件
        if (nsUrl == null || string.IsNullOrEmpty(nsUrl.Scheme) || !nsUrl.Scheme.StartsWith("http"))
        {
            nsUrl = NSUrl.FromFilename(currentItem.Url);
        }

        var playerItem = AVPlayerItem.FromUrl(nsUrl);
        _player = new AVPlayer(playerItem);
        _playerLayer = AVPlayerLayer.FromPlayer(_player);
        _playerLayer.Frame = View.Bounds;
        _playerLayer.VideoGravity = AVLayerVideoGravity.ResizeAspect;
        View.Layer.AddSublayer(_playerLayer);

        // 如果有历史进度，跳转
        if (_playHistory.ContainsKey(currentItem.Url))
        {
            var lastTime = _playHistory[currentItem.Url];
            var cmTime = CMTime.FromSeconds(lastTime, 1);
            playerItem.Seek(cmTime);
        }

        // 添加播放进度观察
        _timeObserver = _player.AddPeriodicTimeObserver(
            CMTime.FromSeconds(1, 1),
            DispatchQueue.MainQueue,
            (time) =>
            {
                if (_isSeeking) return;
                UpdateProgressSlider();
            });

        // 监听播放结束事件
        NSNotificationCenter.DefaultCenter.AddObserver(
            AVPlayerItem.DidPlayToEndTimeNotification,
            PlayerItemDidReachEnd,
            playerItem
        );
    }

    #endregion

    #region UI 布局和事件

    private void SetupUI()
    {
        // 播放/暂停
        _playPauseButton = new UIButton(UIButtonType.System);
        _playPauseButton.SetTitle("播放", UIControlState.Normal);
        _playPauseButton.SetTitleColor(UIColor.White, UIControlState.Normal);
        _playPauseButton.TouchUpInside += (sender, e) =>
        {
            if (_player.TimeControlStatus == AVPlayerTimeControlStatus.Paused)
            {
                // 如果已经播放到结尾，再次点击播放时，从头开始
                var duration = _player.CurrentItem?.Duration.Seconds;
                var currentTime = _player.CurrentTime.Seconds;
                if (duration.HasValue && Math.Abs(duration.Value - currentTime) < 0.1)
                {
                    // 已到末尾，回到开头
                    _player.Seek(CMTime.Zero);
                }

                _player.Play();
            }
            else
            {
                _player.Pause();
            }

            UpdateUIState();
        };

        // 播放进度条
        _progressSlider = new UISlider
        {
            MinValue = 0f,
            MaxValue = 1f,
            Value = 0f
        };
        _progressSlider.TouchDown += (s, e) => { _isSeeking = true; };
        _progressSlider.TouchUpInside += (s, e) =>
        {
            _isSeeking = false;
            SeekToSliderPosition(_progressSlider.Value);
        };
        _progressSlider.TouchCancel += (s, e) => { _isSeeking = false; };

        // 时间标签
        _currentTimeLabel = new UILabel
        {
            Text = "00:00",
            TextColor = UIColor.White,
            TextAlignment = UITextAlignment.Center
        };
        _durationLabel = new UILabel
        {
            Text = "00:00",
            TextColor = UIColor.White,
            TextAlignment = UITextAlignment.Center
        };

        // 横竖屏切换
        _orientationToggleButton = new UIButton(UIButtonType.System);
        _orientationToggleButton.SetTitle("横/竖屏", UIControlState.Normal);
        _orientationToggleButton.SetTitleColor(UIColor.White, UIControlState.Normal);
        _orientationToggleButton.TouchUpInside += (sender, e) => { ToggleOrientation(); };

        View.AddSubview(_playPauseButton);
        View.AddSubview(_progressSlider);
        View.AddSubview(_currentTimeLabel);
        View.AddSubview(_durationLabel);
        View.AddSubview(_orientationToggleButton);
    }

    private void LayoutUI()
    {
        nfloat bottomPadding = 60;
        var viewWidth = View.Bounds.Width;
        var viewHeight = View.Bounds.Height;

        // 播放/暂停按钮
        _playPauseButton.Frame = new CGRect(20, viewHeight - bottomPadding - 40, 60, 40);
        // 当前时间
        _currentTimeLabel.Frame = new CGRect(90, viewHeight - bottomPadding - 40, 60, 40);
        // 总时长
        _durationLabel.Frame = new CGRect(viewWidth - 80, viewHeight - bottomPadding - 40, 60, 40);
        // 播放进度条
        _progressSlider.Frame = new CGRect(
            x: _currentTimeLabel.Frame.Right + 10,
            y: viewHeight - bottomPadding - 30,
            width: (viewWidth - 80) - (_currentTimeLabel.Frame.Right + 10) - 10,
            height: 20);
        // 横竖屏按钮
        _orientationToggleButton.Frame = new CGRect(
            viewWidth - 140,
            20,
            120,
            40
        );

        _playPauseButton.AutoresizingMask =
            UIViewAutoresizing.FlexibleTopMargin | UIViewAutoresizing.FlexibleRightMargin;
        _progressSlider.AutoresizingMask =
            UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleTopMargin;
        _currentTimeLabel.AutoresizingMask =
            UIViewAutoresizing.FlexibleTopMargin | UIViewAutoresizing.FlexibleRightMargin;
        _durationLabel.AutoresizingMask =
            UIViewAutoresizing.FlexibleTopMargin | UIViewAutoresizing.FlexibleLeftMargin;
        _orientationToggleButton.AutoresizingMask =
            UIViewAutoresizing.FlexibleBottomMargin | UIViewAutoresizing.FlexibleLeftMargin;
    }

    private void UpdateUIState()
    {
        if (_player == null) return;
        if (_player.TimeControlStatus == AVPlayerTimeControlStatus.Paused)
        {
            _playPauseButton.SetTitle("播放", UIControlState.Normal);
        }
        else
        {
            _playPauseButton.SetTitle("暂停", UIControlState.Normal);
        }

        UpdateProgressSlider();
    }

    #endregion

    #region 播放操作

    private void PlayerItemDidReachEnd(NSNotification notification)
    {
        // 播放结束时，将按钮恢复为“播放”
        _player.Pause();
        _playPauseButton.SetTitle("播放", UIControlState.Normal);
        Logger.Log("Video playback ended.");
    }

    private void SeekToSliderPosition(float value)
    {
        if (_player?.CurrentItem == null) return;
        var durationSeconds = _player.CurrentItem.Duration.Seconds;
        if (durationSeconds <= 0) return;
        var targetTime = durationSeconds * value;
        var cmTime = CMTime.FromSeconds(targetTime, 1);
        _player.Seek(cmTime);
    }

    private void UpdateProgressSlider()
    {
        if (_player?.CurrentItem == null) return;
        var currentTime = _player.CurrentTime.Seconds;
        var duration = _player.CurrentItem.Duration.Seconds;
        if (double.IsNaN(duration) || duration <= 0)
        {
            _progressSlider.Value = 0;
            _currentTimeLabel.Text = "00:00";
            _durationLabel.Text = "00:00";
            return;
        }

        _progressSlider.Value = (float)(currentTime / duration);
        _currentTimeLabel.Text = FormatTime(currentTime);
        _durationLabel.Text = FormatTime(duration);
    }

    private void SaveCurrentPlayPosition()
    {
        if (_player?.CurrentItem == null) return;
        var currentItem = _playlist[_currentIndex];
        var currentTime = _player.CurrentTime.Seconds;
        if (currentTime >= 0)
        {
            _playHistory[currentItem.Url] = currentTime;
        }
    }

    #endregion

    #region 工具方法

    private string FormatTime(double time)
    {
        if (double.IsNaN(time) || time < 0) return "00:00";
        var ts = TimeSpan.FromSeconds(time);
        return $"{(int)ts.TotalMinutes:D2}:{ts.Seconds:D2}";
    }

    /// <summary>
    /// 横竖屏切换
    /// </summary>
    private void ToggleOrientation()
    {
        var currentOrientation = UIApplication.SharedApplication.StatusBarOrientation;
        if (currentOrientation == UIInterfaceOrientation.Portrait ||
            currentOrientation == UIInterfaceOrientation.PortraitUpsideDown)
        {
            // 切到横屏
            var value = UIInterfaceOrientation.LandscapeRight;
            UIDevice.CurrentDevice.SetValueForKey(NSNumber.FromInt32((int)value),
                new NSString("orientation"));
        }
        else
        {
            // 切到竖屏
            var value = UIInterfaceOrientation.Portrait;
            UIDevice.CurrentDevice.SetValueForKey(NSNumber.FromInt32((int)value),
                new NSString("orientation"));
        }
    }

    #endregion
}