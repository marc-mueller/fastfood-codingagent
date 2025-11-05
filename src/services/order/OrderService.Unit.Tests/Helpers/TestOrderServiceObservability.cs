using System.Diagnostics;
using System.Diagnostics.Metrics;
using FinanceService.Observability;

namespace OrderService.Unit.Tests.Helpers;

public class TestOrderServiceObservability : IOrderServiceObservability
{
    private readonly Meter _meter;

    public TestOrderServiceObservability()
    {
        ServiceName = "OrderService";
        ActivitySourceName = "OrderService";
        _meter = new Meter("OrderService");
        
        OrderTotalAmount = _meter.CreateHistogram<decimal>("order_total_amount");
        OrderTotalDuration = _meter.CreateHistogram<double>("order_total_duration");
        OrderDeliveryDuration = _meter.CreateHistogram<double>("order_delivery_duration");
        OrderPerparationDuration = _meter.CreateHistogram<double>("order_preparation_duration");
        OrderItemsCount = _meter.CreateHistogram<long>("order_items_count");
        OrdersClosedCounter = _meter.CreateCounter<long>("orders_closed");
        OrdersPaidCounter = _meter.CreateCounter<long>("orders_paid");
        OrdersCreatedCounter = _meter.CreateCounter<long>("orders_created");
        OrderSalesDuration = _meter.CreateHistogram<double>("order_sales_duration");
        ClientChannelOrdersCreatedCounter = _meter.CreateCounter<long>("client_channel_orders_created");
    }

    public string ServiceName { get; }
    public string ActivitySourceName { get; }
    
    public Histogram<decimal> OrderTotalAmount { get; }
    public Histogram<double> OrderTotalDuration { get; }
    public Histogram<double> OrderDeliveryDuration { get; }
    public Histogram<double> OrderPerparationDuration { get; }
    public Histogram<long> OrderItemsCount { get; }
    public Counter<long> OrdersClosedCounter { get; }
    public Counter<long> OrdersPaidCounter { get; }
    public Counter<long> OrdersCreatedCounter { get; }
    public Histogram<double> OrderSalesDuration { get; }
    public Counter<long> ClientChannelOrdersCreatedCounter { get; }

    public Activity? StartActivity(string name = "", ActivityKind kind = ActivityKind.Internal)
    {
        return null;
    }

    public Activity? StartActivity(Type callerType, string name = "", ActivityKind kind = ActivityKind.Internal,
        bool includeCallerTypeInName = false)
    {
        return null;
    }
}
