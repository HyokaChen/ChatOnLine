using ChatOnline.Core;
using log4net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

//[assembly: log4net.Config.XmlConfigurator(Watch = true)]
namespace ChatOnLineServer
{
    class Program
    {
        private static ILog log = LogManager.GetLogger(typeof(Program));
        private static TcpListener server;
        private DataService dataService;
        private object locker;
        private object portLocker = new object();
        private string ipaddr;
        public Program()
        {
            dataService = new DataService();
            dataService.ConnectToDatabase();
            dataService.CreateTable();
            locker = new object();
        }
        ~Program()
        {
            server.Stop();
            dataService.CloseDatabase();
        }
        private void TcpListen(string ipaddr, string port)
        {
            try
            {
                IPAddress localAddr = IPAddress.Parse(ipaddr);
                this.ipaddr = ipaddr;
                server = new TcpListener(localAddr, int.Parse(port));

                // Start listening for client requests.
                server.Start();
                log.Info("Waiting for a connection...");
                ThreadPool.SetMaxThreads(20, 10);
                // Enter the listening loop.
                while (true)
                {
                    if (server.Pending())
                    {
                        TcpClient client = server.AcceptTcpClient();
                        ThreadPool.QueueUserWorkItem(new WaitCallback(ClientHandler), client);
                        //Thread TalkThread = new Thread(new ParameterizedThreadStart(TalkInfo))
                        //{
                        //    IsBackground = true
                        //};
                        //TalkThread.Start(client);
                    }
                    //Console.WriteLine(client.Client.LocalEndPoint.ToString());
                }
            }
            catch (SocketException e)
            {
                log.Error("SocketException:::", e);
            }
            log.Info("Hit enter to continue...");
        }


