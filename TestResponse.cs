using System;

namespace Mono.WebServer.Http
{
	public class TestResponse
	{
		public TestResponse ()
		{
		}

		public static string Response1=
			@"HTTP/1.1 200 OK
Date: Fri, 15 Nov 2013 00:29:02 GMT
Content-Type: text/html; charset=utf-8
X-AspNet-Version: 4.0.30319
Cache-Control: private
Set-Cookie: ASP.NET_SessionId=7226F67056A58F7572E98BDB; path=/
Content-Length: 19

<p>Hello, World</p>
";
		public static string Header="HTTP/1.1 200 OK\r\nContent-Type: text/html; charset=utf-8\r\nContent-Length: 20\r\n\r\n";

		public static string Response="<p>Hello, world!</p>";

		public static string Response2 = "Content-type: text/html\r\n\r\n<html>\n<p>Hello, World</p>\n</html>\n\n";

	}
}

