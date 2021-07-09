using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using System.Linq;
using System.Text;
using System.Net.Sockets;

namespace HttpClientTimeWait
{
    class Program
    {

        /* Extrait de Microsoft docs:(https://docs.microsoft.com/fr-fr/dotnet/architecture/microservices/implement-resilient-applications/use-httpclientfactory-to-implement-resilient-http-requests)
         Problèmes avec la classe HttpClient d’origine disponible dans .NET

La classe d’origine et la HttpClient classe connue peuvent être facilement utilisées, mais dans certains cas, elle n’est pas utilisée correctement par de nombreux développeurs.

Bien que cette classe implémente IDisposable , la déclaration et l’instanciation dans une using instruction n’est pas recommandée, car lorsque l' HttpClient objet est supprimé, le Socket sous-jacent n’est pas libéré immédiatement, ce qui peut entraîner un problème d' épuisement du socket . Pour plus d’informations sur ce problème, consultez le billet de blog que vous utilisez httpclient incorrect et déstabiliser votre logiciel.

Par conséquent, HttpClient est destiné à être instancié une seule fois et réutilisé tout au long de la durée de vie d’une application. L’instanciation d’une classe HttpClient pour chaque demande épuise le nombre de sockets disponibles sous des charges élevées. Ce problème entraîne des erreurs SocketException. Les approches possibles pour résoudre ce problème sont basées sur la création de l’objet HttpClient singleton ou statique, comme expliqué dans cet article Microsoft sur l’utilisation de HttpClient. Il peut s’agir d’une bonne solution pour les applications console à courte durée de vie ou similaire, qui s’exécutent plusieurs fois par jour.

L’utilisation d’une instance partagée de dans des processus de longue durée constitue un autre problème que les développeurs peuvent rencontrer HttpClient . Dans le cas où le HttpClient est instancié comme un singleton ou un objet statique, il ne parvient pas à gérer les modifications DNS comme décrit dans ce numéro du référentiel dotnet/Runtime github.

Toutefois, le problème n’est pas vraiment avec HttpClient par se, mais avec le constructeur par défaut pour httpclient, car il crée une nouvelle instance concrète de HttpMessageHandler , qui est celle qui a des problèmes d' épuisement des sockets et des modifications DNS mentionnées ci-dessus.

Pour résoudre les problèmes mentionnés ci-dessus et rendre les HttpClient instances gérables, .net Core 2,1 IHttpClientFactory a introduit l’interface qui peut être utilisée pour configurer et créer des HttpClient instances dans une application via l’injection de dépendances (di). Il fournit également des extensions pour l’intergiciel (middleware) basé sur Polly afin de tirer parti des gestionnaires de délégation dans HttpClient.

Polly est une bibliothèque de gestion des erreurs temporaires qui permet aux développeurs d’ajouter de la résilience à leurs applications à l’aide de stratégies prédéfinies de façon Fluent et thread-safe.
        */



        private static HttpClientHandler _handler = new HttpClientHandler()
        {
            Proxy = new WebProxy("http://proxy:8080"),
            UseProxy = true,
        };

        private static HttpClient _client = new HttpClient();// new HttpClient(_handler);

