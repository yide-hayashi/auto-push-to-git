using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using Xunit;

namespace Project_auto_push_to_gitlab.SqlOptimizer.Tests;

public sealed class SqlOptimizerUserGuideAcceptanceTests
{
    private const string JapaneseSectionHeading = "第I部 日本語版";
    private const string TraditionalChineseSectionHeading = "第II部 台灣華語版";

    private const string GuideFileName =
        "AI_SQL最適化_操作説明書_日台版.docx";
    private const string GuideRelativePath =
        "docs/" + GuideFileName;

    private static readonly string[] SimplifiedChineseForbiddenTerms =
    [
        "优化",
        "数据库",
        "执行",
        "用户",
        "设置",
        "连接",
        "结果",
        "显示",
        "说明",
        "错误",
        "权限",
        "计划",
        "绑定",
        "输入",
        "保存",
        "环境",
        "启动",
        "服务器",
        "进程",
        "只读",
        "继续",
        "状态",
        "统计",
        "选择",
        "创建",
        "获取",
        "点击"
    ];

    private static readonly string[] RequiredFigureLabels =
    [
        "図2-1 Draft入力例（実際のfrm_SqlOptimizer）",
        "図7-1 AwaitingUser結果例（実際のfrm_SqlOptimizer）",
        "図6-1 日本語フロー",
        "圖6-1 台灣華語流程"
    ];

    [Fact]
    public void Guide_IsCopiedToApplicationOutput()
    {
        string guidePath = GetOutputGuidePath();

        Assert.True(
            File.Exists(guidePath),
            $"The SQL optimizer guide was not copied to '{guidePath}'.");
        Assert.True(
            new FileInfo(guidePath).Length > 0,
            "The copied SQL optimizer guide is empty.");
    }

    [Fact]
    public void Project_ConfiguresGuideForBuildAndPublishCopy()
    {
        string projectPath = FindFileUpward(
            AppContext.BaseDirectory,
            "Project auto push to gitlab.csproj");
        XDocument project = XDocument.Load(projectPath);

        XElement guideItem = project
            .Descendants()
            .SingleOrDefault(element =>
                element.Name.LocalName == "None" &&
                NormalizePath((string?)element.Attribute("Update")) ==
                GuideRelativePath)
            ?? throw new InvalidDataException(
                $"The project does not configure '{GuideRelativePath}'.");

        Assert.Equal(
            "PreserveNewest",
            ReadChildValue(guideItem, "CopyToOutputDirectory"));
        Assert.Equal(
            "PreserveNewest",
            ReadChildValue(guideItem, "CopyToPublishDirectory"));
    }

    [Fact]
    public void Guide_IsBilingualIllustratedMacroFreeOpcPackage_WithRequiredWarnings()
    {
        string guidePath = GetOutputGuidePath();
        Assert.True(
            File.Exists(guidePath),
            $"The SQL optimizer guide was not copied to '{guidePath}'.");

        using FileStream stream = File.OpenRead(guidePath);
        using ZipArchive package = new(stream, ZipArchiveMode.Read);

        ZipArchiveEntry contentTypesEntry = RequireEntry(
            package,
            "[Content_Types].xml");
        ZipArchiveEntry packageRelationshipsEntry = RequireEntry(
            package,
            "_rels/.rels");
        ZipArchiveEntry documentEntry = RequireEntry(
            package,
            "word/document.xml");
        ZipArchiveEntry documentRelationshipsEntry = RequireEntry(
            package,
            "word/_rels/document.xml.rels");

        AssertMacroFree(package, contentTypesEntry, documentRelationshipsEntry);
        AssertOfficeDocumentRelationship(packageRelationshipsEntry);
        AssertNoExternalRelationships(package);

        XDocument document = LoadXml(documentEntry);
        string documentText = ExtractDocumentText(document);
        AssertBilingualBody(documentText);
        AssertRequiredWarnings(documentText);
        AssertTraditionalChinese(documentText);
        AssertLanguageMetadata(package);
        AssertEastAsianFonts(package);
        AssertIllustrations(package, document, documentRelationshipsEntry);
    }

