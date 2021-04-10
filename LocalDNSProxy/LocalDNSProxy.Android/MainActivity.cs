using System;

using Android.App;
using Android.Content.PM;
using Android.Runtime;
using Android.OS;
using Android.Content;
using Android.Net;
using Java.IO;
using System.Net.Sockets;
using System.Net;
using System.Threading.Tasks;
using Xamarin.Essentials;
using AndroidX.Core.App;
using DNS.Client.RequestResolver;
using DNS.Protocol.ResourceRecords;
using DNS.Protocol;
using DNS.Server;
using System.Threading;
using System.Linq;
using LeiKaiFeng.TCPIP;

namespace LocalDNSProxy.Droid
{

    public sealed class LocalRequestResolver : IRequestResolver
    {


        MasterFile File { get; }

        public LocalRequestResolver(MasterFile masterFile)
        {
            File = masterFile ?? throw new ArgumentNullException(nameof(masterFile));
        }

        public Task<IResponse> Resolve(IRequest request, CancellationToken cancellationToken = default)
        {

            IResponse response = File.Resolve(request, cancellationToken).Result;

            if (response.Questions.Count == 1)
            {
                Question question = response.Questions.First();

                if (question.Type == RecordType.A || question.Type == RecordType.AAAA)
                {
                    var resourceRecord =
                        (IPAddressResourceRecord)response
                        .AnswerRecords
                        .FirstOrDefault();

                    if (resourceRecord is null)
                    {
                        
                    }
                    else
                    {
                        response.AnswerRecords.Remove(resourceRecord);

                        response
                            .AnswerRecords
                            .Add(new IPAddressResourceRecord(
                                question.Name,
                                resourceRecord.IPAddress));
                    }
                }
            }


            return Task.FromResult(response);
        }


        public static Socket CreateDNSServer(
            MasterFile masterFile,
            IPAddress defaultDnsServer,
            IPEndPoint localDnsServerBind,
            IPEndPoint localUDPBind)
        {
            //为什么要包装一下MasterFile
            //主要是因为有的浏览器假如应答的域名是通配符，他会认为DNS无效
            //所以把通配符改为请求中的绝对域名
            //也就是说MasterFile会返回通配符

            DnsServer server = new DnsServer(new LocalRequestResolver(masterFile), defaultDnsServer);

            server.Listen(localDnsServerBind);


            Socket socket = new Socket(AddressFamily.InterNetwork, System.Net.Sockets.SocketType.Dgram, ProtocolType.Udp);

            socket.Bind(localUDPBind);

            socket.Connect(localDnsServerBind);

            return socket;
        }


        public static void Start(Func<byte[], int, int, int> read, Action<byte[], int, int> write, Socket socket)
        {
            var readPacket = new ReadUDPPacket(ushort.MaxValue);

            var writePacket = new WriteUDPPacket(ushort.MaxValue);

            //这个地方主要是为了简单，否则要保存链接之间的映射关系，还要必要使拆除
            //这样读一个请求，返回一个应答，不需要保存映射关系

            while (true)
            {
                writePacket.InitOffsetCount();

                if (readPacket.Read(read))
                {

                    socket.Send(readPacket.Array, readPacket.Offset, readPacket.Count, SocketFlags.None);


                    writePacket.WriteUDP((buffer, offset, count) => socket.Receive(buffer, offset, count, SocketFlags.None));

                    writePacket.WriteUDP(readPacket.Quaternion.Reverse());


                    writePacket.Write(write);
                }
                else
                {
                    
                }
            }
        }


    }




    [Service(Permission = "android.permission.BIND_VPN_SERVICE")]
    [IntentFilter(actions: new string[] { VpnService.ServiceInterface })]
    public sealed class MyVpnService : VpnService
    {
        private static readonly object s_lock = new object();

        public static void Log(object e)
        {
            string s = System.Environment.NewLine;

            lock (s_lock)
            {
                System.IO.File.AppendAllText($"/storage/emulated/0/textvpn.txt", $"{s}{s}{s}{s}{DateTime.Now}{s}{e}", System.Text.Encoding.UTF8);
            }
        }


