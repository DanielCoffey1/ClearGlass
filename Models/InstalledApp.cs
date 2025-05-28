using System.ComponentModel;

namespace ClearGlass.Models
{
    public class InstalledApp : INotifyPropertyChanged
    {
        private bool _isSelected;
        private string _name;
        private string _id;
        private string _version;

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged(nameof(IsSelected));
                }
            }
        }

        public string Name
        {
            get => _name;
            set
            {
                if (_name != value)
                {
                    _name = value;
                    OnPropertyChanged(nameof(Name));
                }
            }
        }

        public string Id
        {
            get => _id;
            set
            {
                if (_id != value)
                {
                    _id = value;
                    OnPropertyChanged(nameof(Id));
                }
            }
        }

        public string Version
        {
            get => _version;
            set
            {
                if (_version != value)
                {
                    _version = value;
                    OnPropertyChanged(nameof(Version));
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public InstalledApp(string name, string id, string version)
        {
            _name = name;
            _id = id;
            _version = version;
            _isSelected = false;
        }
    }
} 