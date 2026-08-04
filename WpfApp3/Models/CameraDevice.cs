namespace WpfApp3.Models
{
    /// <summary>
    /// Represents a camera device available on the system
    /// </summary>
    public class CameraDevice
    {
        public int Index { get; set; }
        public string Name { get; set; } = string.Empty;
        public string DevicePath { get; set; } = string.Empty;

        public CameraDevice(
            int index,
            string name,
            string devicePath = "")
        {
            Index = index;
            Name = name;
            DevicePath = devicePath;
        }

        public override string ToString()
        {
            return Name;
        }
    }
}