    private static void AssertMacroFree(
        ZipArchive package,
        ZipArchiveEntry contentTypesEntry,
        ZipArchiveEntry documentRelationshipsEntry)
    {
        string[] entryNames = package.Entries
            .Select(entry => entry.FullName)
            .ToArray();
        Assert.DoesNotContain(
            entryNames,
            name => name.EndsWith(
                "vbaProject.bin",
                StringComparison.OrdinalIgnoreCase));

        string contentTypes = ReadText(contentTypesEntry);
        Assert.Contains(
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml",
            contentTypes,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "macroEnabled",
            contentTypes,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            "vbaProject",
            contentTypes,
            StringComparison.OrdinalIgnoreCase);

        string relationships = ReadText(documentRelationshipsEntry);
        Assert.DoesNotContain(
            "vbaProject",
            relationships,
            StringComparison.OrdinalIgnoreCase);
    }

    private static void AssertOfficeDocumentRelationship(
        ZipArchiveEntry packageRelationshipsEntry)
    {
        XDocument relationships = LoadXml(packageRelationshipsEntry);
        XElement officeDocument = relationships
            .Descendants()
            .SingleOrDefault(element =>
                element.Name.LocalName == "Relationship" &&
                ((string?)element.Attribute("Type"))?.EndsWith(
                    "/officeDocument",
                    StringComparison.Ordinal) == true)
            ?? throw new InvalidDataException(
                "The OPC package does not contain an officeDocument relationship.");

        Assert.Equal(
            "word/document.xml",
            NormalizePath((string?)officeDocument.Attribute("Target")));
    }

    private static void AssertNoExternalRelationships(ZipArchive package)
    {
        foreach (ZipArchiveEntry relationshipEntry in package.Entries.Where(
                     entry => entry.FullName.EndsWith(
                         ".rels",
                         StringComparison.OrdinalIgnoreCase)))
        {
            XDocument relationships = LoadXml(relationshipEntry);
            XElement[] externalRelationships = relationships
                .Descendants()
                .Where(element =>
                    element.Name.LocalName == "Relationship" &&
                    string.Equals(
                        (string?)element.Attribute("TargetMode"),
                        "External",
                        StringComparison.OrdinalIgnoreCase))
                .ToArray();

            Assert.True(
                externalRelationships.Length == 0,
                $"The guide contains an external OPC relationship in " +
                $"'{relationshipEntry.FullName}'. All images, templates, and " +
                "other resources must be embedded.");
        }
    }

    private static void AssertBilingualBody(string documentText)
    {
        Assert.Contains(
            JapaneseSectionHeading,
            documentText,
            StringComparison.Ordinal);
        Assert.Contains("AI SQL最適化", documentText, StringComparison.Ordinal);
        Assert.Contains("使い方", documentText, StringComparison.Ordinal);
        Assert.Contains(
            TraditionalChineseSectionHeading,
            documentText,
            StringComparison.Ordinal);
        Assert.Equal(
            1,
            CountOccurrences(documentText, JapaneseSectionHeading));
        Assert.Equal(
            1,
            CountOccurrences(documentText, TraditionalChineseSectionHeading));
        AssertContainsAny(documentText, "AI SQL最佳化", "AI SQL 最佳化");
        AssertContainsAny(documentText, "使用說明", "操作說明");

        int japaneseStart = documentText.IndexOf(
            JapaneseSectionHeading,
            StringComparison.Ordinal);
        int traditionalChineseStart = documentText.IndexOf(
            TraditionalChineseSectionHeading,
            StringComparison.Ordinal);
        Assert.True(
            japaneseStart >= 0 && traditionalChineseStart > japaneseStart,
            "The Japanese section must precede the zh-TW section.");
    }

