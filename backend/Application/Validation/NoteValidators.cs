using FluentValidation;

namespace Application.Validation;

public class NoteInput
{
    public string Text { get; init; } = "";
}

public class NoteInputValidator : AbstractValidator<NoteInput>
{
    public NoteInputValidator()
    {
        RuleFor(x => x.Text)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Текст обязателен")
            .MaximumLength(4000).WithMessage("Максимальная длина — 4000 символов")
            .Must(NoOnlyWhitespace).WithMessage("Недопустим только пробельный текст");
    }

    static bool NoOnlyWhitespace(string s) =>
        s.Any(ch => !char.IsWhiteSpace(ch));
}

public static class TextNormalizer
{
    public static string Normalize(string s)
    {
        s = s.Replace("\r\n", "\n").Replace("\r", "\n");
        s = System.Text.RegularExpressions.Regex.Replace(s, "[ \t]+", " ");
        s = System.Text.RegularExpressions.Regex.Replace(s, " *\n *", "\n");
        return s.Trim();
    }
}
