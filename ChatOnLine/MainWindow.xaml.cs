using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace ChatOnLine
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        //delegate
        private delegate void makeUIUpdateDelegate(User user);
        private delegate void getInfoFromServerDelegate(NetworkStream stream);
        private delegate void initChatContentContainerlist(int count);
        private delegate void updateRecvMsgDelegate(string msg);
        //field
        private bool _isQueryCompleted = false;
        private IPAddress _localIPAddr;
        private int _tcp_client_port = 0;//save port for tcpclient.
        private ObservableCollection<User> remoteUserCollection;//save all friend.
        private List<ChatContentCollectionList> friendChatContentCollectionList;//save friends' message.
        private List<ChatContentCollectionList> myChatContentCollectionList;//save my message.
        private User _localUser;
        private User _currentSelectUser;
        private string _friendId;//friend who want to query.
        private object locker;
        private static readonly string notifyColor = @"#d2691e";
        private static readonly string hideColor = @"#FFFFFF";
        private static readonly string restoreColor = @"#F4A460";
        private static readonly string visibleColor = @"#000000";
        private static readonly string onLine = "在线";
        private static readonly string offLine = "离线";
        //tcp
        private TcpClient tcp_client;
        private NetworkStream tcp_stream;
        //udp
        private UdpClient udp_background;
        private Thread udp_back_recvTh;
        public MainWindow()
        {
            _localUser = new User();
            remoteUserCollection = new ObservableCollection<User>();
            friendChatContentCollectionList = new List<ChatContentCollectionList>();
            myChatContentCollectionList = new List<ChatContentCollectionList>();
            locker = new object();
            _currentSelectUser = new User();
            InitializeComponent();

            _loginGrid.DataContext = _localUser;
            _stateBar.DataContext = _currentSelectUser;
            _listFriendListBox.ItemsSource = remoteUserCollection;
        }
        //登录
        private void _loginBtn_Click(object sender, RoutedEventArgs e)
        {
            _loadProBar.Visibility = Visibility.Visible;
            Dns.BeginGetHostEntry(Dns.GetHostName(), new AsyncCallback(Init), true);
            _noChatGrid.Visibility = Visibility.Visible;
        }

        //初始化配置
        private void Init(IAsyncResult ar)
        {
            IPHostEntry localhost = new IPHostEntry();
            localhost = Dns.EndGetHostEntry(ar);
            string ipaddr = ConfigurationManager.AppSettings["ipaddr"];
            IPEndPoint remoteIpEndPoint = new IPEndPoint(IPAddress.Parse(ipaddr), 13000);
            IPEndPoint tcp_temp;
            string loacladdr = ConfigurationManager.AppSettings["localaddr"];
            //get value from app.config if have value.
            if (loacladdr != "0")
            {
                _localIPAddr = IPAddress.Parse(loacladdr);
                tcp_temp = new IPEndPoint(_localIPAddr, 0);
                tcp_client = new TcpClient(tcp_temp);
                tcp_client.Connect(remoteIpEndPoint);
                int.TryParse(tcp_client.Client.LocalEndPoint.ToString().Split(':')[1], out _tcp_client_port);
            }
            else
            {
                foreach (var item in localhost.AddressList)
                {
                    if (item.AddressFamily == AddressFamily.InterNetwork)
                    {
                        try
                        {
                            tcp_temp = new IPEndPoint(item, 0);
                            tcp_client = new TcpClient(tcp_temp);
                            tcp_client.Connect(remoteIpEndPoint);
                            int.TryParse(tcp_client.Client.LocalEndPoint.ToString().Split(':')[1], out _tcp_client_port);
                            _localIPAddr = item;
                            break;
                        }
                        catch (Exception ex)
                        {
                            string d = ex.Message;
                            tcp_client.Close();
                        }
                        //_allLocalHostIP.Add(item);
                    }
                }
            }
            if (tcp_client.Connected)
            {
                //initialization tcpclient.    
                tcp_stream = tcp_client.GetStream();
                TcpConnectToServer(getInitInfoFromServer);

                udp_back_recvTh = new Thread(new ThreadStart(UDpBackgroundForRecv));
                udp_back_recvTh.IsBackground = true;
                udp_back_recvTh.Start();

                //udpclient init ,for udp_to_friend.
                Dispatcher.BeginInvoke(DispatcherPriority.Normal, new makeUIUpdateDelegate(makeUIUpdate), null);
            }
            else
            {
                MessageBox.Show("无法访问远程服务器！");
            }
        }
        //配置连接TCP服务端
        private void TcpConnectToServer(getInfoFromServerDelegate fun)
        {
            if (_localUser.UserId > 0 && _localUser.Name != "")
            {
                try
                {
                    fun(tcp_stream);
                }
                catch (Exception ex)
                {
                    string a = ex.Message;
                }
                _isQueryCompleted = true;
            }
            else
            {
                _isQueryCompleted = false;
            }
        }
        //从TCP服务器获取初始化信息
        private void getInitInfoFromServer(NetworkStream stream)
        {
            //add local user to collection,and show its information.
            _localUser.State =onLine;
            _localUser.ImgSource = "Resource/me.png";
            _localUser.NotifyColor = restoreColor;
            Dispatcher.BeginInvoke(DispatcherPriority.Send, new makeUIUpdateDelegate(makeUIUpdate), _localUser);

            //send to server.
            string msg = _localUser.UserId.ToString() + "|" + _localUser.Name + "|i";
            Byte[] data = new Byte[msg.Length];
            data = Encoding.Default.GetBytes(msg);
            stream.Write(data, 0, data.Length);
            //receive from server,for about friends' state.
            msg = "";
            int i = 0;
            Byte[] recvData = new Byte[256];
            while ((i = stream.Read(recvData, 0, 256)) != 0)
            {
                if (i == 1)
                {
                    break;
                }
                msg = Encoding.Default.GetString(recvData, 0, i);
                updateFriendState(msg);
                //
                if ((char)recvData[i - 1] == '#')
                {
                    break;
                }
            }
        }

        //异步来更新界面和更新好友的列表栏
        private void makeUIUpdate(User user)
        {

            if (user != null)
            {
                lock (locker)
                {
                    var u = from b in remoteUserCollection
                            where (b.UserId == user.UserId)
                            select b;
                    if (u.Count() > 0)
                    {
                        u.ElementAt(0).State = user.State;//update user's state.
                    }
                    else
                    {
                        remoteUserCollection.Add(user);
                        friendChatContentCollectionList.Add(
                        new ChatContentCollectionList()
                        {
                            ID = user.UserId,
                            ChatContentCollection = new ObservableCollection<ChatContent>()
                        }
                        );
                        myChatContentCollectionList.Add(
                            new ChatContentCollectionList()
                            {
                                ID = user.UserId,
                                ChatContentCollection = new ObservableCollection<ChatContent>()
                            }
                            );
                    }
                }
            }
            //update UI.
            if (_isQueryCompleted)
            {
                _loadProBar.Visibility = Visibility.Collapsed;
                _loginGrid.Visibility = Visibility.Collapsed;
                _isQueryCompleted = false;
            }
        }

        //点击查询并且添加好友
        private void _findBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_findTb.Text != string.Empty)
            {
                _friendId = _findTb.Text;
                TcpConnectToServer(getUserInfoFromServer);
            }
        }
        //从TCP服务器获取查询的好友的信息
        private void getUserInfoFromServer(NetworkStream stream)
        {
            if (_localUser.UserId.ToString() != _friendId)
            {
                //send to server.
                string msg = "a" + _localUser.UserId + "|" + _friendId;
                Byte[] data = new Byte[msg.Length];
                data = Encoding.Default.GetBytes(msg);
                stream.Write(data, 0, data.Length);
                //receive user's information from server,which you want to query.
                msg = "";
                Byte[] recvData = new Byte[256];
                int i = stream.Read(recvData, 0, 256);
                msg = Encoding.Default.GetString(recvData, 0, i);
                if (msg.Substring(msg.Length - 2) == "!|")
                {
                    MessageBox.Show("The find people is off line.");
                }
                else
                {
                    updateFriendState(msg);
                }
                _findTb.Text = string.Empty;
            }
        }

        //更新从TCP服务器获取来的好友的状态
        private void updateFriendState(string msg)//`:online !:offline #:end receive
        {
            User _remoteUser = new User();
            string[] peer_meg = msg.Split('|');
            for (int i = 0; i < peer_meg.Length - 1; i += 2)
            {
                _remoteUser.UserId = int.Parse(peer_meg[i]);
                _remoteUser.Name = peer_meg[i + 1].Substring(0, peer_meg[i + 1].Length - 1);
                if (peer_meg[i + 1].Substring(peer_meg[i + 1].Length - 1) == "!")
                {
                    _remoteUser.State =offLine;
                }
                else
                {
                    _remoteUser.State =onLine;
                }
                _remoteUser.ImgSource = "Resource/someone.png";
                Dispatcher.BeginInvoke(DispatcherPriority.Send, new makeUIUpdateDelegate(makeUIUpdate), _remoteUser);

            }
        }

        //窗体加载
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            _userTb.Text = string.Empty;
            _userTb.Focus();
            //追踪鼠标滚轮事件，注意最后一个参数，防止已经被标记处理。
            _friendSendLb.AddHandler(ListBox.MouseWheelEvent, new MouseWheelEventHandler(_friendSendLb_MouseWheel),true);
            _youSendLb.AddHandler(ListBox.MouseWheelEvent, new MouseWheelEventHandler(_youSendLb_MouseWheel),true);
        }

        private void _youSendLb_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            ScrollViewer _youSendLb_scroll = FindVisualChild<ScrollViewer>(_youSendLb);
            ScrollViewer _friendSendLb_scroll = FindVisualChild<ScrollViewer>(_friendSendLb);
            int d = e.Delta;
            if (d > 0)
            {
                _youSendLb_scroll.LineUp();
                _friendSendLb_scroll.LineUp();
            }
            if (d < 0)
            {
                _youSendLb_scroll.LineDown();
                _friendSendLb_scroll.LineDown();
            }
            e.Handled= true;
        }

        private void _friendSendLb_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            ScrollViewer _youSendLb_scroll = FindVisualChild<ScrollViewer>(_youSendLb);
            ScrollViewer _friendSendLb_scroll = FindVisualChild<ScrollViewer>(_friendSendLb);
            int d = e.Delta;
            if (d > 0)
            {
                _youSendLb_scroll.LineUp();
                _friendSendLb_scroll.LineUp();
            }
            if (d < 0)
            {
                _youSendLb_scroll.LineDown();
                _friendSendLb_scroll.LineDown();
            }
            e.Handled = true;
        }


        public static T FindVisualChild<T>(DependencyObject obj) where T : DependencyObject
        {
            if (obj != null)
            {
                for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
                {
                    DependencyObject child = VisualTreeHelper.GetChild(obj, i);
                    if (child != null && child is T)
                    {
                        return (T)child;
                    }
                    T childItem = FindVisualChild<T>(child);
                    if (childItem != null) return childItem;
                }
            }
            return null;
        }

        //窗体关闭
        private void Window_Closed(object sender, EventArgs e)
        {
            TcpConnectToServer(tellToServerOffline);
            if (tcp_stream != null)
            {
                tcp_stream.Close();
                tcp_client.Close();
            }
            if (udp_background != null)
            {
                udp_background.Close();
                //_timer.Dispose();
            }
            //write to app.config
            if (_localIPAddr != null)
            {
                Configuration cf = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
                cf.AppSettings.Settings["localaddr"].Value = _localIPAddr.ToString();
                cf.Save(ConfigurationSaveMode.Modified);
            }
        }
        
        //发送消息告诉服务器，自己已经下线
        private void tellToServerOffline(NetworkStream stream)
        {
            string msg = "e" + _localUser.UserId;
            Byte[] data = new Byte[msg.Length];
            data = Encoding.Default.GetBytes(msg);
            stream.Write(data, 0, data.Length);
        }

        //后台线程监听TCP服务器发送的UDP信息
        private void UDpBackgroundForRecv()
        {
            if (_localUser.UserId > 0 && _localUser.Name != "")
            {
                try
                {
                    IPEndPoint udp_temp = new IPEndPoint(_localIPAddr, _tcp_client_port + 3);
                    udp_background = new UdpClient(udp_temp);
                    string ipaddr = ConfigurationManager.AppSettings["ipaddr"];
                    IPEndPoint remote = new IPEndPoint(IPAddress.Parse(ipaddr), 12999);
                    while (true)
                    {
                        Byte[] recvData = new Byte[256];
                        string msg = "";
                        recvData = udp_background.Receive(ref remote);
                        msg = Encoding.Default.GetString(recvData);
                        udpForUpdateState(msg);
                    }

                }
                catch (Exception ex)
                {
                    string t = ex.Message;
                }
            }
        }
        //处理UDP的信息
        private void udpForUpdateState(string msg)
        {
            User _remoteUser = new User();
            string[] peer_meg = msg.Split('|');
            for (int i = 0; i < peer_meg.Length - 1; i += 2)//0:userid 1:name+`(on line) or !(off line) or $(message)
            {
                string flag = peer_meg[i + 1].Substring(peer_meg[i + 1].Length - 1);
                if (flag == "`" || flag == "!")
                {
                    _remoteUser.UserId = int.Parse(peer_meg[i]);
                    _remoteUser.Name = peer_meg[i + 1].Substring(0, peer_meg[i + 1].Length - 1);
                    if (flag == "!")
                    {
                        _remoteUser.State =offLine;
                    }
                    else
                    {
                        _remoteUser.State =onLine;
                    }
                    _remoteUser.ImgSource = "Resource/someone.png";
                    _remoteUser.NotifyColor = @"#ffff00";
                    Dispatcher.BeginInvoke(DispatcherPriority.Send, new makeUIUpdateDelegate(makeUIUpdate), _remoteUser);
                }
                else
                {
                    string userid = peer_meg[i];
                    string message = peer_meg[i + 1].Substring(0, peer_meg[i + 1].Length - 1);
                    string temp = userid + "|" + message;
                    Dispatcher.BeginInvoke(DispatcherPriority.Send, new updateRecvMsgDelegate(updateRecvMsg), temp);
                }
            }
        }
        //异步更新UDP数据所包含的内容
        private void updateRecvMsg(string temp)
        {
            string[] msg = temp.Split('|');//0:userid 1:msg 
            int userid = int.Parse(msg[0]);
            //add to friendChatCollection
            ChatContent f = new ChatContent()
            {
                ImgSource = @"Resource/someone.png",
                FontColor = visibleColor,
                Content = msg[1]
            };
            var vf = from b in friendChatContentCollectionList
                     where (b.ID == userid)
                     select b;
            vf.ElementAt(0).ChatContentCollection.Add(f);
            _friendSendLb.ScrollIntoView(f);
            //notify you to read message.
            var notifyUser = from b in remoteUserCollection
                             where (b.UserId == userid)
                             select b;
            notifyUser.ElementAt(0).NotifyColor = notifyColor;

            //add to yourChatCollection
            ChatContent m = new ChatContent()
            {
                ImgSource = @"Resource/hidesomeone.png",
                FontColor = hideColor,
                Content = msg[1]
            };
            var vm = from b in myChatContentCollectionList
                     where (b.ID == userid)
                     select b;
            vm.ElementAt(0).ChatContentCollection.Add(m);
            _youSendLb.ScrollIntoView(m);
        }

        //改变选中好友列表中的某个
        private void _listFriendListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_listFriendListBox.SelectedIndex > 0)
            {
                _chatGrid.Visibility = Visibility.Visible;
                _currentSelectUser = remoteUserCollection[_listFriendListBox.SelectedIndex];
                (_stateBar.Children[1] as TextBlock).Text = _currentSelectUser.Name;
                (_stateBar.Children[2] as TextBlock).Text = _currentSelectUser.State;
                _currentSelectUser.NotifyColor = restoreColor;
                //update listbox's ItemsSource
                var vf = from b in friendChatContentCollectionList
                         where (b.ID == _currentSelectUser.UserId)
                         select b;
                _friendSendLb.ItemsSource = vf.ElementAt(0).ChatContentCollection;
                var vm = from b in myChatContentCollectionList
                         where (b.ID == _currentSelectUser.UserId)
                         select b;
                _youSendLb.ItemsSource = vm.ElementAt(0).ChatContentCollection;
            }
            else
            {
                _chatGrid.Visibility = Visibility.Hidden;
            }
        }
        //双击好友列表中的某个
        private void _listFriendListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (_listFriendListBox.SelectedIndex > 0)
            {
                _chatGrid.Visibility = Visibility.Visible;
                _currentSelectUser = remoteUserCollection[_listFriendListBox.SelectedIndex];
                (_stateBar.Children[1] as TextBlock).Text = _currentSelectUser.Name;
                (_stateBar.Children[2] as TextBlock).Text = _currentSelectUser.State;
                _currentSelectUser.NotifyColor = restoreColor;

                var vf = from b in friendChatContentCollectionList
                         where (b.ID == _currentSelectUser.UserId)
                         select b;
                _friendSendLb.ItemsSource = vf.ElementAt(0).ChatContentCollection;
                var vm = from b in myChatContentCollectionList
                         where (b.ID == _currentSelectUser.UserId)
                         select b;
                _youSendLb.ItemsSource = vm.ElementAt(0).ChatContentCollection;
            }
            else
            {
                _chatGrid.Visibility = Visibility.Hidden;
            }
        }

        //向TCP服务器发送信息，由它转发信息
        private void _sendBtn_Click(object sender, RoutedEventArgs e)
        {
            //_currentSelectUser
            if (_currentSelectUser.State !=offLine)
            {
                string text = "s" + _localUser.UserId.ToString() + "|" + _currentSelectUser.UserId.ToString()
								+ "|" + _sendMseeageTb.Text;
                Byte[] sendData = new Byte[text.Length];
                sendData = Encoding.Default.GetBytes(text);
                tcp_stream.BeginWrite(sendData, 0, sendData.Length, new AsyncCallback(
                    ar =>
                    {
                        tcp_stream.EndWrite(ar);
                        //MessageBox.Show("Send Ok!");
                    }
                    ), (object)string.Empty);
                //add to friendChatCollection
                ChatContent m = new ChatContent()
                {
                    ImgSource = @"Resource/me.png",
                    FontColor = visibleColor,
                    Content = _sendMseeageTb.Text
                };
                var vm = from b in myChatContentCollectionList
                         where (b.ID == _currentSelectUser.UserId)
                         select b;
                vm.ElementAt(0).ChatContentCollection.Add(m);
                _youSendLb.ScrollIntoView(m);
                
                //add to yourChatCollection
                ChatContent f = new ChatContent()
                {
                    ImgSource = @"Resource/hideme.png",
                    FontColor = hideColor,
                    Content = _sendMseeageTb.Text
                };
                var vf = from b in friendChatContentCollectionList
                         where (b.ID == _currentSelectUser.UserId)
                         select b;
                vf.ElementAt(0).ChatContentCollection.Add(f);
                _friendSendLb.ScrollIntoView(f);
                _sendMseeageTb.Text = string.Empty;
            }
        }
        //后退按钮
        private void _backBtn_Click(object sender, RoutedEventArgs e)
        {
            _chatGrid.Visibility = Visibility.Hidden;
        }

    }
}
