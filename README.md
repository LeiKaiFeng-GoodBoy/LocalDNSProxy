
[下载地址蓝奏云](https://wws.lanzous.com/b0261orde) 密码:8p3a

因为Github访问不太稳定，所以索性放在了蓝奏云中

主要原理就是这一块代码
`
var handle = new VpnService.Builder(this)
                .AddAddress("192.168.2.2", 24)
                .AddRoute("192.168.254.254", 32)
                .AddDnsServer("192.168.254.254")
                .SetBlocking(true)
                .AddDisallowedApplication(AppInfo.PackageName)
                .Establish();
`

通过VPN接口，设置了DNSServer，把我自己排除在VPN外，
但是只有ipv4地址192.168.254.254会传递给我的应用，而192.168.254.254被我设定为了DNSServer，
也就是说其他的流量会正常发送，不会传递给我，主要传递给我我也没有实现TCP协议，
并且只有应用使用该设置的DNSServer，并且使用UDP协议进行DNS查询，才会有用，比如火狐浏览器默认可能使用HTTPS并且可单独配置DNSServer，所以对于火狐浏览器默认没有效果，
并且只有一切都理想的情况下，才会有效，假如有应用在UDP上层传了一个错误的东西，行为未定义，当然这个问题是后续实现的问题，因为我为了简单，很多地方偷懒了




## 关于没有实现TCP协议如何取巧

还是可以取巧的，利用操作系统的TCP实现
办法就是读出IP数据包简单的修改一下IP和Port，当然Port在TCP头中，然后再把修改后的IP数据包写回去
就是读出来，修改，写回
然后使用操作系统套接字或者说类库套接字侦听相应的端点就可以了