        static void ReadFirst(string[] args)
        {
            // La cause de tout ça : Cela ressemble à une « bizarrerie » de Windows que j'ai également rencontrée il y a quelque temps où, pour une raison technique dont je ne me souviens pas, Windows s'accroche aux sockets pendant quelques minutes, même après que vous pensiez les avoir fermées. Cela se résume à la technologie que vous utilisez. La cause principale de cela que j'ai trouvé était dans l'utilisation de HttpClient. Il s'agit d'un objet jetable et même la documentation de Microsoft suggère que vous devez créer un HttpClient si nécessaire, l'utiliser, puis le supprimer. mais... il s'avère que c'est faux en raison de la bizarrerie susmentionnée. Ce que vous devriez faire, en fait, est d'utiliser un singleton HttpClient que vous réutilisent pour chaque demande. Voir ici pour une explication complète. (Bien sûr, je ne sais pas si vous utilisez un HttpClient ou non, mais j'espère que cela peut vous diriger dans la bonne direction).
            Console.WriteLine("Thank's to : https://www.aspnetmonsters.com/2016/08/2016-08-27-httpclientwrong/");
            Console.WriteLine("Thank's to : https://docs.microsoft.com/fr-fr/dotnet/architecture/microservices/implement-resilient-applications/use-httpclientfactory-to-implement-resilient-http-requests");
            Console.WriteLine("Read this: https://docs.microsoft.com/fr-fr/dotnet/csharp/tutorials/console-webapiclient");
        }
        public static async Task Main(string[] args)
        {
            Stopwatch chrono = Stopwatch.StartNew();
            Uri adresse = new Uri("http://localhost:3615");
            

            Console.WriteLine($"Appel sur {adresse.OriginalString} sur l'IP  {Dns.GetHostAddresses(adresse.Host)[0]}");

            //var byteArray = Encoding.ASCII.GetBytes("login:password");
            //_client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));
            await NotTheBadestWay(adresse, 10);
            Console.WriteLine($"{CountConnexionPortUsage(adresse.Port)} connexion(s) sur le port {adresse.Port} pour {CountListenerPortUsage(adresse.Port)} listeners");

            // Netstat("");
            StillOpen(adresse);
            //StillOpen2(adresse);
            await BadWay(adresse, 10);
            // Netstat("");
            StillOpen(adresse);
            // StillOpen2(adresse);
            Console.WriteLine($"{CountConnexionPortUsage(adresse.Port)} connexion(s) sur le port {adresse.Port} pour {CountListenerPortUsage(adresse.Port)} listeners");

            await GoodWay(adresse, 10);
            //Netstat("");
            StillOpen(adresse);
            //StillOpen2(adresse);
            Console.WriteLine($"{CountConnexionPortUsage(adresse.Port)} connexion(s) sur le port {adresse.Port} pour {CountListenerPortUsage(adresse.Port)} listeners");

            while (Console.ReadLine() != "q")
            {
                //StillOpen(adresse);
                StillOpen2(adresse);
                Console.WriteLine($"{CountConnexionPortUsage(adresse.Port)} connexion(s) sur le port {adresse.Port} pour {CountListenerPortUsage(adresse.Port)} listeners");

                Console.WriteLine($"au bout de {chrono.ElapsedMilliseconds / 1000} secondes");
            }
            chrono.Stop();

        }

        public static void StillOpen(Uri adresse)
        {
            var ip = System.Net.NetworkInformation.IPGlobalProperties.GetIPGlobalProperties();
            var targetIPs = Dns.GetHostAddresses(adresse.Host);
            foreach (var tcp in ip.GetActiveTcpConnections()) // alternative: ip.GetActiveTcpListeners()
            {
                if (
                   // System.Net.NetworkInformation.TcpState.CloseWait.Equals(tcp.State)
                   targetIPs.Contains(tcp.RemoteEndPoint.Address)
                   && (tcp.LocalEndPoint.Port == adresse.Port
                 || tcp.RemoteEndPoint.Port == adresse.Port))
                {
                    Console.WriteLine($"is {tcp.State} {tcp.LocalEndPoint.Address}:{tcp.LocalEndPoint.Port} ===> {tcp.RemoteEndPoint.Address}:{tcp.RemoteEndPoint.Port}");
                }

            }
        }
        public static void StillOpen2(Uri adresse)
        {
            var targetIPs = Dns.GetHostAddresses(adresse.Host);
            IPGlobalProperties ipProperties = IPGlobalProperties.GetIPGlobalProperties();
            int cpt = 0;
            var candatates = ipProperties.GetActiveTcpConnections().Where(i => targetIPs.Contains(i.RemoteEndPoint.Address) && i.RemoteEndPoint.Port == adresse.Port).OrderBy(i=> i.State);
            foreach (TcpConnectionInformation info in candatates)
            {
                cpt++;
                Console.WriteLine($"({cpt})\t - State :{ info.State} - Local: {info.LocalEndPoint.Address}:{info.LocalEndPoint.Port} ==> Remote: {info.RemoteEndPoint.Address}:{info.RemoteEndPoint.Port} ({Dns.GetHostEntry(info.RemoteEndPoint.Address).HostName}) {Environment.NewLine}");
            }
        }

        public static int CountListenerPortUsage(Int32 port)
        {
            int result = 0;
            IPGlobalProperties ipGP = IPGlobalProperties.GetIPGlobalProperties();
            IPEndPoint[] endpoints = ipGP.GetActiveTcpListeners();
            if (endpoints != null && endpoints.Length > 0)
            {

                for (int i = 0; i < endpoints.Length; i++)
                    if (endpoints[i].Port == port) //here port was passed as a Parameter
                        result++;
            }
            return result;
        }
        public static int CountConnexionPortUsage(Int32 port)
        {
            int result = 0;
            IPGlobalProperties ipGP = IPGlobalProperties.GetIPGlobalProperties();
            TcpConnectionInformation[] endpoints = ipGP.GetActiveTcpConnections();
            if (endpoints != null && endpoints.Length > 0)
            {

                for (int i = 0; i < endpoints.Length; i++)
                    if (endpoints[i].RemoteEndPoint.Port == port) //here port was passed as a Parameter
                        result++;
            }
            return result;
        }
        public static bool CheckPortUsage(Int32 port)
        {
            IPGlobalProperties ipGP = IPGlobalProperties.GetIPGlobalProperties();
            IPEndPoint[] endpoints = ipGP.GetActiveTcpListeners();
            if (endpoints == null || endpoints.Length == 0)
                return false;
            for (int i = 0; i < endpoints.Length; i++)
                if (endpoints[i].Port == port) //here port was passed as a Parameter
                    return true;
            return false;
        }

