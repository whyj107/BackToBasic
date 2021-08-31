using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace SocketServer
{
    class Program
    {
        public static Socket listener;
        private static bool IsListening = true;
        private static List<Socket> connectedClients = new List<Socket>();
        private static string ipAddress = "127.0.0.1";
        private static int port = 7000;

        // 클라이언트 데이터를 비동기식으로 읽기 위한 상태 개체
        public class StateObject
        {
            // 수신 버퍼의 크기
            public const int BufferSize = 1024;
            // 수신 버퍼
            public byte[] buffer = new byte[BufferSize];
            // 수신 데이터 문자열
            public StringBuilder sb = new StringBuilder();
            // 클라이언트 소켓
            public Socket workSocket = null;

            public System.Timers.Timer timer = new System.Timers.Timer();
            public double receive_cnt = 10;

            public void CountDown()
            {
                if (timer.Enabled)
                {
                    timer.Stop();
                }
                receive_cnt = 10;
                timer.Interval = 1000;
                timer.Elapsed += new System.Timers.ElapsedEventHandler(timer_Elapsed);
                timer.Start();
            }
            private void timer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
            {
                Console.WriteLine(DateTime.Now + " : " + receive_cnt );
                receive_cnt--;
                if(receive_cnt < 0)
                {
                    receive_cnt = 0;
                    timer.Stop();
                }
            }
        }

        public class AsynchronousSocketListener
        {
            // 스레드 신호
            public static ManualResetEvent allDone = new ManualResetEvent(false);

            public static void StartListening()
            {
                // "127.0.0.1" 서버에 연결  
                IPEndPoint localEndPoint = new IPEndPoint(IPAddress.Parse(ipAddress), port);

                // TCP/IP 소켓 생성  
                listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

                // 소켓을 로컬 끝점에 바인딩하고 들어오는 연결을 수신
                try
                {
                    listener.Bind(localEndPoint);
                    
                    // 모든 클라이언트에서 오는 요청을 받음
                    // listener.Bind(new IPEndPoint(IPAddress.Any, 7000));
                    
                    listener.Listen(100);

                    while (true)
                    {
                        // 이벤트를 비신호 상태로 설정  
                        allDone.Reset();

                        // 연결을 수신하려면 비동기 소켓을 시작 
                        Console.WriteLine("Waiting for a connection...");
                        listener.BeginAccept(new AsyncCallback(AcceptCallback), listener);

                        // 연결이 될 때까지 기다린 후 계속 진행
                        allDone.WaitOne();
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine("StartListening\n: " + e.Message);
                }

                // Console.WriteLine("\nPress ENTER to continue...");
                // Console.Read();

            }

            // IAsyncResult : 비동기적으로 작동할 수 있는 메서드를 포함하는 클래스에 의해 구현됨
            public static void AcceptCallback(IAsyncResult ar)
            {
                try
                {
                    // 계속하려면 메인 스레드에 신호를 보냄  
                    allDone.Set();

                    if (!IsListening)
                    {
                        return;
                    }

                    // 클라이언트 요청을 처리하는 소켓을 가져옴 
                    Socket l = (Socket)ar.AsyncState;
                    Socket handler = l.EndAccept(ar);

                    // List에 추가
                    connectedClients.Add(handler);

                    // 상태 개체 생성
                    StateObject state = new StateObject();
                    state.workSocket = handler;

                    // 시간 재기 시작
                    if (!state.timer.Enabled)
                    {
                        state.CountDown();
                    }

                    // AsyncCallback : 해당 비동기 작업을 완료할 때 호출되는 메서드를 참조합니다.
                    // public delegate void AsyncCallback(IAsyncResult ar);형식이 사용됨
                    handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0, new AsyncCallback(ReadCallback), state);
                }
                catch(Exception ex)
                {
                    Console.WriteLine("AcceptCallback\n: " + ex.Message);
                }
            }

            public static void ReadCallback(IAsyncResult ar)
            {
                try
                {
                    String content = String.Empty;

                    // 비동기 상태 개체에서 상태 개체 및 처리기 소켓을 검색
                    StateObject state = (StateObject)ar.AsyncState;
                    Socket handler = state.workSocket;

                    // 만약 receive_cnt가 0이면(5초 동안 아무것도 받지 못하면) 종료
                    if (state.receive_cnt == 0)
                    {
                        state.timer.Stop();

                        if (connectedClients.Contains(handler))
                        {
                            connectedClients.Remove(handler);
                        }

                        handler.Shutdown(SocketShutdown.Both);
                        handler.Close();
                        handler.Dispose();
                        Console.WriteLine("handler 종료");
                    }

                    // 클라이언트 소켓에서 데이터 읽기
                    int bytesRead = handler.EndReceive(ar);

                    if (bytesRead > 0)
                    {
                        // 더 많은 데이터가 있을 수 있으므로 지금까지 수신한 데이터를 저장 
                        state.sb.Append(Encoding.ASCII.GetString(state.buffer, 0, bytesRead));

                        // 파일 끝 태그를 확인, 없으면 더 많은 데이터를 읽기
                        content = state.sb.ToString();
                        if (content.IndexOf("<EOF>") > -1)
                        {
                            // 모든 데이터를 클라이언트에서 읽은 후 콘솔에 표시  
                            Console.WriteLine("Read {0} bytes from socket. \n Data : {1}", content.Length, content);
                            // 데이터를 클라이언트로 반향
                            Send(handler, $"ok: {content}");
                            state.sb.Clear();
                            // 시간 다시 세기
                            state.receive_cnt = 10;
                        }
                    }
                    // Receive를 새로 설정해줘서 다음 값을 받을 수 있도록 설정
                    handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0, new AsyncCallback(ReadCallback), state);
                }
                catch(Exception ex)
                {
                    Console.WriteLine("ReadCallback\n: " + ex.Message);
                }
            }

            private static void Send(Socket handler, String data)
            {
                try
                {
                    // ASCII 인코딩을 사용하여 문자열 데이터를 바이트 데이터로 변환 
                    byte[] byteData = Encoding.ASCII.GetBytes(data);

                    // 원격 장치로 데이터 전송을 시작 
                    handler.BeginSend(byteData, 0, byteData.Length, 0, new AsyncCallback(SendCallback), handler);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Send\n: " + ex.Message);
                }
            }

            private static void SendCallback(IAsyncResult ar)
            {
                try
                {
                    // 상태 개체에서 소켓을 검색
                    Socket handler = (Socket)ar.AsyncState;

                    // 원격 장치로 데이터 전송 
                    int bytesSent = handler.EndSend(ar);
                    // Console.WriteLine("Sent {0} bytes to client.", bytesSent);
                    Console.WriteLine("==========================================", bytesSent);

                    // Socket을 닫아버린다.
                    // handler.Shutdown(SocketShutdown.Both);
                    // handler.Close();
                }
                catch (Exception ex)
                {
                    Console.WriteLine("SendCallback\n: " + ex.Message);
                }
            }

            public static void StopListening()
            {
                if(listener != null)
                {
                    IsListening = false;
                    try
                    {
                        foreach (Socket socket in connectedClients)
                        {
                            if (socket.Connected)
                            {
                                socket.Disconnect(false);
                                socket.Shutdown(SocketShutdown.Both);
                                socket.Dispose();
                            }
                        }
                        connectedClients.Clear();

                        listener.Close();
                        listener.Dispose();
                    }
                    catch(Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                    }
                }
            }

            public static void Main(string[] args)
            {
                StartListening();
            }
        }
    }
}
