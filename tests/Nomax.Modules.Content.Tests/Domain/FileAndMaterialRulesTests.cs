using Nomax.Modules.Content.Domain;
using Nomax.SharedKernel;

namespace Nomax.Modules.Content.Tests.Domain;

public sealed class FileAndMaterialRulesTests
{
    [Theory]
    [InlineData(Roles.Admin, FilePurpose.Material, true)]
    [InlineData(Roles.Sensei, FilePurpose.Material, true)]
    [InlineData(Roles.Murid, FilePurpose.Material, false)]
    [InlineData(Roles.Admin, FilePurpose.BookCover, true)]
    [InlineData(Roles.Sensei, FilePurpose.BookCover, false)]
    [InlineData(Roles.Admin, FilePurpose.QuestionImport, true)]
    [InlineData(Roles.Sensei, FilePurpose.QuestionImport, false)]
    [InlineData(Roles.Murid, FilePurpose.Submission, true)]
    [InlineData(Roles.Sensei, FilePurpose.Submission, false)]
    [InlineData(Roles.Admin, FilePurpose.Submission, false)]
    public void BR_FILE_06_upload_purpose_depends_on_the_role(string role, FilePurpose purpose, bool allowed)
    {
        FileRules.CanUpload(role, purpose).ShouldBe(allowed);
    }

    [Fact]
    public void BR_FILE_01_the_limit_is_one_gibibyte()
    {
        StoredFile.MaxSizeBytes.ShouldBe(1_073_741_824);
    }

    [Theory]
    [InlineData(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0 }, "image/png")]
    [InlineData(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0, 0, 0, 0, 0, 0, 0, 0 }, "image/jpeg")]
    [InlineData(new byte[] { 0x52, 0x49, 0x46, 0x46, 1, 2, 3, 4, 0x57, 0x45, 0x42, 0x50 }, "image/webp")]
    [InlineData(new byte[] { 0x3C, 0x73, 0x76, 0x67, 0x20, 0, 0, 0, 0, 0, 0, 0 }, null)]
    [InlineData(new byte[] { 0x25, 0x50, 0x44, 0x46, 0, 0, 0, 0, 0, 0, 0, 0 }, null)]
    public void BR_FILE_02_only_png_jpeg_webp_magic_bytes_count_as_images(byte[] header, string? expected)
    {
        FileRules.DetectImageType(header).ShouldBe(expected);
    }

    [Theory]
    [InlineData("laporan.pdf", "laporan.pdf")]
    [InlineData("C:\\fakepath\\tugas bab 1.docx", "tugas bab 1.docx")]
    [InlineData("../../etc/passwd", "passwd")]
    [InlineData("a<b>c:d*e?.txt", "abcde.txt")]
    [InlineData("  ", "file")]
    [InlineData("..", "file")]
    public void BR_FILE_02_original_names_are_sanitized(string name, string expected)
    {
        FileNames.Sanitize(name).ShouldBe(expected);
    }

    [Fact]
    public void BR_FILE_02_long_names_are_cut_to_255_characters()
    {
        FileNames.Sanitize(new string('a', 300) + ".pdf").Length.ShouldBe(255);
    }

    [Fact]
    public void BR_KONTEN_01_material_is_visible_when_published_now_or_earlier()
    {
        var now = new DateTimeOffset(2026, 10, 18, 10, 0, 0, TimeSpan.Zero);

        MaterialVisibility.IsPublished(null, now).ShouldBeTrue();
        MaterialVisibility.IsPublished(now, now).ShouldBeTrue();
        MaterialVisibility.IsPublished(now.AddMinutes(-1), now).ShouldBeTrue();
        MaterialVisibility.IsPublished(now.AddMinutes(1), now).ShouldBeFalse();
    }

    [Theory]
    [InlineData("dasar", true)]
    [InlineData("n5", true)]
    [InlineData("n-level-2", true)]
    [InlineData("N5", false)]
    [InlineData("", false)]
    [InlineData("a-very-long-level-code", false)]
    public void BR_KONTEN_03_level_codes_are_short_lowercase(string code, bool valid)
    {
        ContentRules.IsValidLevelCode(code).ShouldBe(valid);
    }

    [Fact]
    public void BR_KONTEN_05_a_material_keeps_its_kind()
    {
        var material = Material.CreateLink(Guid.CreateVersion7(), Guid.CreateVersion7(), new("Tautan", null, null), "https://a.example", null, Guid.CreateVersion7(), 0);

        material.TryChangeContent(null, null, "https://b.example").ShouldBeTrue();
        material.Url.ShouldBe("https://b.example");
        material.TryChangeContent(null, Guid.CreateVersion7(), null).ShouldBeFalse();
    }
}
