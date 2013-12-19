using System;
using System.ServiceModel;
using Microsoft.Samples.SsbTransportChannel;
using OrderService;

namespace nServiceBorker.Host
{
    class Program
    {
        static void Main(string[] args)
        {
            SsbBinding serviceBinding = new SsbBinding();
            serviceBinding.SqlConnectionString = Utils.Connectionstring("serviceBinding");
            serviceBinding.UseEncryption = false;
            serviceBinding.UseActionForSsbMessageType = true;
            serviceBinding.Contract = Utils.ChannelContract;

            var host = new ServiceHost(typeof(OrderService.OrderService));
            host.AddServiceEndpoint(typeof(IOrderService), serviceBinding, Utils.ServiceEndpointAddress);
            host.Open();

            Console.WriteLine("Waiting for service to process messages");
            Console.ReadKey();
        }
    }
}
