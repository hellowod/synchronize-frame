using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace AsycServer
{
    public class Client
    {
        public int ID;
        public IPEndPoint tcpAdress, udpAdress;

        private AyncServer server;
        private Socket socket;
        private Stopwatch pingWatch;

        public bool Pinging
        {
            get {
                return pingWatch != null;
            }
        }

        public Client(int id, Socket sock, AyncServer serv)
        {
            ID = id;
            server = serv;
            socket = sock;

            tcpAdress = (IPEndPoint)sock.RemoteEndPoint;
            Thread t = new Thread(AliveThread);
            t.Start();
        }

        public void SendAcceptPoll()
        {
            socket.Send(BitConverter.GetBytes(ID));
        }

        private void AliveThread()
        {
            while (IsConnected() && server.Active) {
                Thread.Sleep(1000);
            }

            Disconnect();
        }

        private bool IsConnected()
        {
            try {
                if (udpAdress != null) socket.Send(new byte[] { 0 });
                return true;
            } catch (SocketException e) {
                server.CatchException(e);
                return false;
            } catch (Exception e) {
                server.CatchException(e);
                return false;
            }
        }

        public void Send(MessageBuffer msg)
        {
            server.Send(msg, this);
        }

        public void Disconnect()
        {
            if (socket == null) {
                return;
            }

            socket.Close();
            socket = null;
            server.ClientDisconnected(this);
        }

        public void Ping()
        {
            if (Pinging) {
                server.PingResult(this, pingWatch.Elapsed.Milliseconds);
                pingWatch = null;
            } else {
                pingWatch = Stopwatch.StartNew();
                server.Send(new MessageBuffer(new byte[] { AyncServer.pingByte }), this);
            }
        }
    }
}