    private static void AssertRequiredWarnings(string documentText)
    {
        string japanese = GetLanguageSection(
            documentText,
            JapaneseSectionHeading,
            TraditionalChineseSectionHeading);
        string traditionalChinese = GetLanguageSection(
            documentText,
            TraditionalChineseSectionHeading,
            endMarker: null);

        Assert.Contains("SELECT", japanese, StringComparison.Ordinal);
        Assert.Contains("DDL", japanese, StringComparison.Ordinal);
        Assert.Contains("200", japanese, StringComparison.Ordinal);
        AssertContainsAny(japanese, "全行", "全件");
        Assert.Contains("EXPLAIN PLAN", japanese, StringComparison.Ordinal);
        Assert.Contains("推定", japanese, StringComparison.Ordinal);
        Assert.Contains("バインドピーキング", japanese, StringComparison.Ordinal);
        Assert.Contains("SQL本文", japanese, StringComparison.Ordinal);
        Assert.Contains("バインド値", japanese, StringComparison.Ordinal);
        Assert.Contains("結果行", japanese, StringComparison.Ordinal);
        Assert.Contains("Token", japanese, StringComparison.OrdinalIgnoreCase);
        AssertContainsAny(
            japanese,
            "完全同値",
            "完全同値性",
            "完全な同値性");
        AssertContainsAny(japanese, "ユーザー確認", "利用者確認");

        Assert.Contains("SELECT", traditionalChinese, StringComparison.Ordinal);
        Assert.Contains("DDL", traditionalChinese, StringComparison.Ordinal);
        Assert.Contains("200", traditionalChinese, StringComparison.Ordinal);
        AssertContainsAny(
            traditionalChinese,
            "完整讀取",
            "完整串流讀取",
            "讀取所有",
            "全數讀取");
        Assert.Contains(
            "EXPLAIN PLAN",
            traditionalChinese,
            StringComparison.Ordinal);
        AssertContainsAny(traditionalChinese, "估算", "預估");
        AssertContainsAny(
            traditionalChinese,
            "綁定變數窺探",
            "繫結變數窺探",
            "綁定窺探",
            "bind peeking");
        AssertContainsAny(traditionalChinese, "SQL本文", "SQL 本文");
        AssertContainsAny(traditionalChinese, "綁定值", "繫結值");
        AssertContainsAny(
            traditionalChinese,
            "結果資料列",
            "查詢結果列");
        Assert.Contains(
            "Token",
            traditionalChinese,
            StringComparison.OrdinalIgnoreCase);
        AssertContainsAny(
            traditionalChinese,
            "語意完全等價",
            "語義完全等價",
            "語意一定完全相同",
            "語意一定相同");
        Assert.Contains(
            "使用者確認",
            traditionalChinese,
            StringComparison.Ordinal);
    }

    private static void AssertTraditionalChinese(string documentText)
    {
        string traditionalChinese = GetLanguageSection(
            documentText,
            TraditionalChineseSectionHeading,
            endMarker: null);
        foreach (string forbiddenTerm in SimplifiedChineseForbiddenTerms)
        {
            Assert.DoesNotContain(
                forbiddenTerm,
                traditionalChinese,
                StringComparison.Ordinal);
        }
    }

    private static void AssertLanguageMetadata(ZipArchive package)
    {
        string[] languageValues = LoadWordXmlParts(package)
            .SelectMany(document => document
                .Descendants()
                .Where(element => element.Name.LocalName == "lang"))
            .SelectMany(element => element.Attributes())
            .Where(attribute =>
                attribute.Name.LocalName is "val" or "eastAsia" or "bidi")
            .Select(attribute => attribute.Value)
            .ToArray();

        Assert.Contains(
            languageValues,
            value => string.Equals(
                value,
                "ja-JP",
                StringComparison.OrdinalIgnoreCase));
        Assert.Contains(
            languageValues,
            value => string.Equals(
                value,
                "zh-TW",
                StringComparison.OrdinalIgnoreCase));
    }

