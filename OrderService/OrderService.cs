using System;
using System.Runtime.Serialization;

namespace OrderService
{
    // 注意: 使用“重构”菜单上的“重命名”命令，可以同时更改代码和配置文件中的类名“OrderService”。
    public class OrderService : IOrderService
    {
        public void SubmitOrder(Order order)
        {
            Console.WriteLine(string.Format("process order {0} for customer {1}", order.OrderId, order.CustomerName));
        }
    }

    [Serializable]
    [DataContract(Namespace = "http://ssbtransport/sample")]
    public class Order
    {
        [DataMember()]
        public Guid OrderId;
        [DataMember(IsRequired = true)]
        public string CustomerName;
    }
}
