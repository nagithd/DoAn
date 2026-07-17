using WpfApp3.ViewModels;

namespace WpfApp3.Models
{
    /// <summary>
    /// Represents a sidebar navigation item
    /// </summary>
    public class SidebarItem : ViewModelBase
    {
        private string _name = string.Empty;
        private string _displayName = string.Empty;
        private string _icon = string.Empty;
        private bool _isSelected;

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        public string DisplayName
        {
            get => _displayName;
            set => SetProperty(ref _displayName, value);
        }

        public string Icon
        {
            get => _icon;
            set => SetProperty(ref _icon, value);
        }

        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        public SidebarItem() { }

        public SidebarItem(string name, string displayName, string icon)
        {
            Name = name;
            DisplayName = displayName;
            Icon = icon;
            IsSelected = false;
        }
    }
}
