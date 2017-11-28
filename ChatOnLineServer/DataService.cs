using log4net;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Text;

namespace ChatOnLineServer
{
    public class DataService
    {
        private static ILog log = LogManager.GetLogger(typeof(Program));
        //数据库连接
        private SQLiteConnection m_dbConnection;
        private string datasource = @".\datebase.db";

        public DataService()
        {
            if (!File.Exists(datasource))
            {
                CreateNewDatabase();
            }
        }

        //创建一个空的数据库
        public void CreateNewDatabase()
        {
            SQLiteConnection.CreateFile(datasource);
        }

        //创建一个连接到指定数据库
        public void ConnectToDatabase()
        {
            m_dbConnection = new SQLiteConnection($@"Data Source={ datasource }");
            try
            {
                m_dbConnection.Open();
                log.Info("SQLite initialize");
            }
            catch (Exception ex)
            {
                log.Error("SQLite Connect fail:::", ex);
            }

        }
        //Close database
        public void CloseDatabase()
        {
            //string sql = "drop table ipEndPoint";
            //SQLiteCommand command = new SQLiteCommand(sql, m_dbConnection);
            //command.ExecuteNonQuery();
            m_dbConnection.Close();
        }
        //在指定数据库中创建一个table
        public void CreateTable()
        {
            SQLiteCommand command;

            //user information table. 
            string sql = "create table if not exists userinfo (userid int primary key not null, name varchar(20)), password varchar(100))";
            command = new SQLiteCommand(sql, m_dbConnection);
            command.ExecuteNonQuery();

            //the information table of users' friends. 
            sql = "create table if not exists relationinfo (userid int not null,friendid int not null,constraint pk_a1 primary key (userid,friendid))";
            command = new SQLiteCommand(sql, m_dbConnection);
            command.ExecuteNonQuery();

            //create temp table for IPEndPoint //ip2port
            sql = "create table if not exists ipEndPoint (userid int primary key not null,ipendpoint varchar(30))";
            command = new SQLiteCommand(sql, m_dbConnection);
            command.ExecuteNonQuery();
        }

        //插入UserInfo数据
        public void InsertUserInfoTable(int userid, string name, string password)
        {
            string sql = @"insert into userinfo (userid, name, password) values ( @userid, @name, @password)";
            SQLiteCommand command = new SQLiteCommand(sql, m_dbConnection);
            //command.Parameters.Add(new SQLiteParameter("@userid", DbType.Int32)
            //{
            //    Value = userid
            //});
            //command.Parameters.Add(new SQLiteParameter("@name", DbType.AnsiString)
            //{
            //    Value = name
            //});
            //command.Parameters.Add(new SQLiteParameter("@password", DbType.AnsiString)
            //{
            //    Value = password
            //});
            SQLiteParameter[] parames ={
                new SQLiteParameter("@userid",DbType.Int32){Value= userid},
                new SQLiteParameter("@name",DbType.AnsiString){Value = name},
                new SQLiteParameter("@password",DbType.AnsiString){Value=password}
            };
            command.Parameters.AddRange(parames);

            command.ExecuteNonQuery();
        }

        //insert ipendpoint info
        public void InsertIPeEndPointTable(int userid, string ip, int port)
        {
            string ipendpoint = $"{ip}:{port}";
            string sql = @"insert into ipEndPoint (userid, ipendpoint) values (@userid, @ipendpoint)";
            SQLiteCommand command = new SQLiteCommand(sql, m_dbConnection);
            //command.Parameters.Add(new SQLiteParameter("@userid", DbType.Int32)
            //{
            //    Value = userid
            //});
            //command.Parameters.Add(new SQLiteParameter("@ipendpoint", DbType.AnsiString)
            //{
            //    Value = ipendpoint
            //});
            SQLiteParameter[] parames ={
                new SQLiteParameter("@userid",DbType.Int32){Value= userid},
                new SQLiteParameter("@ipendpoint",DbType.AnsiString){Value = ipendpoint}
            };
            command.Parameters.AddRange(parames);

            command.ExecuteNonQuery();
        }
        //insert friend info
        public void InsertFriendTable(int userid, int friendid)
        {
            string sql = "insert into relationinfo (userid,friendid) values (@userid,@friendid)";
            SQLiteCommand command = new SQLiteCommand(sql, m_dbConnection);
            SQLiteParameter[] parames ={
                new SQLiteParameter("@userid",DbType.Int32){Value= userid},
                new SQLiteParameter("@friendid",DbType.AnsiString){Value = friendid}
            };
            command.Parameters.AddRange(parames);
            command.ExecuteNonQuery();
        }
        //使用sql查询语句，并显示结果
        public bool FindUsers(int userid)
        {
            string sql = "SELECT count(0) from userinfo WHERE userid=@userid";
            SQLiteCommand command = new SQLiteCommand(sql, m_dbConnection);
            command.Parameters.Add(new SQLiteParameter("@userid", DbType.Int32)
            {
                Value = userid
            });

            return command.ExecuteNonQuery() > 0;
            //SQLiteDataReader reader = command.ExecuteReader(System.Data.CommandBehavior.SingleRow);
            //if (reader.HasRows)
            //{
            //    //Console.WriteLine("UserID: " + reader["userid"] + "\tName: " + reader["name"]);
            //    return true;
            //}
            //return false;
        }
        public bool FindUsers(int userid, out string name)
        {
            string sql = "SELECT name from userinfo WHERE userid=@userid";
            SQLiteCommand command = new SQLiteCommand(sql, m_dbConnection);
            command.Parameters.Add(new SQLiteParameter("@userid", DbType.Int32)
            {
                Value = userid
            });
            SQLiteDataReader reader = command.ExecuteReader(System.Data.CommandBehavior.SingleRow);
            while (reader.Read())
            {
                name = reader["name"] as string;
                //Console.WriteLine("UserID: " + userid + "\tName: " + name);
                return true;
            }
            name = string.Empty;
            return false;
        }
        public void ExitUser(int userid)
        {
            if (FindIPEndPoint(userid))
            {
                string sql = "delete from ipEndPoint where userid=@userid";
                SQLiteCommand command = new SQLiteCommand(sql, m_dbConnection);
                command.Parameters.Add(new SQLiteParameter("@userid", DbType.Int32)
                {
                    Value = userid
                });
                command.ExecuteNonQuery();
                log.Info($"delete:::{userid}");
            }
        }
        public bool FindIPEndPoint(int userid)
        {
            string sql = "SELECT 1 from ipEndPoint WHERE userid=@userid";
            SQLiteCommand command = new SQLiteCommand(sql, m_dbConnection);
            command.Parameters.Add(new SQLiteParameter("@userid", DbType.Int32)
            {
                Value = userid
            });
            SQLiteDataReader reader = command.ExecuteReader(System.Data.CommandBehavior.SingleRow);
            if (reader.HasRows)
            {
                //Console.WriteLine("UserID: " + reader["userid"] + "\nIPEndPoint: " + reader["ipendpoint"]);
                return true;
            }
            return false;
        }

