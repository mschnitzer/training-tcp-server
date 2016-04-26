using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace tcpserver
{
    class Program
    {
        private static Int32 port = 3000;
        private static List<int> clientIDs = new List<int>();

        private struct ClientInfo
        {
            public int clientid;
            public TcpClient socket;
            public DateTime connected;
        };

        private class ClientMessages
        {
            public List<String> messages;
        };

        private static ClientMessages[] clientMessages = new ClientMessages[100];

        static void Main(string[] args)
        {
            Console.WriteLine("Starting server on port {0}...", port);

            TcpListener server = null;
            IPAddress localAddr = IPAddress.Parse("127.0.0.1");

            // start listen
            server = new TcpListener(localAddr, port);
            server.Start();

            int clientid = -1;

            while (true)
            {
                // block until a new client tries to connect
                TcpClient client = server.AcceptTcpClient();

                clientid = -1;

                try
                {
                    // reserve and receive a new client id
                    clientid = ReserveClientID();

                    Console.WriteLine("Client {0} connected!", clientid);

                    // add client info structure
                    ClientInfo cinfo;
                    cinfo.clientid = clientid;
                    cinfo.socket = client;
                    cinfo.connected = DateTime.Now;

                    // handle new client in a separate thread
                    var thread = new Thread(ClientThread);
                    thread.Start(cinfo);
                }
                catch (MaxClientsReachedException)
                {
                    // kick client
                    SendStreamMessage(client, "Max clients reached!");
                    client.Close();
                }
            }
        }

        private static int ReserveClientID()
        {
            // as specified, a maximum of 100 clients are allowed to connect to the server.
            // this can be adapted. 
            for (int i = 0; i < 100; i++)
            {
                if (clientIDs.IndexOf(i) == -1)
                {
                    // reserve free client id
                    clientIDs.Add(i);
                    return i;
                }
            }

            // maximum number of clients reached -> throw exception
            throw new MaxClientsReachedException();
        }

        private static void ClientThread(Object clientInfo)
        {
            ClientInfo client = (ClientInfo)clientInfo;

            Byte[] bytes = new Byte[256];
            String data = null;

            data = null;

            // fetch stream ressource
            NetworkStream stream = client.socket.GetStream();

            try
            {
                // start reading from the socket - it raises a System.IO.IOException
                // if there was an error while reading from the socket
                int i;
                while ((i = stream.Read(bytes, 0, bytes.Length)) != 0)
                {
                    // convert ASCII bytes to a String object
                    data = System.Text.Encoding.ASCII.GetString(bytes, 0, i);

                    // save message (thread-safe!)
                    lock (clientMessages)
                    {
                        if (clientMessages[client.clientid] == null)
                        {
                            clientMessages[client.clientid] = new ClientMessages();
                            clientMessages[client.clientid].messages = new List<String>();
                        }

                        clientMessages[client.clientid].messages.Add(data);
                    }

                    Console.WriteLine("Received ({0}): {1}", client.clientid, data);

                    if (data == "/clientinfo")
                    {
                        // if the client sent "/clientinfo" as message over the socket, we'll
                        // gonna send him some information about his connection
                        SendStreamMessage(
                            client.socket, // the socket ressource from the client
                            "client id: " + client.clientid.ToString() + ", connected since: " + client.connected.ToString("MM/dd/yyyy HH:mm")
                        );
                    }
                    else
                    {
                        // if no command was specified, send him his own message back
                        SendStreamMessage(client.socket, "Your message: " + data.ToString());
                    }
                }

                client.socket.Close();
            }
            catch (System.IO.IOException)
            {
                // a stream error occourred (probably the client disconnected)
                Console.WriteLine("Client {0} disconnected...", client.clientid);
            }

            // this code will be executed when either the server or the client has closed the connection
            if (clientMessages[client.clientid] != null)
            {
                clientMessages[client.clientid] = null;
            }

            RemoveClient(client.clientid);
        }

        private static void RemoveClient(int clientid)
        {
            clientIDs.Remove(clientid);
        }

        private static void SendStreamMessage(TcpClient client, String message)
        {
            // fetch stream ressource
            NetworkStream stream = client.GetStream();

            // convert a String object to ASCII bytes
            byte[] msg = System.Text.Encoding.ASCII.GetBytes(message);

            // write bytes to stream
            stream.Write(msg, 0, msg.Length);
        }
    }
}
