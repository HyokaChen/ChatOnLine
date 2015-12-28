using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Text;

namespace ChatOnLineServer
{
        public class DataService
        {
            //数据库连接
            private SQLiteConnection m_dbConnection;
            private string datasource = @".\test.db";
            public DataService()
            {
                if (!File.Exists(datasource))
                {
                    createNewDatabase();
                }
            }

            //创建一个空的数据库
            public void createNewDatabase()
            {
                SQLiteConnection.CreateFile(datasource);
            }

            //创建一个连接到指定数据库
            public void connectToDatabase()
            {
                m_dbConnection = new SQLiteConnection(@"Data Source=.\test.db");
                try
                {
                    m_dbConnection.Open();
                    Console.WriteLine("OK");
                }
                catch (Exception ex)
                {
                    Console.WriteLine("SQLite Connect fail: {0} ", ex.Message);
                }

            }
            //Close database
            public void closeDatabase()
            {
                //string sql = "drop table ipEndPoint";
                //SQLiteCommand command = new SQLiteCommand(sql, m_dbConnection);
                //command.ExecuteNonQuery();
                m_dbConnection.Close();
            }
            //在指定数据库中创建一个table
            public void createTable()
            {
                SQLiteCommand command;

                //user information table. 
                string sql = "create table if not exists userinfo (userid int primary key not null,name varchar(20))";
                command = new SQLiteCommand(sql, m_dbConnection);
                command.ExecuteNonQuery();

                //the information table of users' friends. 
                sql = "create table if not exists frienduserinfo (userid int not null,friendid int not null,constraint pk_a1 primary key (userid,friendid))";
                command = new SQLiteCommand(sql, m_dbConnection);
                command.ExecuteNonQuery();

                //create temp table for IPEndPoint
                sql = "create table if not exists ipEndPoint (userid int primary key not null,ipendpoint varchar(30))";
                command = new SQLiteCommand(sql, m_dbConnection);
                command.ExecuteNonQuery();
            }

            //插入UserInfo数据
            public void insertUserInfoTable(string userid, string name, string ipendpoint)
            {
                string sql = @"insert into userinfo (userid, name) values (" + userid + @",'" + name + @"')";
                SQLiteCommand command = new SQLiteCommand(sql, m_dbConnection);
                command.ExecuteNonQuery();
            }

            //insert ipendpoint info
            public void insertIPeEndPointTable(string userid, string ipendpoint)
            {
                string sql = @"insert into ipEndPoint (userid, ipendpoint) values (" + userid + ",'" + ipendpoint + "')";
                SQLiteCommand command = new SQLiteCommand(sql, m_dbConnection);
                command.ExecuteNonQuery();
            }
            //insert friend info
            public void insertFriendTable(string userid, string friendid)
            {
                string sql = "insert into frienduserinfo (userid,friendid) values (" + userid + "," + friendid + ")";
                SQLiteCommand command = new SQLiteCommand(sql, m_dbConnection);
                command.ExecuteNonQuery();
            }
            //使用sql查询语句，并显示结果
            public bool findUsers(string userid)
            {
                string sql = "SELECT * from userinfo WHERE userid=" + userid;
                int i = 0;
                SQLiteCommand command = new SQLiteCommand(sql, m_dbConnection);
                SQLiteDataReader reader = command.ExecuteReader();
                while (reader.Read())
                {
                    i++;
                    //Console.WriteLine("UserID: " + reader["userid"] + "\tName: " + reader["name"]);
                    return true;
                }
                return false;
            }
            public bool findUsers(string userid, out string name)
            {
                string sql = "SELECT name from userinfo WHERE userid=" + userid;
                int i = 0;
                SQLiteCommand command = new SQLiteCommand(sql, m_dbConnection);
                SQLiteDataReader reader = command.ExecuteReader();
                while (reader.Read())
                {
                    i++;
                    name = reader["name"] as string;
                    //Console.WriteLine("UserID: " + userid + "\tName: " + name);
                    return true;
                }
                name = string.Empty;
                return false;
            }
            public void exitUser(string userid)
            {
                if (findIPEndPoint(userid))
                {
                    string sql = "delete from ipEndPoint where userid=" + userid;
                    SQLiteCommand command = new SQLiteCommand(sql, m_dbConnection);
                    command.ExecuteNonQuery();
                    Console.WriteLine("delete :{0}", userid);
                }
            }
            public bool findIPEndPoint(string userid)
            {
                string sql = "SELECT * from ipEndPoint WHERE userid=" + userid;
                int i = 0;
                SQLiteCommand command = new SQLiteCommand(sql, m_dbConnection);
                SQLiteDataReader reader = command.ExecuteReader();
                while (reader.Read())
                {
                    i++;
                    //Console.WriteLine("UserID: " + reader["userid"] + "\nIPEndPoint: " + reader["ipendpoint"]);
                    return true;
                }
                return false;
            }

            public bool findIPEndPoint(string userid, out string ipendpoint)
            {
                string sql = "SELECT ipendpoint from ipEndPoint WHERE userid=" + userid;
                int i = 0;
                SQLiteCommand command = new SQLiteCommand(sql, m_dbConnection);
                SQLiteDataReader reader = command.ExecuteReader();
                while (reader.Read())
                {
                    i++;
                    ipendpoint = reader["ipendpoint"] as string;
                    //Console.WriteLine("UserID: " + reader["userid"] + "\nIPEndPoint: " + reader["ipendpoint"]);
                    return true;
                }
                ipendpoint = "";
                return false;
            }
            public bool findFriend(string userid, string friendid)
            {
                string sql = "SELECT * from frienduserinfo WHERE userid=" + userid + " and friendid=" + friendid;
                int i = 0;
                SQLiteCommand command = new SQLiteCommand(sql, m_dbConnection);
                SQLiteDataReader reader = command.ExecuteReader();
                while (reader.Read())
                {
                    i++;
                    //Console.WriteLine("UserID: " + reader["userid"] + "\nIPEndPoint: " + reader["ipendpoint"]);
                    return true;
                }
                return false;
            }

            public void findFriend(string sql, string id, List<int> friendList, string idtype)
            {
                sql += id;
                int i = 0;
                int friendid = 0;
                //List<string> temp = new List<string>();
                SQLiteCommand command = new SQLiteCommand(sql, m_dbConnection);
                SQLiteDataReader reader = command.ExecuteReader();
                while (reader.Read())
                {
                    i++;
                    friendid = (int)reader[idtype];
                    //Console.WriteLine(idtype+":" + reader[idtype]);
                    //temp.Add(friendid);
                    friendList.Add(friendid);
                    //Console.WriteLine("UserID: " + reader["userid"] + "\nIPEndPoint: " + reader["ipendpoint"]);
                }
                //friendList.AddRange(temp);
            }
    }

}