    private static void AssertEastAsianFonts(ZipArchive package)
    {
        string[] fontNames = LoadWordXmlParts(package)
            .SelectMany(document => document
                .Descendants()
                .Where(element => element.Name.LocalName is "rFonts" or "font"))
            .SelectMany(element => element.Attributes())
            .Where(attribute =>
                attribute.Name.LocalName is "ascii" or "hAnsi" or
                    "eastAsia" or "cs" or "name")
            .Select(attribute => attribute.Value)
            .ToArray();

        Assert.True(
            fontNames.Any(IsYuGothicFamily),
            "The guide must declare a Yu Gothic family font for Japanese text.");
        Assert.True(
            fontNames.Any(IsMicrosoftJhengHeiFamily),
            "The guide must declare a Microsoft JhengHei family font for zh-TW text.");
    }

    private static bool IsYuGothicFamily(string fontName)
    {
        return fontName.Contains("Yu Gothic", StringComparison.OrdinalIgnoreCase) ||
               fontName.Contains("游ゴシック", StringComparison.Ordinal);
    }

    private static bool IsMicrosoftJhengHeiFamily(string fontName)
    {
        return fontName.Contains(
                   "Microsoft JhengHei",
                   StringComparison.OrdinalIgnoreCase) ||
               fontName.Contains("微軟正黑體", StringComparison.Ordinal);
    }

    private static void AssertIllustrations(
        ZipArchive package,
        XDocument document,
        ZipArchiveEntry documentRelationshipsEntry)
    {
        ZipArchiveEntry[] mediaEntries = package.Entries
            .Where(entry => entry.FullName.StartsWith(
                "word/media/",
                StringComparison.OrdinalIgnoreCase))
            .ToArray();
        Assert.True(
            mediaEntries.Length >= RequiredFigureLabels.Length,
            $"The guide must embed at least {RequiredFigureLabels.Length} " +
            $"illustrations, but found {mediaEntries.Length}.");
        Assert.All(
            mediaEntries,
            entry => Assert.True(
                entry.Length > 0,
                $"The illustration '{entry.FullName}' is empty."));

        XElement[] drawings = document
            .Descendants()
            .Where(element => element.Name.LocalName is "drawing" or "pict")
            .ToArray();
        Assert.True(
            drawings.Length >= RequiredFigureLabels.Length,
            $"The document body must contain at least " +
            $"{RequiredFigureLabels.Length} drawings, but found {drawings.Length}.");

        string figureMetadata = string.Join(
            "\n",
            document
                .Descendants()
                .Where(element => element.Name.LocalName == "docPr")
                .SelectMany(element => element.Attributes())
                .Where(attribute =>
                    attribute.Name.LocalName is "name" or "title" or "descr")
                .Select(attribute => attribute.Value));
        string searchableFigureText =
            ExtractDocumentText(document) + "\n" + figureMetadata;
        foreach (string requiredFigureLabel in RequiredFigureLabels)
        {
            Assert.Contains(
                requiredFigureLabel,
                searchableFigureText,
                StringComparison.Ordinal);
        }

        XDocument relationships = LoadXml(documentRelationshipsEntry);
        XElement[] imageRelationships = relationships
            .Descendants()
            .Where(element =>
                element.Name.LocalName == "Relationship" &&
                ((string?)element.Attribute("Type"))?.EndsWith(
                    "/image",
                    StringComparison.Ordinal) == true)
            .ToArray();
        Assert.True(
            imageRelationships
                .Select(relationship =>
                    NormalizePath((string?)relationship.Attribute("Target")))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count() >= RequiredFigureLabels.Length,
            $"The document must reference at least " +
            $"{RequiredFigureLabels.Length} distinct embedded images.");

        string[] packageEntries = package.Entries
            .Select(entry => NormalizePath(entry.FullName))
            .ToArray();
        foreach (XElement relationship in imageRelationships)
        {
            string target = (string?)relationship.Attribute("Target")
                ?? throw new InvalidDataException(
                    "An image relationship is missing its target.");
            string resolvedTarget = ResolveWordTarget(target);
            Assert.Contains(
                packageEntries,
                entry => string.Equals(
                    entry,
                    resolvedTarget,
                    StringComparison.OrdinalIgnoreCase));
        }
    }

