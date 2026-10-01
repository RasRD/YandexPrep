namespace CodingTask;

/// <summary>
/// Источник профилей пользователей.
/// </summary>
public interface IUserProfileSource
{
    /// <summary>
    /// Получить профиль по его идентификатору.
    /// </summary>
    /// <param name="userId">Идентификатор.</param>
    /// <param name="cancellationToken">Токен.</param>
    /// <returns>Профиль. Null, если не найден.</returns>
    Task<UserProfile?> GetAsync(
        long userId,
        CancellationToken cancellationToken);
}