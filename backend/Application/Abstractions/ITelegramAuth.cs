namespace Application.Abstractions;

public interface ITelegramAuth
{
    (long userId, string username, string lang) ValidateInitData(string initData);
}
