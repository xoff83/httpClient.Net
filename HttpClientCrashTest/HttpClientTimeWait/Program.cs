using System;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using System.Linq;
using System.Text;

namespace HttpClientTimeWait
{

    class Program
    {


        static void ReadFirst()
        {
            Console.WriteLine(@"

Problèmes avec la classe HttpClient d’origine disponible dans.NET

    La classe d’origine et la HttpClient classe connue peuvent être facilement utilisées, mais dans certains cas, elle n’est pas utilisée correctement par de nombreux développeurs.

    Bien que cette classe implémente IDisposable, la déclaration et l’instanciation dans une using instruction n’est pas recommandée, car lorsque l' HttpClient objet est supprimé, le Socket sous-jacent n’est pas libéré immédiatement, ce qui peut entraîner un problème d' épuisement du socket.Pour plus d’informations sur ce problème, consultez le billet de blog que vous utilisez httpclient incorrect et déstabiliser votre logiciel.

    Par conséquent, HttpClient est destiné à être instancié une seule fois et réutilisé tout au long de la durée de vie d’une application. L’instanciation d’une classe HttpClient pour chaque demande épuise le nombre de sockets disponibles sous des charges élevées. Ce problème entraîne des erreurs SocketException. Les approches possibles pour résoudre ce problème sont basées sur la création de l’objet HttpClient singleton ou statique, comme expliqué dans cet article Microsoft sur l’utilisation de HttpClient.Il peut s’agir d’une bonne solution pour les applications console à courte durée de vie ou similaire, qui s’exécutent plusieurs fois par jour.

    L’utilisation d’une instance partagée dans des processus de longue durée constitue un autre problème que les développeurs peuvent rencontrer HttpClient . Dans le cas où le HttpClient est instancié comme un singleton ou un objet statique, il ne parvient pas à gérer les modifications DNS comme décrit dans ce numéro du référentiel dotnet / Runtime github.

    Toutefois, le problème n’est pas vraiment avec HttpClient par se, mais avec le constructeur par défaut pour httpclient, car il crée une nouvelle instance concrète de HttpMessageHandler, qui est celle qui a des problèmes d' épuisement des sockets et des modifications DNS mentionnées ci-dessus.

    
    Pour résoudre les problèmes mentionnés ci-dessus et rendre les HttpClient instances gérables, .net Core 2,1 IHttpClientFactory a introduit l’interface qui peut être utilisée pour configurer et créer des HttpClient instances dans une application via l’injection de dépendances(di). Il fournit également des extensions pour l’intergiciel(middleware) basé sur Polly afin de tirer parti des gestionnaires de délégation dans HttpClient.

    Polly est une bibliothèque de gestion des erreurs temporaires qui permet aux développeurs d’ajouter de la résilience à leurs applications à l’aide de stratégies prédéfinies de façon Fluent et thread-safe.


-Extrait de Microsoft docs:(https://docs.microsoft.com/fr-fr/dotnet/architecture/microservices/implement-resilient-applications/use-httpclientfactory-to-implement-resilient-http-requests)


");
            // La cause de tout ça : Cela ressemble à une « bizarrerie » de Windows que j'ai également rencontrée il y a quelque temps où, pour une raison technique dont je ne me souviens pas, Windows s'accroche aux sockets pendant quelques minutes, même après que vous pensiez les avoir fermées. Cela se résume à la technologie que vous utilisez. La cause principale de cela que j'ai trouvé était dans l'utilisation de HttpClient. Il s'agit d'un objet jetable et même la documentation de Microsoft suggère que vous devez créer un HttpClient si nécessaire, l'utiliser, puis le supprimer. mais... il s'avère que c'est faux en raison de la bizarrerie susmentionnée. Ce que vous devriez faire, en fait, est d'utiliser un singleton HttpClient que vous réutilisent pour chaque demande. Voir ici pour une explication complète. (Bien sûr, je ne sais pas si vous utilisez un HttpClient ou non, mais j'espère que cela peut vous diriger dans la bonne direction).
            Console.WriteLine(" A lire : https://www.aspnetmonsters.com/2016/08/2016-08-27-httpclientwrong/");
            Console.WriteLine(" A lire : https://docs.microsoft.com/fr-fr/dotnet/architecture/microservices/implement-resilient-applications/use-httpclientfactory-to-implement-resilient-http-requests");
            Console.WriteLine(" A lire: https://docs.microsoft.com/fr-fr/dotnet/csharp/tutorials/console-webapiclient");
        }

        private static HttpClient GetHttpClient(string proxy, string login, string password)
        {
            HttpClient _client;
            if (!String.IsNullOrWhiteSpace(proxy))
            {
                HttpClientHandler _handler = new HttpClientHandler()
                {
                    Proxy = new WebProxy(proxy),
                    UseProxy = true,
                };

                _client = new HttpClient(_handler);
                var byteArray = Encoding.ASCII.GetBytes($"{login}:{password}");
                _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));
            }
            else
            {
                _client = new HttpClient();
            }
            return _client;
        }

