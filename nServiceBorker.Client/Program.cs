using System;
using System.ServiceModel;
using Microsoft.Samples.SsbTransportChannel;
using OrderService;
using OrderService.Proxies;

namespace nServiceBorker.Client
{
    class Program
    {
        static void Main(string[] args)
        {
            int i = 0;
            while (true)
            {
                var method = Console.ReadLine();
                if (method == "trigger")
                {
                    var db = new ssb_dbEntities1();
                    var order = new Order
                    {
                        OrderID = Guid.NewGuid(),
                        CustomerName = "trigger" + i
                    };
                    db.Orders.Add(order);
                    db.SaveChanges();
                }
                else if (method == "wcf")
                {
                    SsbBinding clientBinding = new SsbBinding();
                    clientBinding.SqlConnectionString = Utils.Connectionstring("clientBinding");
                    clientBinding.UseEncryption = false;
                    clientBinding.UseActionForSsbMessageType = true;
                    clientBinding.Contract = Utils.ChannelContract;

                    OrderServiceClient client = new OrderServiceClient(clientBinding, new EndpointAddress(Utils.ServiceEndpointAddress));
                    var order = new OrderService.Proxies.Order
                    {
                        OrderId = Guid.NewGuid(),
                        CustomerName = "wcf" + i
                    };
                    client.SubmitOrder(order);
                    client.Close();
                }
                i++;
            }
        }
    }
}