        public static async Task NotTheBadestWay(Uri adresse, int nbCalls = 100)
        {
            Console.WriteLine($"Starting {nbCalls} calls in {nbCalls} HttpClient without 'using'");
            for (int i = 0; i < nbCalls; i++)
            {
                //HttpClientHandler handler = new HttpClientHandler()
                //{
                //    Proxy = new WebProxy("http://proxy:8080"),
                //    UseProxy = true,
                //};
                //var byteArray = Encoding.ASCII.GetBytes("user:pwd");
                //client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));

                var client = new HttpClient();

                var result = await client.GetAsync(adresse);
                Console.WriteLine(result.StatusCode);


                Console.WriteLine($"{nbCalls} Connections done");

            }
        }
        public static async Task BadWay(Uri adresse, int nbCalls = 100)
        {
            Console.WriteLine($"Starting {nbCalls} calls in {nbCalls} HttpClient within 'using'");
            for (int i = 0; i < nbCalls; i++)
            {
                //HttpClientHandler handler = new HttpClientHandler()
                //{
                //    Proxy = new WebProxy("http://proxy:8080"),
                //    UseProxy = true,
                //};
                //var byteArray = Encoding.ASCII.GetBytes("user:password
                //");
                //client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));

                using (var client = new HttpClient())
                {
                    var result = await client.GetAsync(adresse);
                    Console.WriteLine(result.StatusCode);
                }
            }
            Console.WriteLine($"{nbCalls} Connections done");

        }


        public static async Task GoodWay(Uri adresse, int nbCalls = 100)
        {
            Console.WriteLine($"Starting {nbCalls} calls in a unique HttpClient");
            for (int i = 0; i < nbCalls; i++)
            {
                var result = await _client.GetAsync(adresse);
                Console.WriteLine(result.StatusCode);
            }
            Console.WriteLine($"{nbCalls} Connections done");

        }


        public static void Netstat(string param = "-q | findstr ael.somei.fr ")
        {

            List<string> _netstatLines = new List<string>();

            ProcessStartInfo psi = new ProcessStartInfo("netstat", param);

            psi.CreateNoWindow = true;
            psi.ErrorDialog = false;
            psi.UseShellExecute = false;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            psi.Verb = "runas";
            psi.WindowStyle = ProcessWindowStyle.Hidden;

            Process process = new Process();

            process.EnableRaisingEvents = true;
            process.StartInfo = psi;
            process.ErrorDataReceived += (s, e) => { _netstatLines.Add(e.Data); };
            process.OutputDataReceived += (s, e) => { _netstatLines.Add(e.Data); };

            process.Start();

            process.BeginErrorReadLine();
            process.BeginOutputReadLine();

            process.WaitForExit();



            foreach (var s in _netstatLines)
                Console.WriteLine(s);
        }
    }

    /// <summary>
    /// Basic helper methods around networking objects (IPAddress, IpEndPoint, Socket, etc.)
    /// </summary>
    public static class NetworkingExtensions
    {
        /// <summary>
        /// Converts a string representing a host name or address to its <see cref="IPAddress"/> representation, 
        /// optionally opting to return a IpV6 address (defaults to IpV4)
        /// </summary>
        /// <param name="hostNameOrAddress">Host name or address to convert into an <see cref="IPAddress"/></param>
        /// <param name="favorIpV6">When <code>true</code> will return an IpV6 address whenever available, otherwise 
        /// returns an IpV4 address instead.</param>
        /// <returns>The <see cref="IPAddress"/> represented by <paramref name="hostNameOrAddress"/> in either IpV4 or
        /// IpV6 (when available) format depending on <paramref name="favorIpV6"/></returns>
        public static IPAddress ToIPAddress(this string hostNameOrAddress, bool favorIpV6 = false)
        {
            var favoredFamily = favorIpV6 ? AddressFamily.InterNetworkV6 : AddressFamily.InterNetwork;
            var addrs = Dns.GetHostAddresses(hostNameOrAddress.Replace("http://", "").Replace(":adresse.Port", ""));
            return addrs.FirstOrDefault(addr => addr.AddressFamily == favoredFamily)
                   ??
                   addrs.FirstOrDefault();
        }
    }
}
