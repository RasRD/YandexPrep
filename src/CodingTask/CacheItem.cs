namespace CodingTask;

public sealed class CacheItem
{
    public CacheItem(UserProfile profile, DateTime savedAt)
    {
        Profile = profile;
        SavedAt = savedAt;
    }

    public UserProfile Profile { get; set; }
        
    public DateTime SavedAt { get; set; }
}