using System.Data.SqlClient;

namespace OrderService
{
    public class Utils
    {
        public static string ServiceEndpointAddress = "net.ssb:source=Client:target=Service";
        public static string ClientEndpointAddress = "net.ssb:source=*:target=Client";
        public static string ChannelContract = "http://ssbtransport/sample/OrderServiceContract_OneWay";
        public static string Connectionstring(string applicationName)
        {
            var cb = new SqlConnectionStringBuilder(System.Configuration.ConfigurationManager.ConnectionStrings["ssbTest"].ConnectionString);
            cb.ApplicationName = applicationName;
            return cb.ToString();
        }
    }
}
