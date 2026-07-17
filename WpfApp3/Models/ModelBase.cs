namespace WpfApp3.Models
{
    /// <summary>
    /// Base class for model entities
    /// Provides common entity properties
    /// </summary>
    public abstract class ModelBase
    {
        public int Id { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;
    }
}
