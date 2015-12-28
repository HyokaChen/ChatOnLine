using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;

namespace ChatOnLine
{
    public class DataService : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public void RaisePropertyChange(string propertyName)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }
    public class User : DataService
    {
        private int _userId;

        public int UserId
        {
            get { return _userId; }
            set
            {
                _userId = value;
                RaisePropertyChange("UserId");
            }
        }

        private string _name;

        public string Name
        {
            get { return _name; }
            set
            {
                _name = value;
                RaisePropertyChange("Name");
            }
        }

        private string _state;

        public string State
        {
            get { return _state; }
            set
            {
                _state = value;
                RaisePropertyChange("State");
            }
        }

        private string _imgSource;

        public string ImgSource
        {
            get { return _imgSource; }
            set
            {
                _imgSource = value;
                RaisePropertyChange("ImgSource");
            }
        }

        private string _notifyColor;

        public string NotifyColor//提示通知
        {
            get { return _notifyColor; }
            set
            {
                _notifyColor = value;
                RaisePropertyChange("NotifyColor");
            }
        }

    }

    public class ChatContent : DataService
    {
        private string _imgSourcr;

        public string ImgSource
        {
            get { return _imgSourcr; }
            set
            {
                _imgSourcr = value;
                RaisePropertyChange("ImgSource");
            }
        }
        private string _content;

        public string Content
        {
            get { return _content; }
            set
            {
                _content = value;
            }
        }

        private string _fontColor;

        public string FontColor
        {
            get { return _fontColor; }
            set
            {
                _fontColor = value;
            }
        }

    }

    public class ChatContentCollectionList : DataService
    {
        private int _id;

        public int ID
        {
            get { return _id; }
            set
            {
                _id = value;
            }
        }

        private ObservableCollection<ChatContent> _chatContentCollection;

        public ObservableCollection<ChatContent> ChatContentCollection
        {
            get { return _chatContentCollection; }
            set { _chatContentCollection = value; }
        }

    }
}
