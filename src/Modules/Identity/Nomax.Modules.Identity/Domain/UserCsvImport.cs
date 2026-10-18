using System.Text;
using Nomax.SharedKernel.Text;

namespace Nomax.Modules.Identity.Domain;

/// <summary>A valid CSV row, ready to become an account.</summary>
public sealed record CsvImportRow(
    int Row,
    string Username,
    string? Email,
    string DisplayName,
    UserRole Role,
    string Timezone,
    UiLocale UiLocale);

/// <summary>A cell that blocks the import. <see cref="Row"/> counts like a spreadsheet: the header is row 1.</summary>
public sealed record CsvImportError(int Row, string Column, string Code);

/// <param name="TotalRows">Data rows in the file, without the header and blank lines.</param>
public sealed record CsvImportPlan(
    int TotalRows,
    IReadOnlyList<CsvImportRow> ToCreate,
    IReadOnlyList<CsvImportRow> Skipped,
    IReadOnlyList<CsvImportError> Errors)
{
    public bool IsValid => Errors.Count == 0;
}

/// <summary>
/// BR-AKUN-03. Every row is validated before any account is created. Rows whose username already exists are
/// skipped, so importing the same file again after a partial failure is safe.
/// </summary>
public static class UserCsvImport
{
    public const int MaxRows = 500;

    public const string Username = "username";
    public const string Email = "email";
    public const string DisplayName = "display_name";
    public const string Role = "role";
    public const string Timezone = "timezone";
    public const string UiLocaleColumn = "ui_locale";

    public static IReadOnlyList<string> Columns { get; } = [Username, Email, DisplayName, Role, Timezone, UiLocaleColumn];

    public static IReadOnlyList<string> RequiredColumns { get; } = [Username, DisplayName, Role];

    /// <summary>Row-level error codes (docs/05 §4); the frontend has a label for each.</summary>
    public static class Codes
    {
        public const string EmptyFile = "empty_file";
        public const string MissingColumn = "missing_column";
        public const string UnknownColumn = "unknown_column";
        public const string TooManyRows = "too_many_rows";
        public const string Required = "required";
        public const string InvalidFormat = "invalid_format";
        public const string TooLong = "too_long";
        public const string InvalidRole = "invalid_role";
        public const string InvalidTimezone = "invalid_timezone";
        public const string InvalidLocale = "invalid_locale";
        public const string DuplicateInFile = "duplicate_in_file";
        public const string EmailTaken = "email_taken";
    }

