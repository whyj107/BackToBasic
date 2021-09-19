using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace TcpCli
{
    class Program
    {
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
            private const int port = 11000;

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
                    // TIME OUT 없애기 위해서 옵션 추가
                    client.LingerState = new LingerOption(true, 0);

                    for(int j=0; j<3; j++)
                    {
                        if (!client.Connected)
                        {
                            connectDone.Reset();
                            // 원격 끝점과 연결  
                            client.BeginConnect(remoteEP, new AsyncCallback(ConnectCallback), client);
                            connectDone.WaitOne();
                        }

                        if (client.Connected)
                        {
                            // 원격 기계에 테스트 데이터 전송  
                            sendDone.Reset();
                            Send(client, $"test: {j}<EOF>");
                            sendDone.WaitOne();

                            // 원격 기계에서 응답 수신  
                            receiveDone.Reset();
                            Receive(client);
                            receiveDone.WaitOne();

                            // 응답 내용 출력  
                            Console.WriteLine("Response received : {0}", response);
                            Console.WriteLine("============================================");
                            response = String.Empty;
                        }
                    }

                    while (client.Connected)
                    {
                        // 소켓 해제
                        client.Disconnect(false);
                        // client.Shutdown(SocketShutdown.Both);
                        client.Close();
                        client.Dispose();
                    }

                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
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
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
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
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
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

                        string tmp = state.sb.ToString();
                        if(tmp.IndexOf("<EOF>") > -1)
                        {
                            if (state.sb.Length > 1)
                            {
                                response = state.sb.ToString();
                            }

                            receiveDone.Set();
                        }
                        else
                        {
                            // 나머지 데이터 가져옴
                            client.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0, new AsyncCallback(ReceiveCallback), state);
                        }
                    }
                    else
                    {
                        client.Disconnect(true);
                        response = "데이터 값 없음";
                        receiveDone.Set();
                        return;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
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
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
            }

            public static int Main(String[] args)
            {
                for(int i=0; i<3; i++)
                {
                    StartClient();
                }
                return 0;
            }
        }

    }
}
