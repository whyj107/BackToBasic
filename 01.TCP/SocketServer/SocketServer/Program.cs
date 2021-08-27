using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace SocketServer
{
    class Program
    {
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
        }

        public class AsynchronousSocketListener
        {
            // 스레드 신호
            public static ManualResetEvent allDone = new ManualResetEvent(false);

            public static void StartListening()
            {
                // "127.0.0.1" 서버에 연결  
                IPEndPoint localEndPoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 7000);

                // TCP/IP 소켓 생성  
                Socket listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

                // 소켓을 로컬 끝점에 바인딩하고 들어오는 연결을 수신
                try
                {
                    listener.Bind(localEndPoint);
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
                    Console.WriteLine(e.ToString());
                }

                Console.WriteLine("\nPress ENTER to continue...");
                Console.Read();

            }

            // IAsyncResult : 비동기적으로 작동할 수 있는 메서드를 포함하는 클래스에 의해 구현됨
            public static void AcceptCallback(IAsyncResult ar)
            {
                // 계속하려면 메인 스레드에 신호를 보냄  
                allDone.Set();

                // 클라이언트 요청을 처리하는 소켓을 가져옴 
                Socket listener = (Socket)ar.AsyncState;
                Socket handler = listener.EndAccept(ar);

                // 상태 개체 생성
                StateObject state = new StateObject();
                state.workSocket = handler;

                // AsyncCallback : 해당 비동기 작업을 완료할 때 호출되는 메서드를 참조합니다.
                // public delegate void AsyncCallback(IAsyncResult ar);형식이 사용됨
                handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0, new AsyncCallback(ReadCallback), state);
            }

            public static void ReadCallback(IAsyncResult ar)
            {
                String content = String.Empty;

                // 비동기 상태 개체에서 상태 개체 및 처리기 소켓을 검색
                StateObject state = (StateObject)ar.AsyncState;
                Socket handler = state.workSocket;

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
                    }
                    else
                    {
                        // 일부 데이터가 수신되지 않음, 없으면 더 읽음
                        handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0, new AsyncCallback(ReadCallback), state);
                    }
                }
            }

            private static void Send(Socket handler, String data)
            {
                // ASCII 인코딩을 사용하여 문자열 데이터를 바이트 데이터로 변환 
                byte[] byteData = Encoding.ASCII.GetBytes(data);

                // 원격 장치로 데이터 전송을 시작 
                handler.BeginSend(byteData, 0, byteData.Length, 0, new AsyncCallback(SendCallback), handler);
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

                    handler.Shutdown(SocketShutdown.Both);
                    handler.Close();
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.ToString());
                }
            }

            public static void Main(string[] args)
            {
                StartListening();
            }
        }
    }
}
