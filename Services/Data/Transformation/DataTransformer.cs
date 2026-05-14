using HITAPEX.Models;
using HITAPEX.Services.Data.Models;

namespace HITAPEX.Services.Data.Transformation;

public class DataTransformer
{
    private readonly string _baseUrl;

    public DataTransformer(string baseUrl)
    {
        _baseUrl = baseUrl.TrimEnd('/');
    }

    public GameItem TransformGame(GameApiDto dto)
    {
        return new GameItem
        {
            Id = dto.Id,
            Name = dto.Name,
            Description = dto.Description,
            SteamId = dto.SteamId,
            CoverImageUrl = BuildFullUrl(dto.CoverImage?.Url),
            BgImageUrl = BuildFullUrl(dto.BgImage?.Url),
            ImagePath = BuildFullUrl(dto.CoverImage?.Url) ?? "/Assets/Rectangle 24845.png",
            IsInstalled = false,
            IsPinned = false
        };
    }

    public List<GameItem> TransformGames(IEnumerable<GameApiDto> dtos)
    {
        return dtos.Select(TransformGame).ToList();
    }

    private string? BuildFullUrl(string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            return null;

        return relativePath.StartsWith("http")
            ? relativePath
            : $"{_baseUrl}{relativePath}";
    }
}
