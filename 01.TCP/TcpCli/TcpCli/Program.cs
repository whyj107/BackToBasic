using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace TcpCli
{
    class Program
    {
        static int i = 0;
        private static void Test(int idx)
        {
            try
            {
                // IP 주소와 포트를 지정하고 TCP 연결
                TcpClient tc = new TcpClient("localhost", 7000);

                string msg = $"{idx}: Hello World";
                byte[] buff = Encoding.ASCII.GetBytes(msg);

                // NetworkStream을 얻어옴
                NetworkStream stream = tc.GetStream();

                // 스트림에 바이트 데이타 전송
                stream.Write(buff, 0, buff.Length);
                Console.WriteLine($"{idx} at {DateTime.Now}");

                // 서버가 connection을 닫을 때까지 읽는 경우
                byte[] outbuf = new byte[1024];
                int nbytes;
                /*
                MemoryStream mem = new MemoryStream();
                while ((nbytes = stream.Read(outbuf, 0, outbuf.Length)) > 0)
                {
                    mem.Write(outbuf, 0, nbytes);
                }
                byte[] outbytes = mem.ToArray();
                mem.Close();
                Console.WriteLine(Encoding.ASCII.GetString(outbytes));
                */
                // 스트림과 TcpClient 객체 닫기
                stream.Close();
                tc.Close();

            }
            catch
            {
                // 연결 오류 발생
            }

        }

        private static void SocketTest()
        {
            // (1) 소켓 객체 생성 (TCP 소켓)
            Socket sock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            // (2) 서버에 연결
            var ep = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 7000);
            sock.Connect(ep);

            string cmd = string.Empty;
            byte[] receiverBuff = new byte[8192];

            Console.WriteLine("Connected... Enter Q to exit");

            // Q 를 누를 때까지 계속 Echo 실행
            while ((cmd = Console.ReadLine()) != "Q")
            {
                for (int i = 0; i < 100; i++)
                {
                    byte[] buff = Encoding.UTF8.GetBytes($"{i}");
                    // (3) 서버에 데이타 전송
                    sock.Send(buff, SocketFlags.None);

                    // (4) 서버에서 데이타 수신
                    int n = sock.Receive(receiverBuff);

                    string data = Encoding.UTF8.GetString(receiverBuff, 0, n);
                    Console.WriteLine(data);
                }
            }

            // (5) 소켓 닫기
            sock.Close();
        }

        // 클라이언트 데이터를 비동기식으로 읽기 위한 상태 개체
        public class StateObject
        {
            // 수신 버퍼의 크기
            public const int BufferSize = 256;
            // 수신 버퍼
            public byte[] buffer = new byte[BufferSize];
            // 수신 데이터 문자열
            public StringBuilder sb = new StringBuilder();
            // 클라이언트 소켓
            public Socket workSocket = null;
        }

        public class AsynchronousClient
        {
            // 포트 번호
            private const int port = 7000;

            // 수동 리셋 이벤트 인스턴스 신호
            private static ManualResetEvent connectDone = new ManualResetEvent(false);
            private static ManualResetEvent sendDone = new ManualResetEvent(false);
            private static ManualResetEvent receiveDone = new ManualResetEvent(false);

            // 원격 장치의 응답 
            private static String response = String.Empty;

            public static void StartClient()
            {

                // 원격 장치 연결 
                try
                {
                    // 소켓의 원격 끝점을 설정
                    IPEndPoint remoteEP = new IPEndPoint(IPAddress.Parse("127.0.0.1"), port);
                    // TCP/IP 소켓 생성  
                    Socket client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

                    // 원격 끝점과 연결  
                    client.BeginConnect(remoteEP, new AsyncCallback(ConnectCallback), client);

                    connectDone.WaitOne();

                    // 원격 기계에 테스트 데이터 전송  
                    Send(client, $"test: {i++}<EOF>");
                    sendDone.WaitOne();

                    // 원격 기계에서 응답 수신  
                    Receive(client);
                    receiveDone.WaitOne();

                    // 응답 내용 출력  
                    Console.WriteLine("Response received : {0}", response);
                    Console.WriteLine("============================================");
                    while (client.Connected)
                    {
                        // 소켓 해제
                        client.Shutdown(SocketShutdown.Both);
                        client.Close();
                    }

                }
                catch (Exception e)
                {
                    Console.WriteLine(e.ToString());
                }
            }

            private static void ConnectCallback(IAsyncResult ar)
            {
                try
                {
                    // 상태 개체에서 소켓을 검색  
                    Socket client = (Socket)ar.AsyncState;

                    // 연결 완료
                    client.EndConnect(ar);

                    Console.WriteLine("Socket connected to {0}", client.RemoteEndPoint.ToString());

                    // 연결되었음을 알리는 신호 
                    connectDone.Set();
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.ToString());
                }
            }

            private static void Receive(Socket client)
            {
                try
                {
                    // 상태 개체 생성  
                    StateObject state = new StateObject();
                    state.workSocket = client;

                    // 원격 장치에서 데이터 수신 시작
                    client.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0, new AsyncCallback(ReceiveCallback), state);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.ToString());
                }
            }

            private static void ReceiveCallback(IAsyncResult ar)
            {
                try
                {
                    // 비동기 상태 개체에서 상태 개체 및 클라이언트 소켓을 검색 
                    StateObject state = (StateObject)ar.AsyncState;
                    Socket client = state.workSocket;

                    // 원격 장치에서 데이터 수신  
                    int bytesRead = client.EndReceive(ar);

                    if (bytesRead > 0)
                    {
                        // 데이터가 더 있을 수 있으므로 지금까지 수신한 데이터 저장
                        state.sb.Append(Encoding.ASCII.GetString(state.buffer, 0, bytesRead));

                        // 나머지 데이터 가져옴
                        client.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0, new AsyncCallback(ReceiveCallback), state);
                    }
                    else
                    {
                        // 모든 데이터가 도착했으므로 응답 변수에 넣어서 확인
                        if (state.sb.Length > 1)
                        {
                            response = state.sb.ToString();
                        }
                        // 모든 바이트가 수신되었음을 나타냄
                        receiveDone.Set();
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.ToString());
                }
            }

            private static void Send(Socket client, String data)
            {
                // ASCII 인코딩을 사용하여 문자열 데이터를 바이트 데이터로 변환
                byte[] byteData = Encoding.ASCII.GetBytes(data);

                Console.WriteLine(data);

                // 원격 장치로 데이터 전송 시작
                client.BeginSend(byteData, 0, byteData.Length, 0, new AsyncCallback(SendCallback), client);
            }

            private static void SendCallback(IAsyncResult ar)
            {
                try
                {
                    // 상태 개체에서 소켓을 검색 
                    Socket client = (Socket)ar.AsyncState;

                    // 원격 장치로 데이터 전송 완료
                    int bytesSent = client.EndSend(ar);
                    Console.WriteLine("Sent {0} bytes to server.", bytesSent);

                    // 모든 바이트가 전송되었음을 나타냄
                    sendDone.Set();
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.ToString());
                }
            }

            public static int Main(String[] args)
            {
                /*
                for(int i=0; i<10; i++)
                {
                    Test(i);
                }
                */
                // SocketTest();

                for(int i=0; i<1000; i++)
                {
                    StartClient();
                }
                return 0;
            }
        }

    }
}