        public bool FindIPEndPoint(int userid, out string ipendpoint)
        {
            string sql = "SELECT ipendpoint from ipEndPoint WHERE userid=@userid";
            int i = 0;
            SQLiteCommand command = new SQLiteCommand(sql, m_dbConnection);
            command.Parameters.Add(new SQLiteParameter("@userid", DbType.Int32)
            {
                Value = userid
            });
            SQLiteDataReader reader = command.ExecuteReader(System.Data.CommandBehavior.SingleRow);
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
        public bool FindFriend(int userid, int friendid)
        {
            string sql = "SELECT * from relationinfo WHERE userid=@userid and friendid=@friendid";
            int i = 0;
            SQLiteCommand command = new SQLiteCommand(sql, m_dbConnection);
            SQLiteParameter[] parames =
            {
                new SQLiteParameter("@userid",DbType.Int32){Value = userid},
                new SQLiteParameter("@friendid",DbType.Int32){Value = friendid}
            };
            command.Parameters.AddRange(parames);
            SQLiteDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                i++;
                //Console.WriteLine("UserID: " + reader["userid"] + "\nIPEndPoint: " + reader["ipendpoint"]);
                return true;
            }
            return false;
        }

        [Obsolete("NOT NEED AGAIN")]
        public List<int> FindFriendUserinfoByFriendId(string id)
        {
            //sql += id;
            //int i = 0;
            //int friendid = 0;
            //List<string> temp = new List<string>();

            List<int> friendList = new List<int>();
            string sql = "SELECT userid from frienduserinfo WHERE friendid=@friendid";
            SQLiteCommand command = new SQLiteCommand(sql, m_dbConnection);
            command.Parameters.Add(new SQLiteParameter("@friendid")
            {
                Value = id
            });
            SQLiteDataAdapter dataAdapter = new SQLiteDataAdapter(command);
            DataSet dataSet = new DataSet();
            dataAdapter.Fill(dataSet, "frienduserinfo");
            var dt = dataSet.Tables[0];
            foreach (DataRow item in dt.Rows)
            {
                friendList.Add(int.Parse(item["userid"].ToString()));
            }

            return friendList;
            //SQLiteDataReader reader = command.ExecuteReader();
            //while (reader.Read())
            //{
            //    i++;
            //    friendid = (int)reader[idtype];
            //    //Console.WriteLine(idtype+":" + reader[idtype]);
            //    //temp.Add(friendid);
            //    friendList.Add(friendid);
            //    //Console.WriteLine("UserID: " + reader["userid"] + "\nIPEndPoint: " + reader["ipendpoint"]);
            //}
            ////friendList.AddRange(temp);
        }

        public List<int> FindFriends(int userid)
        {
            List<int> friendidList = new List<int>();
            string sql = "SELECT friendid from relationinfo WHERE userid=@userid";
            int i = 0;
            SQLiteCommand command = new SQLiteCommand(sql, m_dbConnection);
            command.Parameters.Add(new SQLiteParameter("@userid", DbType.Int32)
            {
                Value = userid
            });
            SQLiteDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                i++;
                friendidList.Add((int)reader["friendid"]);
            }
            return friendidList;
        }

        public List<int> FindOwners(int friendid)
        {
            List<int> useridList = new List<int>();
            string sql = "SELECT userid from relationinfo WHERE friendid=@friendid";
            int i = 0;
            SQLiteCommand command = new SQLiteCommand(sql, m_dbConnection);
            command.Parameters.Add(new SQLiteParameter("@friendid", DbType.Int32)
            {
                Value = friendid
            });
            SQLiteDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                i++;
                useridList.Add((int)reader["userid"]);
            }
            return useridList;
        }
    }

}
