using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace TcpSrvAsync
{
    class Program
    {
        /// <summary>
        /// http://www.csharpstudy.com/net/article/6 예제
        /// </summary>
        /// <param name="args"></param>
        static void Main(string[] args)
        {
            AysncEchoServer().Wait();
        }

        async static Task AysncEchoServer()
        {
            // 로컬 포트 7000을 Listen
            TcpListener listener = new TcpListener(IPAddress.Any, 7000);
            listener.Start();
            while (true)
            {
                // TcpClient Connection 요청을 받아들여
                // 서버에서 새 TcpClient 객체를 생성하여 리턴
                // 비동기 Accept
                TcpClient tc = await listener.AcceptTcpClientAsync().ConfigureAwait(false);

                // 새 쓰레드에서 처리
                await Task.Factory.StartNew(AsyncTcpProcess, tc);
            }
        }

        async static void AsyncTcpProcess(object o)
        {
            TcpClient tc = (TcpClient)o;

            int MAX_SIZE = 1924;
            // TcpClient 객체에서 NetworkStream을 얻음
            NetworkStream stream = tc.GetStream();

            // 비동기 수신
            var buff = new byte[MAX_SIZE];
            // 클라이언트가 연결을 끊을 때까지 데이터를 수신
            var nbytes = await stream.ReadAsync(buff, 0, buff.Length).ConfigureAwait(false);
            if(nbytes > 0)
            {
                string msg = Encoding.ASCII.GetString(buff, 0, nbytes);
                Console.WriteLine($"{msg} at {DateTime.Now}");

                // 비동기 송신
                await stream.WriteAsync(buff, 0, nbytes).ConfigureAwait(false);
            }

            // 스트림과 TcpClient 객체
            stream.Close();
            tc.Close();
        }

        /// <summary>
        /// 수신시 타임아웃 기능 추가 예제
        /// 수신 Task와 타임아웃 Task를 생성하고 Task.WhenAnu()를 사용하여 2개 중 하나가 완료되면 리턴하도록 설정
        /// </summary>
        /// <returns></returns>
        async static Task AysncEchoServerTimeOut()
        {
            int MAX_SIZE = 1924;

            TcpListener listener = new TcpListener(IPAddress.Any, 7000);
            listener.Start();
            
            while (true)
            {
                // 비동기 Accept
                TcpClient tc = await listener.AcceptTcpClientAsync().ConfigureAwait(false);

                NetworkStream stream = tc.GetStream();

                var buff = new byte [MAX_SIZE];
                var readTask = stream.ReadAsync(buff, 0, buff.Length);
                var timeoutTask = Task.Delay(10 * 1000); // 10초
                var doneTask = await Task.WhenAny(timeoutTask, readTask).ConfigureAwait(false);

                // 타임 아웃일 경우
                if(doneTask == timeoutTask)
                {
                    var bytes = Encoding.ASCII.GetBytes("Read Timeout Error");
                    await stream.WriteAsync(bytes, 0, bytes.Length);
                }
                // 타임 아웃이 아닐 경우 == 수신 성공
                else
                {
                    int nbytes = readTask.Result;
                    if(nbytes > 0)
                    {
                        await stream.WriteAsync(buff, 0, nbytes).ConfigureAwait(false);
                    }
                }

                stream.Close();
                tc.Close();
            }
        }
    }
}