    private static XDocument[] LoadWordXmlParts(ZipArchive package)
    {
        return package.Entries
            .Where(entry =>
                entry.FullName.StartsWith(
                    "word/",
                    StringComparison.OrdinalIgnoreCase) &&
                entry.FullName.EndsWith(
                    ".xml",
                    StringComparison.OrdinalIgnoreCase))
            .Select(LoadXml)
            .ToArray();
    }

    private static string ExtractDocumentText(XDocument document)
    {
        XNamespace word =
            "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        return string.Join(
            "\n",
            document
                .Descendants(word + "p")
                .Select(paragraph => string.Concat(
                    paragraph
                        .Descendants(word + "t")
                        .Select(text => text.Value)))
                .Where(text => !string.IsNullOrWhiteSpace(text)));
    }

    private static string GetLanguageSection(
        string documentText,
        string startMarker,
        string? endMarker)
    {
        int start = documentText.IndexOf(startMarker, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Section '{startMarker}' was not found.");

        int end = endMarker is null
            ? documentText.Length
            : documentText.IndexOf(endMarker, start + startMarker.Length, StringComparison.Ordinal);
        Assert.True(end > start, $"Section '{startMarker}' is empty.");
        return documentText[start..end];
    }

    private static void AssertContainsAny(
        string actual,
        params string[] expectedAlternatives)
    {
        Assert.True(
            expectedAlternatives.Any(expected =>
                actual.Contains(expected, StringComparison.Ordinal)),
            $"Expected one of [{string.Join(", ", expectedAlternatives)}].");
    }

    private static int CountOccurrences(string text, string value)
    {
        int count = 0;
        int searchStart = 0;
        while ((searchStart = text.IndexOf(
                   value,
                   searchStart,
                   StringComparison.Ordinal)) >= 0)
        {
            count++;
            searchStart += value.Length;
        }

        return count;
    }

    private static ZipArchiveEntry RequireEntry(
        ZipArchive package,
        string entryName)
    {
        return package.GetEntry(entryName)
            ?? throw new InvalidDataException(
                $"The OPC package is missing '{entryName}'.");
    }

    private static XDocument LoadXml(ZipArchiveEntry entry)
    {
        using Stream stream = entry.Open();
        return XDocument.Load(stream, LoadOptions.PreserveWhitespace);
    }

    private static string ReadText(ZipArchiveEntry entry)
    {
        using Stream stream = entry.Open();
        using StreamReader reader = new(
            stream,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }

    private static string GetOutputGuidePath()
    {
        return Path.Combine(
            AppContext.BaseDirectory,
            "docs",
            GuideFileName);
    }

    private static string FindFileUpward(
        string startDirectory,
        string fileName)
    {
        DirectoryInfo? directory = new(startDirectory);
        while (directory is not null)
        {
            string candidate = Path.Combine(directory.FullName, fileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            $"Could not locate '{fileName}' above '{startDirectory}'.");
    }

    private static string? ReadChildValue(
        XElement parent,
        string childName)
    {
        return parent.Elements()
            .SingleOrDefault(element => element.Name.LocalName == childName)
            ?.Value;
    }

    private static string NormalizePath(string? path)
    {
        return (path ?? string.Empty)
            .Replace('\\', '/')
            .TrimStart('/');
    }

    private static string ResolveWordTarget(string target)
    {
        string normalized = NormalizePath(target);
        string combined = normalized.StartsWith("word/", StringComparison.OrdinalIgnoreCase)
            ? normalized
            : "word/" + normalized;
        Stack<string> segments = new();
        foreach (string segment in combined.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (segment == ".")
            {
                continue;
            }

            if (segment == "..")
            {
                if (segments.Count == 0)
                {
                    throw new InvalidDataException(
                        $"Image target '{target}' escapes the OPC package.");
                }

                segments.Pop();
                continue;
            }

            segments.Push(segment);
        }

        return string.Join("/", segments.Reverse());
    }
}
