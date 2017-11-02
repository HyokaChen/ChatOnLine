using GalaSoft.MvvmLight.CommandWpf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ChatOnLine
{
    public class UserViewModel
    {
        #region 单例
        public static readonly Lazy<UserViewModel> singleton = new Lazy<UserViewModel>();

        public static UserViewModel Instance { get { return singleton.Value; } }

        #endregion
        #region 字段
        private TcpClient tcp_client;
        private NetworkStream tcp_stream;
        private UdpClient udp_background;
        private Thread udp_receive;

        private RelayCommand<object> _loginCommand;

        public RelayCommand<object> LoginCommand
        {
            get
            {
                return _loginCommand ?? (_loginCommand = new RelayCommand<object>(
                     u =>
                     {
                         if (u is User user)
                         {

                         }
                     }
                    ));
            }
        }

        #endregion

        #region 方法
        private bool Login(User user)
        {
            bool login_status = false;

            return login_status;
        }

        private UserViewModel()
        {
            Initialize();
        }

        private void Initialize()
        {

        }

        #endregion
    }
}