        void Init()
        {
            const string CHNNEL_ID = "456784343";
            const string CHNNEL_NAME = "545765554";

            const int ID = 3435;

            ServerHelper.CreateNotificationChannel(this, CHNNEL_ID, CHNNEL_NAME);


            var func = ServerHelper.CreateServerNotificationFunc(AppInfo.Name, this, CHNNEL_ID);

            this.StartForeground(ID, func("Run"));
        }

        static Socket DNS()
        {

            MasterFile masterFile = new MasterFile();

            masterFile.AddIPAddressResourceRecord("*.iwara.tv", "141.101.120.83");
            masterFile.AddIPAddressResourceRecord("t66y.com", "141.101.120.83");
            masterFile.AddIPAddressResourceRecord("ajax.googleapis.com", "127.0.0.5");


            Socket socket = LocalRequestResolver.CreateDNSServer(masterFile,
                IPAddress.Parse("114.114.114.114"),
                new IPEndPoint(IPAddress.Loopback, 54663),
                new IPEndPoint(IPAddress.Loopback, 36645));

            return socket;

        }

        public override void OnCreate()
        {
            Init();


            var handle = new VpnService.Builder(this)
                .AddAddress("192.168.2.2", 24)
                .AddRoute("192.168.5.0", 24)
                .AddDnsServer("192.168.5.123")
                .SetBlocking(true)
                .AddDisallowedApplication(AppInfo.PackageName)
                .Establish();

            var inputStream = new ParcelFileDescriptor.AutoCloseInputStream(handle);

            var oustream = new ParcelFileDescriptor.AutoCloseOutputStream(handle);


            Task.Run(() => LocalRequestResolver.Start(inputStream.Read, oustream.Write, DNS()));

            
        }


    }

    public static class ServerHelper
    {
        public static void CreateNotificationChannel(ContextWrapper context, string channelID, string channelName)
        {
            if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
            {
                ((NotificationManager)context.GetSystemService(Context.NotificationService))
                            .CreateNotificationChannel(new NotificationChannel(channelID, channelName, NotificationImportance.Max) { LockscreenVisibility = NotificationVisibility.Public });
            }
        }


        public static void StartServer(ContextWrapper context, Intent intent)
        {
            if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
            {
                context.StartForegroundService(intent);
            }
            else
            {
                context.StartService(intent);
            }
        }

        public static Action<string> CreateUpServerNotificationFunc(ContextWrapper context, int notificationID, Func<string, Notification> func)
        {
            return (contentText) =>
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    ((NotificationManager)context.GetSystemService(Context.NotificationService))
                            .Notify(notificationID, func(contentText));
                });



            };


        }


        public static Func<string, Notification> CreateServerNotificationFunc(string contentTitle, Context context, string channelID)
        {
            return (contentText) =>
            {
                return new NotificationCompat.Builder(context, channelID)
                               .SetContentTitle(contentTitle)
                               .SetContentText(contentText)
                               .SetSmallIcon(Resource.Mipmap.icon)
                               .SetOngoing(true)
                               .Build();
            };
        }
    }



    [Activity(Label = "LocalDNSProxy", Icon = "@mipmap/icon", Theme = "@style/MainTheme", MainLauncher = true, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize )]
    public class MainActivity : global::Xamarin.Forms.Platform.Android.FormsAppCompatActivity
    {
        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            Xamarin.Essentials.Platform.Init(this, savedInstanceState);
            global::Xamarin.Forms.Forms.Init(this, savedInstanceState);
            LoadApplication(new App());

            CreateVpn();
        }

        void CreateVpn()
        {
            Intent inter = VpnService.Prepare(this);


            if (inter is null)
            {
                //null已经有权限

                this.OnActivityResult(0, Result.Ok, null);
            }
            else
            {
                this.StartActivityForResult(inter, 0);
            }
        }

        protected override void OnActivityResult(int requestCode, Result resultCode, Android.Content.Intent data)
        {
            if (resultCode == Result.Ok)
            {

                ServerHelper.StartServer(this, new Intent(this, typeof(MyVpnService)));
            }

        }


        public override void OnRequestPermissionsResult(int requestCode, string[] permissions, [GeneratedEnum] Android.Content.PM.Permission[] grantResults)
        {
            Xamarin.Essentials.Platform.OnRequestPermissionsResult(requestCode, permissions, grantResults);

            base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
        }
    }
}