        public static async Task Main(string[] args)
        {
            if (args == null || args.Length == 0)
            {
                args = new string[5] { "http://srv-smi-svc-d1/waterp-service/nightly/api/product/version", "10", null, null, null };
                Console.WriteLine("Usage : <url> <nbAppels> <urlProxy> <proxyLogin> <proxyPassword>");
            }

            Stopwatch chrono = Stopwatch.StartNew();
            Uri adresse = new Uri(args[0] ?? "http://srv-smi-svc-d1/waterp-service/nightly/api/product/version");
            int nbAppels = args.Length > 1 ? Int32.Parse(args[1]) : 10;

            string choix = "0";

            Console.WriteLine($"Appel sur {adresse.OriginalString} sur l'IP  {Dns.GetHostAddresses(adresse.Host)[0]}");

            while (choix != "q")
            {
                if ("0".Equals(choix))
                {
                    Console.Clear();
                    Console.WriteLine($"Lancer un des tests suivants en tapant le numéro idoine:");
                    Console.WriteLine($" 0 - rappeler ce menu");
                    Console.WriteLine($" 1 - {nbAppels} appels à partir de {nbAppels} HttpClient SANS 'using'");
                    Console.WriteLine($" 2 - {nbAppels} appels à partir de {nbAppels} HttpClient AVEC des 'using'");
                    Console.WriteLine($" 3 - {nbAppels} appels au sein d'un seul HttpClient");
                    Console.WriteLine($" 4 - les 3 tests à la suite");
                    Console.WriteLine($" 99- ... Pour en savoir plus");
                    Console.WriteLine($"<Entrée> pour avoir le détail des connexions ouvertes.");
                    Console.WriteLine($"<q> pour quitter.");
                }

                if ("1".Equals(choix) || "4".Equals(choix))
                {
                    await NotTheBadestWay(adresse, nbAppels);
                    Console.WriteLine($"{CountTCPRemoteConnexionPortUsage(adresse.Port)} connexion(s) vers le port {adresse.Port} pour {CountListenerPortUsage(adresse.Port)} listeners");
                }
                if ("2".Equals(choix) || "4".Equals(choix))
                {
                    await BadWay(adresse, nbAppels);
                    Console.WriteLine($"{CountTCPRemoteConnexionPortUsage(adresse.Port)} connexion(s) vers le port {adresse.Port} pour {CountListenerPortUsage(adresse.Port)} listeners");
                }
                if ("3".Equals(choix) || "4".Equals(choix))
                {
                    await GoodWay(adresse, nbAppels);
                    Console.WriteLine($"{CountTCPRemoteConnexionPortUsage(adresse.Port)} connexion(s) vers le port {adresse.Port} pour {CountListenerPortUsage(adresse.Port)} listeners");

                }
                if ("99".Equals(choix))
                {
                    ReadFirst();
                }


                if (string.IsNullOrWhiteSpace(choix))
                {

                    Console.Clear();
                    Console.WriteLine($"{CountTCPRemoteConnexionPortUsage(adresse.Port)} connexion(s) vers le port" +
                        $" {adresse.Port} pour {CountListenerPortUsage(adresse.Port)} listeners");
                    Console.WriteLine($"Detail des connexions encore ouvertes sur le serveur");
                    infoRemoteConnexionsStillOpen(adresse);
                    Console.WriteLine($"Detail des connexions encore ouvertes sur le client");
                    infoRemoteConnexionsStillOpen(adresse);


                    Console.WriteLine($"au bout de {chrono.ElapsedMilliseconds / 1000} secondes");
                }

                choix = Console.ReadLine();
            }
            chrono.Stop();

        }