        private void ClientHandler(object obj)
        {
            if (obj is TcpClient client)
            {
                try
                {
                    StringBuilder stringBuilder = new StringBuilder("", 256);
                    int i = 0;
                    Byte[] data = new Byte[256];
                    //get client's NetworkStream.
                    using (NetworkStream stream = client.GetStream())
                    {
                        while ((i = stream.Read(data, 0, data.Length)) != 0)
                        {
                            var dataStr = Encoding.Default.GetString(data, 0, i);
                            stringBuilder.Append(dataStr);
                            //dataStr += client.Client.RemoteEndPoint.ToString();
                            //switch (dataStr.First())
                            //{
                            //    case 'a'://find friend,and add friend.
                            //             //Console.WriteLine(dataStr);
                            //        QueryUser(dataStr, stream);
                            //        break;
                            //    case 'e'://tell server off line.
                            //             //Console.WriteLine(dataStr);
                            //        ExitUser(dataStr, stream);
                            //        break;
                            //    case 's':
                            //        //
                            //        SendMessageToFriend(dataStr);
                            //        break;
                            //    default://sign in.
                            //            //Console.WriteLine(dataStr);
                            //        UserLogin(dataStr, stream);
                            //        break;
                            //}
                        }
                        var message = JsonConvert.DeserializeObject<MessageModel>(stringBuilder.ToString());
                        switch (message.MsgCategory)
                        {
                            case MsgType.SignIn:
                                UserLogin(message, stream);
                                break;
                            case MsgType.LoginOut:
                                ExitUser(message);
                                break;
                            default:
                                break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    log.Error("ClientHandler Error:::", ex);
                }
                finally
                {
                    dataService.CloseDatabase();
                    client.Close();
                }
            }
        }

        //send message to friend.
        private void SendMessageToFriend(string dataStr)
        {
            string[] strInfo = dataStr.Split('|');//0:userid  1:friendid  2:message 3:ipendpoint
            string ipendpoint = string.Empty;
            string username = string.Empty;
            string friendStr = strInfo[1];
            UdpClient localudp = new UdpClient(new IPEndPoint(IPAddress.Parse(ipaddr), 12999));
            if (dataService.FindIPEndPoint(int.Parse(strInfo[1]), out ipendpoint))//friend on line
            {
                lock (portLocker)
                {
                    IPEndPoint remoteIpEndPoint = new IPEndPoint(
                            IPAddress.Parse(ipendpoint.Split(':')[0]), int.Parse(ipendpoint.Split(':')[1]) + 3);
                    Byte[] data = new Byte[strInfo[0].Length + strInfo[2].Length + 1];
                    data = Encoding.Default.GetBytes(strInfo[0].Substring(1) + "|" + strInfo[2] + "$|");
                    //Console.WriteLine("tell friend i am on line:" + strInfo[0] + "|" + strInfo[1] + "|" + strInfo[3]);
                    localudp.Send(data, data.Length, remoteIpEndPoint);
                    //Console.WriteLine(strInfo[0].Substring(1) + "|" + strInfo[2] + "$|");
                }
            }
            localudp.Close();
        }

        private void ExitUser(MessageModel model)
        {

        }

        //user exit, and tell user's friend who are on line that he is exit.
        private void ExitUser(string dataStr, NetworkStream stream)
        {
            string userid = dataStr.Split('|')[0].Substring(1);
            string name = string.Empty;
            dataService.FindUsers(int.Parse(userid), out name);
            dataStr = dataStr.Split('|')[0].Substring(1) + "|" + name + "!";//structure string.
            lock (locker)
            {
                dataService.ExitUser(int.Parse(userid));
                Console.WriteLine("exit user {0}", dataStr);
            }

            List<int> userList = dataService.FindFriendUserinfoByFriendId(userid);
            SendUserInfoToFriend(userList, dataStr);
        }

        //find user's information,and insert info into friend table.
        private void QueryUser(string dataStr, NetworkStream stream)
        {
            string ipendpoint = string.Empty;
            string friendname = string.Empty;
            string[] temp = dataStr.Split('|');//0:userid 1:friendid 2:ipendpoint
            int userid = int.Parse(temp[0].Substring(1));
            int friendid = int.Parse(temp[1]);
            bool isFriendOnLine = false;
            if (dataService.FindUsers(friendid, out friendname))
            {
                Byte[] data = new Byte[friendid.ToString().Length + friendname.Length + 2];
                if (dataService.FindIPEndPoint(friendid))//friend on line
                {
                    isFriendOnLine = true;
                    data = Encoding.Default.GetBytes(friendid + "|" + friendname + "`|");

                    //Console.WriteLine(userid+" query resualt:"+ friendname + "|" + ipendpoint);
                }
                else//friend off line.
                {
                    data = Encoding.Default.GetBytes(friendid + "|" + friendname + "!|");
                    isFriendOnLine = false;
                }
                stream.Write(data, 0, data.Length);
            }
            if (isFriendOnLine)
            {
                bool hasBeFriend = dataService.FindFriend(userid, friendid);
                lock (locker)
                {
                    if (!hasBeFriend)
                    {
                        dataService.InsertFriendTable(userid, friendid);
                        dataService.InsertFriendTable(friendid, userid);
                        Console.WriteLine("insert friend info ok!");
                    }
                }

                //synchronous to friend.tell he that I have been your friend.
                List<int> list = new List<int>
                {
                    friendid
                };
                dataService.FindUsers(userid, out string name);
                dataStr = userid + "|" + name + "`";
                SendUserInfoToFriend(list, dataStr);
            }
        }
        private void UserLogin(MessageModel model, NetworkStream stream)
        {
            bool hasFindUser = dataService.FindUsers(model.UserId);
            bool hasFindIPEndPoint = dataService.FindIPEndPoint(model.UserId);
            if (!hasFindUser)
            {
                dataService.InsertUserInfoTable(model.UserId, model.Name, model.Password);
                log.Info($"insert user {model.UserId}");
            }
            if (!hasFindIPEndPoint)
            {
                dataService.InsertIPeEndPointTable(model.UserId, model.IP, model.Port);
                log.Info($"insert ipendpoint {model.UserId}");
            }
            if (hasFindUser)
            {
                List<int> friendidList = dataService.FindFriends(model.UserId);
                SendFriendInfoToUser(friendidList, stream);
                List<int> useridList = dataService.FindOwners(model.UserId);

            }
        }

        //user sign in.
        //private void UserLogin(string dataStr, NetworkStream stream)
        //{
        //    string[] strInfo = dataStr.Split('|');//0:userid  1:name 2:i or none 3:ipendpoint
        //    Console.WriteLine(dataStr + "---Connected!");
        //    bool hasFindUser = dataService.FindUsers(strInfo[0]);
        //    bool hasFindIPEndPoint = dataService.FindIPEndPoint(strInfo[0]);
        //    lock (locker)
        //    {
        //        if (!hasFindUser)
        //        {
        //            dataService.InsertUserInfoTable(strInfo[0], strInfo[1], strInfo[3]);
        //            Console.WriteLine("insert user info ok!");
        //        }
        //        if (!hasFindIPEndPoint)
        //        {
        //            dataService.InsertIPeEndPointTable(strInfo[0], strInfo[3]);
        //            Console.WriteLine("insert ipendpoint ok!");
        //        }
        //    }

        //    //get friend's information .
        //    if (strInfo[2] != "")
        //    {
        //        List<int> friendidList = new List<int>();
        //        string sql = "SELECT friendid from frienduserinfo WHERE userid=";
        //        dataService.FindFriend(sql, strInfo[0], friendidList, "friendid");
        //        GetFriendInfoToUser(friendidList, stream);
        //    }

        //    //send user's information to friend who are on line.On the other port.
        //    List<int> userList = new List<int>();
        //    string sqlTx = "SELECT userid from frienduserinfo WHERE friendid=";
        //    dataService.FindFriend(sqlTx, strInfo[0], userList, "userid");
        //    dataStr = strInfo[0] + "|" + strInfo[1] + "`";
        //    SendUserInfoToFriend(userList, dataStr);//parameter dataStr necessary
        //}

        //send user's information to friend who are on line.
        private void SendUserInfoToFriend(List<int> list, string dataStr)
        {
            string[] strInfo = dataStr.Split('|');//0:userid  1:name + ` or !
            string ipendpoint = string.Empty;
            string username = string.Empty;
            //string friendStr = "";

            UdpClient localudp = new UdpClient(new IPEndPoint(IPAddress.Parse(ipaddr), 12999));
            foreach (var item in list)
            {
                //friendStr = li.ToString();
                if (dataService.FindIPEndPoint(item))
                {
                    dataService.FindIPEndPoint(item, out ipendpoint);
                    lock (portLocker)
                    {
                        IPEndPoint remoteIpEndPoint = new IPEndPoint(
                                IPAddress.Parse(ipendpoint.Split(':')[0]), int.Parse(ipendpoint.Split(':')[1]) + 3);
                        Byte[] data = new Byte[strInfo[0].Length + strInfo[1].Length + 2];
                        data = Encoding.Default.GetBytes(strInfo[0] + "|" + strInfo[1] + "|");
                        //Console.WriteLine("tell friend i am on line:" + strInfo[0] + "|" + strInfo[1] + "|" + strInfo[3]);
                        localudp.Send(data, data.Length, remoteIpEndPoint);
                        //Console.WriteLine(strInfo[0] + "|" + strInfo[1] + "|");
                    }
                }
            }
            localudp.Close();
        }

        private void SendFriendInfoToUser(List<int> list, NetworkStream stream)
        {
            foreach (var friend_id in list)
            {
                if (dataService.FindUsers(friend_id, out string name))
                {
                    var model = new MessageModel()
                    {
                        UserId = friend_id,
                        Name = name
                    };
                    if (dataService.FindIPEndPoint(friend_id, out string ipendpoint))
                    {
                        var temp = ipendpoint.Split(':');
                        int.TryParse(temp[1], out int port);
                        model.IP = temp[0];
                        model.Port = port;
                        model.MsgCategory = MsgType.Online;
                    }
                    else
                    {
                        model.MsgCategory = MsgType.Offline;
                    }
                    var obj = JsonConvert.SerializeObject(model);
                    Byte[] data = new Byte[obj.Length];
                    data = Encoding.Default.GetBytes(obj);
                    stream.Write(data, 0, data.Length);
                    stream.Flush();
                }
            }
        }

        //get friend's information.
        private void GetFriendInfoToUser(List<int> list, NetworkStream stream)
        {
            string ipendpoint = string.Empty;
            string friendname = string.Empty;
            //int friendidStr = "";
            foreach (var friendid in list)
            {
                //friendidStr = friendid.ToString();
                if (dataService.FindUsers(friendid, out friendname))
                {
                    Byte[] data = new Byte[friendid.ToString().Length + friendname.Length + 2];
                    if (dataService.FindIPEndPoint(friendid))//friend on line.
                    {
                        data = Encoding.Default.GetBytes(friendid + "|" + friendname + "`|");
                    }
                    else//friend off line.
                    {
                        data = Encoding.Default.GetBytes(friendid + "|" + friendname + "!|");
                    }
                    stream.Write(data, 0, data.Length);

                }
            }
            Byte[] endData = new Byte[1];
            endData = Encoding.Default.GetBytes("#");//end to send.
            stream.Write(endData, 0, endData.Length);
        }

        static void Main(string[] args)
        {
            Program server = new Program();
            string ipaddr = ConfigurationManager.AppSettings["ipaddr"];
            string port = ConfigurationManager.AppSettings["port"];
            server.TcpListen(ipaddr, port);
        }
    }
}
