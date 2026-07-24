namespace HITAPEX.Models;

/// <summary>
/// 首页横幅项，包含图片地址和点击跳转链接。
/// </summary>
public class BannerItem
{
    /// <summary>横幅图片的 URL 地址</summary>
    public string ImageUrl { get; set; } = "";

    /// <summary>点击横幅后的跳转链接</summary>
    public string LinkUrl { get; set; } = "";
}