        public static void infoRemoteConnexionsStillOpen(Uri adresse)
        {
            var targetIPs = Dns.GetHostAddresses(adresse.Host);
            IPGlobalProperties ipProperties = IPGlobalProperties.GetIPGlobalProperties();
            int cpt = 0;
            var candatates = ipProperties.GetActiveTcpConnections().Where(i => targetIPs.Contains(i.RemoteEndPoint.Address) && i.RemoteEndPoint.Port == adresse.Port).OrderBy(i => i.State);
            foreach (TcpConnectionInformation info in candatates)
            {
                cpt++;
                Console.WriteLine($"({cpt})\t -- { info.State}\t -- Local: {info.LocalEndPoint.Address}:{info.LocalEndPoint.Port} ==> Remote: {info.RemoteEndPoint.Address}:{info.RemoteEndPoint.Port} ({Dns.GetHostEntry(info.RemoteEndPoint.Address).HostName})");
            }
        }

        public static void infoLocalConnexionsStillOpen(Uri adresse)
        {
            var targetIPs = Dns.GetHostAddresses(adresse.Host);
            IPGlobalProperties ipProperties = IPGlobalProperties.GetIPGlobalProperties();
            int cpt = 0;
            var candatates = ipProperties.GetActiveTcpConnections().Where(i => targetIPs.Contains(i.LocalEndPoint.Address) && i.LocalEndPoint.Port == adresse.Port).OrderBy(i => i.State);
            foreach (TcpConnectionInformation info in candatates)
            {
                cpt++;
                Console.WriteLine($"({cpt})\t -- { info.State}\t -- Local: {info.LocalEndPoint.Address}:{info.LocalEndPoint.Port} ==> Remote: {info.RemoteEndPoint.Address}:{info.RemoteEndPoint.Port} ({Dns.GetHostEntry(info.RemoteEndPoint.Address).HostName})");
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
        public static int CountTCPLocalConnexionPortUsage(Int32 port)
        {
            IPGlobalProperties ipGP = IPGlobalProperties.GetIPGlobalProperties();
            TcpConnectionInformation[] endpoints = ipGP.GetActiveTcpConnections();
            return endpoints.Count(c => c.LocalEndPoint.Port == port);
        }

        public static int CountTCPRemoteConnexionPortUsage(Int32 port)
        {
            IPGlobalProperties ipGP = IPGlobalProperties.GetIPGlobalProperties();
            TcpConnectionInformation[] endpoints = ipGP.GetActiveTcpConnections();
            return endpoints.Count(c => c.RemoteEndPoint.Port == port);
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

        public static async Task NotTheBadestWay(Uri adresse, int nbCalls = 100, string proxy = null, string login = null, string password = null)
        {
            Console.WriteLine($"{nbCalls} appels à partir de {nbCalls} HttpClient SANS 'using'");
            Stopwatch chrono1 = Stopwatch.StartNew();
            for (int i = 0; i < nbCalls; i++)
            {
                var client = GetHttpClient(proxy, login, password);

                var result = await client.GetAsync(adresse);
                Console.WriteLine($"   Connexion {i + 1} : {result.StatusCode}");


            }
            Console.WriteLine($"{nbCalls} appels en {chrono1.ElapsedMilliseconds} ms");
        }

        public static async Task BadWay(Uri adresse, int nbCalls = 100, string proxy = null, string login = null, string password = null)
        {
            Console.WriteLine($"{nbCalls} appels à partir de {nbCalls} HttpClient AVEC des 'using'");
            Stopwatch chrono2 = Stopwatch.StartNew();
            for (int i = 0; i < nbCalls; i++)
            {

                using (var client = GetHttpClient(proxy, login, password))
                {
                    var result = await client.GetAsync(adresse);
                    Console.WriteLine($"   Connexion {i + 1} : {result.StatusCode}");
                }
            }
            Console.WriteLine($"{nbCalls} Appels en {chrono2.ElapsedMilliseconds} ms");

        }


        public static async Task GoodWay(Uri adresse, int nbCalls = 100, string proxy = null, string login = null, string password = null)
        {
            Console.WriteLine($"{nbCalls} appels au sein d'un seul HttpClient");
            Stopwatch chrono3 = Stopwatch.StartNew();
            using (var client = GetHttpClient(proxy, login, password))
            {
                for (int i = 0; i < nbCalls; i++)
                {
                    var result = await client.GetAsync(adresse);
                    Console.WriteLine($"   Connexion {i + 1} : {result.StatusCode}");
                }
            }
            Console.WriteLine($"{nbCalls} Appels en {chrono3.ElapsedMilliseconds} ms");

        }



    }
}
