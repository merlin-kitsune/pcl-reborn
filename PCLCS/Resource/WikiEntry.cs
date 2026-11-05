namespace PCLCS;

/// <summary>
/// MC 百科条目。
/// <para/>使用 <see cref="WikiEntry.All"/> 获取所有条目。
/// </summary>
public class WikiEntry {

    /// <summary>
    /// 在 MC 百科中的对应 ID。
    /// </summary>
    public int Id;
    /// <summary>
    /// 中文译名。
    /// <c>null</c> 代表没有翻译。
    /// </summary>
    public string? ChineseName;
    /// <summary>
    /// 各个 Mod 平台的 Slug（例如 advanced-solar-panels）。
    /// 若没有对应键则为不在该平台上。
    /// </summary>
    public Dictionary<ResourcePlatforms, string> Slugs = [];
    /// <summary>
    /// MC 百科的浏览量逆序排行，1 代表浏览量最低。
    /// </summary>
    public int Popularity;

    /// <summary>
    /// 内置数据库中的所有 MC 百科条目。
    /// </summary>
    public static readonly Lazy<List<WikiEntry>> All = new(() => {
        var dataLines = FileUtils.ReadAsLines(@"Resource\WikiEntries.txt", type: typeof(WikiEntry)).ToList();

        // 读取最后一行的浏览量
        Queue<int> popularities = [];
        for (int i = 0; i < dataLines[^1].Length; i += 3) { // 将每 3 个字符切割成一个元素
            popularities.Enqueue(int.Parse(dataLines[^1].Substring(i, 3).ConvertRadix(86, 10)));
        }
        dataLines.RemoveAt(dataLines.Count - 1);

        // 解析每一行
        List<WikiEntry> results = [];
        var lineNumber = 0;
        foreach (var lineData in dataLines) {
            lineNumber++;
            if (lineData == "") continue;

            var popularity = popularities.Dequeue();
            foreach (var entryData in lineData.Split('¨')) {
                var entry = new WikiEntry();
                var parts = entryData.Split('|');
                var slugs = parts[0];
                if (slugs.StartsWithF("@")) {
                    entry.Slugs.Add(ResourcePlatforms.Modrinth, slugs.Replace("@", ""));
                } else if (slugs.EndsWithF("@")) {
                    entry.Slugs.Add(ResourcePlatforms.CurseForge, slugs.TrimEnd('@'));
                    entry.Slugs.Add(ResourcePlatforms.Modrinth, entry.Slugs[ResourcePlatforms.CurseForge]);
                } else if (slugs.Contains("@")) {
                    var splitSlugs = slugs.Split('@');
                    entry.Slugs.Add(ResourcePlatforms.CurseForge, splitSlugs[0]);
                    entry.Slugs.Add(ResourcePlatforms.Modrinth, splitSlugs[1]);
                } else {
                    entry.Slugs.Add(ResourcePlatforms.CurseForge, slugs);
                }

                entry.Id = lineNumber;
                entry.Popularity = popularity;
                if (parts.Length >= 2) {
                    entry.ChineseName = parts[^1]; // 最后一项
                    if (entry.ChineseName.Contains("*")) { // 处理 *
                        var englishName = entry.Slugs.Values.First().Replace("-", " ").Capitalize();
                        entry.ChineseName = entry.ChineseName.Replace("*", $" ({englishName})");
                    }
                }
                results.Add(entry);
            }
        }
        ApplySupplement(results);
        return results;
    });

    /// <summary>
    /// 启动器从 GitHub 同步下来的补充条目文件路径（TSV 格式，每行 <c>ID \t CurseForge Slug \t Modrinth Slug \t 中文名</c>，<c>#</c> 开头为注释）。
    /// <para/>需要在首次访问 <see cref="All"/> 之前赋值；为 <c>null</c> 或文件不存在时直接忽略。
    /// </summary>
    public static string? SupplementPath;

    /// <summary>
    /// 将补充文件中的条目并入内置数据库。已有的条目（同 ID 或同名 Slug）不会被覆盖。
    /// </summary>
    private static void ApplySupplement(List<WikiEntry> results) {
        var path = SupplementPath;
        if (string.IsNullOrWhiteSpace(path)) return;
        string[] lines;
        try {
            if (!File.Exists(path)) return;
            lines = File.ReadAllLines(path, Encoding.UTF8);
        } catch {
            return; // 补充数据只是锦上添花，读取失败不影响内置数据库
        }

        var knownSlugs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var knownIds = new HashSet<int>();
        foreach (var entry in results) {
            knownIds.Add(entry.Id);
            foreach (var slug in entry.Slugs.Values) {
                if (!string.IsNullOrEmpty(slug)) knownSlugs.Add(slug);
            }
        }

        foreach (var rawLine in lines) {
            var line = rawLine.Trim();
            if (line.Length == 0 || line[0] == '#') continue;
            var parts = line.Split('\t');
            if (parts.Length < 4) continue;
            if (!int.TryParse(parts[0].Trim(), out var id) || id <= 0) continue;
            var curseForgeSlug = parts[1].Trim();
            var modrinthSlug = parts[2].Trim();
            var chineseName = parts[3].Trim();
            if (chineseName.Length == 0) continue;
            if (curseForgeSlug.Length == 0 && modrinthSlug.Length == 0) continue;
            if (knownIds.Contains(id)) continue;
            if ((curseForgeSlug.Length > 0 && knownSlugs.Contains(curseForgeSlug))
                || (modrinthSlug.Length > 0 && knownSlugs.Contains(modrinthSlug))) continue;

            var supplement = new WikiEntry { Id = id, ChineseName = chineseName, Popularity = 1 };
            if (curseForgeSlug.Length > 0) supplement.Slugs.Add(ResourcePlatforms.CurseForge, curseForgeSlug);
            if (modrinthSlug.Length > 0) supplement.Slugs.Add(ResourcePlatforms.Modrinth, modrinthSlug);
            results.Add(supplement);
            knownIds.Add(id);
            if (curseForgeSlug.Length > 0) knownSlugs.Add(curseForgeSlug);
            if (modrinthSlug.Length > 0) knownSlugs.Add(modrinthSlug);
        }
    }

    public override string ToString()
        => $"{Slugs.GetOrDefault(ResourcePlatforms.CurseForge)}&{Slugs.GetOrDefault(ResourcePlatforms.Modrinth)}|{Id}|{ChineseName}，浏览量 {Popularity}";
}