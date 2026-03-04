using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.Serialization;

namespace M_A_G_A.Models
{
    [DataContract]
    public class ChatFolder : INotifyPropertyChanged
    {
        private string _name;

        [DataMember] public string Id { get; set; } = System.Guid.NewGuid().ToString();

        [DataMember]
        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(nameof(Name)); }
        }

        [DataMember] public List<string> ContactIds { get; set; } = new List<string>();

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
