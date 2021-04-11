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
using System.Collections.Generic;

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

            var task = server.Listen(localDnsServerBind);

            task.ContinueWith((t) =>
            {
                if (task.Exception != null)
                {
                    MyVpnService.Log(task.Exception);
                }
            });



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

        bool _isrun;

        void Init()
        {
            const string CHNNEL_ID = "456784343";
            const string CHNNEL_NAME = "545765554";

            const int ID = 3435;

            ServerHelper.CreateNotificationChannel(this, CHNNEL_ID, CHNNEL_NAME);


            var func = ServerHelper.CreateServerNotificationFunc(AppInfo.Name, this, CHNNEL_ID);

            this.StartForeground(ID, func("Run"));
        }

        static Socket DNS(string ip, string hosts)
        {
            MasterFile masterFile = new MasterFile();


            foreach (var item in MainActivity.CreateMasterFile(hosts))
            {
                MyVpnService.Log($"{item.Item1} {item.Item2}");

                masterFile.AddIPAddressResourceRecord(item.Item1, item.Item2);
            }

            

            Socket socket = LocalRequestResolver.CreateDNSServer(masterFile,
                IPAddress.Parse(ip),
                new IPEndPoint(IPAddress.Loopback, 54663),
                new IPEndPoint(IPAddress.Loopback, 36645));


            return socket;

        }

        public override void OnCreate()
        {
            Init();

            _isrun = false;
            
        }

        [return: GeneratedEnum]
        public override StartCommandResult OnStartCommand(Intent intent, [GeneratedEnum] StartCommandFlags flags, int startId)
        {
            if (_isrun)
            {
                return StartCommandResult.NotSticky;
            }

            _isrun = true;

            var handle = new VpnService.Builder(this)
                .AddAddress("192.168.2.2", 24)
                .AddRoute("192.168.5.0", 24)
                .AddDnsServer("192.168.5.123")
                .SetBlocking(true)
                .AddDisallowedApplication(AppInfo.PackageName)
                .Establish();

            var inputStream = new ParcelFileDescriptor.AutoCloseInputStream(handle);

            var oustream = new ParcelFileDescriptor.AutoCloseOutputStream(handle);

            var ip = intent.GetStringExtra("ip");

            var hosts = intent.GetStringExtra("hosts");

            MyVpnService.Log(ip);
            MyVpnService.Log(hosts);

            Socket socket = DNS(ip, hosts);



           var task = Task.Run(() => LocalRequestResolver.Start(inputStream.Read, oustream.Write, socket));


            task.ContinueWith((t) =>
            {
                if (task.Exception != null)
                {
                    Log(task.Exception);
                }
            });




            return StartCommandResult.NotSticky;
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


    static class AppSetting
    {
        static string IPAddress
        {
            get => Preferences.Get(nameof(IPAddress), string.Empty);

            set => Preferences.Set(nameof(IPAddress), value);
        }

        static string Hosts
        {
            get => Preferences.Get(nameof(Hosts), string.Empty);

            set => Preferences.Set(nameof(Hosts), value);
        }


        static string Create(params string[] ps)
        {
            return string.Join(System.Environment.NewLine, ps);
        }

        static string Create()
        {

            return Create(
                "#开头的是注释",
                "*.iwara.tv 141.101.120.83",
                "t66y.com 141.101.120.83",
                "ajax.googleapis.com 127.0.0.5");
        }


        public static StartInfo Get()
        {
            string ip;

            string hosts;


            if (string.IsNullOrWhiteSpace(IPAddress))
            {
                ip = "144.144.144.144";
            }
            else
            {
                ip = IPAddress;
            }


            if (string.IsNullOrWhiteSpace(Hosts))
            {
                hosts = Create();
            }
            else
            {
                hosts = Hosts;
            }



            return new StartInfo(ip, hosts);
        }



        public static void Set(StartInfo info)
        {

            Hosts = info.Hosts;

            IPAddress = info.IPAddress;
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


            Xamarin.Essentials.Permissions.RequestAsync<Permissions.StorageWrite>();



            AppDomain.CurrentDomain.FirstChanceException += CurrentDomain_FirstChanceException;

            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;

            AndroidEnvironment.UnhandledExceptionRaiser += AndroidEnvironment_UnhandledExceptionRaiser;


            LoadApplication(new App(new MainPageInfo(AppSetting.Get(), (info) => Ckuck(info), CreateVpn)));

            
        }

        static string[] Remove(string s)
        {
            return s.Split(System.Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
                .Where((s) => s.StartsWith("#") == false)
                .Where((s) => string.IsNullOrWhiteSpace(s) == false)
                .ToArray();

        }

        static void PraseHost(string s)
        {

            if (s.StartsWith("*"))
            {
                s = "www" + s.Remove(0, 1);
            }


            if (System.Uri.CheckHostName(s) == UriHostNameType.Dns)
            {

            }
            else
            {
                throw new UriFormatException();
            }
        }

        public static (string,string)[] CreateMasterFile(string s)
        {
            
            var vs = new List<(string, string)>();
            foreach (var item in Remove(s))
            {
                var kv = item.Split(" ", 3, StringSplitOptions.RemoveEmptyEntries);




                try
                {
                    if (kv.Length < 2)
                    {
                        throw new FormatException();
                    }


                    PraseHost(kv[0]);



                    vs.Add((kv[0], IPAddress.Parse(kv[1]).ToString()));

                }
                catch (FormatException)
                {
                    throw new FormatException($"{item} 这一项存在错误");
                }
            }

            return vs.ToArray();
        }

        

        static void Ckuck(StartInfo startInfo)
        {
            IPAddress.Parse(startInfo.IPAddress);
            CreateMasterFile(startInfo.Hosts);

            AppSetting.Set(startInfo);

        }

        StartInfo _info;
        void CreateVpn(StartInfo info)
        {
            Ckuck(info);

            _info = info;
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

                Intent inter = new Intent(this, typeof(MyVpnService));


                inter.PutExtra("ip", _info.IPAddress);

                inter.PutExtra("hosts", _info.Hosts);


                ServerHelper.StartServer(this, inter);
            }

        }


        public override void OnRequestPermissionsResult(int requestCode, string[] permissions, [GeneratedEnum] Android.Content.PM.Permission[] grantResults)
        {
            Xamarin.Essentials.Platform.OnRequestPermissionsResult(requestCode, permissions, grantResults);

            base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
        }




        static void CurrentDomain_FirstChanceException(object sender, System.Runtime.ExceptionServices.FirstChanceExceptionEventArgs e)
        {
            Log("FirstChanceException", e.Exception);
        }

        static void TaskScheduler_UnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            Log("TaskScheduler", e.Exception);
        }

        static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Log("Domain", e.ExceptionObject);
        }

        static void AndroidEnvironment_UnhandledExceptionRaiser(object sender, RaiseThrowableEventArgs e)
        {
            Log("Android", e.Exception);
        }

        
        private static readonly object _lock = new object();

        public static void Log(string name, object e)
        {
            lock (_lock)
            {

                string s = System.Environment.NewLine;

                System.IO.File.AppendAllText($"/storage/emulated/0/dns.{name}.txt", $"{s}{s}{s}{s}{DateTime.Now}{s}{e}", System.Text.Encoding.UTF8);
            }

        }

    }
}