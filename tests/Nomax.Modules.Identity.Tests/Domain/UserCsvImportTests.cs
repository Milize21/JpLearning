using Nomax.Modules.Identity.Domain;
using static Nomax.Modules.Identity.Domain.UserCsvImport;

namespace Nomax.Modules.Identity.Tests.Domain;

public sealed class UserCsvImportTests
{
    private static readonly HashSet<string> NoUsernames = [];
    private static readonly Dictionary<string, string> NoEmails = [];

    [Fact]
    public void BR_AKUN_03_valid_file_plans_every_row_with_defaults()
    {
        const string csv = """
            username,email,display_name,role,timezone,ui_locale
            budi.s,budi@contoh.id,Budi Santoso,murid,,
            tanaka,,Tanaka Sensei,sensei,Asia/Tokyo,en
            """;

        var plan = Plan(csv, NoUsernames, NoEmails);

        plan.IsValid.ShouldBeTrue();
        plan.TotalRows.ShouldBe(2);
        plan.ToCreate.ShouldBe(
        [
            new CsvImportRow(2, "budi.s", "budi@contoh.id", "Budi Santoso", UserRole.Murid, "Asia/Jakarta", UiLocale.Id),
            new CsvImportRow(3, "tanaka", null, "Tanaka Sensei", UserRole.Sensei, "Asia/Tokyo", UiLocale.En),
        ]);
    }

    [Fact]
    public void BR_AKUN_03_optional_columns_may_be_left_out_and_order_is_free()
    {
        var plan = Plan("role,username,display_name\nmurid,citra,Citra\n", NoUsernames, NoEmails);

        plan.IsValid.ShouldBeTrue();
        plan.ToCreate.ShouldHaveSingleItem().Username.ShouldBe("citra");
    }

    [Fact]
    public void BR_AKUN_03_semicolon_files_from_spreadsheets_and_bom_are_accepted()
    {
        var plan = Plan("﻿username;display_name;role\r\ndewi;Dewi, S.Pd.;murid\r\n", NoUsernames, NoEmails);

        plan.IsValid.ShouldBeTrue();
        plan.ToCreate.ShouldHaveSingleItem().DisplayName.ShouldBe("Dewi, S.Pd.");
    }

    [Fact]
    public void BR_AKUN_03_quoted_fields_may_contain_commas_quotes_and_line_breaks()
    {
        const string csv = "username,display_name,role\neka,\"Eka \"\"Kak\"\", M.\",murid\n";

        Plan(csv, NoUsernames, NoEmails).ToCreate.ShouldHaveSingleItem().DisplayName.ShouldBe("Eka \"Kak\", M.");
    }

    [Fact]
    public void BR_AKUN_03_any_error_blocks_the_whole_file_and_is_reported_per_cell()
    {
        const string csv = """
            username,email,display_name,role,timezone,ui_locale
            ok.user,,Ok,murid,,
            Bad Name,bukan-email,,admin,Mars/Base,jp
            """;

        var plan = Plan(csv, NoUsernames, NoEmails);

        plan.IsValid.ShouldBeFalse();
        plan.ToCreate.ShouldHaveSingleItem().Username.ShouldBe("ok.user");
        plan.Errors.ShouldBe(
        [
            new CsvImportError(3, Username, Codes.InvalidFormat),
            new CsvImportError(3, Email, Codes.InvalidFormat),
            new CsvImportError(3, DisplayName, Codes.Required),
            new CsvImportError(3, Role, Codes.InvalidRole),
            new CsvImportError(3, Timezone, Codes.InvalidTimezone),
            new CsvImportError(3, UiLocaleColumn, Codes.InvalidLocale),
        ]);
    }

    [Fact]
    public void BR_AKUN_01_csv_cannot_create_admins()
    {
        var plan = Plan("username,display_name,role\nboss,Boss,admin\n", NoUsernames, NoEmails);

        plan.Errors.ShouldBe([new CsvImportError(2, Role, Codes.InvalidRole)]);
    }

    [Fact]
    public void BR_AKUN_03_duplicates_inside_the_file_are_errors()
    {
        const string csv = "username,email,display_name,role\nfajar,f@contoh.id,Fajar,murid\nfajar,F@contoh.id,Fajar 2,murid\n";

        var plan = Plan(csv, NoUsernames, NoEmails);

        plan.Errors.ShouldBe(
        [
            new CsvImportError(3, Username, Codes.DuplicateInFile),
            new CsvImportError(3, Email, Codes.DuplicateInFile),
        ]);
    }

    [Fact]
    public void BR_AKUN_03_existing_usernames_are_skipped_so_the_file_can_be_imported_again()
    {
        var plan = Plan(
            "username,email,display_name,role\ngita,gita@contoh.id,Gita,murid\nhadi,,Hadi,murid\n",
            new HashSet<string> { "gita" },
            new Dictionary<string, string> { ["gita@contoh.id"] = "gita" });

        plan.IsValid.ShouldBeTrue();
        plan.Skipped.ShouldHaveSingleItem().Username.ShouldBe("gita");
        plan.ToCreate.ShouldHaveSingleItem().Username.ShouldBe("hadi");
    }

    [Fact]
    public void BR_AKUN_03_email_of_another_account_is_an_error()
    {
        var plan = Plan(
            "username,email,display_name,role\nindah,ADMIN@contoh.id,Indah,murid\n",
            NoUsernames,
            new Dictionary<string, string> { ["admin@contoh.id"] = "admin.dev" });

        plan.Errors.ShouldBe([new CsvImportError(2, Email, Codes.EmailTaken)]);
    }

    [Fact]
    public void BR_AKUN_03_header_must_have_the_required_columns_and_no_unknown_ones()
    {
        var plan = Plan("username,nama,role\njoko,Joko,murid\n", NoUsernames, NoEmails);

        plan.Errors.ShouldBe(
        [
            new CsvImportError(1, "nama", Codes.UnknownColumn),
            new CsvImportError(1, DisplayName, Codes.MissingColumn),
        ]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("\n\n")]
    [InlineData("username,display_name,role\n")]
    public void BR_AKUN_03_empty_file_is_an_error(string csv)
    {
        Plan(csv, NoUsernames, NoEmails).Errors.ShouldHaveSingleItem().Code.ShouldBe(Codes.EmptyFile);
    }

    [Fact]
    public void BR_AKUN_03_at_most_500_rows()
    {
        var rows = Enumerable.Range(1, 501).Select(i => $"user{i:000},User {i},murid");
        var csv = "username,display_name,role\n" + string.Join('\n', rows);

        var plan = Plan(csv, NoUsernames, NoEmails);

        plan.Errors.ShouldHaveSingleItem().Code.ShouldBe(Codes.TooManyRows);
        Plan(string.Join('\n', csv.Split('\n')[..501]), NoUsernames, NoEmails).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void BR_AKUN_03_blank_lines_are_ignored_and_rows_are_numbered_from_the_header()
    {
        var plan = Plan("username,display_name,role\n\nkiki,Kiki,murid\n,,\nlala,,murid\n", NoUsernames, NoEmails);

        plan.Errors.ShouldBe([new CsvImportError(3, DisplayName, Codes.Required)]);
    }

    [Fact]
    public void BR_AKUN_03_result_file_lists_username_and_temporary_password()
    {
        PasswordsCsv([("mira", "Abc234defGHJ"), ("nina", "Xyz789kmnPQR")])
            .ShouldBe("username,temporary_password\nmira,Abc234defGHJ\nnina,Xyz789kmnPQR\n");
    }
}