    /// <param name="existingUsernames">Usernames that already have a profile.</param>
    /// <param name="existingEmails">Lower-cased emails already in use, mapped to their username.</param>
    public static CsvImportPlan Plan(
        string csv,
        IReadOnlySet<string> existingUsernames,
        IReadOnlyDictionary<string, string> existingEmails)
    {
        ArgumentNullException.ThrowIfNull(existingUsernames);
        ArgumentNullException.ThrowIfNull(existingEmails);

        var records = CsvReader.Read(csv ?? string.Empty);
        var errors = new List<CsvImportError>();
        if (records.Count == 0)
        {
            return new CsvImportPlan(0, [], [], [new CsvImportError(1, string.Empty, Codes.EmptyFile)]);
        }

        var header = records[0].Select(cell => cell.Trim().ToLowerInvariant()).ToList();
        foreach (var column in header.Where(column => !Columns.Contains(column)))
        {
            errors.Add(new CsvImportError(1, column, Codes.UnknownColumn));
        }

        foreach (var column in RequiredColumns.Where(column => !header.Contains(column)))
        {
            errors.Add(new CsvImportError(1, column, Codes.MissingColumn));
        }

        var dataRows = records.Skip(1).ToList();
        if (dataRows.Count == 0)
        {
            errors.Add(new CsvImportError(2, string.Empty, Codes.EmptyFile));
        }

        if (dataRows.Count > MaxRows)
        {
            errors.Add(new CsvImportError(MaxRows + 2, string.Empty, Codes.TooManyRows));
        }

        if (errors.Count > 0)
        {
            return new CsvImportPlan(dataRows.Count, [], [], errors);
        }

        var toCreate = new List<CsvImportRow>();
        var skipped = new List<CsvImportRow>();
        var usernamesInFile = new HashSet<string>(StringComparer.Ordinal);
        var emailsInFile = new HashSet<string>(StringComparer.Ordinal);

        for (var index = 0; index < dataRows.Count; index++)
        {
            var rowNumber = index + 2;
            var cells = dataRows[index];
            string Cell(string column)
            {
                var position = header.IndexOf(column);
                return position >= 0 && position < cells.Count ? cells[position].Trim() : string.Empty;
            }

            void Fail(string column, string code) => errors.Add(new CsvImportError(rowNumber, column, code));

            var username = Cell(Username);
            var email = Cell(Email);
            var displayName = Cell(DisplayName);
            var roleText = Cell(Role).ToLowerInvariant();
            var timezone = Cell(Timezone);
            var localeText = Cell(UiLocaleColumn).ToLowerInvariant();
            var errorsBefore = errors.Count;

            if (username.Length == 0)
            {
                Fail(Username, Codes.Required);
            }
            else if (username.Length > AccountRules.UsernameMaxLength)
            {
                Fail(Username, Codes.TooLong);
            }
            else if (!AccountRules.IsValidUsername(username))
            {
                Fail(Username, Codes.InvalidFormat);
            }
            else if (!usernamesInFile.Add(username))
            {
                Fail(Username, Codes.DuplicateInFile);
            }

            var emailKey = email.ToLowerInvariant();
            if (email.Length > 0)
            {
                if (email.Length > AccountRules.EmailMaxLength)
                {
                    Fail(Email, Codes.TooLong);
                }
                else if (!AccountRules.IsValidEmail(email))
                {
                    Fail(Email, Codes.InvalidFormat);
                }
                else if (!emailsInFile.Add(emailKey))
                {
                    Fail(Email, Codes.DuplicateInFile);
                }
                else if (existingEmails.TryGetValue(emailKey, out var owner) && owner != username)
                {
                    Fail(Email, Codes.EmailTaken);
                }
            }

            if (displayName.Length == 0)
            {
                Fail(DisplayName, Codes.Required);
            }
            else if (displayName.Length > AccountRules.DisplayNameMaxLength)
            {
                Fail(DisplayName, Codes.TooLong);
            }

            // BR-AKUN-01: CSV import creates sensei and murid accounts only.
            UserRole? role = roleText switch
            {
                "" => null,
                "sensei" => UserRole.Sensei,
                "murid" => UserRole.Murid,
                _ => null,
            };
            if (roleText.Length == 0)
            {
                Fail(Role, Codes.Required);
            }
            else if (role is null)
            {
                Fail(Role, Codes.InvalidRole);
            }

            if (timezone.Length == 0)
            {
                timezone = AccountRules.DefaultTimezone;
            }
            else if (!AccountRules.IsValidTimezone(timezone))
            {
                Fail(Timezone, Codes.InvalidTimezone);
            }

            UiLocale? locale = localeText switch
            {
                "" or "id" => UiLocale.Id,
                "en" => UiLocale.En,
                _ => null,
            };
            if (locale is null)
            {
                Fail(UiLocaleColumn, Codes.InvalidLocale);
            }

            if (errors.Count > errorsBefore)
            {
                continue;
            }

            var row = new CsvImportRow(
                rowNumber,
                username,
                email.Length == 0 ? null : email,
                displayName,
                role!.Value,
                timezone,
                locale!.Value);

            (existingUsernames.Contains(username) ? skipped : toCreate).Add(row);
        }

        return new CsvImportPlan(dataRows.Count, toCreate, skipped, errors);
    }

    /// <summary>The one-time result file: <c>username,temporary_password</c>.</summary>
    public static string PasswordsCsv(IEnumerable<(string Username, string TemporaryPassword)> accounts)
    {
        var builder = new StringBuilder("username,temporary_password\n");
        foreach (var (username, password) in accounts)
        {
            // Both values come from restricted alphabets without commas or quotes.
            builder.Append(username).Append(',').Append(password).Append('\n');
        }

        return builder.ToString();
    }
}
