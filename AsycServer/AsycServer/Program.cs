using System;
using System.Collections.Generic;
using System.Threading;

namespace AsycServer
{
    class Program
    {
        static void Main(string[] args)
        {
            Program p = new Program();
            while (true) {
                p.Logic();
                Thread.Sleep(10);
            }
        }

        private AyncServer server;

        private List<Client> clientList = new List<Client>();
        private Dictionary<Client, int> userList = new Dictionary<Client, int>();
        // 关键帧
        private Dictionary<int, Dictionary<int, List<string>>> keyDic = new Dictionary<int, Dictionary<int, List<string>>>();
        // 客户端的人物id
        private int roleId = 100000;
        // 当前帧数
        private int frameCount = 1;

        public Program()
        {
            server = new AyncServer(1255, 1337);

            AyncServer.DebugInfo.upData = true;

            server.OnStart += OnStart;
            server.OnConnect += OnConnect;
            server.OnMessage += OnMessage;
            server.OnDisconnect += OnDisconnect;
            server.OnDebug += OnDebug;

            Thread t = new Thread(InputThread);
            t.Start();
        }

        public void Logic()
        {
            server.Update();
        }

        public void OnStart()
        {
            Console.WriteLine("Server started!");
        }

        public void OnConnect(Client client)
        {
            Console.WriteLine("{0}[{1}, {2}] connected!", client.ID, client.tcpAdress, client.udpAdress);
            clientList.Add(client);

            MessageBuffer msg = new MessageBuffer();
            msg.WriteInt(CProto.CONNECT);
            msg.WriteInt(roleId);
            client.Send(msg);
            roleId++;
        }

        public void OnMessage(Client client, MessageBuffer message)
        {
            int cproto = message.ReadInt();
            switch (cproto) {
                case CProto.CONNECT:
                    break;
                case CProto.READY:
                    if (!userList.ContainsKey(client)) {
                        int id = message.ReadInt();
                        userList.Add(client, id);
                    }
                    //所有的玩家都准备好了，可以开始同步
                    if (userList.Count >= clientList.Count) {
                        frameCount = 1;
                        keyDic = new Dictionary<int, Dictionary<int, List<string>>>();
                        string playStr = "";
                        List<string> playList = new List<string>();
                        foreach (var play in userList) {
                            CharData charData = new CharData(play.Value, play.Value + "_" + play.Value);
                            playList.Add(charData.ToString());
                        }
                        playStr = string.Join(";", playList.ToArray());
                        MessageBuffer buff = new MessageBuffer();
                        buff.WriteInt(CProto.START);
                        buff.WriteString(playStr);

                        for (int i = 0; i < clientList.Count; ++i) {
                            clientList[i].Send(buff);
                        }
                    }
                    break;
                case CProto.SYNC_POS:
                    for (int i = 0; i < clientList.Count; ++i) {
                        if (client == clientList[i]) {
                            continue;
                        }
                        clientList[i].Send(message);
                    }
                    break;
                case CProto.SYNC_KEY:
                    int clientCurFrameCount = message.ReadInt();
                    string keyStr = message.ReadString();
                    if (keyDic.ContainsKey(clientCurFrameCount)) {
                        if (keyDic[clientCurFrameCount].ContainsKey(userList[client])) {
                            keyDic[clientCurFrameCount][userList[client]].Add(keyStr);
                        } else {
                            keyDic[clientCurFrameCount][userList[client]] = new List<string>();
                            keyDic[clientCurFrameCount][userList[client]].Add(keyStr);
                        }
                    } else {
                        keyDic[clientCurFrameCount] = new Dictionary<int, List<string>>();
                        keyDic[clientCurFrameCount][userList[client]] = new List<string>();
                        keyDic[clientCurFrameCount][userList[client]].Add(keyStr);
                    }
                    if (clientCurFrameCount == frameCount) {
                        if (keyDic[clientCurFrameCount].Count == clientList.Count) {
                            List<string> keyDataList = new List<string>();
                            foreach (var dataList in keyDic[clientCurFrameCount].Values) {
                                keyDataList.AddRange(dataList);
                            }

                            string keyData = string.Join(";", keyDataList.ToArray());
                            MessageBuffer buff = new MessageBuffer();
                            buff.WriteInt(CProto.SYNC_KEY);
                            buff.WriteInt(frameCount);
                            buff.WriteString(keyData);
                            for (int i = 0; i < clientList.Count; ++i) {
                                clientList[i].Send(buff);
                            }
                            frameCount += 1;
                        }
                    }
                    break;
                case CProto.START:
                    break;
            }
        }

        public void OnDisconnect(Client c)
        {
            Console.WriteLine("{0}[{1}, {2}] disconnected!", c.ID, c.tcpAdress, c.udpAdress);
            clientList.Remove(c);
            if (userList.ContainsKey(c)) {
                userList.Remove(c);
            }
        }

        public void OnDebug(string s)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine(s);
            Console.ForegroundColor = ConsoleColor.Gray;
        }

        public void InputThread()
        {
            while (true) {
                string input = Console.ReadLine();

                if (server.Active) {
                    string[] inputArgs = input.Split(' ');
                    if (inputArgs[0] == "quit") {
                        server.Close();
                    }
                    if (inputArgs[0] == "kick") {
                        server.GetClient(int.Parse(inputArgs[1])).Disconnect();
                    }
                } else {
                    if (input == "start") {
                        server.StartUp("127.0.0.1");
                    }
                }
            }
        }
    }
}
