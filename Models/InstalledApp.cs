using System.ComponentModel;

namespace ClearGlass.Models
{
    public class InstalledApp : INotifyPropertyChanged
    {
        private bool _isSelected;
        private readonly string _name;
        private readonly string _id;
        private readonly string _version;
        private bool _isSteamGame;

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

        public bool IsSteamGame
        {
            get => _isSteamGame;
            set
            {
                if (_isSteamGame != value)
                {
                    _isSteamGame = value;
                    OnPropertyChanged(nameof(IsSteamGame));
                }
            }
        }

        public string Name => _name;
        public string Id => _id;
        public string Version => _version;

        public event PropertyChangedEventHandler? PropertyChanged;

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
            _isSteamGame = id.StartsWith("Steam", System.StringComparison.OrdinalIgnoreCase);
        }
    }
} 