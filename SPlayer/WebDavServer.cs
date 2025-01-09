namespace SPlayer;

/// <summary>
/// 用于存储 WebDAV 服务器信息的数据结构
/// </summary>
public class WebDavServer
{
    public string Title { get; set; }       // 给这个服务器起一个名称
    public string ServerUrl { get; set; }   // WebDAV 基础 URL，如 https://xxx/dav
    public string UserName { get; set; }    // 登录用户名
    public string Password { get; set; }    // 登录密码
}