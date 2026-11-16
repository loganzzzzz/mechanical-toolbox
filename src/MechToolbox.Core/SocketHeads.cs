using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MechToolbox.Core;

public sealed record SocketHeadSize(decimal Diameter, decimal SmoothDiameterMax,
    decimal KnurledDiameterMax, decimal HeightMax, decimal SocketNominal, decimal SocketDepthMin)
{
    public string SmoothText => Format.Diameter(SmoothDiameterMax);
    public string HeightText => Format.Number(HeightMax);
    public string SocketText => Format.Number(SocketNominal);
    public string DetailText => $"滚花头最大外径 {Format.Diameter(KnurledDiameterMax)}；内六角深度 t ≥ {Format.Number(SocketDepthMin)} mm。";
}

public sealed record SocketHeadDataset(int SchemaVersion, string DataVersion, string UpdatedOn,
    StandardSource Source, IReadOnlyList<SocketHeadSize> Sizes);

public sealed class JsonSocketHeadDataProvider : IStandardDataProvider<SocketHeadDataset>
{
    public SocketHeadDataset Load(string path) => Parse(File.ReadAllText(path));
    public SocketHeadDataset Parse(string json)
    {
        var data = JsonSerializer.Deserialize<SocketHeadDataset>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
        }) ?? throw new InvalidDataException("内六角头部数据为空。");
        if (data.SchemaVersion != 1 || string.IsNullOrWhiteSpace(data.DataVersion) ||
            !DateOnly.TryParseExact(data.UpdatedOn, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _) ||
            data.Source is null || string.IsNullOrWhiteSpace(data.Source.Id) || string.IsNullOrWhiteSpace(data.Source.Title) ||
            string.IsNullOrWhiteSpace(data.Source.Scope) || !Uri.TryCreate(data.Source.Url, UriKind.Absolute, out var uri) || uri.Scheme != "https" ||
            data.Sizes is null || data.Sizes.Count == 0 || data.Sizes.Any(x => x is null) ||
            data.Sizes.Select(x => x.Diameter).Distinct().Count() != data.Sizes.Count)
            throw new InvalidDataException("内六角头部数据版本、来源或规格无效。");
        foreach (var s in data.Sizes)
            if (s.Diameter <= 0 || s.SmoothDiameterMax <= s.Diameter || s.KnurledDiameterMax < s.SmoothDiameterMax ||
                s.HeightMax <= 0 || s.SocketNominal <= 0 || s.SocketNominal >= s.SmoothDiameterMax ||
                s.SocketDepthMin <= 0 || s.SocketDepthMin > s.HeightMax)
                throw new InvalidDataException("内六角头部尺寸无效。");
        return data with { Sizes = data.Sizes.OrderBy(x => x.Diameter).ToArray() };
    }
}
