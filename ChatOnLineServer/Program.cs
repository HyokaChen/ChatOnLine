using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace ChatOnLineServer
{
    class Program
    {
        private static TcpListener server;
        private DataService dataService;
        private object locker;
        private object portLocker = new object();
        private string ipaddr;
        public Program()
        {
            dataService = new DataService();
            dataService.connectToDatabase();
            dataService.createTable();
            locker = new object();
        }
        ~Program()
        {
            server.Stop();
            dataService.closeDatabase();
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
                Console.Write("Waiting for a connection...\n");
                // Enter the listening loop.
                while (true)
                {
                    if (server.Pending())
                    {
                        TcpClient tempclient = server.AcceptTcpClient();
                        Thread TalkThread = new Thread(new ParameterizedThreadStart(TalkInfo));
                        TalkThread.IsBackground = true;
                        TalkThread.Start(tempclient);
                    }
                    //Console.WriteLine(client.Client.LocalEndPoint.ToString());
                }
            }
            catch (SocketException e)
            {
                Console.WriteLine("SocketException: {0}", e);
            }
            Console.WriteLine("\nHit enter to continue...");
        }


        private void TalkInfo(object obj)
        {
            TcpClient p = obj as TcpClient;
            try
            {
                string dataStr = "";
                int i = 0;
                Byte[] data = new Byte[256];
                //get client's NetworkStream.
                using (NetworkStream stream = p.GetStream())
                {
                    while ((i = stream.Read(data, 0, data.Length)) != 0)
                    {
                        dataStr = Encoding.Default.GetString(data, 0, i);
                        dataStr += "|";
                        dataStr += p.Client.RemoteEndPoint.ToString();
                        switch (dataStr.First())
                        {
                            case 'a'://find friend,and add friend.
                                //Console.WriteLine(dataStr);
                                queryUser(dataStr, stream);
                                break;
                            case 'e'://tell server off line.
                                //Console.WriteLine(dataStr);
                                exitUser(dataStr, stream);
                                break;
                            case 's':
                                //
                                sendMessageToFriend(dataStr);
                                break;
                            default://sign in.
                                //Console.WriteLine(dataStr);
                                UserLogin(dataStr, stream);
                                break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error:{0}", ex.Message);
            }
            finally
            {
                p.Close();
            }
        }

        //send message to friend.
        private void sendMessageToFriend(string dataStr)
        {
            string[] strInfo = dataStr.Split('|');//0:userid  1:friendid  2:message 3:ipendpoint
            string ipendpoint = string.Empty;
            string username = string.Empty;
            string friendStr = strInfo[1];
            UdpClient localudp = new UdpClient(new IPEndPoint(IPAddress.Parse(ipaddr), 12999));
            if (dataService.findIPEndPoint(strInfo[1], out ipendpoint))//friend on line
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

        //user exit, and tell user's friend who are on line that he is exit.
        private void exitUser(string dataStr, NetworkStream stream)
        {
            string userid = dataStr.Split('|')[0].Substring(1);
            string name = string.Empty;
            dataService.findUsers(userid, out name);
            dataStr = dataStr.Split('|')[0].Substring(1) + "|" + name + "!";//structure string.
            lock (locker)
            {
                dataService.exitUser(userid);
                Console.WriteLine("exit user {0}", dataStr);
            }
            List<int> userList = new List<int>();
            string sql = "SELECT userid from frienduserinfo WHERE friendid=";
            dataService.findFriend(sql, userid, userList, "userid");
            sendUserInfoToFriend(userList, dataStr);
        }

        //find user's information,and insert info into friend table.
        private void queryUser(string dataStr, NetworkStream stream)
        {
            string ipendpoint = string.Empty;
            string friendname = string.Empty;
            string[] temp = dataStr.Split('|');//0:userid 1:friendid 2:ipendpoint
            string userid = temp[0].Substring(1);
            string friendid = temp[1];
            bool isFriendOnLine = false;
            if (dataService.findUsers(friendid, out friendname))
            {
                Byte[] data = new Byte[friendid.Length + friendname.Length + 2];
                if (dataService.findIPEndPoint(friendid))//friend on line
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
                bool hasBeFriend = dataService.findFriend(userid, friendid);
                lock (locker)
                {
                    if (!hasBeFriend)
                    {
                        dataService.insertFriendTable(userid, friendid);
                        dataService.insertFriendTable(friendid, userid);
                        Console.WriteLine("insert friend info ok!");
                    }
                }

                //synchronous to friend.tell he that I have been your friend.
                List<int> list = new List<int>();
                list.Add(int.Parse(friendid));
                string name;
                dataService.findUsers(userid, out name);
                dataStr = userid + "|" + name + "`";
                sendUserInfoToFriend(list, dataStr);
            }
        }

        //user sign in.
        private void UserLogin(string dataStr, NetworkStream stream)
        {
            string[] strInfo = dataStr.Split('|');//0:userid  1:name 2:i or none 3:ipendpoint
            Console.WriteLine(dataStr + "---Connected!");
            bool hasFindUser = dataService.findUsers(strInfo[0]);
            bool hasFindIPEndPoint = dataService.findIPEndPoint(strInfo[0]);
            lock (locker)
            {
                if (!hasFindUser)
                {
                    dataService.insertUserInfoTable(strInfo[0], strInfo[1], strInfo[3]);
                    Console.WriteLine("insert user info ok!");
                }
                if (!hasFindIPEndPoint)
                {
                    dataService.insertIPeEndPointTable(strInfo[0], strInfo[3]);
                    Console.WriteLine("insert ipendpoint ok!");
                }
            }

            //get friend's information .
            if (strInfo[2] != "")
            {
                List<int> friendidList = new List<int>();
                string sql = "SELECT friendid from frienduserinfo WHERE userid=";
                dataService.findFriend(sql, strInfo[0], friendidList, "friendid");
                getFriendInfoToUser(friendidList, stream);
            }

            //send user's information to friend who are on line.On the other port.
            List<int> userList = new List<int>();
            string sqlTx = "SELECT userid from frienduserinfo WHERE friendid=";
            dataService.findFriend(sqlTx, strInfo[0], userList, "userid");
            dataStr = strInfo[0] + "|" + strInfo[1] + "`";
            sendUserInfoToFriend(userList, dataStr);//parameter dataStr necessary
        }

        //send user's information to friend who are on line.
        private void sendUserInfoToFriend(List<int> list, string dataStr)
        {
            string[] strInfo = dataStr.Split('|');//0:userid  1:name + ` or !
            string ipendpoint = string.Empty;
            string username = string.Empty;
            string friendStr = "";

            UdpClient localudp = new UdpClient(new IPEndPoint(IPAddress.Parse(ipaddr), 12999));
            foreach (var li in list)
            {
                friendStr = li.ToString();
                if (dataService.findIPEndPoint(friendStr))
                {
                    dataService.findIPEndPoint(friendStr, out ipendpoint);
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

        //get friend's information.
        private void getFriendInfoToUser(List<int> list, NetworkStream stream)
        {
            string ipendpoint = string.Empty;
            string friendname = string.Empty;
            string friendidStr = "";
            foreach (var friendid in list)
            {
                friendidStr = friendid.ToString();
                if (dataService.findUsers(friendidStr, out friendname))
                {
                    Byte[] data = new Byte[friendidStr.Length + friendname.Length + 2];
                    if (dataService.findIPEndPoint(friendidStr))//friend on line.
                    {
                        data = Encoding.Default.GetBytes(friendidStr + "|" + friendname + "`|");
                    }
                    else//friend off line.
                    {
                        data = Encoding.Default.GetBytes(friendidStr + "|" + friendname + "!|");
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
            Program a = new Program();
            string ipaddr = ConfigurationManager.AppSettings["ipaddr"];
            string port = ConfigurationManager.AppSettings["port"];
            a.TcpListen(ipaddr, port);
        }
    }
}
