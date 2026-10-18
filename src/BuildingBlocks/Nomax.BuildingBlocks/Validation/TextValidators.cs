using FluentValidation;
using Nomax.SharedKernel.Text;

namespace Nomax.BuildingBlocks.Validation;

/// <summary>FluentValidation rules for content text (docs/06 §10: one validator per usage, shared by all modules).</summary>
public static class TextValidators
{
    /// <summary>Validates a <see cref="TextBlock"/>; errors are keyed like <c>title.ja[2].r</c>.</summary>
    public static IRuleBuilderOptionsConditions<T, TextBlock?> ValidTextBlock<T>(this IRuleBuilder<T, TextBlock?> rule, TextBlockUsage usage) =>
        rule.Custom((block, context) =>
        {
            foreach (var issue in TextRules.Validate(block, usage))
            {
                context.AddFailure(Join(context.PropertyPath, issue.Path), issue.Message);
            }
        });

    public static IRuleBuilderOptionsConditions<T, RichText?> ValidRichText<T>(this IRuleBuilder<T, RichText?> rule) =>
        rule.Custom((text, context) =>
        {
            foreach (var issue in TextRules.Validate(text))
            {
                context.AddFailure(Join(context.PropertyPath, issue.Path), issue.Message);
            }
        });

    public static IRuleBuilderOptions<T, string?> HttpsUrl<T>(this IRuleBuilder<T, string?> rule, int maxLength = 2048) =>
        rule.Must(value => value is not null && value.Length <= maxLength && TextRules.IsHttpsUrl(value))
            .WithMessage($"Must be an https:// address of at most {maxLength} characters.");

    private static string Join(string property, string path) =>
        path.Length == 0 ? property : path.StartsWith('[') ? property + path : $"{property}.{path}";
}
