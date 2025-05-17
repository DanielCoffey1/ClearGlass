using System.ComponentModel;

namespace ClearGlass.Models
{
    public class WindowsApp : INotifyPropertyChanged
    {
        private bool _isSelected;
        private string _name = string.Empty;
        private string _displayName = string.Empty;
        private string _packageFullName = string.Empty;

        public string Name
        {
            get => _name;
            set
            {
                _name = value;
                OnPropertyChanged(nameof(Name));
            }
        }

        public string DisplayName
        {
            get => _displayName;
            set
            {
                _displayName = value;
                OnPropertyChanged(nameof(DisplayName));
            }
        }

        public string PackageFullName
        {
            get => _packageFullName;
            set
            {
                _packageFullName = value;
                OnPropertyChanged(nameof(PackageFullName));
            }
        }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                _isSelected = value;
                OnPropertyChanged(nameof(IsSelected));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
} 