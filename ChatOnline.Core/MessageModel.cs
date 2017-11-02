using System;

namespace ChatOnline.Core
{
    public sealed class MessageModel
    {
        private int _user_id;

        public int UserId
        {
            get { return _user_id; }
            set { _user_id = value; }
        }

        private string _name;

        public string Name
        {
            get { return _name; }
            set { _name = value; }
        }


        private string _password = string.Empty;

        public string Password
        {
            get { return _password; }
            set { _password = value; }
        }

        private string _ip;

        public string IP
        {
            get { return _ip; }
            set { _ip = value; }
        }
        private int _port;

        public int Port
        {
            get { return _port; }
            set { _port = value; }
        }

        private MsgType _msg_category;

        public MsgType MsgCategory
        {
            get { return _msg_category; }
            set { _msg_category = value; }
        }

    }

    public enum MsgType: int
    {
        Empty = 0,
        SignIn,
        LoginOut,
        Query,
        SendMsg,
        Online,
        Offline
    }

    public static class MessageCategory
    {
        public static string Empty => nameof(Empty);
        public static string SignIn => nameof(SignIn);
        public static string LoginOut => nameof(LoginOut);
        public static string SendMsg => nameof(SendMsg);
    }
}
